using Regalis.Domain.Common;
using Regalis.Domain.Enums;

namespace Regalis.Domain.Entities.Finance;

/// <summary>TRN_ExpenseAdjustment — a correction recorded against an existing expense
/// instead of editing the voucher. It is the only way to correct an expense that a bank
/// statement line has been matched to, because the voucher's own Amount stays what the
/// bank saw. The expense's net total is Amount + Expense.AdjustmentTotal.</summary>
public class ExpenseAdjustment : AuditableEntity
{
    public long ExpenseAdjustmentId { get; set; }
    public long ExpenseId { get; set; }
    public ExpenseAdjustmentType AdjustmentType { get; set; }
    /// <summary>Always positive; AdjustmentType carries the sign.</summary>
    public decimal Amount { get; set; }
    public DateOnly AdjustmentDate { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string? Remarks { get; set; }
}
