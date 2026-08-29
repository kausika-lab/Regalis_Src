using FluentValidation;
using Regalis.Shared.Api;

namespace Regalis.Application.Bookings;

/// <summary>How the booking is paid at confirmation time.</summary>
public static class PaymentMethods
{
    public const string Wallet = "wallet";
    public const string Gateway = "gateway";
    /// <summary>Admin bookings only — cash collected at the desk.</summary>
    public const string Cash = "cash";
}

public record ConfirmBookingRequest(
    string PaymentMethod,
    string? GatewayOrderId,
    string? GatewayPaymentId,
    string? GatewaySignature);

public record ConfirmBookingResponse(
    BookingDto Booking,
    long PaymentId,
    string PaymentNumber,
    long InvoiceId,
    string InvoiceNumber,
    string AccessTokenValue,
    string QrPayload);

public class ConfirmBookingRequestValidator : AbstractValidator<ConfirmBookingRequest>
{
    public ConfirmBookingRequestValidator()
    {
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

public record AdminCreateBookingRequest(
    long CustomerId,
    int WorkspaceId,
    DateTime StartUtc,
    int DurationMinutes,
    string? Notes,
    // 'wallet' or 'cash' — the admin flow confirms immediately.
    string PaymentMethod);

public class AdminCreateBookingRequestValidator : AbstractValidator<AdminCreateBookingRequest>
{
    public AdminCreateBookingRequestValidator()
    {
        RuleFor(x => x.CustomerId).GreaterThan(0);
        RuleFor(x => x.WorkspaceId).GreaterThan(0);
        RuleFor(x => x.DurationMinutes).GreaterThan(0);
        RuleFor(x => x.Notes).MaximumLength(500);
        RuleFor(x => x.PaymentMethod)
            .Must(m => m is PaymentMethods.Wallet or PaymentMethods.Cash)
            .WithMessage("Admin bookings are paid by 'wallet' or 'cash'.");
    }
}

public class BookingListRequest : PagedRequest
{
    public DateOnly? From { get; set; }
    public DateOnly? To { get; set; }
    public byte? Status { get; set; }
    public int? LocationId { get; set; }
    public int? WorkspaceTypeId { get; set; }
    public long? CustomerId { get; set; }
}

public static class MyBookingTabs
{
    public const string Upcoming = "upcoming";
    public const string Completed = "completed";
    public const string Cancelled = "cancelled";
}
