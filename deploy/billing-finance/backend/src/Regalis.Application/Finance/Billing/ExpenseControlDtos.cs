using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Regalis.Shared.Api;

namespace Regalis.Application.Finance.Billing;

// ── Expenses: approval, receipts, department, recurring, reimbursement (build doc §12) ──

public record ExpenseReceiptDto(long ExpenseReceiptId, string FileName, string ContentType, long SizeBytes, DateTime UploadedUtc);

/// <summary>ApprovalStatus 1 awaiting, 2 approved, 3 rejected. PaidFrom 1 bank, 2 petty cash, 3 staff.
/// StatusLabel is what the Expenses list shows.</summary>
public record ExpenseControlDto(
    long ExpenseId, int? DepartmentId, string? DepartmentName, byte ApprovalStatus, byte ApprovalLevel, string StatusLabel,
    string? DecidedBy, DateTime? DecidedUtc, string? DecisionComment, byte PaidFrom, bool IsReimbursement, long? ReimburseToUserId,
    string? ReimburseToName, DateTime? ReimbursedUtc, string? ReimbursedVia, bool IsRecurringDraft, long? RecurringExpenseId,
    IReadOnlyList<ExpenseReceiptDto> Receipts, bool ReceiptRequired, bool ReceiptMissing, bool CanApprove, bool IsMine, bool IsBankMatched);

public record SaveExpenseControlRequest(int? DepartmentId, byte PaidFrom, bool IsReimbursement, long? ReimburseToUserId);

public record ExpenseDecisionRequest(string? Comment);

public record ReimburseRequest(string Via, int? BankAccountId, string? Reference, DateOnly? PaidOn);

public record ConfirmRecurringRequest(decimal? Amount);

public class ExpenseQueueRequest : PagedRequest
{
    /// <summary>approval, reimbursement, drafts.</summary>
    public string Queue { get; set; } = "approval";
}

public record ExpenseQueueItemDto(long ExpenseId, string ExpenseNumber, DateOnly ExpenseDate, string Category, string Description,
    decimal Amount, string? EnteredBy, long EnteredByUserId, string? DepartmentName, byte ApprovalLevel, int ReceiptCount, string StatusLabel,
    string? ReimburseToName, bool CanApprove);

/// <summary>The expense register filtered by the new columns. Status: awaiting, approved,
/// rejected, reimbursement-due, petty-cash, recurring-draft.</summary>
public class ExpenseRegisterRequest : PagedRequest
{
    public string? Status { get; set; }
    public int? DepartmentId { get; set; }
    public string? Category { get; set; }
    public DateOnly? From { get; set; }
    public DateOnly? To { get; set; }
}

public record ExpenseRulesDto(decimal NoApprovalUpTo, decimal ManagerUpTo, decimal ReceiptRequiredAbove, int MaxReceipts);

public record DepartmentDto(int DepartmentId, string Name, int DisplayOrder, bool IsActive);

public record SaveDepartmentRequest(string Name, int DisplayOrder, bool IsActive);

public record RecurringExpenseDto(long RecurringExpenseId, string Category, string Description, int? DepartmentId, string? DepartmentName,
    int? SupplierId, string? SupplierName, int LocationId, decimal Amount, byte PaymentMethod, int DayOfMonth, DateOnly StartOn, DateOnly? EndOn,
    DateOnly? LastRaisedFor, DateOnly? NextDue, bool IsActive);

public record SaveRecurringExpenseRequest(string Category, string Description, int? DepartmentId, int? SupplierId, int LocationId,
    decimal Amount, byte PaymentMethod, int DayOfMonth, DateOnly StartOn, DateOnly? EndOn, bool IsActive);

public interface IExpenseControlService
{
    Task<IReadOnlyList<ExpenseControlDto>> GetControlsAsync(IReadOnlyList<long> expenseIds, CancellationToken ct = default);
    Task<ExpenseControlDto> SaveControlAsync(long expenseId, SaveExpenseControlRequest request, CancellationToken ct = default);
    Task<ExpenseControlDto> ApproveAsync(long expenseId, ExpenseDecisionRequest request, CancellationToken ct = default);
    Task<ExpenseControlDto> RejectAsync(long expenseId, ExpenseDecisionRequest request, CancellationToken ct = default);
    Task<ExpenseControlDto> ResubmitAsync(long expenseId, CancellationToken ct = default);
    Task<ExpenseControlDto> ConfirmRecurringAsync(long expenseId, ConfirmRecurringRequest request, CancellationToken ct = default);
    Task<ExpenseControlDto> ReimburseAsync(long expenseId, ReimburseRequest request, CancellationToken ct = default);
    Task<PagedResult<ExpenseQueueItemDto>> GetQueueAsync(ExpenseQueueRequest request, CancellationToken ct = default);
    Task<PagedResult<ExpenseDto>> GetRegisterAsync(ExpenseRegisterRequest request, CancellationToken ct = default);
    Task<ExpenseControlDto> AddReceiptAsync(long expenseId, AttachmentUploadInput file, CancellationToken ct = default);
    Task<ExpenseControlDto> RemoveReceiptAsync(long expenseId, long receiptId, CancellationToken ct = default);
    Task<AttachmentFileDto> GetReceiptAsync(long expenseId, long receiptId, CancellationToken ct = default);
    Task<ExpenseRulesDto> GetRulesAsync(CancellationToken ct = default);
    Task<IReadOnlyList<DepartmentDto>> GetDepartmentsAsync(bool includeInactive, CancellationToken ct = default);
    Task<DepartmentDto> SaveDepartmentAsync(int? id, SaveDepartmentRequest request, CancellationToken ct = default);
    Task<IReadOnlyList<RecurringExpenseDto>> GetRecurringAsync(CancellationToken ct = default);
    Task<RecurringExpenseDto> SaveRecurringAsync(long? id, SaveRecurringExpenseRequest request, CancellationToken ct = default);

    /// <summary>Raises the drafts due within the next three days. Returns how many were raised.</summary>
    Task<int> RaiseRecurringDraftsAsync(CancellationToken ct = default);
}

// ── Petty cash (doc §12.3) ─────────────────────────────────────────────────────

public record PettyCashEntryDto(long PettyCashEntryId, byte EntryType, DateOnly HappenedOn, string Description, decimal In, decimal Out,
    decimal BalanceAfter, string? Reason, decimal? CountedAmount, long? ExpenseId, string? ExpenseNumber, long? VendorPaymentId,
    int? BankAccountId, string? EnteredBy, DateTime CreatedDate, bool IsReconciled);

public record PettyCashOverviewDto(decimal Balance, decimal Float, DateTime? LastCountedUtc, string? LastCountedBy, decimal? LastCountDifference,
    decimal InThisMonth, decimal OutThisMonth, PagedResult<PettyCashEntryDto> Entries);

public record PettyCashTopUpRequest(decimal Amount, DateOnly HappenedOn, int BankAccountId, string? Note);

public record PettyCashCountRequest(decimal CountedAmount, string? Reason);

public interface IPettyCashService
{
    Task<PettyCashOverviewDto> GetOverviewAsync(PagedRequest request, CancellationToken ct = default);
    Task<PettyCashEntryDto> TopUpAsync(PettyCashTopUpRequest request, CancellationToken ct = default);
    Task<PettyCashEntryDto> CountAsync(PettyCashCountRequest request, CancellationToken ct = default);
}
