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
    DateOnly InvoiceDate,
    DateOnly DueDate,
    decimal SubTotal,
    decimal DiscountAmount,
    decimal TaxableAmount,
    decimal CgstPercent,
    decimal CgstAmount,
    decimal SgstPercent,
    decimal SgstAmount,
    decimal TotalAmount,
    decimal PaidAmount,
    decimal OutstandingAmount,
    byte Status,
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
}
