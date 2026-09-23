using Regalis.Domain.Common;

namespace Regalis.Domain.Entities.Finance;

/// <summary>MST_BankAccount (document §13). Deliberately stores only the opening
/// balance: the current balance is derived from the transaction ledger the same way
/// inventory stock is derived (Invariant 11), so there is never a second source of
/// truth for how much money the account holds.</summary>
public class BankAccount : AuditableEntity
{
    public int BankAccountId { get; set; }
    public string AccountCode { get; set; } = string.Empty;
    public string BankName { get; set; } = string.Empty;
    public string AccountName { get; set; } = string.Empty;
    /// <summary>Stored masked (last four kept) — the full number is never needed to
    /// reconcile and is a needless liability at rest.</summary>
    public string AccountNumberMasked { get; set; } = string.Empty;
    public string? Ifsc { get; set; }
    public string? Branch { get; set; }
    public string? AccountType { get; set; }
    public decimal OpeningBalance { get; set; }
    public DateOnly? OpeningBalanceAsOn { get; set; }
    /// <summary>Null means the account is company-wide, as with MST_PassPlan.</summary>
    public int? LocationId { get; set; }
    public string? Notes { get; set; }

    public ICollection<BankTransaction> Transactions { get; set; } = new List<BankTransaction>();
}
