using FluentValidation;

namespace Regalis.Application.Bookings;

public record CancelBookingRequest(string? Reason);

public record CancelBookingResponse(
    BookingDto Booking,
    decimal RefundAmount,
    decimal RefundPercent,
    // Refunds always credit the personal wallet (A15).
    bool RefundedToWallet);

public record RescheduleBookingRequest(DateTime NewStartUtc);

public record ExtendBookingRequest(
    int AdditionalMinutes,
    // 'wallet', or 'cash' for the front desk.
    string PaymentMethod);

public record ExtendBookingResponse(
    BookingDto Booking,
    decimal AdditionalAmount,
    long InvoiceId,
    string InvoiceNumber);

public class CancelBookingRequestValidator : AbstractValidator<CancelBookingRequest>
{
    public CancelBookingRequestValidator()
    {
        RuleFor(x => x.Reason).MaximumLength(500);
    }
}

public class RescheduleBookingRequestValidator : AbstractValidator<RescheduleBookingRequest>
{
    public RescheduleBookingRequestValidator()
    {
        RuleFor(x => x.NewStartUtc).NotEmpty();
    }
}

public class ExtendBookingRequestValidator : AbstractValidator<ExtendBookingRequest>
{
    public ExtendBookingRequestValidator()
    {
        RuleFor(x => x.AdditionalMinutes).GreaterThan(0);
        RuleFor(x => x.PaymentMethod)
            .Must(m => m is PaymentMethods.Wallet or PaymentMethods.Cash)
            .WithMessage("Extensions are settled by wallet, or cash at the front desk.");
    }
}

public interface IBookingMutationService
{
    /// <summary>Cancellation is a status change plus history, never a delete
    /// (Invariant 3). The refund percent comes from the cancellation policy
    /// settings and credits the personal wallet.</summary>
    Task<CancelBookingResponse> CancelAsync(long bookingId, CancelBookingRequest request,
        CancellationToken ct = default);

    /// <summary>Moves a confirmed booking to a new start (same duration, same price)
    /// after re-running the locked availability check.</summary>
    Task<BookingDto> RescheduleAsync(long bookingId, RescheduleBookingRequest request,
        CancellationToken ct = default);

    /// <summary>Adds minutes to the end of a confirmed/checked-in booking:
    /// availability-checked, paid, and invoiced as a supplementary invoice.</summary>
    Task<ExtendBookingResponse> ExtendAsync(long bookingId, ExtendBookingRequest request,
        CancellationToken ct = default);
}
