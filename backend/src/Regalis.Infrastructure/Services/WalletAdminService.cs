using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Regalis.Application.Abstractions;
using Regalis.Application.Common.Exceptions;
using Regalis.Application.Finance;
using Regalis.Domain.Entities.Finance;
using Regalis.Domain.Entities.Identity;
using Regalis.Domain.Enums;
using Regalis.Infrastructure.Persistence;
using Regalis.Shared.Api;
using Regalis.Shared.Constants;

namespace Regalis.Infrastructure.Services;

public class WalletAdminService(
    RegalisDbContext db,
    IWalletService walletService,
    IPaymentGateway paymentGateway,
    INumberSequenceService sequences,
    ISettingService settings,
    ICurrentUser currentUser,
    IDateTimeProvider clock,
    ILogger<WalletAdminService> logger) : IWalletAdminService
{
    public async Task<PaymentOrderResponse> CreateRechargeOrderAsync(RechargeOrderRequest request,
        CancellationToken ct = default)
    {
        if (currentUser.UserType != UserType.Customer)
        {
            throw new ForbiddenException("Only customers recharge a wallet.");
        }
        if (!await settings.GetBoolAsync(AppSettingKeys.Wallet.Enabled, true, ct))
        {
            throw new BusinessRuleException("The wallet is currently disabled.");
        }
        var min = await settings.GetDecimalAsync(AppSettingKeys.Wallet.MinRecharge, 100m, ct);
        var max = await settings.GetDecimalAsync(AppSettingKeys.Wallet.MaxRecharge, 50000m, ct);
        if (request.Amount < min || request.Amount > max)
        {
            throw new BusinessRuleException(
                $"Recharge must be between ₹{min:N0} and ₹{max:N0}.");
        }

        var order = await paymentGateway.CreateOrderAsync(request.Amount, "INR",
            $"WALLET-{currentUser.UserId}", ct);
        var payment = new Payment
        {
            PaymentNumber = await sequences.NextAsync(SequenceKeys.Payment, ct),
            CustomerId = currentUser.UserId,
            Amount = request.Amount,
            PaymentMethod = PaymentMethod.Upi,
            Gateway = paymentGateway.ProviderName,
            GatewayOrderId = order.OrderId,
            Status = PaymentStatus.Pending
        };
        db.Payments.Add(payment);
        await db.SaveChangesAsync(ct);

        return new PaymentOrderResponse(payment.PaymentId, payment.PaymentNumber,
            order.OrderId, order.KeyId, order.Amount, order.Currency, paymentGateway.ProviderName);
    }

    public async Task<RechargeResponse> VerifyRechargeAsync(RechargeVerifyRequest request,
        CancellationToken ct = default)
    {
        var strategy = db.Database.CreateExecutionStrategy();
        var transactionNumber = await strategy.ExecuteAsync(async () =>
        {
            db.ChangeTracker.Clear();
            await using var transaction = await db.Database.BeginTransactionAsync(ct);

            var payment = await db.Payments.FirstOrDefaultAsync(p =>
                p.GatewayOrderId == request.GatewayOrderId &&
                p.CustomerId == currentUser.UserId, ct)
                ?? throw new BusinessRuleException("No recharge order was found.",
                    ErrorCodes.PaymentVerificationFailed);
            if (payment.Status != PaymentStatus.Pending)
            {
                throw new BusinessRuleException("This recharge is already settled.",
                    ErrorCodes.PaymentVerificationFailed);
            }
            if (!paymentGateway.VerifySignature(request.GatewayOrderId,
                    request.GatewayPaymentId, request.GatewaySignature))
            {
                payment.Status = PaymentStatus.Failed;
                payment.FailureReason = "Signature verification failed";
                await db.SaveChangesAsync(ct);
                throw new BusinessRuleException("Payment verification failed.",
                    ErrorCodes.PaymentVerificationFailed);
            }

            payment.GatewayPaymentId = request.GatewayPaymentId;
            payment.GatewaySignature = request.GatewaySignature;
            payment.IsSignatureVerified = true;
            payment.Status = PaymentStatus.Success;
            payment.PaidUtc = clock.UtcNow;

            await walletService.CreditAsync(currentUser.UserId, payment.Amount,
                WalletCategory.Recharge, $"Wallet recharge {payment.PaymentNumber}",
                paymentId: payment.PaymentId, ct: ct);
            await db.SaveChangesAsync(ct);

            var number = await db.WalletTransactions
                .Where(t => t.PaymentId == payment.PaymentId)
                .Select(t => t.TransactionNumber)
                .FirstAsync(ct);
            await transaction.CommitAsync(ct);
            return number;
        });

        logger.LogInformation("Wallet recharge settled for customer {CustomerId}", currentUser.UserId);
        var wallet = await walletService.GetMyWalletAsync(ct);
        return new RechargeResponse(wallet, transactionNumber);
    }

    public async Task<WalletDto> GetWalletAsync(long customerId, CancellationToken ct = default)
    {
        await RequireCustomerAsync(customerId, ct);
        var wallet = await db.Wallets.AsNoTracking()
            .FirstOrDefaultAsync(w => w.CustomerId == customerId, ct);
        return wallet is null
            ? new WalletDto(0, customerId, 0, 0, 0, null)
            : new WalletDto(wallet.WalletId, wallet.CustomerId, wallet.Balance,
                wallet.TotalCredited, wallet.TotalDebited, wallet.LastTransactionUtc);
    }

    public async Task<PagedResult<WalletTransactionDto>> GetTransactionsAsync(long customerId,
        WalletTransactionListRequest request, CancellationToken ct = default)
    {
        await RequireCustomerAsync(customerId, ct);
        var query = db.WalletTransactions.AsNoTracking()
            .Where(t => t.CustomerId == customerId);
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

    public async Task<WalletDto> AdjustAsync(long customerId, WalletAdjustRequest request,
        CancellationToken ct = default)
    {
        await RequireCustomerAsync(customerId, ct);
        var narration = $"Admin adjustment: {request.Reason.Trim()}";
        var strategy = db.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            db.ChangeTracker.Clear();
            await using var transaction = await db.Database.BeginTransactionAsync(ct);
            if (request.Direction == "credit")
            {
                await walletService.CreditAsync(customerId, request.Amount,
                    WalletCategory.AdminAdjustment, narration, ct: ct);
            }
            else
            {
                await walletService.DebitAsync(customerId, request.Amount,
                    WalletCategory.AdminAdjustment, narration, ct: ct);
            }
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            return true;
        });

        logger.LogWarning("Wallet {Direction} adjustment of {Amount} for customer {CustomerId} by {AdminId}: {Reason}",
            request.Direction, request.Amount, customerId, currentUser.UserId, request.Reason);
        return await GetWalletAsync(customerId, ct);
    }

    private async Task RequireCustomerAsync(long customerId, CancellationToken ct)
    {
        var exists = await db.Users.AnyAsync(
            u => u.UserId == customerId && u.UserType == UserType.Customer, ct);
        if (!exists)
        {
            throw new NotFoundException(nameof(User), customerId);
        }
    }
}
