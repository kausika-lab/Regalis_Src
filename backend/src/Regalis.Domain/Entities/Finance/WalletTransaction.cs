using Regalis.Domain.Common;
using Regalis.Domain.Enums;

namespace Regalis.Domain.Entities.Finance;

/// <summary>TRN_WalletTransaction — append-only ledger. BalanceAfter is written inside
/// the same transaction that updates MST_Wallet.Balance, so the ledger is self-checking:
/// SUM(credits) − SUM(debits) = Balance.</summary>
public class WalletTransaction : AuditableEntity
{
    public long WalletTransactionId { get; set; }
    public long WalletId { get; set; }
    public long CustomerId { get; set; }
    public string TransactionNumber { get; set; } = string.Empty;
    public WalletDirection Direction { get; set; }
    public WalletCategory Category { get; set; }
    public decimal Amount { get; set; }
    public decimal BalanceAfter { get; set; }
    public string? ReferenceType { get; set; }
    public long? ReferenceId { get; set; }
    public long? PaymentId { get; set; }
    public string Narration { get; set; } = string.Empty;
    public DateTime TransactionUtc { get; set; }
    public long PerformedByUserId { get; set; }

    public Wallet Wallet { get; set; } = null!;
}
