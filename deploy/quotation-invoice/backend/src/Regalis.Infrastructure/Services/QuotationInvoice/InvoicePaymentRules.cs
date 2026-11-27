using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;
using Regalis.Application.Abstractions;
using Regalis.Domain.Entities.Booking;
using Regalis.Domain.Entities.Finance;
using Regalis.Domain.Entities.Operations;
using Regalis.Domain.Enums;
using Regalis.Infrastructure.Persistence;

namespace Regalis.Infrastructure.Services.QuotationInvoice;

/// <summary>
/// quotation-invoice kit (27 Nov 2026): the shared rules for invoice payment status (B3, B4, B5, B7).
/// </summary>
public static class InvoicePaymentRules
{
    public const string Due = "Due";
    public const string PartiallyPaid = "Partially Paid";
    public const string Paid = "Paid";

    /// <summary>
    /// The invoice's payment status, from the stored figures (user decision, 27 Nov 2026):
    /// paid 0 is Due, 0 &lt; paid &lt; total is Partially Paid, paid &gt;= total is Paid. Cancelled and
    /// Refunded invoices keep those words. Labels only: the stored enum values do not change.
    /// </summary>
    public static string InvoiceLabel(InvoiceStatus status, decimal total, decimal paid)
    {
        if (status == InvoiceStatus.Cancelled) return "Cancelled";
        if (status == InvoiceStatus.Refunded) return "Refunded";
        if (paid <= 0m) return Due;
        return paid < total ? PartiallyPaid : Paid;
    }

    public static string PaymentLabel(PaymentStatus status) => status switch
    {
        PaymentStatus.Pending => "Pending",
        PaymentStatus.Success => "Success",
        PaymentStatus.Failed => "Failed",
        PaymentStatus.Refunded => "Refunded",
        PaymentStatus.Cancelled => "Cancelled",
        _ => "—"
    };

    /// <summary>
    /// B3: invoices that may be listed. An invoice raised from a quotation is listed only while that
    /// quotation is Approved, so nothing linked to a rejected or not-approved quotation reaches an
    /// invoice list, the payment register or the payments list. With valid data this filters nothing:
    /// an invoice can only be raised from an Approved quotation, which cannot change status after.
    /// </summary>
    public static IQueryable<Invoice> Billable(this IQueryable<Invoice> invoices, RegalisDbContext db) =>
        invoices.Where(i => i.QuotationId == null
                            || db.Quotations.Any(q => (long?)q.QuotationId == i.QuotationId && q.Status == QuotationStatus.Approved));

    /// <summary>B3: the same rule for payments. A payment against an invoice raised from a quotation
    /// that is not Approved is left out; payments without an invoice are unaffected.</summary>
    public static IQueryable<Payment> Billable(this IQueryable<Payment> payments, RegalisDbContext db) =>
        payments.Where(p => p.InvoiceId == null
                            || !db.Invoices.Any(i => (long?)i.InvoiceId == p.InvoiceId && i.QuotationId != null
                                                     && !db.Quotations.Any(q => (long?)q.QuotationId == i.QuotationId && q.Status == QuotationStatus.Approved)));

    /// <summary>
    /// B7: a captured payment reported by the gateway webhook, for a payment raised against an
    /// invoice (Pay Now on an invoice: no booking or pass on the payment). Before this kit the
    /// webhook marked such a payment Success and left TRN_Invoice.PaidAmount/Status untouched.
    ///
    /// Mirrors PaymentService.VerifyInvoicePaymentAsync: in one transaction the payment becomes
    /// Success and the invoice's paid amount and status move with it (and a pay-later booking or
    /// pass behind the invoice). A payment larger than the outstanding amount is marked Failed with
    /// the same reason the browser path uses. The payment row is locked (UPDLOCK) first and its
    /// status re-read, and the browser path takes the same lock, so a webhook and a browser verify
    /// arriving together apply the money once. The payment is marked IsSignatureVerified (the
    /// webhook's own HMAC was checked), which is what lets a later browser verify answer without
    /// applying it again.
    /// </summary>
    public static async Task SettleInvoiceWebhookAsync(RegalisDbContext db, long paymentId, string gatewayPaymentId, string body,
        IDateTimeProvider clock, ILogger logger, CancellationToken ct)
    {
        try
        {
            await db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
            {
                db.ChangeTracker.Clear();
                await using IDbContextTransaction tx = await db.Database.BeginTransactionAsync(ct);
                await db.Database.ExecuteSqlInterpolatedAsync($"SELECT PaymentId FROM TRN_Payment WITH (UPDLOCK, HOLDLOCK) WHERE PaymentId = {paymentId}", ct);
                Payment payment = await db.Payments.FirstAsync(p => p.PaymentId == paymentId, ct);
                if (payment.Status != PaymentStatus.Pending)
                {
                    logger.LogInformation("Webhook for invoice payment {PaymentNumber} ignored: already {Status}", payment.PaymentNumber, payment.Status);
                    return;
                }
                DateTime nowUtc = clock.UtcNow;
                Invoice invoice = await db.Invoices.FirstAsync(i => i.InvoiceId == payment.InvoiceId!.Value, ct);
                if (payment.Amount > invoice.OutstandingAmount)
                {
                    payment.Status = PaymentStatus.Failed;
                    payment.FailureReason = "Amount exceeds the outstanding balance";
                    payment.GatewayPaymentId ??= gatewayPaymentId;
                    payment.RawGatewayResponse = body;
                    await db.SaveChangesAsync(ct);
                    await tx.CommitAsync(ct);
                    logger.LogError("Webhook payment {PaymentNumber} of {Amount} exceeds the {Outstanding} outstanding on {InvoiceNumber}; not applied, marked Failed for review",
                        payment.PaymentNumber, payment.Amount, invoice.OutstandingAmount, invoice.InvoiceNumber);
                    return;
                }
                payment.GatewayPaymentId = gatewayPaymentId;
                payment.IsSignatureVerified = true;
                payment.Status = PaymentStatus.Success;
                payment.PaidUtc = nowUtc;
                payment.RawGatewayResponse = body;
                invoice.PaidAmount += payment.Amount;
                bool settled = invoice.PaidAmount >= invoice.TotalAmount;
                if (invoice.BookingId.HasValue)
                {
                    Booking? booking = await db.Bookings.FirstOrDefaultAsync(b => (long?)b.BookingId == invoice.BookingId && b.IsPayLater, ct);
                    if (booking != null) CashCollectionService.ApplyToBooking(booking, payment.Amount);
                }
                if (invoice.CustomerPassId.HasValue)
                {
                    CustomerPass? pass = await db.CustomerPasses.FirstOrDefaultAsync(p => (long?)p.CustomerPassId == invoice.CustomerPassId && p.IsPayLater, ct);
                    if (pass != null) CashCollectionService.ApplyToPass(pass, payment.Amount);
                }
                invoice.Status = settled ? InvoiceStatus.Paid : InvoiceStatus.PartiallyPaid;
                if (settled)
                {
                    invoice.PaidUtc = nowUtc;
                    invoice.PaymentId = payment.PaymentId;
                }
                db.Notifications.Add(new Notification
                {
                    UserId = invoice.CustomerId,
                    Audience = NotificationAudience.SingleUser,
                    Title = settled ? "Payment received" : "Part payment received",
                    Message = $"Rs {payment.Amount:N2} received against invoice {invoice.InvoiceNumber}. "
                              + (settled ? "It is now paid in full." : $"Rs {invoice.TotalAmount - invoice.PaidAmount:N2} remains outstanding."),
                    Category = NotificationCategory.Payment,
                    Priority = NotificationPriority.Normal,
                    ReferenceType = "Payment",
                    ReferenceId = payment.PaymentId,
                    ActionLabel = "View invoice",
                    ActionRoute = "/invoices",
                    SentUtc = nowUtc
                });
                await db.SaveChangesAsync(ct);
                await tx.CommitAsync(ct);
                logger.LogInformation("Webhook settled invoice payment {PaymentNumber}: {InvoiceNumber} paid {Paid} of {Total}",
                    payment.PaymentNumber, invoice.InvoiceNumber, invoice.PaidAmount, invoice.TotalAmount);
            });
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Webhook settlement of invoice payment {PaymentId} failed", paymentId);
            throw;
        }
    }
}
