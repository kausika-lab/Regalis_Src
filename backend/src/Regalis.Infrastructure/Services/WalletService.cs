using Microsoft.EntityFrameworkCore;
using Regalis.Application.Abstractions;
using Regalis.Application.Common.Exceptions;
using Regalis.Application.Finance;
using Regalis.Domain.Entities.Finance;
using Regalis.Domain.Enums;
using Regalis.Infrastructure.Persistence;
using Regalis.Shared.Api;
using Regalis.Shared.Constants;

namespace Regalis.Infrastructure.Services;

/// <summary>
/// The wallet ledger is append-only and self-checking: BalanceAfter is written in
/// the same transaction that moves MST_Wallet.Balance, so SUM(credits) − SUM(debits)
/// always equals Balance. The CHECK constraint refuses an overdraft at the database
/// level, and RowVersion catches concurrent movements.
/// </summary>
public class WalletService(
    RegalisDbContext db,
    INumberSequenceService sequences,
    ICurrentUser currentUser,
    IDateTimeProvider clock) : IWalletService
{
    public async Task<WalletDto> GetMyWalletAsync(CancellationToken ct = default)
    {
        if (currentUser.UserType != UserType.Customer)
        {
            throw new ForbiddenException("Only customers have a wallet.");
        }
        var wallet = await GetOrCreateWalletAsync(currentUser.UserId, ct);
        return new WalletDto(wallet.WalletId, wallet.CustomerId, wallet.Balance,
            wallet.TotalCredited, wallet.TotalDebited, wallet.LastTransactionUtc);
    }

    public async Task<PagedResult<WalletTransactionDto>> GetMyTransactionsAsync(
        WalletTransactionListRequest request, CancellationToken ct = default)
    {
        var query = db.WalletTransactions.AsNoTracking()
            .Where(t => t.CustomerId == currentUser.UserId);

        if (!string.IsNullOrWhiteSpace(request.Month) &&
            DateOnly.TryParse($"{request.Month}-01", out var monthStart))
        {
            var fromUtc = monthStart.ToDateTime(TimeOnly.MinValue);
            var toUtc = fromUtc.AddMonths(1);
            query = query.Where(t => t.TransactionUtc >= fromUtc && t.TransactionUtc < toUtc);
        }
        if (request.Category is not null)
        {
            query = query.Where(t => (byte)t.Category == request.Category);
        }

        var totalCount = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(t => t.TransactionUtc)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(t => new WalletTransactionDto(
                t.WalletTransactionId, t.TransactionNumber, (byte)t.Direction, (byte)t.Category,
                t.Amount, t.BalanceAfter, t.Narration, t.TransactionUtc))
            .ToListAsync(ct);
        return PagedResult<WalletTransactionDto>.Create(items, request.Page, request.PageSize, totalCount);
    }

    public async Task CreditAsync(long customerId, decimal amount, WalletCategory category,
        string narration, string? referenceType = null, long? referenceId = null,
        long? paymentId = null, CancellationToken ct = default)
    {
        ValidateAmount(amount);
        var wallet = await GetOrCreateWalletAsync(customerId, ct);
        wallet.Balance += amount;
        wallet.TotalCredited += amount;
        wallet.LastTransactionUtc = clock.UtcNow;
        await AddLedgerRowAsync(wallet, WalletDirection.Credit, category, amount, narration,
            referenceType, referenceId, paymentId, ct);
    }

    public async Task DebitAsync(long customerId, decimal amount, WalletCategory category,
        string narration, string? referenceType = null, long? referenceId = null,
        long? paymentId = null, CancellationToken ct = default)
    {
        ValidateAmount(amount);
        var wallet = await GetOrCreateWalletAsync(customerId, ct);
        if (wallet.Balance < amount)
        {
            throw new BusinessRuleException(
                $"Insufficient wallet balance: ₹{wallet.Balance:N2} available, ₹{amount:N2} required.",
                ErrorCodes.InsufficientWalletBalance);
        }
        wallet.Balance -= amount;
        wallet.TotalDebited += amount;
        wallet.LastTransactionUtc = clock.UtcNow;
        await AddLedgerRowAsync(wallet, WalletDirection.Debit, category, amount, narration,
            referenceType, referenceId, paymentId, ct);
    }

    private static void ValidateAmount(decimal amount)
    {
        if (amount <= 0)
        {
            throw new BusinessRuleException("The amount must be positive.");
        }
    }

    private async Task AddLedgerRowAsync(Wallet wallet, WalletDirection direction,
        WalletCategory category, decimal amount, string narration, string? referenceType,
        long? referenceId, long? paymentId, CancellationToken ct)
    {
        db.WalletTransactions.Add(new WalletTransaction
        {
            Wallet = wallet,
            CustomerId = wallet.CustomerId,
            TransactionNumber = await sequences.NextAsync(SequenceKeys.WalletTransaction, ct),
            Direction = direction,
            Category = category,
            Amount = amount,
            BalanceAfter = wallet.Balance,
            ReferenceType = referenceType,
            ReferenceId = referenceId,
            PaymentId = paymentId,
            Narration = narration,
            TransactionUtc = clock.UtcNow,
            PerformedByUserId = currentUser.IsAuthenticated ? currentUser.UserId : wallet.CustomerId
        });
    }

    private async Task<Wallet> GetOrCreateWalletAsync(long customerId, CancellationToken ct)
    {
        var wallet = await db.Wallets.FirstOrDefaultAsync(w => w.CustomerId == customerId, ct);
        if (wallet is null)
        {
            wallet = new Wallet { CustomerId = customerId };
            db.Wallets.Add(wallet);
            await db.SaveChangesAsync(ct);
        }
        return wallet;
    }
}
