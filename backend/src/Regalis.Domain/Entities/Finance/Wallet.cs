using Regalis.Domain.Common;
using Regalis.Domain.Entities.Identity;

namespace Regalis.Domain.Entities.Finance;

/// <summary>MST_Wallet — one row per customer. A CHECK constraint refuses an overdraft
/// at the database level; the wallet is non-withdrawable (Doc-1 §22).</summary>
public class Wallet : AuditableEntity
{
    public long WalletId { get; set; }
    public long CustomerId { get; set; }
    public decimal Balance { get; set; }
    public decimal TotalCredited { get; set; }
    public decimal TotalDebited { get; set; }
    public DateTime? LastTransactionUtc { get; set; }
    public byte[] RowVersion { get; set; } = [];

    public User Customer { get; set; } = null!;
    public ICollection<WalletTransaction> Transactions { get; set; } = new List<WalletTransaction>();
}
