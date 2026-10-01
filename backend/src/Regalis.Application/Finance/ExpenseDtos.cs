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
    DateTime CreatedDate,
    // Signed sum of the adjustments (increases minus decreases) and the result:
    // NetAmount = Amount + AdjustmentTotal is what every total counts.
    decimal AdjustmentTotal,
    decimal NetAmount,
    int AdjustmentCount);

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

/// <summary>Totals for the expense list header and the finance dashboard card.
/// TotalAmount and every category Amount are net of adjustments; GrossAmount is the
/// vouchers as entered and AdjustmentTotal the signed difference.</summary>
public record ExpenseSummaryDto(
    decimal TotalAmount,
    int TotalCount,
    List<ExpenseCategoryTotalDto> ByCategory,
    decimal GrossAmount,
    decimal AdjustmentTotal);

public record ExpenseCategoryTotalDto(string Category, decimal Amount, int Count);

/// <summary>The expense list filters plus the calendar year the monthly chart covers.
/// From/To still apply, so a month outside the range reads zero.</summary>
public class ExpenseMonthlyRequest : ExpenseListRequest
{
    public int? Year { get; set; }
}

/// <summary>Always twelve months, January to December, net of adjustments.</summary>
public record ExpenseMonthlyDto(int Year, decimal TotalAmount, List<ExpenseMonthTotalDto> Months);

public record ExpenseMonthTotalDto(int Month, decimal Amount, int Count);

// ── Adjustments ─────────────────────────────────────────────────────────────

public record ExpenseAdjustmentDto(
    long ExpenseAdjustmentId,
    long ExpenseId,
    string ExpenseNumber,
    string Category,
    DateOnly ExpenseDate,
    decimal ExpenseAmount,
    decimal ExpenseNetAmount,
    // 1 Increase, 2 Decrease.
    byte AdjustmentType,
    decimal Amount,
    // Amount with the type's sign applied: what this row adds to the expense.
    decimal SignedAmount,
    DateOnly AdjustmentDate,
    string Reason,
    string? Remarks,
    string? CreatedByName,
    DateTime CreatedDate,
    DateTime? ModifiedDate);

public record SaveExpenseAdjustmentRequest(
    long ExpenseId,
    byte AdjustmentType,
    decimal Amount,
    DateOnly? AdjustmentDate,
    string Reason,
    string? Remarks);

public class ExpenseAdjustmentListRequest : PagedRequest
{
    public long? ExpenseId { get; set; }
    public byte? AdjustmentType { get; set; }
    public string? Category { get; set; }
    /// <summary>On the adjustment date, not the expense date.</summary>
    public DateOnly? From { get; set; }
    public DateOnly? To { get; set; }
}

public class SaveExpenseAdjustmentRequestValidator : AbstractValidator<SaveExpenseAdjustmentRequest>
{
    public SaveExpenseAdjustmentRequestValidator()
    {
        RuleFor(x => x.ExpenseId).GreaterThan(0).WithMessage("Choose the expense to adjust.");
        RuleFor(x => x.AdjustmentType).InclusiveBetween((byte)1, (byte)2)
            .WithMessage("Choose whether the adjustment increases or decreases the expense.");
        RuleFor(x => x.Amount).GreaterThan(0)
            .WithMessage("An adjustment must be greater than zero.");
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(500)
            .WithMessage("Give the reason for the adjustment.");
        RuleFor(x => x.Remarks).MaximumLength(500);
    }
}

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

    /// <summary>Net totals per calendar month of the requested year (default: this year).</summary>
    Task<ExpenseMonthlyDto> GetMonthlyAsync(ExpenseMonthlyRequest request,
        CancellationToken ct = default);

    Task<PagedResult<ExpenseAdjustmentDto>> GetAdjustmentsAsync(
        ExpenseAdjustmentListRequest request, CancellationToken ct = default);

    Task<ExpenseAdjustmentDto> GetAdjustmentAsync(long adjustmentId, CancellationToken ct = default);

    /// <summary>Allowed on a bank-matched expense: the voucher amount the bank saw is
    /// untouched, and the adjustment is the record of the correction. Refused when it
    /// would take the expense's net total below zero.</summary>
    Task<ExpenseAdjustmentDto> CreateAdjustmentAsync(SaveExpenseAdjustmentRequest request,
        CancellationToken ct = default);

    Task<ExpenseAdjustmentDto> UpdateAdjustmentAsync(long adjustmentId,
        SaveExpenseAdjustmentRequest request, CancellationToken ct = default);

    Task DeleteAdjustmentAsync(long adjustmentId, CancellationToken ct = default);
}
