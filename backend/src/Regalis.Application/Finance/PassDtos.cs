using FluentValidation;
using Regalis.Application.Bookings;
using Regalis.Shared.Api;

namespace Regalis.Application.Finance;

// ── Plans ───────────────────────────────────────────────────────────────────

public record PassPlanDto(
    int PassPlanId,
    string PlanCode,
    string PlanName,
    byte PassType,
    int? LocationId,
    string? LocationName,
    decimal Price,
    int ValidityDays,
    int? MaxGuests,
    string? Description,
    string? TermsAndConditions,
    int DisplayOrder,
    bool IsActive,
    // Price + GST as it will be charged, so plan cards can show the real total.
    decimal CgstAmount,
    decimal SgstAmount,
    decimal TotalAmount);

public record SavePassPlanRequest(
    string PlanCode,
    string PlanName,
    byte PassType,
    int? LocationId,
    decimal Price,
    int ValidityDays,
    int? MaxGuests,
    string? Description,
    string? TermsAndConditions,
    int DisplayOrder,
    bool IsActive);

public class SavePassPlanRequestValidator : AbstractValidator<SavePassPlanRequest>
{
    public SavePassPlanRequestValidator()
    {
        RuleFor(x => x.PlanCode).NotEmpty().MaximumLength(40).Matches("^[A-Z0-9-]+$")
            .WithMessage("Plan code must be uppercase letters, digits and hyphens.");
        RuleFor(x => x.PlanName).NotEmpty().MaximumLength(120);
        RuleFor(x => x.PassType).Must(t => t is 1 or 2 or 3 or 4)
            .WithMessage("PassType must be 1 (day), 2 (monthly), 3 (weekly) or 4 (yearly).");
        RuleFor(x => x.Price).GreaterThan(0);
        RuleFor(x => x.ValidityDays).GreaterThan(0);
        RuleFor(x => x.MaxGuests).GreaterThanOrEqualTo(0).When(x => x.MaxGuests is not null);
    }
}

// ── Purchases ───────────────────────────────────────────────────────────────

public record PassOrderRequest(int PassPlanId);

public record PurchasePassRequest(
    int PassPlanId,
    // First valid day (IST calendar date).
    DateOnly ValidFrom,
    string PaymentMethod,
    string? GatewayOrderId,
    string? GatewayPaymentId,
    string? GatewaySignature);

public record CustomerPassDto(
    long CustomerPassId,
    string PassNumber,
    long CustomerId,
    string CustomerName,
    int PassPlanId,
    string PlanName,
    byte PassType,
    int LocationId,
    DateOnly ValidFrom,
    DateOnly ValidTo,
    decimal Price,
    decimal TaxAmount,
    decimal TotalAmount,
    byte Status,
    DateTime PurchaseUtc,
    long? InvoiceId);

public record PurchasePassResponse(
    CustomerPassDto Pass,
    string PaymentNumber,
    long InvoiceId,
    string InvoiceNumber,
    string AccessTokenValue,
    string QrPayload);

// ── One live day pass at a time ─────────────────────────────────────────────

/// <summary>The customer's running (or not yet started) day pass and what its unused
/// days are worth: price paid × remaining days / total days, before GST.</summary>
public record CurrentPassDto(
    long CustomerPassId,
    string PassNumber,
    string PlanName,
    DateOnly ValidFrom,
    DateOnly ValidTo,
    int TotalDays,
    int RemainingDays,
    decimal UnusedCredit);

/// <summary>Whether one plan can be bought right now and what it would cost. While a
/// day pass is live only a longer pack is allowed (an upgrade): it starts today, or on
/// the current pass's start date if that is still ahead, and the current pass's unused
/// days come off the price. Amounts are what the purchase will actually charge.</summary>
public record PassPurchaseOptionDto(
    int PassPlanId,
    bool Allowed,
    string? Reason,
    bool IsUpgrade,
    DateOnly StartsOn,
    decimal Price,
    decimal Credit,
    decimal TaxableAmount,
    decimal CgstAmount,
    decimal SgstAmount,
    decimal TotalAmount);

public record PassPurchaseOptionsDto(CurrentPassDto? Current, List<PassPurchaseOptionDto> Plans);

public class PassListRequest : PagedRequest
{
    public byte? PassType { get; set; }
    public byte? Status { get; set; }
}

public class PassOrderRequestValidator : AbstractValidator<PassOrderRequest>
{
    public PassOrderRequestValidator()
    {
        RuleFor(x => x.PassPlanId).GreaterThan(0);
    }
}

public class PurchasePassRequestValidator : AbstractValidator<PurchasePassRequest>
{
    public PurchasePassRequestValidator()
    {
        RuleFor(x => x.PassPlanId).GreaterThan(0);
        RuleFor(x => x.PaymentMethod)
            .Must(m => m is PaymentMethods.Wallet or PaymentMethods.Gateway or PaymentMethods.Cash)
            .WithMessage("Payment method must be 'wallet', 'gateway' or 'cash'.");
        When(x => x.PaymentMethod == PaymentMethods.Gateway, () =>
        {
            RuleFor(x => x.GatewayOrderId).NotEmpty();
            RuleFor(x => x.GatewayPaymentId).NotEmpty();
            RuleFor(x => x.GatewaySignature).NotEmpty();
        });
    }
}

public interface IPassService
{
    Task<List<PassPlanDto>> GetActivePlansAsync(byte? passType, CancellationToken ct = default);
    Task<List<PassPlanDto>> GetAllPlansAsync(CancellationToken ct = default);
    Task<PassPlanDto> CreatePlanAsync(SavePassPlanRequest request, CancellationToken ct = default);
    Task<PassPlanDto> UpdatePlanAsync(int passPlanId, SavePassPlanRequest request, CancellationToken ct = default);
    Task DeletePlanAsync(int passPlanId, CancellationToken ct = default);

    /// <summary>Gateway order for a pass purchase (wallet purchases skip this).</summary>
    Task<PaymentOrderResponse> CreatePurchaseOrderAsync(PassOrderRequest request, CancellationToken ct = default);

    /// <summary>The atomic purchase: verified payment, TRN_CustomerPass, GST invoice,
    /// access token valid for the whole pass window, notification. A day pass bought
    /// while another day pass is live must be a longer pack; it replaces the live pass
    /// (cancelled, QR revoked) and its unused days are credited against the price.</summary>
    Task<PurchasePassResponse> PurchaseAsync(PurchasePassRequest request, CancellationToken ct = default);

    /// <summary>What the caller may buy right now: the live day pass, if any, and for
    /// each active plan whether it is allowed, whether it is an upgrade, and the price
    /// after the credit. The purchase charges exactly these amounts.</summary>
    Task<PassPurchaseOptionsDto> GetPurchaseOptionsAsync(byte? passType, CancellationToken ct = default);

    Task<PagedResult<CustomerPassDto>> GetMyPassesAsync(PassListRequest request, CancellationToken ct = default);
    Task<PagedResult<CustomerPassDto>> GetPassesAsync(PassListRequest request, CancellationToken ct = default);

    /// <summary>Cancel before the pass starts: full refund to the wallet; once
    /// started it can only be cancelled without a refund.</summary>
    Task<CustomerPassDto> CancelAsync(long customerPassId, CancellationToken ct = default);
}
