using FluentValidation;

namespace Regalis.Application.Finance;

public record RechargeOrderRequest(decimal Amount);

public record RechargeVerifyRequest(
    string GatewayOrderId,
    string GatewayPaymentId,
    string GatewaySignature);

public record RechargeResponse(WalletDto Wallet, string TransactionNumber);

public record WalletAdjustRequest(
    // 'credit' or 'debit'.
    string Direction,
    decimal Amount,
    string Reason);

public class RechargeOrderRequestValidator : AbstractValidator<RechargeOrderRequest>
{
    public RechargeOrderRequestValidator()
    {
        RuleFor(x => x.Amount).GreaterThan(0);
    }
}

public class RechargeVerifyRequestValidator : AbstractValidator<RechargeVerifyRequest>
{
    public RechargeVerifyRequestValidator()
    {
        RuleFor(x => x.GatewayOrderId).NotEmpty();
        RuleFor(x => x.GatewayPaymentId).NotEmpty();
        RuleFor(x => x.GatewaySignature).NotEmpty();
    }
}

public class WalletAdjustRequestValidator : AbstractValidator<WalletAdjustRequest>
{
    public WalletAdjustRequestValidator()
    {
        RuleFor(x => x.Direction).Must(d => d is "credit" or "debit")
            .WithMessage("Direction must be 'credit' or 'debit'.");
        RuleFor(x => x.Amount).GreaterThan(0);
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(300)
            .WithMessage("A reason is mandatory; adjustments are fully audited.");
    }
}

public interface IWalletAdminService
{
    /// <summary>Creates the gateway order for a wallet top-up (limits from settings).</summary>
    Task<PaymentOrderResponse> CreateRechargeOrderAsync(RechargeOrderRequest request,
        CancellationToken ct = default);

    /// <summary>Verifies the gateway signature and credits the wallet atomically.</summary>
    Task<RechargeResponse> VerifyRechargeAsync(RechargeVerifyRequest request,
        CancellationToken ct = default);

    /// <summary>Admin view of any customer's wallet.</summary>
    Task<WalletDto> GetWalletAsync(long customerId, CancellationToken ct = default);

    Task<Shared.Api.PagedResult<WalletTransactionDto>> GetTransactionsAsync(long customerId,
        WalletTransactionListRequest request, CancellationToken ct = default);

    /// <summary>Manual credit/debit with a mandatory reason (WALLET.EDIT, audited).</summary>
    Task<WalletDto> AdjustAsync(long customerId, WalletAdjustRequest request,
        CancellationToken ct = default);
}
