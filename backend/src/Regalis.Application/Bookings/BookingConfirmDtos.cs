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
    /// <summary>Confirmed now, paid at the front desk later (cash, UPI or card).</summary>
    public const string PayLater = "paylater";
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
    string QrPayload,
    bool IsPayLater = false);

public class ConfirmBookingRequestValidator : AbstractValidator<ConfirmBookingRequest>
{
    public ConfirmBookingRequestValidator()
    {
        RuleFor(x => x.PaymentMethod)
            .Must(m => m is PaymentMethods.Wallet or PaymentMethods.Gateway or PaymentMethods.Cash or PaymentMethods.PayLater)
            .WithMessage("Payment method must be 'wallet', 'gateway', 'cash' or 'paylater'.");
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
            .Must(m => m is PaymentMethods.Wallet or PaymentMethods.Cash or PaymentMethods.PayLater)
            .WithMessage("Admin bookings are paid by 'wallet', 'cash' or 'paylater'.");
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

/// <summary>The admin list's filters plus the calendar year the monthly chart covers.</summary>
public class BookingMonthlyRequest : BookingListRequest
{
    public int? Year { get; set; }
}

/// <summary>Always twelve months, January to December, by booking date (IST).
/// Count is every booking the filters match — the same rows as the list. Revenue counts
/// only bookings that were paid for and kept (Confirmed, CheckedIn, Completed, NoShow):
/// cancelled bookings, expired holds and unpaid pending holds bring in nothing.</summary>
public record BookingMonthlyDto(int Year, int TotalCount, decimal TotalRevenue,
    List<BookingMonthTotalDto> Months);

public record BookingMonthTotalDto(int Month, int Count, decimal Revenue);

public static class MyBookingTabs
{
    public const string Upcoming = "upcoming";
    public const string Completed = "completed";
    public const string Cancelled = "cancelled";
}
