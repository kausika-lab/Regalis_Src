using Regalis.Domain.Common;
using Regalis.Domain.Enums;

namespace Regalis.Domain.Entities.Finance;

/// <summary>TRN_BankTransaction (document §13–§14). Reconciliation state lives HERE
/// and nowhere else: matching a bank row to a payment or an expense never writes back
/// to TRN_Payment, TRN_Invoice or TRN_Expense, so cash-received keeps exactly one
/// owner. Matching also never creates a payment or expense row — it only points at an
/// existing one, which is the document's explicit no-duplicates rule.</summary>
public class BankTransaction : AuditableEntity
{
    public long BankTransactionId { get; set; }
    public int BankAccountId { get; set; }
    public DateOnly TransactionDate { get; set; }
    public BankTransactionDirection Direction { get; set; }
    public decimal Amount { get; set; }
    public string Narration { get; set; } = string.Empty;
    /// <summary>Cheque number, UTR, NEFT reference — whatever the statement shows.</summary>
    public string? ReferenceNumber { get; set; }
    /// <summary>The bank's own transaction id, when the statement carries one.</summary>
    public string? BankReferenceId { get; set; }
    public string? Category { get; set; }

    public ReconciliationStatus ReconciliationStatus { get; set; } = ReconciliationStatus.Unreconciled;
    public long? MatchedPaymentId { get; set; }
    public long? MatchedExpenseId { get; set; }
    public DateTime? MatchedUtc { get; set; }
    public long? MatchedByUserId { get; set; }
    public DateTime? ReconciledUtc { get; set; }
    public long? ReconciledByUserId { get; set; }

    public int LocationId { get; set; }
    public long EnteredByUserId { get; set; }
    /// <summary>Set when the row arrived through a statement import rather than
    /// manual entry; lets a bad import be identified and reversed as a batch.</summary>
    public string? ImportBatchId { get; set; }
    public string? Remarks { get; set; }

    public BankAccount BankAccount { get; set; } = null!;
}
