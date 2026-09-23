using Regalis.Shared.Api;

namespace Regalis.Application.Finance;

public record InvoiceLineDto(
    int LineNumber,
    string Description,
    string? HsnSacCode,
    decimal Quantity,
    decimal UnitPrice,
    decimal LineAmount,
    decimal TaxPercent,
    decimal TaxAmount,
    decimal TotalAmount);

public record InvoiceDto(
    long InvoiceId,
    string InvoiceNumber,
    long CustomerId,
    long? BookingId,
    long? QuotationId,
    string? QuotationNumber,
    DateOnly InvoiceDate,
    DateOnly DueDate,
    decimal SubTotal,
    decimal DiscountAmount,
    decimal TaxableAmount,
    decimal CgstPercent,
    decimal CgstAmount,
    decimal SgstPercent,
    decimal SgstAmount,
    decimal IgstPercent,
    decimal IgstAmount,
    decimal TotalAmount,
    decimal PaidAmount,
    decimal OutstandingAmount,
    byte Status,
    // True when the due date has passed and money is still owed. Computed, never
    // stored -- an invoice does not need rewriting the morning it falls due.
    bool IsOverdue,
    DateTime? PaidUtc,
    string? CancellationReason,
    string? TermsAndConditions,
    string CustomerName,
    string CustomerEmail,
    string CompanyName,
    string? CompanyGstin,
    List<InvoiceLineDto> Lines);

public class InvoiceListRequest : PagedRequest
{
    /// <summary>pending | paid — the customer's tab filter.</summary>
    public string? Status { get; set; }
}

public class AdminInvoiceListRequest : PagedRequest
{
    public byte? Status { get; set; }
    /// <summary>true = due date passed with an outstanding balance.</summary>
    public bool? Overdue { get; set; }
    public long? CustomerId { get; set; }
    public DateOnly? From { get; set; }
    public DateOnly? To { get; set; }
}

public interface IInvoiceService
{
    Task<InvoiceDto> GetInvoiceAsync(long invoiceId, CancellationToken ct = default);
    Task<PagedResult<InvoiceDto>> GetMyInvoicesAsync(InvoiceListRequest request, CancellationToken ct = default);
    /// <summary>Admin list with the common filter set.</summary>
    Task<PagedResult<InvoiceDto>> GetInvoicesAsync(AdminInvoiceListRequest request, CancellationToken ct = default);
    /// <summary>The GST invoice as a PDF, generated on demand (QuestPDF).</summary>
    Task<byte[]> GetInvoicePdfAsync(long invoiceId, CancellationToken ct = default);

    /// <summary>
    /// Raises the invoice for an approved quotation (document §9, rules 4 and 5).
    /// The quotation is the only source: its figures, its customer and company snapshot
    /// and its tax rates are carried across verbatim rather than recomputed, so the
    /// invoice always matches what the customer approved. A quotation can be invoiced
    /// once — the database enforces that as well through UX_Invoice_Quotation.
    /// </summary>
    Task<InvoiceDto> CreateFromQuotationAsync(long quotationId, CancellationToken ct = default);

    /// <summary>The security-deposit invoice an approved quotation can raise
    /// automatically (document §3.4) — Zoho's "retainer invoice". A refundable deposit
    /// is not a supply, so it carries no GST; it is a separate invoice from the one
    /// the quotation converts into, and does not consume UX_Invoice_Quotation.</summary>
    Task<InvoiceDto> CreateAdvanceFromQuotationAsync(long quotationId,
        CancellationToken ct = default);

    /// <summary>Marks a pending invoice as delivered to the customer.</summary>
    Task<InvoiceDto> SendAsync(long invoiceId, CancellationToken ct = default);

    /// <summary>Cancels an unpaid invoice with a reason. An issued invoice is never
    /// edited or deleted; this is the correction path.</summary>
    Task<InvoiceDto> CancelAsync(long invoiceId, string reason, CancellationToken ct = default);
}
