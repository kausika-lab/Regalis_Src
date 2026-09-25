using System.Globalization;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
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
/// Quotations (document §6–§8). Four rules shape this service.
///
/// A quotation is freely editable while Draft and frozen once sent: the customer,
/// company, place-of-supply and tax rates are snapshotted at that moment and the PDF is
/// written to disk, so a later settings change or profile edit never rewrites what the
/// customer was quoted — the same rule TRN_Invoice already lives by.
///
/// New prices never overwrite an old offer. <see cref="ReviseAsync"/> opens a fresh
/// draft numbered …-R1 and marks the original Revised, so the document the customer
/// approves is always exactly the document they were sent (document §1).
///
/// Money is computed here, never accepted from the client. The whole-quotation discount
/// is split across lines in proportion to their value with the last line absorbing the
/// paisa difference, GST is charged per line and split CGST/SGST inside the home state
/// or as IGST outside it, and the total is rounded to the rupee (document §5.2).
///
/// Approval and rejection are the customer's own act, recorded with their identity, the
/// moment and their IP. Everything that happens to a quotation — including sends,
/// notes and downloads — is written to the append-only TRN_QuotationEvent timeline.
/// </summary>
public class QuotationService(
    RegalisDbContext db,
    ISettingService settings,
    INumberSequenceService numberSequence,
    ICurrentUser currentUser,
    IDateTimeProvider clock,
    IQuotationDispatcher dispatcher,
    IInvoiceService invoiceService,
    ILogger<QuotationService> logger,
    IOptions<StorageOptions> storageOptions) : IQuotationService
{
    private const int MaxAttachments = 5;
    private const long MaxAttachmentBytes = 10 * 1024 * 1024;

    /// <summary>Document §3.4: PDF, images, Word and Excel. Checked by extension, size
    /// and leading magic bytes, exactly as ticket attachments are.</summary>
    private static readonly Dictionary<string, string> AllowedAttachmentTypes =
        new(StringComparer.OrdinalIgnoreCase)
        {
            [".pdf"] = "application/pdf",
            [".jpg"] = "image/jpeg",
            [".jpeg"] = "image/jpeg",
            [".png"] = "image/png",
            [".docx"] = "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            [".xlsx"] = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"
        };

    /// <summary>Statuses that are still waiting on the customer — the "Awaiting reply"
    /// card and the expiry sweep both mean exactly these two (document §3.1).</summary>
    private static readonly QuotationStatus[] OpenStatuses =
        [QuotationStatus.Sent, QuotationStatus.Viewed];

    // ── Reads ───────────────────────────────────────────────────────────────

    public async Task<PagedResult<QuotationDto>> GetQuotationsAsync(QuotationListRequest request,
        CancellationToken ct = default) =>
        await PageAsync(ApplyFilters(db.Quotations.AsNoTracking(), request), request, ct);

    public async Task<List<QuotationItemRateDto>> GetItemRatesAsync(int inventoryItemId, int take,
        CancellationToken ct = default)
    {
        take = Math.Clamp(take, 1, 20);
        return await db.QuotationLines.AsNoTracking()
            .Where(l => l.InventoryItemId == inventoryItemId
                        && l.Quotation.Status != QuotationStatus.Draft
                        && l.Quotation.Status != QuotationStatus.Cancelled)
            .OrderByDescending(l => l.Quotation.QuotationDate)
            .ThenByDescending(l => l.QuotationId)
            .Take(take)
            .Select(l => new QuotationItemRateDto(
                l.QuotationId, l.Quotation.QuotationNumber, l.Quotation.QuotationDate,
                l.Quotation.CustomerName, l.UnitPrice, (byte)l.Quotation.Status,
                db.Invoices.Where(i => i.InvoiceId == l.Quotation.InvoiceId)
                    .Select(i => i.InvoiceNumber).FirstOrDefault()))
            .ToListAsync(ct);
    }

    public async Task<QuotationStatusCountsDto> GetStatusCountsAsync(
        QuotationListRequest request, CancellationToken ct = default)
    {
        var today = IstClock.IstDateOf(clock.UtcNow);
        // Every filter on screen except the tab itself, so each tab says how many rows
        // it would show. A lapsed open quotation counts as Expired, as its pill reads.
        request.Status = null;
        var groups = await ApplyFilters(db.Quotations.AsNoTracking(), request)
            .GroupBy(q => new
            {
                q.Status,
                Lapsed = (q.Status == QuotationStatus.Sent || q.Status == QuotationStatus.Viewed)
                         && q.ValidUntil < today
            })
            .Select(g => new { g.Key.Status, g.Key.Lapsed, Count = g.Count() })
            .ToListAsync(ct);

        var counts = Enum.GetValues<QuotationStatus>().ToDictionary(s => (byte)s, _ => 0);
        foreach (var g in groups)
        {
            var effective = g.Lapsed ? QuotationStatus.Expired : g.Status;
            counts[(byte)effective] += g.Count;
        }
        return new QuotationStatusCountsDto(groups.Sum(g => g.Count), counts);
    }

    public async Task<QuotationSummaryDto> GetSummaryAsync(CancellationToken ct = default)
    {
        var today = IstClock.IstDateOf(clock.UtcNow);
        var yearStart = IndianMoney.FinancialYearStart(today);
        var yearEnd = IndianMoney.FinancialYearEnd(today);

        // One pass over the financial year: the four cards and every tab count come
        // from the same rows, so the tabs can never disagree with the cards.
        var rows = await db.Quotations.AsNoTracking()
            .Where(q => q.QuotationDate >= yearStart && q.QuotationDate <= yearEnd)
            .Select(q => new { q.Status, q.TotalAmount, q.ValidUntil })
            .ToListAsync(ct);

        var statusCounts = rows.GroupBy(r => (byte)r.Status)
            .ToDictionary(g => g.Key, g => g.Count());
        // A lapsed row reads as Expired on the tabs the moment it lapses, matching the
        // IsExpired the list itself reports, rather than waiting for the sweeper.
        var lapsed = rows.Count(r => OpenStatuses.Contains(r.Status) && r.ValidUntil < today);
        if (lapsed > 0)
        {
            foreach (var open in OpenStatuses)
            {
                var stillOpen = rows.Count(r => r.Status == open && r.ValidUntil >= today);
                statusCounts[(byte)open] = stillOpen;
            }
            statusCounts[(byte)QuotationStatus.Expired] =
                statusCounts.GetValueOrDefault((byte)QuotationStatus.Expired) + lapsed;
        }
        foreach (var status in Enum.GetValues<QuotationStatus>())
        {
            statusCounts.TryAdd((byte)status, 0);
        }

        var awaiting = rows.Where(r => OpenStatuses.Contains(r.Status) && r.ValidUntil >= today)
            .ToList();
        var approved = rows.Where(r => r.Status == QuotationStatus.Approved).ToList();
        var expiringSoon = awaiting
            .Where(r => (r.ValidUntil.DayNumber - today.DayNumber) <= 3)
            .ToList();

        return new QuotationSummaryDto(
            IndianMoney.FinancialYearOf(today),
            rows.Count, rows.Sum(r => r.TotalAmount),
            awaiting.Count, awaiting.Sum(r => r.TotalAmount),
            approved.Count, approved.Sum(r => r.TotalAmount),
            expiringSoon.Count, expiringSoon.Sum(r => r.TotalAmount),
            statusCounts);
    }

    public async Task<QuotationFormOptionsDto> GetFormOptionsAsync(CancellationToken ct = default)
    {
        var defaultGst = await settings.GetDecimalAsync(
            AppSettingKeys.Quotation.DefaultGstPercent, 18m, ct);
        var rateOptions = ParseRates(await settings.GetStringAsync(
            AppSettingKeys.Quotation.GstRateOptions, "0,5,12,18,28", ct));

        var workspaces = await db.Workspaces.AsNoTracking()
            .Where(w => w.IsActive && !w.IsDeleted)
            .Select(w => new
            {
                w.WorkspaceId,
                w.WorkspaceTypeId,
                TypeCode = w.WorkspaceType.TypeCode,
                TypeName = w.WorkspaceType.TypeName,
                w.WorkspaceCode,
                w.WorkspaceName,
                w.Capacity,
                w.PricePerHour,
                w.PricePerDay,
                w.PricePerMonth
            })
            .OrderBy(w => w.TypeCode).ThenBy(w => w.WorkspaceCode)
            .ToListAsync(ct);

        var services = new List<QuotationServiceOptionDto>();
        foreach (var w in workspaces)
        {
            var serviceType = ServiceTypeOf(w.TypeCode);
            var group = GroupOf(serviceType);
            services.Add(new QuotationServiceOptionDto(
                (byte)serviceType, group, w.WorkspaceId, w.WorkspaceTypeId, null,
                w.WorkspaceCode, w.WorkspaceName, w.Capacity,
                DefaultUnitOf(serviceType),
                w.PricePerHour, w.PricePerDay, w.PricePerMonth, null,
                // A named cabin or meeting room is one thing: a second one is a second
                // line, not a quantity of two (document §3.3).
                QuantityLocked: serviceType is QuotationServiceType.Cabin
                    or QuotationServiceType.MeetingRoom));
        }

        var passPlans = await db.PassPlans.AsNoTracking()
            .Where(p => p.IsActive && !p.IsDeleted)
            .OrderBy(p => p.DisplayOrder).ThenBy(p => p.PlanName)
            .Select(p => new { p.PassPlanId, p.PlanCode, p.PlanName, p.Price })
            .ToListAsync(ct);
        services.AddRange(passPlans.Select(p => new QuotationServiceOptionDto(
            (byte)QuotationServiceType.Pass, "Passes", null, null, p.PassPlanId,
            p.PlanCode, p.PlanName, null, "One-time", null, null, null, p.Price,
            QuantityLocked: false)));

        var serviceItems = await db.ServiceItems.AsNoTracking()
            .Where(s => s.IsActive && !s.IsDeleted)
            .OrderBy(s => s.ItemName)
            .Select(s => new { s.ServiceItemId, s.ItemCode, s.ItemName, s.Price })
            .ToListAsync(ct);
        services.AddRange(serviceItems.Select(s => new QuotationServiceOptionDto(
            (byte)QuotationServiceType.Other, "Other", null, null, null,
            s.ItemCode, s.ItemName, null, "One-time", null, null, null, s.Price,
            QuantityLocked: false)));

        return new QuotationFormOptionsDto(
            await numberSequence.PeekAsync(SequenceKeys.Quotation, ct),
            await settings.GetIntAsync(AppSettingKeys.Quotation.ValidityDays, 15, ct),
            defaultGst,
            rateOptions,
            await settings.GetStringAsync(AppSettingKeys.Quotation.HomeStateCode, "29", ct),
            await settings.GetBoolAsync(AppSettingKeys.Quotation.RoundOffEnabled, true, ct),
            await settings.GetStringAsync(AppSettingKeys.Quotation.TermsAndConditions, "", ct),
            await settings.GetStringAsync(AppSettingKeys.Quotation.CustomerNotes, "", ct),
            await settings.GetDecimalAsync(AppSettingKeys.Quotation.AdvanceMonths, 1m, ct),
            await LoadTdsOptionsAsync(ct),
            services,
            GstStates.All);
    }

    public async Task<byte[]> ExportAsync(QuotationListRequest request,
        CancellationToken ct = default)
    {
        // The file follows the screen: same filters, same order.
        var rows = await Sorted(ApplyFilters(db.Quotations.AsNoTracking(), request), request)
            .Include(q => q.Lines)
            .Take(5000)
            .ToListAsync(ct);

        var today = IstClock.IstDateOf(clock.UtcNow);
        var csv = new StringBuilder();
        csv.AppendLine("Quotation,Date,Customer,Company,Reference,Subject,Lines,Sub total," +
                       "Discount,Taxable,CGST,SGST,IGST,TDS,Round off,Total,Valid until,Status,Invoice");
        foreach (var q in rows)
        {
            csv.AppendLine(string.Join(',', new[]
            {
                Csv(q.QuotationNumber),
                Csv(q.QuotationDate.ToString("yyyy-MM-dd")),
                Csv(q.CustomerName),
                Csv(q.CustomerCompanyName),
                Csv(q.ReferenceNumber),
                Csv(q.Subject),
                Csv(q.Lines.Count.ToString()),
                Csv(Money(q.SubTotal)),
                Csv(Money(q.DiscountAmount)),
                Csv(Money(q.TaxableAmount)),
                Csv(Money(q.CgstAmount)),
                Csv(Money(q.SgstAmount)),
                Csv(Money(q.IgstAmount)),
                Csv(Money(q.TdsAmount)),
                Csv(Money(q.RoundOffAmount)),
                Csv(Money(q.TotalAmount)),
                Csv(q.ValidUntil.ToString("yyyy-MM-dd")),
                Csv(StatusLabel(q, today)),
                Csv(db.Invoices.Where(i => i.InvoiceId == q.InvoiceId)
                    .Select(i => i.InvoiceNumber).FirstOrDefault())
            }));
        }
        // Excel opens a UTF-8 CSV as ANSI without the BOM, which mangles the ₹ sign
        // and any non-ASCII customer name.
        return Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(csv.ToString())).ToArray();
    }

    public async Task<QuotationDto> GetQuotationAsync(long quotationId,
        CancellationToken ct = default) =>
        await ToDtoAsync(await LoadScopedAsync(quotationId, ct), ct);

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

    public async Task<QuotationDto> GetMyQuotationAsync(long quotationId,
        CancellationToken ct = default)
    {
        var quotation = await LoadScopedAsync(quotationId, ct, track: true);
        if (quotation.Status == QuotationStatus.Draft)
        {
            // Never disclose that an unsent draft exists.
            throw new NotFoundException("Quotation", quotationId);
        }

        // Opening a sent quotation is what "Viewed" means (document §6). An admin
        // opening the customer's copy never counts — only the customer's own visit.
        if (quotation.Status == QuotationStatus.Sent && quotation.CustomerId == currentUser.UserId)
        {
            var nowUtc = clock.UtcNow;
            AddHistory(quotation, QuotationStatus.Viewed, nowUtc, null);
            AddEvent(quotation, QuotationEventType.Viewed, QuotationActorType.Customer,
                $"{quotation.CustomerName} opened the quotation.", nowUtc);
            quotation.Status = QuotationStatus.Viewed;
            quotation.ViewedUtc = nowUtc;

            Notify(quotation, null, NotificationAudience.AllAdmins, "Quotation viewed",
                $"{quotation.CustomerName} opened quotation {quotation.QuotationNumber}.",
                $"/admin/billing/quotations/{quotation.QuotationId}", "Open");
            await db.SaveChangesAsync(ct);
        }

        return await ToDtoAsync(quotation, ct, forCustomer: true);
    }

    // ── Create / update / delete ────────────────────────────────────────────

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
            LocationId = await ResolveLocationAsync(request.LocationId, null, ct),
            QuotationDate = quotationDate,
            ValidUntil = request.ValidUntil ?? quotationDate.AddDays(validityDays),
            Status = QuotationStatus.Draft,
            // The creator is the salesperson unless one was named (document §2.2).
            SalespersonId = request.SalespersonId ?? currentUser.UserId,
            TermsAndConditions = string.IsNullOrWhiteSpace(request.TermsAndConditions)
                ? await settings.GetStringAsync(AppSettingKeys.Quotation.TermsAndConditions, "", ct)
                : request.TermsAndConditions.Trim(),
            Remarks = string.IsNullOrWhiteSpace(request.Remarks)
                ? await settings.GetStringAsync(AppSettingKeys.Quotation.CustomerNotes, "", ct)
                : request.Remarks.Trim()
        };
        ApplyHeader(quotation, request);
        await ApplyLinesAndTotalsAsync(quotation, request, ct);

        var strategy = db.Database.CreateExecutionStrategy();
        var newId = await strategy.ExecuteAsync(async () =>
        {
            await using var dbTransaction = await db.Database.BeginTransactionAsync(ct);
            quotation.QuotationNumber = await numberSequence.NextAsync(SequenceKeys.Quotation, ct);
            db.Quotations.Add(quotation);
            AddHistory(quotation, QuotationStatus.Draft, clock.UtcNow, null);
            AddEvent(quotation, QuotationEventType.Created, QuotationActorType.Admin,
                "Quotation created as a draft.", clock.UtcNow);
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

        EnsureDraft(quotation);
        await GuardHeaderAsync(request, ct);

        quotation.CustomerId = request.CustomerId;
        quotation.LocationId = await ResolveLocationAsync(request.LocationId, quotation.LocationId, ct);
        quotation.QuotationDate = request.QuotationDate ?? quotation.QuotationDate;
        quotation.ValidUntil = request.ValidUntil ?? quotation.ValidUntil;
        quotation.TermsAndConditions = request.TermsAndConditions?.Trim();
        quotation.Remarks = request.Remarks?.Trim();
        ApplyHeader(quotation, request);

        db.QuotationLines.RemoveRange(quotation.Lines);
        quotation.Lines.Clear();
        await ApplyLinesAndTotalsAsync(quotation, request, ct);
        AddEvent(quotation, QuotationEventType.Updated, QuotationActorType.Admin,
            "Draft updated.", clock.UtcNow);

        await db.SaveChangesAsync(ct);
        return await GetQuotationAsync(quotationId, ct);
    }

    public async Task DeleteAsync(long quotationId, CancellationToken ct = default)
    {
        var quotation = await db.Quotations
            .FirstOrDefaultAsync(q => q.QuotationId == quotationId, ct)
            ?? throw new NotFoundException("Quotation", quotationId);
        EnsureDraft(quotation);

        // Soft delete, like every other business table: the number it consumed is
        // never handed out again (document §5.3).
        quotation.IsDeleted = true;
        quotation.IsActive = false;
        quotation.DeletedBy = currentUser.UserId;
        quotation.DeletedDate = clock.UtcNow;
        AddEvent(quotation, QuotationEventType.Deleted, QuotationActorType.Admin,
            "Draft deleted.", clock.UtcNow);
        await db.SaveChangesAsync(ct);
    }

    // ── Send ────────────────────────────────────────────────────────────────

    public async Task<QuotationDto> SendAsync(long quotationId, SendQuotationRequest request,
        CancellationToken ct = default)
    {
        var quotation = await db.Quotations
            .Include(q => q.Lines)
            .Include(q => q.Attachments)
            .FirstOrDefaultAsync(q => q.QuotationId == quotationId, ct)
            ?? throw new NotFoundException("Quotation", quotationId);

        var resend = quotation.Status is QuotationStatus.Sent or QuotationStatus.Viewed;
        if (quotation.Status != QuotationStatus.Draft && !resend)
        {
            throw new BusinessRuleException("Only a draft or an already-sent quotation can be sent.");
        }
        if (quotation.ValidUntil < IstClock.IstDateOf(clock.UtcNow))
        {
            throw new BusinessRuleException(
                "The validity date has already passed. Revise it before sending.");
        }
        if (!quotation.Lines.Any(l => l.LineType != QuotationLineType.Header && l.TotalAmount > 0))
        {
            throw new BusinessRuleException("Add at least one priced item before sending.");
        }

        if (!resend)
        {
            await SnapshotAsync(quotation, ct);
            // A typed-in customer has no account to read contacts from: the send dialog
            // is where they were given, so that is what the PDF and the record keep.
            if (quotation.CustomerId is null)
            {
                if (request.SendEmail) quotation.CustomerEmail = request.EmailTo!.Trim();
                if (request.SendWhatsApp) quotation.CustomerMobile = request.WhatsAppTo!.Trim();
            }
        }

        // The PDF is written once and never regenerated, so the customer's copy never
        // changes underneath them (document §3.8).
        var pdf = await EnsurePdfAsync(quotation, ct);
        var portalLink = await PortalLinkAsync(quotation, ct);

        var nowUtc = clock.UtcNow;
        var anySucceeded = false;
        var failures = new List<string>();

        if (request.SendEmail)
        {
            var files = new List<QuotationOutboundFile>
            {
                new(PdfFileName(quotation), "application/pdf", pdf)
            };
            foreach (var attachment in quotation.Attachments.Where(a => a.SendWithEmail))
            {
                if (File.Exists(attachment.FilePath))
                {
                    files.Add(new QuotationOutboundFile(attachment.FileName, attachment.ContentType,
                        await File.ReadAllBytesAsync(attachment.FilePath, ct)));
                }
            }

            var subject = string.IsNullOrWhiteSpace(request.EmailSubject)
                ? await TemplateAsync(AppSettingKeys.Quotation.EmailSubjectTemplate,
                    "Quotation {quote_number} from Regalis WorkSpace", quotation, portalLink, ct)
                : Fill(request.EmailSubject, quotation, portalLink);
            // Without an account there is no portal to approve in, so the configured
            // template (which points there) gives way to one that asks for a reply.
            var body = !string.IsNullOrWhiteSpace(request.EmailMessage)
                ? Fill(request.EmailMessage, quotation, portalLink)
                : quotation.CustomerId is null
                    ? Fill(UnlinkedEmailBody, quotation, portalLink)
                    : await TemplateAsync(AppSettingKeys.Quotation.EmailBodyTemplate,
                        DefaultEmailBody, quotation, portalLink, ct);

            var result = await dispatcher.SendEmailAsync(new QuotationEmailMessage(
                request.EmailTo!.Trim(), request.EmailCc?.Trim(),
                request.BccMe ? currentUser.Email : null,
                subject, EmailHtml(body, portalLink), files), ct);

            anySucceeded |= result.Succeeded;
            if (!result.Succeeded)
            {
                failures.Add($"Email: {result.FailureReason}");
            }
            AddEvent(quotation, QuotationEventType.SentEmail, QuotationActorType.Admin,
                result.Succeeded
                    ? result.Note ?? $"Emailed to {request.EmailTo}."
                    : $"Email to {request.EmailTo} failed.",
                nowUtc, "Email", request.EmailTo,
                result.Succeeded ? QuotationDeliveryStatus.Sent : QuotationDeliveryStatus.Failed,
                result.FailureReason);
        }

        if (request.SendWhatsApp)
        {
            var message = !string.IsNullOrWhiteSpace(request.WhatsAppMessage)
                ? Fill(request.WhatsAppMessage, quotation, portalLink)
                : quotation.CustomerId is null
                    ? Fill(UnlinkedWhatsAppBody, quotation, portalLink)
                    : await TemplateAsync(AppSettingKeys.Quotation.WhatsAppTemplate,
                        DefaultWhatsAppBody, quotation, portalLink, ct);

            var result = await dispatcher.SendWhatsAppAsync(new QuotationWhatsAppMessage(
                request.WhatsAppTo!.Trim(), message, portalLink,
                new QuotationOutboundFile(PdfFileName(quotation), "application/pdf", pdf)), ct);

            anySucceeded |= result.Succeeded;
            if (!result.Succeeded)
            {
                failures.Add($"WhatsApp: {result.FailureReason}");
            }
            AddEvent(quotation, QuotationEventType.SentWhatsApp, QuotationActorType.Admin,
                result.Succeeded
                    ? result.Note ?? $"WhatsApp sent to {request.WhatsAppTo}."
                    : $"WhatsApp to {request.WhatsAppTo} failed.",
                nowUtc, "WhatsApp", request.WhatsAppTo,
                result.Succeeded ? QuotationDeliveryStatus.Sent : QuotationDeliveryStatus.Failed,
                result.FailureReason);
        }

        if (!anySucceeded)
        {
            // Nothing reached the customer, so the quotation stays a draft and stays
            // editable. The events above are still saved: a failed send is a fact.
            await db.SaveChangesAsync(ct);
            throw new BusinessRuleException(
                "The quotation could not be sent. " + string.Join(" ", failures));
        }

        if (!resend)
        {
            AddHistory(quotation, QuotationStatus.Sent, nowUtc, null);
            quotation.Status = QuotationStatus.Sent;
            quotation.SentUtc = nowUtc;

            if (quotation.CustomerId is { } customerId)
            {
                Notify(quotation, customerId, NotificationAudience.SingleUser,
                    "A new quotation is ready",
                    $"Quotation {quotation.QuotationNumber} for Rs {quotation.TotalAmount:N2} " +
                    "is waiting for your approval.",
                    $"/quotations/{quotation.QuotationId}", "View quotation");
            }
        }
        await db.SaveChangesAsync(ct);

        return await GetQuotationAsync(quotationId, ct);
    }

    // ── Revise / clone ──────────────────────────────────────────────────────

    public async Task<QuotationDto> ReviseAsync(long quotationId, ReviseQuotationRequest request,
        CancellationToken ct = default)
    {
        var original = await db.Quotations
            .Include(q => q.Lines)
            .FirstOrDefaultAsync(q => q.QuotationId == quotationId, ct)
            ?? throw new NotFoundException("Quotation", quotationId);

        if (original.Status is not (QuotationStatus.Sent or QuotationStatus.Viewed
            or QuotationStatus.Expired or QuotationStatus.Rejected))
        {
            throw new BusinessRuleException(
                "Only a quotation the customer already has can be revised. " +
                "A draft is edited, and a decided quotation is cloned.");
        }
        if (await db.Quotations.AnyAsync(q => q.RevisionOfQuotationId == quotationId, ct))
        {
            throw new BusinessRuleException("This quotation has already been revised.");
        }

        var nowUtc = clock.UtcNow;
        var today = IstClock.IstDateOf(nowUtc);
        var validityDays = await settings.GetIntAsync(AppSettingKeys.Quotation.ValidityDays, 15, ct);

        var revision = CopyOf(original);
        revision.RevisionOfQuotationId = original.QuotationId;
        revision.RevisionNumber = original.RevisionNumber + 1;
        revision.QuotationNumber = $"{BaseNumber(original.QuotationNumber)}-R{revision.RevisionNumber}";
        revision.QuotationDate = today;
        revision.ValidUntil = today.AddDays(validityDays);
        revision.Status = QuotationStatus.Draft;

        db.Quotations.Add(revision);

        AddHistory(original, QuotationStatus.Revised, nowUtc, request.Reason?.Trim());
        AddEvent(original, QuotationEventType.Revised, QuotationActorType.Admin,
            $"Revised as {revision.QuotationNumber}."
            + (string.IsNullOrWhiteSpace(request.Reason) ? "" : $" Reason: {request.Reason.Trim()}"),
            nowUtc);
        original.Status = QuotationStatus.Revised;

        await db.SaveChangesAsync(ct);

        AddEvent(revision, QuotationEventType.Created, QuotationActorType.Admin,
            $"Created as revision {revision.RevisionNumber} of {original.QuotationNumber}.", nowUtc);
        AddHistory(revision, QuotationStatus.Draft, nowUtc, null);
        await db.SaveChangesAsync(ct);

        return await GetQuotationAsync(revision.QuotationId, ct);
    }

    public async Task<QuotationDto> CloneAsync(long quotationId, CancellationToken ct = default)
    {
        var original = await db.Quotations
            .Include(q => q.Lines)
            .FirstOrDefaultAsync(q => q.QuotationId == quotationId, ct)
            ?? throw new NotFoundException("Quotation", quotationId);

        var nowUtc = clock.UtcNow;
        var today = IstClock.IstDateOf(nowUtc);
        var validityDays = await settings.GetIntAsync(AppSettingKeys.Quotation.ValidityDays, 15, ct);

        var clone = CopyOf(original);
        clone.QuotationDate = today;
        clone.ValidUntil = today.AddDays(validityDays);
        clone.Status = QuotationStatus.Draft;

        var strategy = db.Database.CreateExecutionStrategy();
        var newId = await strategy.ExecuteAsync(async () =>
        {
            await using var dbTransaction = await db.Database.BeginTransactionAsync(ct);
            clone.QuotationNumber = await numberSequence.NextAsync(SequenceKeys.Quotation, ct);
            db.Quotations.Add(clone);
            AddHistory(clone, QuotationStatus.Draft, nowUtc, null);
            AddEvent(clone, QuotationEventType.Cloned, QuotationActorType.Admin,
                $"Cloned from {original.QuotationNumber}.", nowUtc);
            await db.SaveChangesAsync(ct);
            await dbTransaction.CommitAsync(ct);
            return clone.QuotationId;
        });

        return await GetQuotationAsync(newId, ct);
    }

    // ── Decisions ───────────────────────────────────────────────────────────

    public async Task<QuotationDto> ApproveAsync(long quotationId,
        QuotationDecisionRequest request, CancellationToken ct = default)
    {
        if (!request.AcceptedTerms)
        {
            throw new AppValidationException([new ApiError(ErrorCodes.ValidationError,
                "Accept the terms & conditions to approve this quotation.",
                nameof(QuotationDecisionRequest.AcceptedTerms))]);
        }
        return await DecideAsync(quotationId, QuotationStatus.Approved, request.ReasonCode,
            request.Reason, onBehalf: false, null, null, ct);
    }

    public async Task<QuotationDto> RejectAsync(long quotationId,
        QuotationDecisionRequest request, CancellationToken ct = default)
    {
        // "Other" is the one reason that needs saying out loud (document §3.7).
        if (string.Equals(request.ReasonCode, "OTHER", StringComparison.OrdinalIgnoreCase)
            && string.IsNullOrWhiteSpace(request.Reason))
        {
            throw new AppValidationException([new ApiError(ErrorCodes.ValidationError,
                "Tell us a little more about why.", nameof(QuotationDecisionRequest.Reason))]);
        }
        return await DecideAsync(quotationId, QuotationStatus.Rejected, request.ReasonCode,
            request.Reason, onBehalf: false, null, null, ct);
    }

    public async Task<QuotationDto> RecordDecisionAsync(long quotationId,
        RecordQuotationDecisionRequest request, CancellationToken ct = default) =>
        await DecideAsync(quotationId,
            request.Approved ? QuotationStatus.Approved : QuotationStatus.Rejected,
            request.ReasonCode, request.Note, onBehalf: true,
            (QuotationDecisionMode)request.Mode, request.ConfirmedBy.Trim(), ct);

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
        AddEvent(quotation, QuotationEventType.Cancelled, QuotationActorType.Admin,
            $"Cancelled. Reason: {reason.Trim()}", nowUtc);
        quotation.Status = QuotationStatus.Cancelled;
        await db.SaveChangesAsync(ct);
        return await GetQuotationAsync(quotationId, ct);
    }

    public async Task<QuotationDto> AddInternalNoteAsync(long quotationId, string note,
        CancellationToken ct = default)
    {
        var quotation = await db.Quotations
            .FirstOrDefaultAsync(q => q.QuotationId == quotationId, ct)
            ?? throw new NotFoundException("Quotation", quotationId);

        // An internal note is the one thing that may be written to a frozen quotation:
        // it is never shown to the customer and never printed (document §3.6).
        quotation.InternalNote = note.Trim();
        AddEvent(quotation, QuotationEventType.NoteAdded, QuotationActorType.Admin,
            "Internal note updated.", clock.UtcNow);
        await db.SaveChangesAsync(ct);
        return await GetQuotationAsync(quotationId, ct);
    }

    // ── Attachments ─────────────────────────────────────────────────────────

    public async Task<QuotationDto> AddAttachmentAsync(long quotationId, AttachmentUpload upload,
        CancellationToken ct = default)
    {
        var quotation = await db.Quotations
            .FirstOrDefaultAsync(q => q.QuotationId == quotationId, ct)
            ?? throw new NotFoundException("Quotation", quotationId);
        EnsureDraft(quotation);

        if (await db.QuotationAttachments.CountAsync(a => a.QuotationId == quotationId, ct)
            >= MaxAttachments)
        {
            throw new BusinessRuleException(
                $"Maximum {MaxAttachments} files of 10 MB each.");
        }
        if (upload.Length is <= 0 or > MaxAttachmentBytes)
        {
            throw new BusinessRuleException($"Maximum {MaxAttachments} files of 10 MB each.");
        }
        var extension = Path.GetExtension(upload.FileName);
        if (string.IsNullOrEmpty(extension) ||
            !AllowedAttachmentTypes.TryGetValue(extension, out var contentType))
        {
            throw new BusinessRuleException("Allowed file types: .pdf .jpg .jpeg .png .docx .xlsx");
        }

        using var buffer = new MemoryStream();
        await upload.Content.CopyToAsync(buffer, ct);
        var bytes = buffer.ToArray();
        if (bytes.LongLength > MaxAttachmentBytes)
        {
            throw new BusinessRuleException($"Maximum {MaxAttachments} files of 10 MB each.");
        }
        // Office files are ZIP containers, which the sniffer does not cover; everything
        // it does know about is checked, so a renamed executable never gets stored.
        if (AllowedAttachmentTypes.ContainsKey(extension)
            && extension is not (".docx" or ".xlsx")
            && !FileTypeSniffer.Matches(extension, bytes))
        {
            throw new BusinessRuleException("The file content does not match its extension.");
        }
        if (extension is ".docx" or ".xlsx" && !(bytes is [0x50, 0x4B, ..]))
        {
            throw new BusinessRuleException("The file content does not match its extension.");
        }

        var storedFileName = $"{Guid.NewGuid():N}{extension.ToLowerInvariant()}";
        var directory = Path.Combine(storageOptions.Value.UploadRoot, "quotations",
            quotation.QuotationId.ToString(CultureInfo.InvariantCulture));
        Directory.CreateDirectory(directory);
        var filePath = Path.Combine(directory, storedFileName);
        await File.WriteAllBytesAsync(filePath, bytes, ct);

        db.QuotationAttachments.Add(new QuotationAttachment
        {
            QuotationId = quotation.QuotationId,
            FileName = Path.GetFileName(upload.FileName),
            StoredFileName = storedFileName,
            FilePath = filePath,
            ContentType = contentType,
            FileSizeBytes = bytes.LongLength,
            SendWithEmail = true,
            UploadedByUserId = currentUser.UserId,
            UploadedUtc = clock.UtcNow
        });
        AddEvent(quotation, QuotationEventType.AttachmentAdded, QuotationActorType.Admin,
            $"Attached {Path.GetFileName(upload.FileName)}.", clock.UtcNow);
        await db.SaveChangesAsync(ct);
        return await GetQuotationAsync(quotationId, ct);
    }

    public async Task<QuotationDto> UpdateAttachmentAsync(long quotationId, long attachmentId,
        bool sendWithEmail, CancellationToken ct = default)
    {
        var quotation = await db.Quotations
            .FirstOrDefaultAsync(q => q.QuotationId == quotationId, ct)
            ?? throw new NotFoundException("Quotation", quotationId);
        EnsureDraft(quotation);

        var attachment = await db.QuotationAttachments
            .FirstOrDefaultAsync(a => a.QuotationAttachmentId == attachmentId
                                      && a.QuotationId == quotationId, ct)
            ?? throw new NotFoundException("QuotationAttachment", attachmentId);

        attachment.SendWithEmail = sendWithEmail;
        await db.SaveChangesAsync(ct);
        return await GetQuotationAsync(quotationId, ct);
    }

    public async Task<QuotationDto> RemoveAttachmentAsync(long quotationId, long attachmentId,
        CancellationToken ct = default)
    {
        var quotation = await db.Quotations
            .FirstOrDefaultAsync(q => q.QuotationId == quotationId, ct)
            ?? throw new NotFoundException("Quotation", quotationId);
        EnsureDraft(quotation);

        var attachment = await db.QuotationAttachments
            .FirstOrDefaultAsync(a => a.QuotationAttachmentId == attachmentId
                                      && a.QuotationId == quotationId, ct)
            ?? throw new NotFoundException("QuotationAttachment", attachmentId);

        db.QuotationAttachments.Remove(attachment);
        AddEvent(quotation, QuotationEventType.AttachmentRemoved, QuotationActorType.Admin,
            $"Removed {attachment.FileName}.", clock.UtcNow);
        await db.SaveChangesAsync(ct);

        // The row is gone whether or not the file is: a missing file must not leave an
        // attachment the admin cannot delete.
        if (File.Exists(attachment.FilePath))
        {
            File.Delete(attachment.FilePath);
        }
        return await GetQuotationAsync(quotationId, ct);
    }

    public async Task<(byte[] Content, string ContentType, string FileName)> GetAttachmentAsync(
        long quotationId, long attachmentId, CancellationToken ct = default)
    {
        // Goes through the scoped load, so a customer can only read their own.
        await LoadScopedAsync(quotationId, ct);
        var attachment = await db.QuotationAttachments.AsNoTracking()
            .FirstOrDefaultAsync(a => a.QuotationAttachmentId == attachmentId
                                      && a.QuotationId == quotationId, ct)
            ?? throw new NotFoundException("QuotationAttachment", attachmentId);
        if (!File.Exists(attachment.FilePath))
        {
            throw new NotFoundException("QuotationAttachment", attachmentId);
        }
        return (await File.ReadAllBytesAsync(attachment.FilePath, ct),
            attachment.ContentType, attachment.FileName);
    }

    public async Task<byte[]> GetQuotationPdfAsync(long quotationId, CancellationToken ct = default)
    {
        var quotation = await LoadScopedAsync(quotationId, ct);
        // A sent quotation serves the frozen file; a draft is rendered fresh each time,
        // because a draft has nothing to be faithful to yet.
        if (!string.IsNullOrWhiteSpace(quotation.PdfPath) && File.Exists(quotation.PdfPath))
        {
            return await File.ReadAllBytesAsync(quotation.PdfPath, ct);
        }
        // Previewing a draft: it has no snapshot yet, so fill the customer and
        // company blocks from what they are right now. The row is untracked, so
        // nothing here is written back — the real snapshot happens on send.
        if (quotation.SentUtc is null)
        {
            await SnapshotAsync(quotation, ct);
        }
        return QuotationPdfGenerator.Generate(quotation, await PdfContextAsync(quotation, ct));
    }

    /// <summary>The salesperson, the portal link and the company's contact details —
    /// everything the printed document needs that the row itself does not carry.</summary>
    private async Task<QuotationPdfContext> PdfContextAsync(Quotation quotation,
        CancellationToken ct) =>
        new(quotation.SalespersonId is null
                ? null
                : await db.Users.AsNoTracking()
                    .Where(u => u.UserId == quotation.SalespersonId)
                    .Select(u => u.FullName).FirstOrDefaultAsync(ct),
            await PortalLinkAsync(quotation, ct),
            await settings.GetStringAsync(AppSettingKeys.Company.Email, "", ct),
            await settings.GetStringAsync(AppSettingKeys.Company.Phone, "", ct));

    // ── decision core ───────────────────────────────────────────────────────

    private async Task<QuotationDto> DecideAsync(long quotationId, QuotationStatus decision,
        string? reasonCode, string? reason, bool onBehalf, QuotationDecisionMode? mode,
        string? confirmedBy, CancellationToken ct)
    {
        var quotation = await LoadScopedAsync(quotationId, ct, track: true);

        if (quotation.Status is QuotationStatus.Approved or QuotationStatus.Rejected)
        {
            throw new BusinessRuleException(
                $"This quotation was already {quotation.Status.ToString().ToLowerInvariant()}.");
        }
        if (quotation.Status is QuotationStatus.Draft or QuotationStatus.Cancelled
            or QuotationStatus.Revised)
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
        // Only the newest revision can be approved (document §5.3).
        if (await db.Quotations.AnyAsync(q => q.RevisionOfQuotationId == quotationId, ct))
        {
            throw new BusinessRuleException(
                "This quotation has been replaced by a newer revision. Act on that one instead.");
        }

        var nowUtc = clock.UtcNow;
        AddHistory(quotation, decision, nowUtc, reason?.Trim());
        quotation.Status = decision;
        quotation.RespondedUtc = nowUtc;
        // On-behalf keeps the customer as the decision's subject; who typed it is on
        // the event and in DecisionConfirmedBy (document §3.6).
        quotation.RespondedByUserId = onBehalf ? quotation.CustomerId : currentUser.UserId;
        quotation.DecisionIpAddress = currentUser.IpAddress;
        quotation.DecisionOnBehalf = onBehalf;
        quotation.DecisionMode = onBehalf ? mode : QuotationDecisionMode.Portal;
        quotation.DecisionConfirmedBy = confirmedBy;
        quotation.DecisionNote = onBehalf ? reason?.Trim() : null;
        if (decision == QuotationStatus.Rejected)
        {
            quotation.RejectionReason = reason?.Trim();
            quotation.RejectionCode = reasonCode?.Trim();
        }

        var verb = decision == QuotationStatus.Approved ? "approved" : "rejected";
        AddEvent(quotation,
            decision == QuotationStatus.Approved
                ? QuotationEventType.Approved
                : QuotationEventType.Rejected,
            onBehalf ? QuotationActorType.Admin : QuotationActorType.Customer,
            onBehalf
                ? $"Recorded as {verb} on behalf of the customer, confirmed by {confirmedBy} " +
                  $"({mode})." + (string.IsNullOrWhiteSpace(reason) ? "" : $" {reason.Trim()}")
                : $"{quotation.CustomerName} {verb} the quotation."
                  + (string.IsNullOrWhiteSpace(reason) ? "" : $" Reason: {reason.Trim()}"),
            nowUtc);

        // The document asks that the admin be told either way (§8).
        Notify(quotation, null, NotificationAudience.AllAdmins, $"Quotation {verb}",
            $"{quotation.CustomerName} {verb} quotation {quotation.QuotationNumber}."
            + (string.IsNullOrWhiteSpace(reason) ? "" : $" Reason: {reason.Trim()}"),
            $"/admin/billing/quotations/{quotation.QuotationId}", "Open");
        await db.SaveChangesAsync(ct);

        // The security-deposit invoice the quotation asked for (document §3.4). It is
        // a convenience, not part of the decision: if raising it fails, the customer's
        // approval still stands and an admin can raise it by hand.
        if (decision == QuotationStatus.Approved && quotation.AdvanceInvoiceEnabled)
        {
            try
            {
                var advance = await invoiceService.CreateAdvanceFromQuotationAsync(quotationId, ct);
                AddEvent(quotation, QuotationEventType.Converted, QuotationActorType.System,
                    $"Advance invoice {advance.InvoiceNumber} raised for the security deposit.",
                    clock.UtcNow);
                await db.SaveChangesAsync(ct);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Advance invoice for quotation {QuotationId} failed",
                    quotationId);
            }
        }

        return await ToDtoAsync(quotation, ct, forCustomer: !onBehalf
            && currentUser.UserType == UserType.Customer);
    }

    // ── money (document §5.2) ───────────────────────────────────────────────

    /// <summary>
    /// Server-side money, recomputed on every save — totals sent from the browser are
    /// ignored. Line value is quantity × duration × unit price; the whole-quotation
    /// discount is spread across lines in proportion to that value, with the last
    /// paying line absorbing the rounding difference so the shares always add back to
    /// the discount. GST is charged per line on what is left, then split CGST/SGST
    /// inside the home state or charged as IGST outside it.
    /// </summary>
    private async Task ApplyLinesAndTotalsAsync(Quotation quotation, SaveQuotationRequest request,
        CancellationToken ct)
    {
        var defaultGst = await settings.GetDecimalAsync(
            AppSettingKeys.Quotation.DefaultGstPercent, 18m, ct);
        var homeState = await settings.GetStringAsync(AppSettingKeys.Quotation.HomeStateCode, "29", ct);
        var roundOffEnabled = await settings.GetBoolAsync(
            AppSettingKeys.Quotation.RoundOffEnabled, true, ct);

        var intraState = string.IsNullOrWhiteSpace(quotation.PlaceOfSupplyStateCode)
                         || quotation.PlaceOfSupplyStateCode == homeState;
        quotation.TaxMode = intraState ? QuotationTaxMode.IntraState : QuotationTaxMode.InterState;

        // A picked item must exist; its details are copied onto the line as sent.
        var itemIds = request.Lines.Where(l => l.InventoryItemId is not null)
            .Select(l => l.InventoryItemId!.Value).Distinct().ToList();
        if (itemIds.Count > 0)
        {
            var known = await db.InventoryItems.Where(i => itemIds.Contains(i.InventoryItemId))
                .Select(i => i.InventoryItemId).ToListAsync(ct);
            var missing = itemIds.Except(known).FirstOrDefault();
            if (missing != 0)
            {
                throw new NotFoundException("InventoryItem", missing);
            }
        }

        var lineNumber = 0;
        foreach (var source in request.Lines)
        {
            lineNumber++;
            var lineType = (QuotationLineType)(source.LineType ?? (byte)QuotationLineType.Service);
            var isHeader = lineType == QuotationLineType.Header;

            // A header is a caption: no quantity, no rate, no tax, no money (§3.3).
            var quantity = isHeader ? 0m : source.Quantity;
            var duration = isHeader ? null : source.DurationValue;
            var unitPrice = isHeader ? 0m : source.UnitPrice;
            var gross = Math.Round(quantity * (duration ?? 1m) * unitPrice, 2,
                MidpointRounding.AwayFromZero);
            var lineDiscount = isHeader ? 0m : Math.Min(source.DiscountAmount, gross);
            var lineAmount = Math.Round(gross - lineDiscount, 2, MidpointRounding.AwayFromZero);

            quotation.Lines.Add(new QuotationLine
            {
                LineNumber = lineNumber,
                LineType = lineType,
                ServiceType = (QuotationServiceType)(source.ServiceType
                    ?? (byte)QuotationServiceType.Other),
                WorkspaceTypeId = source.WorkspaceTypeId,
                ServiceItemId = source.ServiceItemId,
                WorkspaceId = source.WorkspaceId,
                PassPlanId = source.PassPlanId,
                Description = source.Description.Trim(),
                LineNotes = source.LineNotes?.Trim(),
                HsnSacCode = source.HsnSacCode?.Trim(),
                Quantity = quantity,
                DurationValue = duration,
                DurationUnit = source.DurationUnit?.Trim(),
                UnitPrice = unitPrice,
                StandardRate = source.StandardRate,
                DiscountAmount = lineDiscount,
                LineAmount = lineAmount,
                // Outside GST charges nothing, whatever rate came with it.
                TaxPercent = isHeader || source.IsNonTaxable == true ? 0m : source.TaxPercent ?? defaultGst,
                IsNonTaxable = !isHeader && source.IsNonTaxable == true,
                InventoryItemId = isHeader ? null : source.InventoryItemId,
                ItemType = isHeader || source.ItemType is null ? null : (InventoryItemType)source.ItemType.Value,
                Unit = isHeader ? null : Trimmed(source.Unit)
            });
        }

        var payingLines = quotation.Lines
            .Where(l => l.LineType != QuotationLineType.Header)
            .ToList();
        quotation.SubTotal = Math.Round(payingLines.Sum(l => l.LineAmount), 2,
            MidpointRounding.AwayFromZero);

        // Discount: percentage of the sub total, or the flat amount, never more than
        // the sub total itself (document §5.4).
        quotation.DiscountType = (QuotationDiscountType)(request.DiscountType
            ?? (byte)QuotationDiscountType.Percentage);
        quotation.DiscountValue = request.DiscountValue ?? 0m;
        var discountTotal = quotation.DiscountType == QuotationDiscountType.Percentage
            ? Math.Round(quotation.SubTotal * quotation.DiscountValue / 100m, 2,
                MidpointRounding.AwayFromZero)
            : quotation.DiscountValue;
        if (discountTotal > quotation.SubTotal)
        {
            throw new AppValidationException([new ApiError(ErrorCodes.ValidationError,
                "Discount cannot be more than the sub total.",
                nameof(SaveQuotationRequest.DiscountValue))]);
        }
        quotation.DiscountAmount = discountTotal;

        // Proportional split; the last paying line takes the paisa difference so the
        // shares always add back to exactly the discount (document §5.2).
        var allocated = 0m;
        for (var i = 0; i < payingLines.Count; i++)
        {
            var line = payingLines[i];
            var share = i == payingLines.Count - 1
                ? discountTotal - allocated
                : quotation.SubTotal == 0m
                    ? 0m
                    : Math.Round(discountTotal * line.LineAmount / quotation.SubTotal, 2,
                        MidpointRounding.AwayFromZero);
            allocated += share;
            line.DiscountShare = share;
            line.TaxableAmount = Math.Round(line.LineAmount - share, 2, MidpointRounding.AwayFromZero);

            var tax = Math.Round(line.TaxableAmount * line.TaxPercent / 100m, 2,
                MidpointRounding.AwayFromZero);
            line.TaxAmount = tax;
            if (intraState)
            {
                // Halve once and derive the other half, so CGST + SGST is always the
                // tax actually charged even when the halves do not divide evenly.
                line.CgstAmount = Math.Round(tax / 2m, 2, MidpointRounding.AwayFromZero);
                line.SgstAmount = tax - line.CgstAmount;
                line.IgstAmount = 0m;
            }
            else
            {
                line.CgstAmount = 0m;
                line.SgstAmount = 0m;
                line.IgstAmount = tax;
            }
            line.TotalAmount = Math.Round(line.TaxableAmount + tax, 2, MidpointRounding.AwayFromZero);
        }

        quotation.TaxableAmount = payingLines.Sum(l => l.TaxableAmount);
        quotation.CgstAmount = payingLines.Sum(l => l.CgstAmount);
        quotation.SgstAmount = payingLines.Sum(l => l.SgstAmount);
        quotation.IgstAmount = payingLines.Sum(l => l.IgstAmount);

        // The header percentages are for the printed document: they only read as a
        // single rate when every line carries the same one, which is the normal case.
        var rates = payingLines.Select(l => l.TaxPercent).Distinct().ToList();
        var headerRate = rates.Count == 1 ? rates[0] : 0m;
        quotation.CgstPercent = intraState ? headerRate / 2m : 0m;
        quotation.SgstPercent = intraState ? headerRate / 2m : 0m;
        quotation.IgstPercent = intraState ? 0m : headerRate;

        await ApplyTdsAsync(quotation, request.TdsCode, ct);

        quotation.AdjustmentLabel = string.IsNullOrWhiteSpace(request.AdjustmentLabel)
            ? null
            : request.AdjustmentLabel.Trim();
        quotation.AdjustmentAmount = request.AdjustmentAmount ?? 0m;

        var beforeRounding = quotation.TaxableAmount
                             + quotation.CgstAmount + quotation.SgstAmount + quotation.IgstAmount
                             - quotation.TdsAmount
                             + quotation.AdjustmentAmount;
        quotation.RoundOffAmount = roundOffEnabled
            ? Math.Round(beforeRounding, 0, MidpointRounding.AwayFromZero) - beforeRounding
            : 0m;
        quotation.TotalAmount = Math.Round(beforeRounding + quotation.RoundOffAmount, 2,
            MidpointRounding.AwayFromZero);

        if (quotation.TotalAmount < 0)
        {
            throw new AppValidationException([new ApiError(ErrorCodes.ValidationError,
                "Total cannot be negative. Check the discount and adjustment.",
                nameof(SaveQuotationRequest.AdjustmentAmount))]);
        }
    }

    private async Task ApplyTdsAsync(Quotation quotation, string? tdsCode, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(tdsCode))
        {
            quotation.TdsCode = null;
            quotation.TdsLabel = null;
            quotation.TdsPercent = 0m;
            quotation.TdsAmount = 0m;
            return;
        }

        var option = (await LoadTdsOptionsAsync(ct))
            .FirstOrDefault(o => string.Equals(o.Code, tdsCode.Trim(),
                StringComparison.OrdinalIgnoreCase))
            ?? throw new AppValidationException([new ApiError(ErrorCodes.ValidationError,
                "That TDS section is not configured.", nameof(SaveQuotationRequest.TdsCode))]);

        // TDS is deducted on the value before GST (document §2.3).
        quotation.TdsCode = option.Code;
        quotation.TdsLabel = option.Label;
        quotation.TdsPercent = option.Percent;
        quotation.TdsAmount = Math.Round(quotation.TaxableAmount * option.Percent / 100m, 2,
            MidpointRounding.AwayFromZero);
    }

    private async Task<IReadOnlyList<QuotationTdsOptionDto>> LoadTdsOptionsAsync(CancellationToken ct)
    {
        var raw = await settings.GetStringAsync(AppSettingKeys.Quotation.TdsOptions,
            "194I|194-I Rent 10%|10;194J|194-J Professional 10%|10", ct);
        var options = new List<QuotationTdsOptionDto>();
        foreach (var entry in raw.Split(';', StringSplitOptions.RemoveEmptyEntries
                                             | StringSplitOptions.TrimEntries))
        {
            var parts = entry.Split('|', StringSplitOptions.TrimEntries);
            if (parts.Length == 3 && decimal.TryParse(parts[2], NumberStyles.Number,
                    CultureInfo.InvariantCulture, out var percent))
            {
                options.Add(new QuotationTdsOptionDto(parts[0], parts[1], percent));
            }
        }
        return options;
    }

    // ── helpers ─────────────────────────────────────────────────────────────

    private IQueryable<Quotation> ApplyFilters(IQueryable<Quotation> query,
        QuotationListRequest request)
    {
        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var term = request.Search.Trim();
            query = query.Where(q =>
                q.QuotationNumber.Contains(term)
                || q.CustomerName.Contains(term)
                || (q.CustomerCompanyName != null && q.CustomerCompanyName.Contains(term))
                || (q.CustomerMobile != null && q.CustomerMobile.Contains(term))
                || (q.ReferenceNumber != null && q.ReferenceNumber.Contains(term))
                || (q.Subject != null && q.Subject.Contains(term)));
        }
        if (request.Status is not null)
        {
            var today = IstClock.IstDateOf(clock.UtcNow);
            var status = (QuotationStatus)request.Status.Value;
            // A lapsed quotation reads as Expired the moment it lapses, whether or not
            // the sweeper has run, so the tab has to agree with the pill beside it.
            query = status switch
            {
                QuotationStatus.Expired => query.Where(q =>
                    q.Status == QuotationStatus.Expired
                    || ((q.Status == QuotationStatus.Sent || q.Status == QuotationStatus.Viewed)
                        && q.ValidUntil < today)),
                QuotationStatus.Sent or QuotationStatus.Viewed => query.Where(q =>
                    q.Status == status && q.ValidUntil >= today),
                _ => query.Where(q => q.Status == status)
            };
        }
        if (request.CustomerId is not null)
        {
            query = query.Where(q => q.CustomerId == request.CustomerId);
        }
        if (request.LocationId is not null)
        {
            query = query.Where(q => q.LocationId == request.LocationId);
        }
        if (request.ServiceType is not null)
        {
            var serviceType = (QuotationServiceType)request.ServiceType.Value;
            query = query.Where(q => q.Lines.Any(l => l.ServiceType == serviceType));
        }
        if (request.ExpiringInDays is not null)
        {
            var today = IstClock.IstDateOf(clock.UtcNow);
            var limit = today.AddDays(request.ExpiringInDays.Value);
            query = query.Where(q => (q.Status == QuotationStatus.Sent
                                      || q.Status == QuotationStatus.Viewed)
                                     && q.ValidUntil >= today && q.ValidUntil <= limit);
        }

        var (from, to) = ResolveDateRange(request);
        if (from is not null)
        {
            query = query.Where(q => q.QuotationDate >= from);
        }
        if (to is not null)
        {
            query = query.Where(q => q.QuotationDate <= to);
        }
        return query;
    }

    /// <summary>The Date filter's presets from document §3.1. Explicit From/To win, and
    /// both ends are inclusive. Every preset covers the whole period, so a quotation
    /// dated later this month still counts as "this month".</summary>
    private (DateOnly? From, DateOnly? To) ResolveDateRange(QuotationListRequest request)
    {
        if (request.From is not null || request.To is not null)
        {
            if (request.From is not null && request.To is not null && request.To < request.From)
            {
                throw new BusinessRuleException("The To date cannot be before the From date.");
            }
            return (request.From, request.To);
        }
        var today = IstClock.IstDateOf(clock.UtcNow);
        var monthStart = new DateOnly(today.Year, today.Month, 1);
        // Calendar quarters line up with the Indian financial year's (Apr–Jun is Q1).
        var quarterStart = new DateOnly(today.Year, (today.Month - 1) / 3 * 3 + 1, 1);
        var yearStart = IndianMoney.FinancialYearStart(today);
        var lastYearStart = IndianMoney.FinancialYearStart(yearStart.AddDays(-1));
        return request.DatePreset?.Trim().ToLowerInvariant() switch
        {
            "today" => (today, today),
            // The week runs Monday to Sunday, as the calendar screen already does.
            "week" => (today.AddDays(-(((int)today.DayOfWeek + 6) % 7)),
                today.AddDays(-(((int)today.DayOfWeek + 6) % 7) + 6)),
            "month" => (monthStart, monthStart.AddMonths(1).AddDays(-1)),
            "lastmonth" => (monthStart.AddMonths(-1), monthStart.AddDays(-1)),
            "quarter" => (quarterStart, quarterStart.AddMonths(3).AddDays(-1)),
            "fy" => (yearStart, IndianMoney.FinancialYearEnd(today)),
            "lastfy" => (lastYearStart, yearStart.AddDays(-1)),
            _ => (null, null)
        };
    }

    /// <summary>The list's sortable columns: date (the default, newest first), quote
    /// number and amount. Ties fall back to the newest row so paging stays stable.</summary>
    private static IQueryable<Quotation> Sorted(IQueryable<Quotation> query,
        QuotationListRequest request)
    {
        var ascending = string.Equals(request.SortDir, "asc", StringComparison.OrdinalIgnoreCase);
        IOrderedQueryable<Quotation> ordered = request.SortBy?.Trim().ToLowerInvariant() switch
        {
            "number" => ascending
                ? query.OrderBy(q => q.QuotationNumber)
                : query.OrderByDescending(q => q.QuotationNumber),
            "amount" => ascending
                ? query.OrderBy(q => q.TotalAmount)
                : query.OrderByDescending(q => q.TotalAmount),
            _ => ascending
                ? query.OrderBy(q => q.QuotationDate)
                : query.OrderByDescending(q => q.QuotationDate),
        };
        return ordered.ThenByDescending(q => q.QuotationId);
    }

    private async Task<PagedResult<QuotationDto>> PageAsync(IQueryable<Quotation> query,
        QuotationListRequest request, CancellationToken ct)
    {
        var totalCount = await query.CountAsync(ct);
        var rows = await Sorted(query, request)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .Include(q => q.Lines)
            .Include(q => q.StatusHistory)
            .Include(q => q.Attachments)
            .ToListAsync(ct);

        var items = new List<QuotationDto>(rows.Count);
        foreach (var row in rows)
        {
            items.Add(await ToDtoAsync(row, ct, includeTimeline: false));
        }
        return PagedResult<QuotationDto>.Create(items, request.Page, request.PageSize, totalCount);
    }

    /// <summary>Customers read their own quotations only, whatever id they pass.</summary>
    private async Task<Quotation> LoadScopedAsync(long quotationId, CancellationToken ct,
        bool track = false)
    {
        var query = db.Quotations
            .Include(q => q.Lines.OrderBy(l => l.LineNumber))
            .Include(q => q.StatusHistory)
            .Include(q => q.Attachments)
            .Include(q => q.Events)
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

    private static void EnsureDraft(Quotation quotation)
    {
        if (quotation.Status != QuotationStatus.Draft)
        {
            throw new BusinessRuleException(
                "This quotation has already been sent to the customer and can no longer be edited. " +
                "Use Revise to send new prices, which keeps the customer's copy intact.");
        }
    }

    private static void ApplyHeader(Quotation quotation, SaveQuotationRequest request)
    {
        quotation.ReferenceNumber = Trimmed(request.ReferenceNumber);
        quotation.Subject = Trimmed(request.Subject);
        quotation.SalespersonId = request.SalespersonId ?? quotation.SalespersonId;
        quotation.PlaceOfSupplyStateCode = Trimmed(request.PlaceOfSupplyStateCode);
        quotation.InternalNote = Trimmed(request.InternalNote);
        quotation.AdvanceInvoiceEnabled = request.AdvanceInvoiceEnabled ?? false;
        quotation.AdvanceType = (QuotationAdvanceType)(request.AdvanceType
            ?? (byte)QuotationAdvanceType.Months);
        quotation.AdvanceValue = request.AdvanceValue ?? 0m;
        // Typed on the form, so a draft reads correctly before any snapshot exists.
        quotation.CustomerName = Trimmed(request.CustomerName) ?? string.Empty;
        quotation.BillingAddress = Trimmed(request.BillingAddress);
        quotation.CustomerGstin = Trimmed(request.CustomerGstin)?.ToUpperInvariant();
    }

    private async Task GuardHeaderAsync(SaveQuotationRequest request, CancellationToken ct)
    {
        if (request.CustomerId is { } customerId)
        {
            var customer = await db.Users
                .Where(u => u.UserId == customerId)
                .Select(u => new { u.UserType })
                .FirstOrDefaultAsync(ct)
                ?? throw new NotFoundException("Customer", customerId);
            if (customer.UserType != UserType.Customer)
            {
                throw new BusinessRuleException("A quotation can only be raised for a customer account.");
            }
        }
        if (request.SalespersonId is not null
            && !await db.Users.AnyAsync(u => u.UserId == request.SalespersonId
                                             && u.UserType != UserType.Customer, ct))
        {
            throw new NotFoundException("Salesperson", request.SalespersonId.Value);
        }
    }

    /// <summary>The form no longer asks for a location, so an edit keeps the one the
    /// quotation has and a new one takes the default location, as bookings do.</summary>
    private async Task<int> ResolveLocationAsync(int? requested, int? current,
        CancellationToken ct)
    {
        if (requested is { } locationId)
        {
            if (!await db.Locations.AnyAsync(l => l.LocationId == locationId, ct))
            {
                throw new NotFoundException("Location", locationId);
            }
            return locationId;
        }
        return current
            ?? await db.Locations.OrderByDescending(l => l.IsDefault).ThenBy(l => l.LocationId)
                .Select(l => l.LocationId).FirstAsync(ct);
    }

    /// <summary>Freezes customer, company and place of supply onto the quotation. Called
    /// once, on the first send — a resend must not re-snapshot a changed profile. What
    /// the admin typed on the form wins; a linked account only fills the blanks.</summary>
    private async Task SnapshotAsync(Quotation quotation, CancellationToken ct)
    {
        if (quotation.CustomerId is { } customerId)
        {
            var customer = await db.Users
                .FirstOrDefaultAsync(u => u.UserId == customerId, ct)
                ?? throw new NotFoundException("Customer", customerId);

            if (string.IsNullOrWhiteSpace(quotation.CustomerName))
            {
                quotation.CustomerName = customer.FullName;
            }
            quotation.CustomerEmail = customer.Email;
            quotation.CustomerMobile = customer.MobileNumber;
            quotation.CustomerGstin ??= customer.Gstin;
            quotation.CustomerCompanyName = customer.CompanyName;
            quotation.BillingAddress ??= customer.Address;
        }
        quotation.CompanyName = await settings.GetStringAsync(
            AppSettingKeys.Company.Name, "Regalis WorkSpace", ct);
        quotation.CompanyGstin = await settings.GetStringAsync(AppSettingKeys.Company.Gstin, "", ct);
        quotation.CompanyAddress = await settings.GetStringAsync(AppSettingKeys.Company.Address, "", ct);
        quotation.PlaceOfSupply = string.IsNullOrWhiteSpace(quotation.PlaceOfSupplyStateCode)
            ? await settings.GetStringAsync(AppSettingKeys.Tax.PlaceOfSupply, "", ct)
            : GstStates.NameOf(quotation.PlaceOfSupplyStateCode);
    }

    private async Task<byte[]> EnsurePdfAsync(Quotation quotation, CancellationToken ct)
    {
        if (!string.IsNullOrWhiteSpace(quotation.PdfPath) && File.Exists(quotation.PdfPath))
        {
            return await File.ReadAllBytesAsync(quotation.PdfPath, ct);
        }

        // The row is still Draft at this point — the status flips only once a channel
        // has accepted it — but the copy being frozen is the customer's, so it prints
        // as awaiting approval rather than as a draft.
        var context = (await PdfContextAsync(quotation, ct))
            with { StatusOverride = QuotationStatus.Sent };
        var pdf = QuotationPdfGenerator.Generate(quotation, context);
        var directory = Path.Combine(storageOptions.Value.UploadRoot, "quotations",
            quotation.QuotationId.ToString(CultureInfo.InvariantCulture));
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, PdfFileName(quotation));
        await File.WriteAllBytesAsync(path, pdf, ct);
        quotation.PdfPath = path;
        return pdf;
    }

    private static string PdfFileName(Quotation quotation) =>
        $"{quotation.QuotationNumber.Replace('/', '-')}.pdf";

    /// <summary>Blank for a typed-in customer: with no account there is nothing to sign
    /// in to, and the PDF and email leave the link out when it is blank.</summary>
    private async Task<string> PortalLinkAsync(Quotation quotation, CancellationToken ct)
    {
        if (quotation.CustomerId is null)
        {
            return string.Empty;
        }
        var baseUrl = await settings.GetStringAsync(AppSettingKeys.Quotation.PortalBaseUrl, "", ct);
        return $"{baseUrl.TrimEnd('/')}/quotations/{quotation.QuotationId}";
    }

    private async Task<string> TemplateAsync(string key, string fallback, Quotation quotation,
        string portalLink, CancellationToken ct)
    {
        var template = await settings.GetStringAsync(key, fallback, ct);
        // A setting row that exists but is blank must not send an empty email.
        return Fill(string.IsNullOrWhiteSpace(template) ? fallback : template, quotation, portalLink);
    }

    /// <summary>The placeholders document §3.5 names, substituted in both templates.</summary>
    private static string Fill(string template, Quotation quotation, string portalLink) =>
        template
            .Replace("{customer_name}", quotation.CustomerName)
            .Replace("{quote_number}", quotation.QuotationNumber)
            .Replace("{total}", $"Rs {quotation.TotalAmount:N2}")
            .Replace("{valid_until}", quotation.ValidUntil.ToString("dd MMM yyyy"))
            .Replace("{portal_link}", portalLink);

    private const string DefaultEmailBody =
        "Dear {customer_name},\n\nPlease find attached quotation {quote_number} for {total}, " +
        "valid until {valid_until}.\n\nYou can view and approve it online at {portal_link}.\n\n" +
        "Regards,\nRegalis WorkSpace";

    private const string DefaultWhatsAppBody =
        "Hello {customer_name}, your Regalis WorkSpace quotation {quote_number} for {total} " +
        "is ready. It is valid until {valid_until}. View and approve: {portal_link}";

    private const string UnlinkedEmailBody =
        "Dear {customer_name},\n\nPlease find attached quotation {quote_number} for {total}, " +
        "valid until {valid_until}.\n\nTo accept it, simply reply to this email.\n\n" +
        "Regards,\nRegalis WorkSpace";

    private const string UnlinkedWhatsAppBody =
        "Hello {customer_name}, your Regalis WorkSpace quotation {quote_number} for {total} " +
        "is attached. It is valid until {valid_until}. Reply here to accept it.";

    /// <summary>The gold "View &amp; approve quotation" button the document asks for
    /// (§3.5), wrapped around whatever message the admin typed.</summary>
    private static string EmailHtml(string body, string portalLink) =>
        $"""
         <div style="font-family:Georgia,serif;max-width:560px;margin:0 auto;padding:24px;color:#241F19;">
           <div style="white-space:pre-wrap;line-height:1.6;">{System.Net.WebUtility.HtmlEncode(body)}</div>
           {(string.IsNullOrEmpty(portalLink) ? "" : ApproveButtonHtml(portalLink))}
           <p style="color:#8C8272;font-size:13px;font-family:Arial,sans-serif;">
             Regalis WorkSpace · This quotation is valid until the date shown above.
           </p>
         </div>
         """;

    /// <summary>Left out for a typed-in customer, who has no portal account.</summary>
    private static string ApproveButtonHtml(string portalLink) =>
        $"""
         <p style="margin:28px 0;">
           <a href="{portalLink}" style="background:#CC9A3D;color:#fff;text-decoration:none;
              padding:12px 22px;border-radius:8px;font-family:Arial,sans-serif;font-weight:bold;">
             View &amp; approve quotation
           </a>
         </p>
         """;

    /// <summary>A fresh draft carrying everything but the identity and the history.</summary>
    private static Quotation CopyOf(Quotation source) => new()
    {
        CustomerId = source.CustomerId,
        // The customer block as typed; for a hand-typed prospect it is the only record.
        CustomerName = source.CustomerName,
        BillingAddress = source.BillingAddress,
        CustomerGstin = source.CustomerGstin,
        LocationId = source.LocationId,
        ReferenceNumber = source.ReferenceNumber,
        Subject = source.Subject,
        SalespersonId = source.SalespersonId,
        PlaceOfSupplyStateCode = source.PlaceOfSupplyStateCode,
        SubTotal = source.SubTotal,
        DiscountType = source.DiscountType,
        DiscountValue = source.DiscountValue,
        DiscountAmount = source.DiscountAmount,
        TaxableAmount = source.TaxableAmount,
        TaxMode = source.TaxMode,
        CgstPercent = source.CgstPercent,
        SgstPercent = source.SgstPercent,
        IgstPercent = source.IgstPercent,
        CgstAmount = source.CgstAmount,
        SgstAmount = source.SgstAmount,
        IgstAmount = source.IgstAmount,
        TdsCode = source.TdsCode,
        TdsLabel = source.TdsLabel,
        TdsPercent = source.TdsPercent,
        TdsAmount = source.TdsAmount,
        AdjustmentLabel = source.AdjustmentLabel,
        AdjustmentAmount = source.AdjustmentAmount,
        RoundOffAmount = source.RoundOffAmount,
        TotalAmount = source.TotalAmount,
        AdvanceInvoiceEnabled = source.AdvanceInvoiceEnabled,
        AdvanceType = source.AdvanceType,
        AdvanceValue = source.AdvanceValue,
        TermsAndConditions = source.TermsAndConditions,
        Remarks = source.Remarks,
        Lines = source.Lines.OrderBy(l => l.LineNumber).Select(l => new QuotationLine
        {
            LineNumber = l.LineNumber,
            LineType = l.LineType,
            ServiceType = l.ServiceType,
            WorkspaceTypeId = l.WorkspaceTypeId,
            ServiceItemId = l.ServiceItemId,
            WorkspaceId = l.WorkspaceId,
            PassPlanId = l.PassPlanId,
            Description = l.Description,
            LineNotes = l.LineNotes,
            HsnSacCode = l.HsnSacCode,
            Quantity = l.Quantity,
            DurationValue = l.DurationValue,
            DurationUnit = l.DurationUnit,
            InventoryItemId = l.InventoryItemId,
            ItemType = l.ItemType,
            Unit = l.Unit,
            IsNonTaxable = l.IsNonTaxable,
            UnitPrice = l.UnitPrice,
            StandardRate = l.StandardRate,
            DiscountAmount = l.DiscountAmount,
            DiscountShare = l.DiscountShare,
            LineAmount = l.LineAmount,
            TaxableAmount = l.TaxableAmount,
            TaxPercent = l.TaxPercent,
            TaxAmount = l.TaxAmount,
            CgstAmount = l.CgstAmount,
            SgstAmount = l.SgstAmount,
            IgstAmount = l.IgstAmount,
            TotalAmount = l.TotalAmount,
            StartUtc = l.StartUtc,
            EndUtc = l.EndUtc
        }).ToList()
    };

    /// <summary>"REG/QTN/2026-27/00004-R1" → "REG/QTN/2026-27/00004", so -R2 follows
    /// -R1 rather than becoming -R1-R2.</summary>
    private static string BaseNumber(string quotationNumber)
    {
        var marker = quotationNumber.LastIndexOf("-R", StringComparison.Ordinal);
        return marker > 0 && quotationNumber[(marker + 2)..].All(char.IsDigit)
            ? quotationNumber[..marker]
            : quotationNumber;
    }

    private void AddHistory(Quotation quotation, QuotationStatus to, DateTime whenUtc,
        string? reason) =>
        quotation.StatusHistory.Add(new QuotationStatusHistory
        {
            QuotationId = quotation.QuotationId,
            FromStatus = quotation.QuotationId == 0 ? null : quotation.Status,
            ToStatus = to,
            ChangedByUserId = currentUser.UserId,
            ChangedUtc = whenUtc,
            Reason = reason
        });

    private void AddEvent(Quotation quotation, QuotationEventType type, QuotationActorType actor,
        string summary, DateTime whenUtc, string? channel = null, string? destination = null,
        QuotationDeliveryStatus? delivery = null, string? failureReason = null) =>
        quotation.Events.Add(new QuotationEvent
        {
            QuotationId = quotation.QuotationId,
            EventType = type,
            ActorType = actor,
            ActorUserId = actor == QuotationActorType.System ? null : currentUser.UserId,
            Summary = summary.Length > 400 ? summary[..400] : summary,
            Channel = channel,
            Destination = destination,
            DeliveryStatus = delivery,
            FailureReason = failureReason?.Length > 500 ? failureReason[..500] : failureReason,
            CreatedUtc = whenUtc
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

    private static QuotationServiceType ServiceTypeOf(string typeCode) => typeCode switch
    {
        "CABIN" => QuotationServiceType.Cabin,
        "DESK" => QuotationServiceType.Desk,
        "MEETING_ROOM" => QuotationServiceType.MeetingRoom,
        _ => QuotationServiceType.Other
    };

    private static string GroupOf(QuotationServiceType serviceType) => serviceType switch
    {
        QuotationServiceType.Cabin => "Cabins",
        QuotationServiceType.Desk => "Desks",
        QuotationServiceType.MeetingRoom => "Meeting Rooms",
        QuotationServiceType.Pass => "Passes",
        _ => "Other"
    };

    /// <summary>The unit each service is normally sold in (document §2.1).</summary>
    private static string DefaultUnitOf(QuotationServiceType serviceType) => serviceType switch
    {
        QuotationServiceType.Cabin => "Month",
        QuotationServiceType.Desk => "Month",
        QuotationServiceType.MeetingRoom => "Hour",
        QuotationServiceType.Pass => "One-time",
        _ => "One-time"
    };

    private static IReadOnlyList<decimal> ParseRates(string raw) =>
        raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(part => decimal.TryParse(part, NumberStyles.Number,
                CultureInfo.InvariantCulture, out var rate) ? rate : (decimal?)null)
            .Where(rate => rate is not null)
            .Select(rate => rate!.Value)
            .ToList();

    private static string Money(decimal value) =>
        value.ToString("0.00", CultureInfo.InvariantCulture);

    private static string Csv(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return "";
        }
        // A number, a comma or a leading '=' all need quoting before Excel sees them.
        var escaped = value.Replace("\"", "\"\"");
        return $"\"{escaped}\"";
    }

    private static string StatusLabel(Quotation quotation, DateOnly today) =>
        quotation.ValidUntil < today && OpenStatuses.Contains(quotation.Status)
            ? nameof(QuotationStatus.Expired)
            : quotation.Status.ToString();

    // ── projection ──────────────────────────────────────────────────────────

    private async Task<QuotationDto> ToDtoAsync(Quotation q, CancellationToken ct,
        bool forCustomer = false, bool includeTimeline = true)
    {
        var today = IstClock.IstDateOf(clock.UtcNow);
        var isExpired = q.ValidUntil < today && OpenStatuses.Contains(q.Status);

        var replacedBy = await db.Quotations.AsNoTracking()
            .Where(r => r.RevisionOfQuotationId == q.QuotationId)
            .Select(r => new { r.QuotationId, r.QuotationNumber })
            .FirstOrDefaultAsync(ct);
        var revisionOf = q.RevisionOfQuotationId is null
            ? null
            : await db.Quotations.AsNoTracking()
                .Where(r => r.QuotationId == q.RevisionOfQuotationId)
                .Select(r => r.QuotationNumber)
                .FirstOrDefaultAsync(ct);

        var names = await NamesForAsync(q, ct);

        // Until it is sent, a quotation has no snapshot yet, so the customer block
        // would read blank on the list and the send dialog would have nothing to
        // prefill. A draft therefore shows the customer's current details; the
        // moment it is sent, SnapshotAsync freezes them and this falls away. Name,
        // address and GSTIN typed on the form win over the account, as on send.
        var contact = q.SentUtc is null && q.CustomerId is not null
            ? await db.Users.AsNoTracking()
                .Where(u => u.UserId == q.CustomerId)
                .Select(u => new QuotationContact(u.FullName, u.Email, u.MobileNumber,
                    u.Gstin, u.CompanyName, u.Address))
                .FirstOrDefaultAsync(ct)
            : null;

        return new QuotationDto(
            q.QuotationId, q.QuotationNumber, q.CustomerId,
            Trimmed(q.CustomerName) ?? contact?.Name ?? string.Empty,
            contact?.Email ?? q.CustomerEmail,
            contact?.Mobile ?? q.CustomerMobile,
            q.CustomerGstin ?? contact?.Gstin,
            contact?.CompanyName ?? q.CustomerCompanyName,
            q.BillingAddress ?? contact?.Address,
            q.LocationId, names.LocationName,
            q.QuotationDate, q.ValidUntil,
            q.RevisionOfQuotationId, revisionOf, q.RevisionNumber,
            replacedBy?.QuotationId, replacedBy?.QuotationNumber,
            q.ReferenceNumber, q.Subject, q.SalespersonId, names.SalespersonName,
            q.SubTotal, (byte)q.DiscountType, q.DiscountValue, q.DiscountAmount, q.TaxableAmount,
            (byte)q.TaxMode,
            q.CgstPercent, q.CgstAmount, q.SgstPercent, q.SgstAmount, q.IgstPercent, q.IgstAmount,
            q.TdsCode, q.TdsLabel, q.TdsPercent, q.TdsAmount,
            q.AdjustmentLabel, q.AdjustmentAmount, q.RoundOffAmount, q.TotalAmount,
            IndianMoney.ToWords(q.TotalAmount),
            (byte)q.Status, q.SentUtc, q.ViewedUtc, q.RespondedUtc, q.RespondedByUserId,
            q.RejectionReason, q.RejectionCode,
            q.DecisionOnBehalf, q.DecisionMode is null ? null : (byte)q.DecisionMode,
            forCustomer ? null : q.DecisionConfirmedBy, forCustomer ? null : q.DecisionNote,
            q.InvoiceId, names.InvoiceNumber,
            q.AdvanceInvoiceEnabled, (byte)q.AdvanceType, q.AdvanceValue,
            q.CompanyName, q.CompanyGstin, q.CompanyAddress, q.PlaceOfSupply,
            q.PlaceOfSupplyStateCode,
            q.TermsAndConditions, q.Remarks,
            // The internal note never leaves the admin portal (document §3.6).
            forCustomer ? null : q.InternalNote,
            isExpired,
            q.ValidUntil.DayNumber - today.DayNumber,
            ActionsFor(q, isExpired, replacedBy is not null, forCustomer),
            q.CreatedDate,
            q.Lines.OrderBy(l => l.LineNumber).Select(l => new QuotationLineDto(
                l.QuotationLineId, l.LineNumber, (byte)l.LineType, (byte)l.ServiceType,
                l.WorkspaceTypeId, names.WorkspaceTypeNames.GetValueOrDefault(l.WorkspaceTypeId ?? 0),
                l.ServiceItemId, names.ServiceItemNames.GetValueOrDefault(l.ServiceItemId ?? 0),
                l.WorkspaceId, l.PassPlanId,
                l.Description, l.LineNotes, l.HsnSacCode, l.Quantity, l.DurationValue, l.DurationUnit,
                l.UnitPrice, l.StandardRate, l.DiscountAmount, l.DiscountShare, l.LineAmount,
                l.TaxableAmount, l.TaxPercent, l.TaxAmount, l.CgstAmount, l.SgstAmount, l.IgstAmount,
                l.TotalAmount, l.InventoryItemId, l.ItemType is null ? null : (byte)l.ItemType,
                l.Unit, l.IsNonTaxable, QuotationLineText.Summary(l),
                l.StandardRate is not null && l.UnitPrice != l.StandardRate)).ToList(),
            q.StatusHistory.OrderBy(h => h.ChangedUtc).Select(h => new QuotationStatusHistoryDto(
                h.FromStatus is null ? null : (byte)h.FromStatus, (byte)h.ToStatus,
                h.ChangedByUserId, names.UserNames.GetValueOrDefault(h.ChangedByUserId, ""),
                h.ChangedUtc, h.Reason)).ToList(),
            q.Attachments.OrderBy(a => a.UploadedUtc).Select(a => new QuotationAttachmentDto(
                a.QuotationAttachmentId, a.FileName, a.ContentType, a.FileSizeBytes,
                a.SendWithEmail, a.UploadedUtc)).ToList(),
            // The timeline is a detail-screen concern; a 20-row list does not carry it.
            includeTimeline && !forCustomer
                ? q.Events.OrderBy(e => e.CreatedUtc).Select(e => new QuotationEventDto(
                    e.QuotationEventId, (byte)e.EventType, (byte)e.ActorType, e.ActorUserId,
                    e.ActorUserId is null
                        ? "System"
                        : names.UserNames.GetValueOrDefault(e.ActorUserId.Value, ""),
                    e.Summary, e.Channel, e.Destination,
                    e.DeliveryStatus is null ? null : (byte)e.DeliveryStatus,
                    e.FailureReason, e.CreatedUtc)).ToList()
                : []);
    }

    /// <summary>The customer's live details, used only while a quotation is a draft.</summary>
    private sealed record QuotationContact(string Name, string Email, string? Mobile,
        string? Gstin, string? CompanyName, string? Address);

    private sealed record QuotationNames(
        string? LocationName,
        string? SalespersonName,
        string? InvoiceNumber,
        Dictionary<long, string> UserNames,
        Dictionary<int, string> WorkspaceTypeNames,
        Dictionary<int, string> ServiceItemNames);

    /// <summary>Every display name the projection needs, fetched in one round trip each
    /// rather than per row — the old projection issued a query per line and per history
    /// entry, which a 20-row page turned into hundreds.</summary>
    private async Task<QuotationNames> NamesForAsync(Quotation q, CancellationToken ct)
    {
        var userIds = q.StatusHistory.Select(h => h.ChangedByUserId)
            .Concat(q.Events.Where(e => e.ActorUserId is not null).Select(e => e.ActorUserId!.Value))
            .Append(q.SalespersonId ?? 0)
            .Where(id => id > 0)
            .Distinct()
            .ToList();
        var userNames = userIds.Count == 0
            ? []
            : await db.Users.AsNoTracking().Where(u => userIds.Contains(u.UserId))
                .ToDictionaryAsync(u => u.UserId, u => u.FullName, ct);

        var typeIds = q.Lines.Where(l => l.WorkspaceTypeId is not null)
            .Select(l => l.WorkspaceTypeId!.Value).Distinct().ToList();
        var typeNames = typeIds.Count == 0
            ? []
            : await db.WorkspaceTypes.AsNoTracking().Where(w => typeIds.Contains(w.WorkspaceTypeId))
                .ToDictionaryAsync(w => w.WorkspaceTypeId, w => w.TypeName, ct);

        var itemIds = q.Lines.Where(l => l.ServiceItemId is not null)
            .Select(l => l.ServiceItemId!.Value).Distinct().ToList();
        var itemNames = itemIds.Count == 0
            ? []
            : await db.ServiceItems.AsNoTracking().Where(s => itemIds.Contains(s.ServiceItemId))
                .ToDictionaryAsync(s => s.ServiceItemId, s => s.ItemName, ct);

        return new QuotationNames(
            await db.Locations.AsNoTracking().Where(l => l.LocationId == q.LocationId)
                .Select(l => l.LocationName).FirstOrDefaultAsync(ct),
            q.SalespersonId is null ? null : userNames.GetValueOrDefault(q.SalespersonId.Value),
            q.InvoiceId is null
                ? null
                : await db.Invoices.AsNoTracking().Where(i => i.InvoiceId == q.InvoiceId)
                    .Select(i => i.InvoiceNumber).FirstOrDefaultAsync(ct),
            userNames, typeNames, itemNames);
    }

    /// <summary>The action matrix from document §3.6, crossed with the caller's
    /// permissions. The API re-checks each rule regardless; this only decides what the
    /// screen offers, so a customer never sees an admin action at all.</summary>
    private QuotationActionsDto ActionsFor(Quotation q, bool isExpired, bool alreadyRevised,
        bool forCustomer)
    {
        if (forCustomer)
        {
            return new QuotationActionsDto(false, false, true, false, false, false, false,
                false, false, false);
        }

        var status = isExpired ? QuotationStatus.Expired : q.Status;
        var isDraft = status == QuotationStatus.Draft;
        var isOpen = status is QuotationStatus.Sent or QuotationStatus.Viewed;

        return new QuotationActionsDto(
            CanEdit: isDraft && currentUser.HasPermission(PermissionCodes.Quotation.Edit),
            CanSend: (isDraft || isOpen) && currentUser.HasPermission(PermissionCodes.Quotation.Edit),
            CanDownloadPdf: currentUser.HasPermission(PermissionCodes.Quotation.Print),
            // A typed-in customer has no account, so no portal and nothing to invoice to.
            CanCopyPortalLink: !isDraft && status != QuotationStatus.Revised
                               && q.CustomerId is not null,
            CanRevise: (isOpen || status is QuotationStatus.Expired or QuotationStatus.Rejected)
                       && !alreadyRevised
                       && currentUser.HasPermission(PermissionCodes.Quotation.Create),
            CanClone: currentUser.HasPermission(PermissionCodes.Quotation.Create),
            CanRecordDecision: isOpen && !alreadyRevised
                               && currentUser.HasPermission(PermissionCodes.Quotation.Edit),
            CanConvertToInvoice: status == QuotationStatus.Approved && q.InvoiceId is null
                                 && q.CustomerId is not null
                                 && currentUser.HasPermission(PermissionCodes.Invoice.Create),
            CanDelete: isDraft && currentUser.HasPermission(PermissionCodes.Quotation.Delete),
            CanCancel: status is not (QuotationStatus.Approved or QuotationStatus.Rejected
                           or QuotationStatus.Cancelled or QuotationStatus.Revised)
                       && q.InvoiceId is null
                       && currentUser.HasPermission(PermissionCodes.Quotation.Delete));
    }

    private static string? Trimmed(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
