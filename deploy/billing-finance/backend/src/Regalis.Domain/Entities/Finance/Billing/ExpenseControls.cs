using System;
using Regalis.Domain.Common;

namespace Regalis.Domain.Entities.Finance.Billing;

public enum ExpenseApprovalStatus : byte
{
    AwaitingApproval = 1,
    Approved = 2,
    Rejected = 3
}

/// <summary>Where the money for an expense came from.</summary>
public enum ExpensePaidFrom : byte
{
    Bank = 1,
    PettyCash = 2,

    /// <summary>A member of staff paid; Regalis owes them until reimbursed.</summary>
    Staff = 3
}

/// <summary>MST_Department - Front desk, Facilities, Operations, Overheads, Marketing, IT. Editable.</summary>
public class Department : AuditableEntity
{
    public int DepartmentId { get; set; }
    public string Name { get; set; } = string.Empty;
    public int DisplayOrder { get; set; }
}

/// <summary>TRN_ExpenseControl - the approval, department, receipts and reimbursement side of
/// an expense (TRN_Expense keeps the voucher). One row per expense: a database trigger writes
/// it when the expense is inserted, so an expense saved through any path gets one.</summary>
public class ExpenseControl : AuditableEntity
{
    public long ExpenseId { get; set; }
    public int? DepartmentId { get; set; }
    public ExpenseApprovalStatus ApprovalStatus { get; set; } = ExpenseApprovalStatus.Approved;

    /// <summary>0 none needed, 1 office manager, 2 owner - from the limits at the time.</summary>
    public byte ApprovalLevel { get; set; }

    public long? DecidedByUserId { get; set; }
    public DateTime? DecidedUtc { get; set; }
    public string? DecisionComment { get; set; }
    public ExpensePaidFrom PaidFrom { get; set; } = ExpensePaidFrom.Bank;
    public bool IsReimbursement { get; set; }
    public long? ReimburseToUserId { get; set; }
    public DateTime? ReimbursedUtc { get; set; }
    public long? ReimbursedByUserId { get; set; }
    public string? ReimbursedVia { get; set; }
    public string? ReimbursementReference { get; set; }
    public long? RecurringExpenseId { get; set; }

    /// <summary>A recurring draft waiting for someone to confirm it.</summary>
    public bool IsRecurringDraft { get; set; }

    public long? PettyCashEntryId { get; set; }
}

/// <summary>TRN_ExpenseReceipt - up to five files on an expense.</summary>
public class ExpenseReceipt
{
    public long ExpenseReceiptId { get; set; }
    public long ExpenseId { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string StoredPath { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
    public long UploadedByUserId { get; set; }
    public DateTime UploadedUtc { get; set; }
    public bool IsDeleted { get; set; }
}

/// <summary>TRN_RecurringExpense - rent, internet, housekeeping contract... Never posted
/// automatically: a draft expense appears three days early for someone to confirm.</summary>
public class RecurringExpense : AuditableEntity
{
    public long RecurringExpenseId { get; set; }
    public string Category { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int? DepartmentId { get; set; }
    public int? SupplierId { get; set; }
    public int LocationId { get; set; }
    public decimal Amount { get; set; }
    public byte PaymentMethod { get; set; }
    public int DayOfMonth { get; set; }
    public DateOnly StartOn { get; set; }
    public DateOnly? EndOn { get; set; }

    /// <summary>The month (first day) last raised as a draft.</summary>
    public DateOnly? LastRaisedFor { get; set; }
}

public enum PettyCashEntryType : byte
{
    TopUp = 1,
    Expense = 2,
    Adjustment = 3
}

/// <summary>TRN_PettyCashEntry - the float: top-ups in, cash expenses out, counted
/// adjustments. Not the Cash Register (money taken from customers) - two different tins.</summary>
public class PettyCashEntry : AuditableEntity
{
    public long PettyCashEntryId { get; set; }
    public PettyCashEntryType EntryType { get; set; }

    /// <summary>Positive in, negative out.</summary>
    public decimal Amount { get; set; }

    public DateOnly HappenedOn { get; set; }
    public string Description { get; set; } = string.Empty;
    public long? ExpenseId { get; set; }
    public long? VendorPaymentId { get; set; }
    public int? BankAccountId { get; set; }
    public decimal BalanceAfter { get; set; }
    public string? Reason { get; set; }

    /// <summary>On a count: what the tin actually held.</summary>
    public decimal? CountedAmount { get; set; }

    public long EnteredByUserId { get; set; }
}
