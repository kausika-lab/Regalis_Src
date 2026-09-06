using Microsoft.EntityFrameworkCore;
using Regalis.Application.Abstractions;
using Regalis.Application.Common.Exceptions;
using Regalis.Application.Finance;
using Regalis.Domain.Entities.Finance;
using Regalis.Domain.Enums;
using Regalis.Infrastructure.Reporting;
using Regalis.Infrastructure.Persistence;
using Regalis.Shared;
using Regalis.Shared.Api;
using Regalis.Shared.Constants;
using Regalis.Domain.Entities.Operations;

namespace Regalis.Infrastructure.Services;

/// <summary>
/// Invoices. Reads are row-scoped; writes are deliberately narrow. There is no
/// free-form create and no edit: an issued invoice is a snapshot, so the only ways to
/// change one are to cancel it or to let a payment move its status. The one creation
/// path is an approved quotation, whose figures are carried across verbatim.
/// </summary>
public class InvoiceService(
    RegalisDbContext db,
    ISettingService settings,
    INumberSequenceService numberSequence,
    ICurrentUser currentUser,
    IDateTimeProvider clock) : IInvoiceService
{
    public async Task<InvoiceDto> GetInvoiceAsync(long invoiceId, CancellationToken ct = default)
    {
        var invoice = await LoadScopedAsync(invoiceId, ct);
        return ToDto(invoice);
    }

    public async Task<PagedResult<InvoiceDto>> GetMyInvoicesAsync(InvoiceListRequest request,
        CancellationToken ct = default)
    {
        var query = db.Invoices.AsNoTracking()
            .Where(i => i.CustomerId == currentUser.UserId);
        query = request.Status?.ToLowerInvariant() switch
        {
            "pending" => query.Where(i =>
                i.Status == InvoiceStatus.Pending || i.Status == InvoiceStatus.PartiallyPaid),
            "paid" => query.Where(i => i.Status == InvoiceStatus.Paid),
            _ => query
        };
        query = query.OrderByDescending(i => i.InvoiceId);

        var totalCount = await query.CountAsync(ct);
        var items = await query
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .Include(i => i.Lines)
            .ToListAsync(ct);
        return PagedResult<InvoiceDto>.Create(
            items.Select(ToDto).ToList(), request.Page, request.PageSize, totalCount);
    }

    public async Task<PagedResult<InvoiceDto>> GetInvoicesAsync(AdminInvoiceListRequest request,
        CancellationToken ct = default)
    {
        var query = db.Invoices.AsNoTracking();
        if (request.Status is not null)
        {
            query = query.Where(i => (byte)i.Status == request.Status);
        }
        if (request.CustomerId is not null)
        {
            query = query.Where(i => i.CustomerId == request.CustomerId);
        }
        if (request.Overdue == true)
        {
            // Same predicate as InvoiceDto.IsOverdue, evaluated in SQL.
            var todayIst = IstClock.IstDateOf(clock.UtcNow);
            query = query.Where(i => i.DueDate < todayIst
                                     && i.OutstandingAmount > 0
                                     && (i.Status == InvoiceStatus.Pending
                                         || i.Status == InvoiceStatus.Sent
                                         || i.Status == InvoiceStatus.PartiallyPaid));
        }
        if (request.From is not null)
        {
            query = query.Where(i => i.InvoiceDate >= request.From);
        }
        if (request.To is not null)
        {
            query = query.Where(i => i.InvoiceDate <= request.To);
        }
        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var term = request.Search.Trim();
            query = query.Where(i => i.InvoiceNumber.Contains(term) ||
                                     i.CustomerName.Contains(term) ||
                                     i.CustomerEmail.Contains(term));
        }
        query = query.OrderByDescending(i => i.InvoiceId);

        var totalCount = await query.CountAsync(ct);
        var items = await query
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .Include(i => i.Lines)
            .ToListAsync(ct);
        return PagedResult<InvoiceDto>.Create(
            items.Select(ToDto).ToList(), request.Page, request.PageSize, totalCount);
    }

    public async Task<byte[]> GetInvoicePdfAsync(long invoiceId, CancellationToken ct = default)
    {
        var invoice = await LoadScopedAsync(invoiceId, ct);
        return InvoicePdfGenerator.Generate(invoice);
    }


    public async Task<InvoiceDto> CreateFromQuotationAsync(long quotationId,
        CancellationToken ct = default)
    {
        var quotation = await db.Quotations
            .Include(q => q.Lines.OrderBy(l => l.LineNumber))
            .FirstOrDefaultAsync(q => q.QuotationId == quotationId, ct)
            ?? throw new NotFoundException("Quotation", quotationId);

        // Rule 4: an approved quotation is the source. Rule 5: nothing else is.
        if (quotation.Status != QuotationStatus.Approved)
        {
            throw new BusinessRuleException(
                "Only an approved quotation can be invoiced. " +
                $"{quotation.QuotationNumber} is {quotation.Status.ToString().ToLowerInvariant()}.");
        }
        if (quotation.InvoiceId is not null)
        {
            throw new ConflictException(
                $"{quotation.QuotationNumber} has already been invoiced.",
                ErrorCodes.DuplicateRecord);
        }

        var invoiceDate = IstClock.IstDateOf(clock.UtcNow);
        var dueDays = await settings.GetIntAsync(AppSettingKeys.Invoice.DueDays, 7, ct);

        var invoice = new Invoice
        {
            CustomerId = quotation.CustomerId,
            QuotationId = quotation.QuotationId,
            LocationId = quotation.LocationId,
            InvoiceDate = invoiceDate,
            DueDate = invoiceDate.AddDays(dueDays),

            // Carried across, not recomputed. The customer approved these numbers and
            // these tax rates; a settings change since then must not alter the bill.
            SubTotal = quotation.SubTotal,
            DiscountAmount = quotation.DiscountAmount,
            TaxableAmount = quotation.TaxableAmount,
            CgstPercent = quotation.CgstPercent,
            SgstPercent = quotation.SgstPercent,
            IgstPercent = quotation.IgstPercent,
            CgstAmount = quotation.CgstAmount,
            SgstAmount = quotation.SgstAmount,
            IgstAmount = quotation.IgstAmount,
            TotalAmount = quotation.TotalAmount,
            PaidAmount = 0,
            Status = InvoiceStatus.Pending,

            // The quotation's own snapshot, taken when it was sent.
            CustomerName = quotation.CustomerName,
            CustomerEmail = quotation.CustomerEmail,
            CustomerMobile = quotation.CustomerMobile,
            CustomerGstin = quotation.CustomerGstin,
            BillingAddress = quotation.BillingAddress,
            CompanyName = quotation.CompanyName,
            CompanyGstin = quotation.CompanyGstin,
            CompanyAddress = quotation.CompanyAddress,
            TermsAndConditions = quotation.TermsAndConditions,
            Notes = $"Raised from quotation {quotation.QuotationNumber}."
        };

        foreach (var line in quotation.Lines.OrderBy(l => l.LineNumber))
        {
            // The duration is folded into the printed description rather than the
            // quantity, so the invoice reads the way the quotation did.
            var description = line.DurationValue is null
                ? line.Description
                : $"{line.Description} ({line.DurationValue:0.###} {line.DurationUnit})".Trim();

            invoice.Lines.Add(new InvoiceLine
            {
                LineNumber = line.LineNumber,
                Description = description,
                HsnSacCode = line.HsnSacCode,
                Quantity = line.Quantity,
                UnitPrice = line.UnitPrice,
                LineAmount = line.LineAmount,
                TaxPercent = line.TaxPercent,
                TaxAmount = line.TaxAmount,
                TotalAmount = line.TotalAmount
            });
        }

        // Invoice, its number, the quotation's back-reference and the customer's
        // notification all commit together or not at all.
        var strategy = db.Database.CreateExecutionStrategy();
        var newId = await strategy.ExecuteAsync(async () =>
        {
            await using var dbTransaction = await db.Database.BeginTransactionAsync(ct);

            invoice.InvoiceNumber = await numberSequence.NextAsync(SequenceKeys.Invoice, ct);
            db.Invoices.Add(invoice);
            await db.SaveChangesAsync(ct);

            var tracked = await db.Quotations
                .FirstAsync(q => q.QuotationId == quotationId, ct);
            tracked.InvoiceId = invoice.InvoiceId;

            db.Notifications.Add(new Notification
            {
                UserId = invoice.CustomerId,
                Audience = NotificationAudience.SingleUser,
                Title = "Your invoice is ready",
                Message = $"Invoice {invoice.InvoiceNumber} for Rs {invoice.TotalAmount:N2} "
                          + $"is due on {invoice.DueDate:dd MMM yyyy}.",
                Category = NotificationCategory.Invoice,
                Priority = NotificationPriority.Normal,
                ReferenceType = "Invoice",
                ReferenceId = invoice.InvoiceId,
                ActionLabel = "View invoice",
                ActionRoute = "/invoices",
                SentUtc = clock.UtcNow
            });

            await db.SaveChangesAsync(ct);
            await dbTransaction.CommitAsync(ct);
            return invoice.InvoiceId;
        });

        return await GetInvoiceAsync(newId, ct);
    }

    public async Task<InvoiceDto> SendAsync(long invoiceId, CancellationToken ct = default)
    {
        var invoice = await db.Invoices
            .FirstOrDefaultAsync(i => i.InvoiceId == invoiceId, ct)
            ?? throw new NotFoundException("Invoice", invoiceId);

        if (invoice.Status != InvoiceStatus.Pending)
        {
            throw new BusinessRuleException(
                "Only a pending invoice can be marked as sent.");
        }
        invoice.Status = InvoiceStatus.Sent;
        await db.SaveChangesAsync(ct);
        return await GetInvoiceAsync(invoiceId, ct);
    }

    public async Task<InvoiceDto> CancelAsync(long invoiceId, string reason,
        CancellationToken ct = default)
    {
        var invoice = await db.Invoices
            .FirstOrDefaultAsync(i => i.InvoiceId == invoiceId, ct)
            ?? throw new NotFoundException("Invoice", invoiceId);

        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new AppValidationException([new ApiError(ErrorCodes.ValidationError,
                "Give a reason for cancelling the invoice.", nameof(reason))]);
        }
        if (invoice.Status == InvoiceStatus.Cancelled)
        {
            throw new BusinessRuleException("This invoice is already cancelled.");
        }
        // Money has changed hands; cancelling would leave the payment unexplained.
        if (invoice.PaidAmount > 0)
        {
            throw new BusinessRuleException(
                "This invoice has payments against it and cannot be cancelled. "
                + "Refund the payment first.");
        }

        invoice.Status = InvoiceStatus.Cancelled;
        invoice.CancellationReason = reason.Trim();
        await db.SaveChangesAsync(ct);
        return await GetInvoiceAsync(invoiceId, ct);
    }

    /// <summary>Customers read their own invoices only, whatever id they pass.</summary>
    private async Task<Invoice> LoadScopedAsync(long invoiceId, CancellationToken ct)
    {
        var invoice = await db.Invoices.AsNoTracking()
            .Include(i => i.Lines.OrderBy(l => l.LineNumber))
            .FirstOrDefaultAsync(i => i.InvoiceId == invoiceId, ct)
            ?? throw new NotFoundException("Invoice", invoiceId);
        if (currentUser.UserType == UserType.Customer && invoice.CustomerId != currentUser.UserId)
        {
            throw new NotFoundException("Invoice", invoiceId);
        }
        return invoice;
    }

    private InvoiceDto ToDto(Invoice i)
    {
        // Overdue is derived, not a status: due date passed and money still owed, and
        // only while the invoice is actually awaiting payment.
        var isOverdue = i.DueDate < IstClock.IstDateOf(clock.UtcNow)
                        && i.OutstandingAmount > 0
                        && i.Status is InvoiceStatus.Pending
                            or InvoiceStatus.Sent or InvoiceStatus.PartiallyPaid;

        return new InvoiceDto(
            i.InvoiceId, i.InvoiceNumber, i.CustomerId, i.BookingId, i.QuotationId,
            db.Quotations.Where(q => q.QuotationId == i.QuotationId)
                .Select(q => q.QuotationNumber).FirstOrDefault(),
            i.InvoiceDate, i.DueDate,
            i.SubTotal, i.DiscountAmount, i.TaxableAmount,
            i.CgstPercent, i.CgstAmount, i.SgstPercent, i.SgstAmount,
            i.IgstPercent, i.IgstAmount,
            i.TotalAmount, i.PaidAmount, i.OutstandingAmount, (byte)i.Status,
            isOverdue, i.PaidUtc, i.CancellationReason, i.TermsAndConditions,
            i.CustomerName, i.CustomerEmail, i.CompanyName, i.CompanyGstin,
            i.Lines.Select(l => new InvoiceLineDto(
                l.LineNumber, l.Description, l.HsnSacCode, l.Quantity, l.UnitPrice,
                l.LineAmount, l.TaxPercent, l.TaxAmount, l.TotalAmount)).ToList());
    }
}
