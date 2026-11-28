using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Regalis.Shared.Api;

namespace Regalis.Application.Finance.QuotationPayment;

/// <summary>
/// quotation-payment kit (27 Nov 2026). The Payments screen's "Quotations" view: every APPROVED quotation
/// with the payment position of the one invoice it raised. Read-only. The invoice is the single source of
/// truth: paid and outstanding are the invoice's own stored figures (the Invoices screen shows the same),
/// and a payment is always recorded against the invoice (POST /api/invoices/{id}/payments), never
/// against the quotation. Draft, sent, viewed, rejected, expired, cancelled and revised quotations are
/// never listed.
/// </summary>
public interface IQuotationPaymentService
{
    Task<PagedResult<QuotationPaymentRowDto>> GetAsync(QuotationPaymentListRequest request, CancellationToken ct = default);

    Task<QuotationPaymentSummaryDto> GetSummaryAsync(QuotationPaymentListRequest request, CancellationToken ct = default);
}

/// <summary>Filters. <c>Status</c>: open (due + partially paid), due, partial, paid, noinvoice, cancelled; empty = all.</summary>
public sealed class QuotationPaymentListRequest : PagedRequest
{
    public string? Status { get; set; }

    public int? LocationId { get; set; }
}

/// <summary>
/// One approved quotation. <c>PaymentStatus</c> uses the invoice labels of the Invoices and Payments screens
/// (Due / Partially Paid / Paid, or Cancelled / Refunded), and "Not invoiced" while the quotation has no
/// invoice. The security-deposit (advance) invoice, when the quotation asked for one, is reported beside it
/// and never added into the quotation's own figures.
/// </summary>
public sealed record QuotationPaymentRowDto(
    long QuotationId,
    string QuotationNumber,
    string? ReferenceNumber,
    DateOnly QuotationDate,
    DateTime? ApprovedUtc,
    long? CustomerId,
    string CustomerName,
    string CustomerEmail,
    int LocationId,
    decimal QuotationTotal,
    long? InvoiceId,
    string? InvoiceNumber,
    DateOnly? InvoiceDate,
    DateOnly? DueDate,
    byte? InvoiceStatus,
    decimal? InvoiceTotal,
    decimal PaidAmount,
    decimal? OutstandingAmount,
    string PaymentStatus,
    bool IsOverdue,
    int PaymentCount,
    DateTime? LastPaymentUtc,
    bool CanRecordPayment,
    string? NoInvoiceReason,
    QuotationDepositInvoiceDto? Deposit);

public sealed record QuotationDepositInvoiceDto(
    long InvoiceId,
    string InvoiceNumber,
    decimal TotalAmount,
    decimal PaidAmount,
    decimal? OutstandingAmount,
    string PaymentStatus,
    bool CanRecordPayment);

/// <summary>Totals over the same search as the list (the status filter is not applied). Cancelled and
/// refunded invoices are counted but kept out of the money totals.</summary>
public sealed record QuotationPaymentSummaryDto(
    int Approved,
    int NotInvoiced,
    int Due,
    int PartiallyPaid,
    int Paid,
    int Cancelled,
    decimal InvoicedTotal,
    decimal PaidTotal,
    decimal OutstandingTotal);
