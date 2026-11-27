using System.Threading;
using System.Threading.Tasks;

namespace Regalis.Application.Finance.QuotationInvoice;

/// <summary>The outcome of raising the invoice of an approved quotation. <see cref="Created"/> is
/// false when the quotation already had its invoice: that one is returned, never a second.</summary>
public sealed record QuotationInvoiceOutcome(long InvoiceId, string InvoiceNumber, bool Created);

/// <summary>
/// quotation-invoice kit (27 Nov 2026), B2: one issued invoice per approved quotation.
///
/// Approval (customer portal or the admin's "Mark approved on behalf") and "Convert to invoice"
/// both come here. Inside the caller's transaction when there is one (approval: the decision and
/// the invoice commit together or not at all), else in a transaction of its own. Idempotent: a
/// quotation that already has its invoice gets that invoice back. The database enforces it too
/// (UX_Invoice_Quotation, unique on TRN_Invoice.QuotationId).
/// </summary>
public interface IQuotationInvoicing
{
    Task<QuotationInvoiceOutcome> ConvertAsync(long quotationId, CancellationToken ct = default);
}
