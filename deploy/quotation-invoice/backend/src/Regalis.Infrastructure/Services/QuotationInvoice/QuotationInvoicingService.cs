using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;
using Regalis.Application.Abstractions;
using Regalis.Application.Common.Exceptions;
using Regalis.Application.Finance.QuotationInvoice;
using Regalis.Domain.Entities.Finance;
using Regalis.Domain.Entities.Operations;
using Regalis.Domain.Enums;
using Regalis.Infrastructure.Persistence;
using Regalis.Shared;

namespace Regalis.Infrastructure.Services.QuotationInvoice;

/// <summary>
/// quotation-invoice kit (27 Nov 2026), B2. Raises the one invoice of an approved quotation.
///
/// The invoice is built exactly as <c>InvoiceService.CreateFromQuotationAsync</c> built it before
/// this kit (moved here, figures carried across verbatim): customer and company snapshot, lines,
/// quantity, unit price, discount, CGST/SGST/IGST, TDS, adjustment, round-off and total; status
/// Pending (shown as Due), paid 0, due after Invoice.DueDays. The quotation keeps its status
/// (Approved); its InvoiceId points at the invoice and a "Converted" event is written.
///
/// Idempotent: a quotation that already has its invoice returns it (Created = false). A raced
/// second insert is stopped by UX_Invoice_Quotation and rolls back with its transaction.
/// </summary>
public sealed class QuotationInvoicingService(
    RegalisDbContext db,
    ISettingService settings,
    INumberSequenceService numberSequence,
    IDateTimeProvider clock,
    ILogger<QuotationInvoicingService> logger,
    Regalis.Application.Finance.QuotationAccount.IQuotationCustomerAccounts customerAccounts) : IQuotationInvoicing
{
    public async Task<QuotationInvoiceOutcome> ConvertAsync(long quotationId, CancellationToken ct = default)
    {
        if (db.Database.CurrentTransaction != null)
        {
            // The caller (approval) owns the transaction: the decision and the invoice commit together.
            return await RaiseAsync(quotationId, ct);
        }
        return await db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            await using IDbContextTransaction tx = await db.Database.BeginTransactionAsync(ct);
            QuotationInvoiceOutcome outcome = await RaiseAsync(quotationId, ct);
            await tx.CommitAsync(ct);
            return outcome;
        });
    }

    private async Task<QuotationInvoiceOutcome> RaiseAsync(long quotationId, CancellationToken ct)
    {
        // quotation-account kit: one raise at a time per quotation (concurrent approve / convert calls
        // queue here and the second one finds the first one's invoice and customer link).
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT QuotationId FROM TRN_Quotation WITH (UPDLOCK, HOLDLOCK) WHERE QuotationId = {quotationId}", ct);
        Quotation quotation = await db.Quotations.Include(q => q.Lines)
            .FirstOrDefaultAsync(q => q.QuotationId == quotationId, ct)
            ?? throw new NotFoundException("Quotation", quotationId);
        if (db.Entry(quotation).State == EntityState.Unchanged)
        {
            await db.Entry(quotation).ReloadAsync(ct);
        }
        if (quotation.Status != QuotationStatus.Approved)
        {
            throw new BusinessRuleException("Only an approved quotation can be invoiced. " + quotation.QuotationNumber + " is " + quotation.Status.ToString().ToLowerInvariant() + ".");
        }

        // Already invoiced: hand back that invoice, never a second one.
        if (quotation.InvoiceId.HasValue)
        {
            string? number = await db.Invoices.AsNoTracking().Where(i => i.InvoiceId == quotation.InvoiceId.Value)
                .Select(i => i.InvoiceNumber).FirstOrDefaultAsync(ct);
            if (number != null)
            {
                return new QuotationInvoiceOutcome(quotation.InvoiceId.Value, number, false);
            }
        }
        var linked = await db.Invoices.AsNoTracking().Where(i => i.QuotationId == quotationId)
            .Select(i => new { i.InvoiceId, i.InvoiceNumber }).FirstOrDefaultAsync(ct);
        if (linked != null)
        {
            quotation.InvoiceId = linked.InvoiceId;
            await db.SaveChangesAsync(ct);
            return new QuotationInvoiceOutcome(linked.InvoiceId, linked.InvoiceNumber, false);
        }

        // quotation-account kit: no customer account yet -> link the customer with the same email / mobile,
        // or create the account (refused with the reason for a staff email, missing details, ...).
        if (!quotation.CustomerId.HasValue)
        {
            await customerAccounts.EnsureCustomerAsync(quotation, ct);
        }
        long customerId = quotation.CustomerId!.Value;
        DateOnly invoiceDate = IstClock.IstDateOf(clock.UtcNow);
        int dueDays = await settings.GetIntAsync("Invoice.DueDays", 7, ct);
        var invoice = new Invoice
        {
            CustomerId = customerId,
            QuotationId = quotation.QuotationId,
            LocationId = quotation.LocationId,
            InvoiceDate = invoiceDate,
            DueDate = invoiceDate.AddDays(dueDays),
            SubTotal = quotation.SubTotal,
            DiscountAmount = quotation.DiscountAmount,
            TaxableAmount = quotation.TaxableAmount,
            CgstPercent = quotation.CgstPercent,
            SgstPercent = quotation.SgstPercent,
            IgstPercent = quotation.IgstPercent,
            CgstAmount = quotation.CgstAmount,
            SgstAmount = quotation.SgstAmount,
            IgstAmount = quotation.IgstAmount,
            TdsLabel = quotation.TdsLabel,
            TdsPercent = quotation.TdsPercent,
            TdsAmount = quotation.TdsAmount,
            AdjustmentLabel = quotation.AdjustmentLabel,
            AdjustmentAmount = quotation.AdjustmentAmount,
            RoundOffAmount = quotation.RoundOffAmount,
            TotalAmount = quotation.TotalAmount,
            PaidAmount = 0m,
            Status = InvoiceStatus.Pending,
            CustomerName = quotation.CustomerName,
            CustomerEmail = quotation.CustomerEmail,
            CustomerMobile = quotation.CustomerMobile,
            CustomerGstin = quotation.CustomerGstin,
            CustomerCompanyName = quotation.CustomerCompanyName,
            BillingAddress = quotation.BillingAddress,
            CompanyName = quotation.CompanyName,
            CompanyGstin = quotation.CompanyGstin,
            CompanyAddress = quotation.CompanyAddress,
            TermsAndConditions = quotation.TermsAndConditions,
            Notes = "Raised from quotation " + quotation.QuotationNumber + "."
        };
        foreach (QuotationLine line in quotation.Lines.OrderBy(l => l.LineNumber))
        {
            string description = !line.DurationValue.HasValue
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

        DateTime nowUtc = clock.UtcNow;
        invoice.InvoiceNumber = await numberSequence.NextAsync("Invoice", ct);
        db.Invoices.Add(invoice);
        await db.SaveChangesAsync(ct);
        quotation.InvoiceId = invoice.InvoiceId;
        db.Notifications.Add(new Notification
        {
            UserId = invoice.CustomerId,
            Audience = NotificationAudience.SingleUser,
            Title = "Your invoice is ready",
            Message = $"Invoice {invoice.InvoiceNumber} for Rs {invoice.TotalAmount:N2} is due on {invoice.DueDate:dd MMM yyyy}.",
            Category = NotificationCategory.Invoice,
            Priority = NotificationPriority.Normal,
            ReferenceType = "Invoice",
            ReferenceId = invoice.InvoiceId,
            ActionLabel = "View invoice",
            ActionRoute = "/invoices",
            SentUtc = nowUtc
        });
        db.Set<QuotationEvent>().Add(new QuotationEvent
        {
            QuotationId = quotation.QuotationId,
            EventType = QuotationEventType.Converted,
            ActorType = QuotationActorType.System,
            ActorUserId = null,
            Summary = $"Invoice {invoice.InvoiceNumber} raised for Rs {invoice.TotalAmount:N2}.",
            CreatedUtc = nowUtc
        });
        await db.SaveChangesAsync(ct);
        logger.LogInformation("Invoice {InvoiceNumber} raised from quotation {QuotationNumber}", invoice.InvoiceNumber, quotation.QuotationNumber);
        return new QuotationInvoiceOutcome(invoice.InvoiceId, invoice.InvoiceNumber, true);
    }
}
