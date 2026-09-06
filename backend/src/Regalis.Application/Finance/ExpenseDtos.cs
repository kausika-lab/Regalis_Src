using FluentValidation;
using Regalis.Shared.Api;

namespace Regalis.Application.Finance;

public record ExpenseDto(
    long ExpenseId,
    string ExpenseNumber,
    DateOnly ExpenseDate,
    string Category,
    string Description,
    decimal Amount,
    // 1 Wallet, 2 Upi, 3 CreditCard, 4 DebitCard, 5 NetBanking, 6 Cash, 7 Other.
    byte PaymentMethod,
    string Remarks,
    string? AttachmentPath,
    int LocationId,
    string? LocationName,
    long EnteredByUserId,
    string EnteredByName,
    int? SupplierId,
    string? SupplierName,
    long? PurchaseOrderId,
    string? PoNumber,
    // Null until a bank statement line is matched to this expense (document §14).
    long? BankTransactionId,
    bool IsReconciled,
    DateTime CreatedDate);

public record SaveExpenseRequest(
    DateOnly? ExpenseDate,
    string Category,
    string Description,
    decimal Amount,
    byte PaymentMethod,
    string Remarks,
    string? AttachmentPath,
    int LocationId,
    int? SupplierId,
    long? PurchaseOrderId);

public class ExpenseListRequest : PagedRequest
{
    public string? Category { get; set; }
    public byte? PaymentMethod { get; set; }
    public int? LocationId { get; set; }
    public int? SupplierId { get; set; }
    public DateOnly? From { get; set; }
    public DateOnly? To { get; set; }
}

public record ExpenseExportResult(byte[] Content, string ContentType, string FileName);

/// <summary>Totals for the expense list header and the finance dashboard card.</summary>
public record ExpenseSummaryDto(
    decimal TotalAmount,
    int TotalCount,
    List<ExpenseCategoryTotalDto> ByCategory);

public record ExpenseCategoryTotalDto(string Category, decimal Amount, int Count);

public class SaveExpenseRequestValidator : AbstractValidator<SaveExpenseRequest>
{
    public SaveExpenseRequestValidator()
    {
        RuleFor(x => x.Category).NotEmpty().MaximumLength(100);
        // The document calls Description and Remarks out explicitly as required.
        RuleFor(x => x.Description).NotEmpty().MaximumLength(500)
            .WithMessage("Describe what the expense was for.");
        RuleFor(x => x.Remarks).NotEmpty().MaximumLength(500)
            .WithMessage("Remarks are required on an expense.");
        RuleFor(x => x.Amount).GreaterThan(0)
            .WithMessage("An expense must be greater than zero.");
        RuleFor(x => x.PaymentMethod).InclusiveBetween((byte)1, (byte)7);
        RuleFor(x => x.AttachmentPath).MaximumLength(500);
        RuleFor(x => x.LocationId).GreaterThan(0);
        RuleFor(x => x.SupplierId).GreaterThan(0).When(x => x.SupplierId is not null);
        RuleFor(x => x.PurchaseOrderId).GreaterThan(0).When(x => x.PurchaseOrderId is not null);
    }
}

/// <summary>Daily company expenses (document §5). An expense is never hard-deleted;
/// the interceptor turns a delete into a soft delete so the expense report and any
/// bank reconciliation that referenced it stay explicable.</summary>
public interface IExpenseService
{
    Task<PagedResult<ExpenseDto>> GetExpensesAsync(ExpenseListRequest request,
        CancellationToken ct = default);

    Task<ExpenseDto> GetExpenseAsync(long expenseId, CancellationToken ct = default);

    Task<ExpenseSummaryDto> GetSummaryAsync(ExpenseListRequest request,
        CancellationToken ct = default);

    /// <summary>Distinct categories already in use, for the entry screen's combo.</summary>
    Task<List<string>> GetCategoriesAsync(CancellationToken ct = default);

    Task<ExpenseDto> CreateAsync(SaveExpenseRequest request, CancellationToken ct = default);

    /// <summary>Refused once a bank transaction has been matched to the expense —
    /// editing the amount then would silently break the reconciliation.</summary>
    Task<ExpenseDto> UpdateAsync(long expenseId, SaveExpenseRequest request,
        CancellationToken ct = default);

    Task DeleteAsync(long expenseId, CancellationToken ct = default);

    Task<ExpenseExportResult> ExportAsync(ExpenseListRequest request, CancellationToken ct = default);
}
