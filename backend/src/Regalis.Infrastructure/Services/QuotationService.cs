using Microsoft.EntityFrameworkCore;
using Regalis.Application.Abstractions;
using Regalis.Application.Common.Exceptions;
using Regalis.Application.Finance;
using Regalis.Application.Operations;
using Regalis.Domain.Entities.Finance;
using Regalis.Domain.Entities.Operations;
using Regalis.Domain.Enums;
using Regalis.Infrastructure.Persistence;
using Regalis.Infrastructure.Reporting;
using Regalis.Shared;
using Regalis.Shared.Api;
using Regalis.Shared.Constants;

namespace Regalis.Infrastructure.Services;

/// <summary>
/// Quotations (document §6–§8). Three rules shape this service.
///
/// A quotation is freely editable while Draft and frozen once sent: the customer,
/// company, place-of-supply and tax rates are snapshotted at that moment, so a later
/// settings change or profile edit never rewrites what the customer was quoted — the
/// same rule TRN_Invoice already lives by.
///
/// Money is computed here, never accepted from the client. Tax comes from Tax.* in
/// MST_AppSetting (Invariant 6) and is rounded away from zero, matching the booking
/// and pass engines, so the invoice generated from an approved quotation agrees to
/// the paisa.
///
/// Approval and rejection are the customer's own act. Each writes an append-only
/// TRN_QuotationStatusHistory row carrying who decided, when, and any reason —
/// the quotation's own row is never rewritten to record history.
/// </summary>
public class QuotationService(
    RegalisDbContext db,
    ISettingService settings,
    INumberSequenceService numberSequence,
    ICurrentUser currentUser,
    IDateTimeProvider clock) : IQuotationService
{
    public async Task<PagedResult<QuotationDto>> GetQuotationsAsync(QuotationListRequest request,
        CancellationToken ct = default)
    {
        var query = db.Quotations.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var term = request.Search.Trim();
            query = query.Where(q => q.QuotationNumber.Contains(term) || q.CustomerName.Contains(term));
        }
        if (request.Status is not null)
        {
            query = query.Where(q => (byte)q.Status == request.Status);
        }
        if (request.CustomerId is not null)
        {
            query = query.Where(q => q.CustomerId == request.CustomerId);
        }
        if (request.LocationId is not null)
        {
            query = query.Where(q => q.LocationId == request.LocationId);
        }
        if (request.From is not null)
        {
            query = query.Where(q => q.QuotationDate >= request.From);
        }
        if (request.To is not null)
        {
            query = query.Where(q => q.QuotationDate <= request.To);
        }

        return await PageAsync(query, request, ct);
    }

    public async Task<QuotationDto> GetQuotationAsync(long quotationId, CancellationToken ct = default) =>
        ToDto(await LoadScopedAsync(quotationId, ct));

    public async Task<PagedResult<QuotationDto>> GetMyQuotationsAsync(QuotationListRequest request,
        CancellationToken ct = default)
    {
        // Row scoping: the caller's own rows, whatever CustomerId the request carries.
        // A draft the admin has not sent yet is not the customer's business.
        var query = db.Quotations.AsNoTracking()
            .Where(q => q.CustomerId == currentUser.UserId && q.Status != QuotationStatus.Draft);

        if (request.Status is not null)
        {
            query = query.Where(q => (byte)q.Status == request.Status);
        }
        return await PageAsync(query, request, ct);
    }

    public async Task<QuotationDto> GetMyQuotationAsync(long quotationId, CancellationToken ct = default)
    {
        var quotation = await LoadScopedAsync(quotationId, ct, track: true);
        if (quotation.Status == QuotationStatus.Draft)
        {
            // Never disclose that an unsent draft exists.
            throw new NotFoundException("Quotation", quotationId);
        }

        // Opening a sent quotation is what "Viewed" means (document §6).
        if (quotation.Status == QuotationStatus.Sent && quotation.CustomerId == currentUser.UserId)
        {
            var nowUtc = clock.UtcNow;
            AddHistory(quotation, QuotationStatus.Viewed, nowUtc, null);
            quotation.Status = QuotationStatus.Viewed;
            quotation.ViewedUtc = nowUtc;
            await db.SaveChangesAsync(ct);
        }

        return ToDto(quotation);
    }

    public async Task<QuotationDto> CreateAsync(SaveQuotationRequest request,
        CancellationToken ct = default)
    {
        await GuardHeaderAsync(request, ct);

        var today = IstClock.IstDateOf(clock.UtcNow);
        var validityDays = await settings.GetIntAsync(AppSettingKeys.Quotation.ValidityDays, 15, ct);
        var quotationDate = request.QuotationDate ?? today;

        var quotation = new Quotation
        {
            CustomerId = request.CustomerId,
            LocationId = request.LocationId,
            QuotationDate = quotationDate,
            ValidUntil = request.ValidUntil ?? quotationDate.AddDays(validityDays),
            Status = QuotationStatus.Draft,
            TermsAndConditions = request.TermsAndConditions?.Trim()
                ?? await settings.GetStringAsync(AppSettingKeys.Quotation.TermsAndConditions, "", ct),
            Remarks = request.Remarks?.Trim()
        };
        await ApplyLinesAndTotalsAsync(quotation, request, ct);

        var strategy = db.Database.CreateExecutionStrategy();
        var newId = await strategy.ExecuteAsync(async () =>
        {
            await using var dbTransaction = await db.Database.BeginTransactionAsync(ct);
            quotation.QuotationNumber = await numberSequence.NextAsync(SequenceKeys.Quotation, ct);
            db.Quotations.Add(quotation);
            AddHistory(quotation, QuotationStatus.Draft, clock.UtcNow, null);
            await db.SaveChangesAsync(ct);
            await dbTransaction.CommitAsync(ct);
            return quotation.QuotationId;
        });

        return await GetQuotationAsync(newId, ct);
    }

    public async Task<QuotationDto> UpdateAsync(long quotationId, SaveQuotationRequest request,
        CancellationToken ct = default)
    {
        var quotation = await db.Quotations
            .Include(q => q.Lines)
            .FirstOrDefaultAsync(q => q.QuotationId == quotationId, ct)
            ?? throw new NotFoundException("Quotation", quotationId);

        if (quotation.Status != QuotationStatus.Draft)
        {
            throw new BusinessRuleException(
                "This quotation has already been sent to the customer and can no longer be edited. " +
                "Cancel it and raise a new one.");
        }
        await GuardHeaderAsync(request, ct);

        quotation.CustomerId = request.CustomerId;
        quotation.LocationId = request.LocationId;
        quotation.QuotationDate = request.QuotationDate ?? quotation.QuotationDate;
        quotation.ValidUntil = request.ValidUntil ?? quotation.ValidUntil;
        quotation.TermsAndConditions = request.TermsAndConditions?.Trim();
        quotation.Remarks = request.Remarks?.Trim();

        db.QuotationLines.RemoveRange(quotation.Lines);
        quotation.Lines.Clear();
        await ApplyLinesAndTotalsAsync(quotation, request, ct);

        await db.SaveChangesAsync(ct);
        return await GetQuotationAsync(quotationId, ct);
    }

    public async Task<QuotationDto> SendAsync(long quotationId, CancellationToken ct = default)
    {
        var quotation = await db.Quotations
            .Include(q => q.Lines)
            .FirstOrDefaultAsync(q => q.QuotationId == quotationId, ct)
            ?? throw new NotFoundException("Quotation", quotationId);

        if (quotation.Status != QuotationStatus.Draft)
        {
            throw new BusinessRuleException("Only a draft quotation can be sent.");
        }
        if (quotation.ValidUntil < IstClock.IstDateOf(clock.UtcNow))
        {
            throw new BusinessRuleException(
                "The validity date has already passed. Extend it before sending.");
        }

        var customer = await db.Users
            .FirstOrDefaultAsync(u => u.UserId == quotation.CustomerId, ct)
            ?? throw new NotFoundException("Customer", quotation.CustomerId);

        // The snapshot. Everything below is frozen from here on.
        quotation.CustomerName = customer.FullName;
        quotation.CustomerEmail = customer.Email;
        quotation.CustomerMobile = customer.MobileNumber;
        quotation.BillingAddress = customer.Address;
        quotation.CompanyName = await settings.GetStringAsync(
            AppSettingKeys.Company.Name, "Regalis WorkSpace", ct);
        quotation.CompanyGstin = await settings.GetStringAsync(AppSettingKeys.Company.Gstin, "", ct);
        quotation.CompanyAddress = await settings.GetStringAsync(AppSettingKeys.Company.Address, "", ct);
        quotation.PlaceOfSupply = await settings.GetStringAsync(AppSettingKeys.Tax.PlaceOfSupply, "", ct);

        var nowUtc = clock.UtcNow;
        AddHistory(quotation, QuotationStatus.Sent, nowUtc, null);
        quotation.Status = QuotationStatus.Sent;
        quotation.SentUtc = nowUtc;

        Notify(quotation, quotation.CustomerId, NotificationAudience.SingleUser,
            "A new quotation is ready",
            $"Quotation {quotation.QuotationNumber} for Rs {quotation.TotalAmount:N2} is waiting for your approval.",
            $"/quotations/{quotation.QuotationId}", "View quotation");
        await db.SaveChangesAsync(ct);

        return await GetQuotationAsync(quotationId, ct);
    }

    public Task<QuotationDto> ApproveAsync(long quotationId, QuotationDecisionRequest request,
        CancellationToken ct = default) =>
        DecideAsync(quotationId, QuotationStatus.Approved, request.Reason, ct);

    public Task<QuotationDto> RejectAsync(long quotationId, QuotationDecisionRequest request,
        CancellationToken ct = default) =>
        DecideAsync(quotationId, QuotationStatus.Rejected, request.Reason, ct);

    public async Task<QuotationDto> CancelAsync(long quotationId, string reason,
        CancellationToken ct = default)
    {
        var quotation = await db.Quotations
            .FirstOrDefaultAsync(q => q.QuotationId == quotationId, ct)
            ?? throw new NotFoundException("Quotation", quotationId);

        if (quotation.Status == QuotationStatus.Cancelled)
        {
            throw new BusinessRuleException("This quotation is already cancelled.");
        }
        if (quotation.InvoiceId is not null)
        {
            throw new BusinessRuleException(
                "An invoice has already been raised from this quotation. Cancel the invoice instead.");
        }
        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new AppValidationException([new ApiError(ErrorCodes.ValidationError,
                "Give a reason for the cancellation.", nameof(reason))]);
        }

        var nowUtc = clock.UtcNow;
        AddHistory(quotation, QuotationStatus.Cancelled, nowUtc, reason.Trim());
        quotation.Status = QuotationStatus.Cancelled;
        await db.SaveChangesAsync(ct);
        return await GetQuotationAsync(quotationId, ct);
    }

    public async Task<byte[]> GetQuotationPdfAsync(long quotationId, CancellationToken ct = default)
    {
        var quotation = await LoadScopedAsync(quotationId, ct);
        return QuotationPdfGenerator.Generate(quotation);
    }

    // ── decision ────────────────────────────────────────────────────────────

    private async Task<QuotationDto> DecideAsync(long quotationId, QuotationStatus decision,
        string? reason, CancellationToken ct)
    {
        var quotation = await LoadScopedAsync(quotationId, ct, track: true);

        if (quotation.Status is QuotationStatus.Approved or QuotationStatus.Rejected)
        {
            throw new BusinessRuleException(
                $"This quotation was already {quotation.Status.ToString().ToLowerInvariant()}.");
        }
        if (quotation.Status is QuotationStatus.Draft or QuotationStatus.Cancelled)
        {
            throw new BusinessRuleException("This quotation is not open for a decision.");
        }
        // A lapsed quotation cannot be accepted at yesterday's prices.
        if (quotation.ValidUntil < IstClock.IstDateOf(clock.UtcNow))
        {
            throw new BusinessRuleException(
                $"Quotation {quotation.QuotationNumber} expired on {quotation.ValidUntil:dd MMM yyyy}. " +
                "Ask for a fresh quotation.");
        }

        var nowUtc = clock.UtcNow;
        AddHistory(quotation, decision, nowUtc, reason?.Trim());
        quotation.Status = decision;
        quotation.RespondedUtc = nowUtc;
        quotation.RespondedByUserId = currentUser.UserId;
        if (decision == QuotationStatus.Rejected)
        {
            quotation.RejectionReason = reason?.Trim();
        }

        // The document asks that the admin be told either way (§8).
        var verb = decision == QuotationStatus.Approved ? "approved" : "rejected";
        Notify(quotation, null, NotificationAudience.AllAdmins, $"Quotation {verb}",
            $"{quotation.CustomerName} {verb} quotation {quotation.QuotationNumber}."
            + (string.IsNullOrWhiteSpace(reason) ? "" : $" Reason: {reason.Trim()}"),
            "/admin/quotations", "Open");
        await db.SaveChangesAsync(ct);

        return ToDto(quotation);
    }

    // ── helpers ─────────────────────────────────────────────────────────────

    private async Task<PagedResult<QuotationDto>> PageAsync(IQueryable<Quotation> query,
        QuotationListRequest request, CancellationToken ct)
    {
        var totalCount = await query.CountAsync(ct);
        var rows = await query
            .OrderByDescending(q => q.QuotationDate).ThenByDescending(q => q.QuotationId)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .Include(q => q.Lines)
            .Include(q => q.StatusHistory)
            .ToListAsync(ct);
        return PagedResult<QuotationDto>.Create(
            rows.Select(ToDto).ToList(), request.Page, request.PageSize, totalCount);
    }

    /// <summary>Customers read their own quotations only, whatever id they pass.</summary>
    private async Task<Quotation> LoadScopedAsync(long quotationId, CancellationToken ct,
        bool track = false)
    {
        var query = db.Quotations
            .Include(q => q.Lines.OrderBy(l => l.LineNumber))
            .Include(q => q.StatusHistory)
            .AsQueryable();
        if (!track)
        {
            query = query.AsNoTracking();
        }

        var quotation = await query.FirstOrDefaultAsync(q => q.QuotationId == quotationId, ct)
            ?? throw new NotFoundException("Quotation", quotationId);

        if (currentUser.UserType == UserType.Customer && quotation.CustomerId != currentUser.UserId)
        {
            // 404 rather than 403: a customer learns nothing about other customers' rows.
            throw new NotFoundException("Quotation", quotationId);
        }
        return quotation;
    }

    private async Task GuardHeaderAsync(SaveQuotationRequest request, CancellationToken ct)
    {
        var customer = await db.Users
            .Where(u => u.UserId == request.CustomerId)
            .Select(u => new { u.UserType })
            .FirstOrDefaultAsync(ct)
            ?? throw new NotFoundException("Customer", request.CustomerId);
        if (customer.UserType != UserType.Customer)
        {
            throw new BusinessRuleException("A quotation can only be raised for a customer account.");
        }
        if (!await db.Locations.AnyAsync(l => l.LocationId == request.LocationId, ct))
        {
            throw new NotFoundException("Location", request.LocationId);
        }
    }

    /// <summary>
    /// Server-side money. Line value is quantity × duration × unit price − discount; GST
    /// comes from Tax.* and is split CGST/SGST, matching the booking and pass engines.
    /// Inter-state IGST is not applied anywhere in the system yet, so it stays zero here
    /// too rather than this module inventing a second tax rule.
    /// </summary>
    private async Task ApplyLinesAndTotalsAsync(Quotation quotation, SaveQuotationRequest request,
        CancellationToken ct)
    {
        var cgstPercent = await settings.GetDecimalAsync(AppSettingKeys.Tax.CgstPercent, 9m, ct);
        var sgstPercent = await settings.GetDecimalAsync(AppSettingKeys.Tax.SgstPercent, 9m, ct);
        var taxPercent = cgstPercent + sgstPercent;

        var lineNumber = 0;
        foreach (var source in request.Lines)
        {
            lineNumber++;
            // Duration multiplies the quantity: 5 desks for 3 months is 15 desk-months.
            var units = source.Quantity * (source.DurationValue ?? 1m);
            var gross = Math.Round(units * source.UnitPrice, 2, MidpointRounding.AwayFromZero);
            var lineAmount = Math.Round(gross - source.DiscountAmount, 2, MidpointRounding.AwayFromZero);
            var taxAmount = Math.Round(lineAmount * taxPercent / 100m, 2, MidpointRounding.AwayFromZero);

            quotation.Lines.Add(new QuotationLine
            {
                LineNumber = lineNumber,
                WorkspaceTypeId = source.WorkspaceTypeId,
                ServiceItemId = source.ServiceItemId,
                Description = source.Description.Trim(),
                HsnSacCode = source.HsnSacCode?.Trim(),
                Quantity = source.Quantity,
                DurationValue = source.DurationValue,
                DurationUnit = source.DurationUnit?.Trim(),
                UnitPrice = source.UnitPrice,
                DiscountAmount = source.DiscountAmount,
                LineAmount = lineAmount,
                TaxPercent = taxPercent,
                TaxAmount = taxAmount,
                TotalAmount = Math.Round(lineAmount + taxAmount, 2, MidpointRounding.AwayFromZero)
            });
        }

        quotation.SubTotal = Math.Round(
            quotation.Lines.Sum(l => l.Quantity * (l.DurationValue ?? 1m) * l.UnitPrice), 2,
            MidpointRounding.AwayFromZero);
        quotation.DiscountAmount = quotation.Lines.Sum(l => l.DiscountAmount);
        quotation.TaxableAmount = quotation.Lines.Sum(l => l.LineAmount);

        // Split the total tax so CGST + SGST always equals the tax actually charged,
        // even when the two percentages differ or a line rounds oddly.
        var totalTax = quotation.Lines.Sum(l => l.TaxAmount);
        var cgstAmount = Math.Round(quotation.TaxableAmount * cgstPercent / 100m, 2,
            MidpointRounding.AwayFromZero);
        if (cgstAmount > totalTax)
        {
            cgstAmount = totalTax;
        }
        quotation.CgstPercent = cgstPercent;
        quotation.SgstPercent = sgstPercent;
        quotation.IgstPercent = 0;
        quotation.CgstAmount = cgstAmount;
        quotation.SgstAmount = totalTax - cgstAmount;
        quotation.IgstAmount = 0;
        quotation.TotalAmount = quotation.TaxableAmount + totalTax;
    }

    private void AddHistory(Quotation quotation, QuotationStatus to, DateTime whenUtc, string? reason) =>
        quotation.StatusHistory.Add(new QuotationStatusHistory
        {
            QuotationId = quotation.QuotationId,
            FromStatus = quotation.QuotationId == 0 ? null : quotation.Status,
            ToStatus = to,
            ChangedByUserId = currentUser.UserId,
            ChangedUtc = whenUtc,
            Reason = reason
        });

    /// <summary>Notifications are written as rows, the same way the booking and pass
    /// engines do it — the broadcast service only serves the admin announcement screen.</summary>
    private void Notify(Quotation quotation, long? userId, NotificationAudience audience,
        string title, string message, string route, string actionLabel)
    {
        db.Notifications.Add(new Notification
        {
            UserId = userId,
            Audience = audience,
            Title = title,
            Message = message,
            Category = NotificationCategory.Invoice,
            Priority = NotificationPriority.Normal,
            ReferenceType = "Quotation",
            ReferenceId = quotation.QuotationId,
            ActionLabel = actionLabel,
            ActionRoute = route,
            SentUtc = clock.UtcNow
        });
    }

    private QuotationDto ToDto(Quotation q)
    {
        var today = IstClock.IstDateOf(clock.UtcNow);
        return new QuotationDto(
            q.QuotationId, q.QuotationNumber, q.CustomerId, q.CustomerName, q.CustomerEmail,
            q.CustomerMobile, q.CustomerGstin, q.BillingAddress, q.LocationId,
            db.Locations.Where(l => l.LocationId == q.LocationId)
                .Select(l => l.LocationName).FirstOrDefault(),
            q.QuotationDate, q.ValidUntil, q.SubTotal, q.DiscountAmount, q.TaxableAmount,
            q.CgstPercent, q.CgstAmount, q.SgstPercent, q.SgstAmount, q.IgstPercent, q.IgstAmount,
            q.TotalAmount, (byte)q.Status, q.SentUtc, q.ViewedUtc, q.RespondedUtc,
            q.RespondedByUserId, q.RejectionReason, q.InvoiceId,
            db.Invoices.Where(i => i.InvoiceId == q.InvoiceId)
                .Select(i => i.InvoiceNumber).FirstOrDefault(),
            q.CompanyName, q.CompanyGstin, q.CompanyAddress, q.PlaceOfSupply,
            q.TermsAndConditions, q.Remarks,
            q.ValidUntil < today && q.Status is QuotationStatus.Sent or QuotationStatus.Viewed,
            q.CreatedDate,
            q.Lines.OrderBy(l => l.LineNumber).Select(l => new QuotationLineDto(
                l.QuotationLineId, l.LineNumber, l.WorkspaceTypeId,
                db.WorkspaceTypes.Where(w => w.WorkspaceTypeId == l.WorkspaceTypeId)
                    .Select(w => w.TypeName).FirstOrDefault(),
                l.ServiceItemId,
                db.ServiceItems.Where(s => s.ServiceItemId == l.ServiceItemId)
                    .Select(s => s.ItemName).FirstOrDefault(),
                l.Description, l.HsnSacCode, l.Quantity, l.DurationValue, l.DurationUnit,
                l.UnitPrice, l.DiscountAmount, l.LineAmount, l.TaxPercent, l.TaxAmount,
                l.TotalAmount)).ToList(),
            q.StatusHistory.OrderBy(h => h.ChangedUtc).Select(h => new QuotationStatusHistoryDto(
                h.FromStatus is null ? null : (byte)h.FromStatus, (byte)h.ToStatus,
                h.ChangedByUserId,
                db.Users.Where(u => u.UserId == h.ChangedByUserId)
                    .Select(u => u.FullName).FirstOrDefault() ?? string.Empty,
                h.ChangedUtc, h.Reason)).ToList());
    }
}
