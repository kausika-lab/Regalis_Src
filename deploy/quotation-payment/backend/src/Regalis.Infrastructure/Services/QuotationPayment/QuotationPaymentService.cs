using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Regalis.Application.Abstractions;
using Regalis.Application.Finance.QuotationPayment;
using Regalis.Domain.Entities.Finance;
using Regalis.Domain.Enums;
using Regalis.Infrastructure.Persistence;
using Regalis.Infrastructure.Services.QuotationInvoice;
using Regalis.Shared;
using Regalis.Shared.Api;

namespace Regalis.Infrastructure.Services.QuotationPayment;

/// <summary>
/// quotation-payment kit (27 Nov 2026). Approved quotations and the payment position of their invoice,
/// for the Payments screen. Read-only: nothing here writes a payment, an invoice or a quotation.
///
/// Only <see cref="QuotationStatus.Approved"/> quotations are listed (an approved quotation cannot be
/// rejected, revised or expired afterwards; it can be cancelled only while it has no invoice, and then it
/// leaves this list). The invoice is found by <c>TRN_Invoice.QuotationId</c> (unique, UX_Invoice_Quotation),
/// the same link the payment register and the Invoices screen use. Paid = the invoice's PaidAmount,
/// Outstanding = Total - Paid, status label = <see cref="InvoicePaymentRules.InvoiceLabel"/>: the figures
/// and words of the Invoices screen, so the screens cannot disagree.
///
/// The security-deposit (advance) invoice has no QuotationId; it is matched by the note the app writes
/// on it ("Refundable security deposit against quotation X.", InvoiceService.CreateAdvanceFromQuotationAsync)
/// and reported beside the quotation's invoice, never added into its figures.
/// </summary>
public sealed class QuotationPaymentService(RegalisDbContext db, ICurrentUser currentUser, IDateTimeProvider clock) : IQuotationPaymentService
{
    public const string NotInvoiced = "Not invoiced";
    private const string NoInvoicePrefix = "No invoice raised: ";
    private const string DepositNotePrefix = "Refundable security deposit against quotation ";

    private sealed class Pair
    {
        public Quotation Q { get; set; } = null!;
        public Invoice? I { get; set; }
    }

    private IQueryable<Pair> Approved(QuotationPaymentListRequest request)
    {
        IQueryable<Pair> query =
            from q in db.Quotations.AsNoTracking()
            where q.Status == QuotationStatus.Approved
            join i in db.Invoices.AsNoTracking() on (long?)q.QuotationId equals i.QuotationId into ij
            from i in ij.DefaultIfEmpty()
            select new Pair { Q = q, I = i };
        if (request.LocationId.HasValue)
        {
            query = query.Where(x => x.Q.LocationId == request.LocationId.Value);
        }
        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            string s = request.Search.Trim();
            query = query.Where(x => x.Q.QuotationNumber.Contains(s)
                                     || (x.Q.ReferenceNumber != null && x.Q.ReferenceNumber.Contains(s))
                                     || x.Q.CustomerName.Contains(s) || x.Q.CustomerEmail.Contains(s)
                                     || (x.I != null && (x.I.InvoiceNumber.Contains(s) || x.I.CustomerName.Contains(s) || x.I.CustomerEmail.Contains(s))));
        }
        return query;
    }

    private static IQueryable<Pair> WithStatus(IQueryable<Pair> query, string? status)
    {
        switch ((status ?? "").Trim().ToLowerInvariant())
        {
            case "noinvoice":
                return query.Where(x => x.I == null);
            case "cancelled":
                return query.Where(x => x.I != null && (x.I.Status == InvoiceStatus.Cancelled || x.I.Status == InvoiceStatus.Refunded));
            case "due":
                return query.Where(x => x.I != null && x.I.Status != InvoiceStatus.Cancelled && x.I.Status != InvoiceStatus.Refunded && x.I.PaidAmount <= 0m);
            case "partial":
                return query.Where(x => x.I != null && x.I.Status != InvoiceStatus.Cancelled && x.I.Status != InvoiceStatus.Refunded && x.I.PaidAmount > 0m && x.I.PaidAmount < x.I.TotalAmount);
            case "paid":
                return query.Where(x => x.I != null && x.I.Status != InvoiceStatus.Cancelled && x.I.Status != InvoiceStatus.Refunded && x.I.PaidAmount > 0m && x.I.PaidAmount >= x.I.TotalAmount);
            case "open":
                return query.Where(x => x.I != null && x.I.Status != InvoiceStatus.Cancelled && x.I.Status != InvoiceStatus.Refunded && (x.I.PaidAmount <= 0m || x.I.PaidAmount < x.I.TotalAmount));
            default:
                return query;
        }
    }

    public async Task<PagedResult<QuotationPaymentRowDto>> GetAsync(QuotationPaymentListRequest request, CancellationToken ct = default)
    {
        IQueryable<Pair> query = WithStatus(Approved(request), request.Status);
        int total = await query.CountAsync(ct);
        var rows = await query
            .OrderByDescending(x => x.Q.RespondedUtc).ThenByDescending(x => x.Q.QuotationId)
            .Skip((request.Page - 1) * request.PageSize).Take(request.PageSize)
            .Select(x => new
            {
                x.Q.QuotationId, x.Q.QuotationNumber, x.Q.ReferenceNumber, x.Q.QuotationDate, x.Q.RespondedUtc, x.Q.CustomerId,
                x.Q.CustomerName, x.Q.CustomerEmail, x.Q.LocationId, QuotationTotal = x.Q.TotalAmount,
                InvoiceId = x.I != null ? (long?)x.I.InvoiceId : null,
                InvoiceNumber = x.I != null ? x.I.InvoiceNumber : null,
                InvoiceDate = x.I != null ? (DateOnly?)x.I.InvoiceDate : null,
                DueDate = x.I != null ? (DateOnly?)x.I.DueDate : null,
                InvoiceStatus = x.I != null ? (InvoiceStatus?)x.I.Status : null,
                InvoiceTotal = x.I != null ? (decimal?)x.I.TotalAmount : null,
                InvoicePaid = x.I != null ? (decimal?)x.I.PaidAmount : null,
                InvoiceCustomerName = x.I != null ? x.I.CustomerName : null,
                InvoiceCustomerEmail = x.I != null ? x.I.CustomerEmail : null
            })
            .ToListAsync(ct);

        List<long> invoiceIds = rows.Where(r => r.InvoiceId.HasValue).Select(r => r.InvoiceId!.Value).ToList();
        List<long> quotationIds = rows.Select(r => r.QuotationId).ToList();

        // Successful payments on those invoices (a reversed receipt's payment is Refunded, so it is not counted).
        var payments = invoiceIds.Count == 0 ? new Dictionary<long, (int Count, DateTime? Last)>() :
            (await db.Payments.AsNoTracking()
                .Where(p => p.InvoiceId != null && invoiceIds.Contains(p.InvoiceId.Value) && p.Status == PaymentStatus.Success)
                .GroupBy(p => p.InvoiceId!.Value)
                .Select(g => new { InvoiceId = g.Key, Count = g.Count(), Last = g.Max(p => p.PaidUtc) })
                .ToListAsync(ct))
            .ToDictionary(g => g.InvoiceId, g => (g.Count, g.Last));

        // Why an approved quotation has no invoice (the note the approval / convert path writes on its timeline).
        List<long> noInvoice = rows.Where(r => !r.InvoiceId.HasValue).Select(r => r.QuotationId).ToList();
        var reasons = noInvoice.Count == 0 ? new Dictionary<long, string>() :
            (await db.QuotationEvents.AsNoTracking()
                .Where(ev => noInvoice.Contains(ev.QuotationId) && ev.Summary.StartsWith(NoInvoicePrefix))
                .Select(ev => new { ev.QuotationId, ev.QuotationEventId, ev.Summary })
                .ToListAsync(ct))
            .GroupBy(ev => ev.QuotationId)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(ev => ev.QuotationEventId).First().Summary);

        // Security-deposit invoices, by the exact note the app writes on them.
        List<string> depositNotes = rows.Select(r => DepositNotePrefix + r.QuotationNumber + ".").ToList();
        var deposits = depositNotes.Count == 0 ? new Dictionary<string, Invoice>() :
            (await db.Invoices.AsNoTracking()
                .Where(i => i.QuotationId == null && i.Notes != null && depositNotes.Contains(i.Notes))
                .ToListAsync(ct))
            .GroupBy(i => i.Notes!)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(i => i.InvoiceId).First());

        bool canRecord = currentUser.HasPermission("PAYMENT.RECORD");
        DateOnly todayIst = IstClock.IstDateOf(clock.UtcNow);
        List<QuotationPaymentRowDto> items = rows.Select(r =>
        {
            bool invoiced = r.InvoiceId.HasValue;
            decimal paid = r.InvoicePaid ?? 0m;
            bool live = invoiced && r.InvoiceStatus != InvoiceStatus.Cancelled && r.InvoiceStatus != InvoiceStatus.Refunded;
            // Outstanding as on the Invoices screen: Total - Paid, and none for a cancelled / refunded invoice.
            decimal? outstanding = live ? r.InvoiceTotal - r.InvoicePaid : null;
            string label = invoiced ? InvoicePaymentRules.InvoiceLabel(r.InvoiceStatus!.Value, r.InvoiceTotal ?? 0m, paid) : NotInvoiced;
            (int Count, DateTime? Last) pay = invoiced && payments.TryGetValue(r.InvoiceId!.Value, out var p) ? p : (0, null);
            QuotationDepositInvoiceDto? deposit = null;
            if (deposits.TryGetValue(DepositNotePrefix + r.QuotationNumber + ".", out Invoice? d))
            {
                bool dLive = d.Status != InvoiceStatus.Cancelled && d.Status != InvoiceStatus.Refunded;
                deposit = new QuotationDepositInvoiceDto(d.InvoiceId, d.InvoiceNumber, d.TotalAmount, d.PaidAmount, dLive ? d.TotalAmount - d.PaidAmount : null,
                    InvoicePaymentRules.InvoiceLabel(d.Status, d.TotalAmount, d.PaidAmount), canRecord && dLive && d.TotalAmount - d.PaidAmount > 0m);
            }
            return new QuotationPaymentRowDto(
                r.QuotationId, r.QuotationNumber, r.ReferenceNumber, r.QuotationDate, r.RespondedUtc, r.CustomerId,
                r.InvoiceCustomerName ?? r.CustomerName, r.InvoiceCustomerEmail ?? r.CustomerEmail, r.LocationId, r.QuotationTotal,
                r.InvoiceId, r.InvoiceNumber, r.InvoiceDate, r.DueDate, r.InvoiceStatus.HasValue ? (byte)r.InvoiceStatus.Value : null,
                r.InvoiceTotal, paid, outstanding, label,
                live && outstanding > 0m && r.DueDate < todayIst,
                pay.Count, pay.Last,
                canRecord && live && outstanding > 0m,
                invoiced ? null : (reasons.TryGetValue(r.QuotationId, out string? why) ? why.Substring(NoInvoicePrefix.Length) : null),
                deposit);
        }).ToList();
        return PagedResult<QuotationPaymentRowDto>.Create(items, request.Page, request.PageSize, total);
    }

    public async Task<QuotationPaymentSummaryDto> GetSummaryAsync(QuotationPaymentListRequest request, CancellationToken ct = default)
    {
        var rows = await Approved(request)
            .Select(x => new
            {
                Invoiced = x.I != null,
                Status = x.I != null ? (InvoiceStatus?)x.I.Status : null,
                Total = x.I != null ? x.I.TotalAmount : 0m,
                Paid = x.I != null ? x.I.PaidAmount : 0m
            })
            .ToListAsync(ct);
        int notInvoiced = 0, due = 0, partial = 0, paid = 0, cancelled = 0;
        decimal invoicedTotal = 0m, paidTotal = 0m, outstandingTotal = 0m;
        foreach (var r in rows)
        {
            if (!r.Invoiced)
            {
                notInvoiced++;
                continue;
            }
            switch (InvoicePaymentRules.InvoiceLabel(r.Status!.Value, r.Total, r.Paid))
            {
                case "Cancelled":
                case "Refunded":
                    cancelled++;
                    continue;
                case InvoicePaymentRules.Due:
                    due++;
                    break;
                case InvoicePaymentRules.PartiallyPaid:
                    partial++;
                    break;
                default:
                    paid++;
                    break;
            }
            invoicedTotal += r.Total;
            paidTotal += r.Paid;
            outstandingTotal += r.Total - r.Paid;
        }
        return new QuotationPaymentSummaryDto(rows.Count, notInvoiced, due, partial, paid, cancelled, invoicedTotal, paidTotal, outstandingTotal);
    }
}
