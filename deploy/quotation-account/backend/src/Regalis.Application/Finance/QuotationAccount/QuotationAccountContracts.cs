using System.Threading;
using System.Threading.Tasks;
using Regalis.Application.Common.Exceptions;
using Regalis.Domain.Entities.Finance;

namespace Regalis.Application.Finance.QuotationAccount;

/// <summary>
/// quotation-account kit (27 Nov 2026). A quotation typed in for a lead or walk-in has no customer
/// account (CustomerId NULL). Before its invoice is raised, the customer is resolved and linked:
/// an existing customer with the same email (case-insensitive), else the same mobile; otherwise a
/// customer account is created from the quotation's details through the Customers module
/// (IUserAdminService.CreateUserAsync, the path admin "New user" and CRM "convert" use).
/// A quotation is never linked to a staff or admin account.
/// </summary>
public interface IQuotationCustomerAccounts
{
    /// <summary>Links the quotation to its customer (sets <c>CustomerId</c> and saves), inside the
    /// caller's transaction. Throws <see cref="QuotationAccountRefusedException"/> with the exact reason
    /// when no customer can be linked or created; nothing is changed in that case.</summary>
    Task<QuotationCustomerLink> EnsureCustomerAsync(Quotation quotation, CancellationToken ct = default);

    /// <summary>The last link made in this request scope (null when none was needed).</summary>
    QuotationCustomerLink? LastLink { get; }
}

/// <summary>How the customer was found. Kind: "Existing" (already linked), "Email", "Mobile", "Created".</summary>
public sealed record QuotationCustomerLink(long CustomerId, string CustomerName, string Kind, string Summary);

/// <summary>No customer account could be linked or created for the quotation. 422, code QUOTATION_NO_ACCOUNT.</summary>
public sealed class QuotationAccountRefusedException(string reason)
    : BusinessRuleException(reason, "QUOTATION_NO_ACCOUNT")
{
    public string Reason { get; } = reason;
}

/// <summary>Optional corrections to the quotation's contact before the invoice is raised. Only for an
/// approved quotation that has no customer account and no invoice yet.</summary>
public sealed record RaiseQuotationInvoiceRequest(string? CustomerEmail, string? CustomerMobile);

public sealed record QuotationInvoiceRaisedDto(
    long QuotationId,
    string QuotationNumber,
    long InvoiceId,
    string InvoiceNumber,
    bool InvoiceCreated,
    long CustomerId,
    string? CustomerName,
    string? CustomerLink,
    string Message);

public interface IQuotationAccountService
{
    Task<QuotationInvoiceRaisedDto> RaiseInvoiceAsync(long quotationId, RaiseQuotationInvoiceRequest request, CancellationToken ct = default);
}
