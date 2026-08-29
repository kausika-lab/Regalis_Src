using Regalis.Domain.Enums;
using Regalis.Shared.Api;

namespace Regalis.Application.Finance;

public record WalletDto(
    long WalletId,
    long CustomerId,
    decimal Balance,
    decimal TotalCredited,
    decimal TotalDebited,
    DateTime? LastTransactionUtc);

public record WalletTransactionDto(
    long WalletTransactionId,
    string TransactionNumber,
    byte Direction,
    byte Category,
    decimal Amount,
    decimal BalanceAfter,
    string Narration,
    DateTime TransactionUtc);

public class WalletTransactionListRequest : PagedRequest
{
    /// <summary>Filter to a calendar month, e.g. "2026-09".</summary>
    public string? Month { get; set; }
    public byte? Category { get; set; }
}

public interface IWalletService
{
    Task<WalletDto> GetMyWalletAsync(CancellationToken ct = default);
    Task<PagedResult<WalletTransactionDto>> GetMyTransactionsAsync(
        WalletTransactionListRequest request, CancellationToken ct = default);

    /// <summary>Credits inside the ambient transaction, writing the self-checking
    /// ledger row (BalanceAfter). Caller owns SaveChanges/commit.</summary>
    Task CreditAsync(long customerId, decimal amount, WalletCategory category, string narration,
        string? referenceType = null, long? referenceId = null, long? paymentId = null,
        CancellationToken ct = default);

    /// <summary>Debits inside the ambient transaction. Throws
    /// INSUFFICIENT_WALLET_BALANCE when the balance cannot cover the amount —
    /// and the database CHECK constraint refuses an overdraft regardless.</summary>
    Task DebitAsync(long customerId, decimal amount, WalletCategory category, string narration,
        string? referenceType = null, long? referenceId = null, long? paymentId = null,
        CancellationToken ct = default);
}
