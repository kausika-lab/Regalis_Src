using FluentValidation;

namespace Regalis.Application.Bookings;

// ── Availability ────────────────────────────────────────────────────────────

public record SlotDto(DateTime StartUtc, DateTime EndUtc, bool IsAvailable);

public record AvailableWorkspaceDto(
    int WorkspaceId,
    string WorkspaceCode,
    string WorkspaceName,
    string WorkspaceTypeName,
    string? FloorNumber,
    int Capacity,
    decimal PricePerHour,
    string? Equipment,
    IReadOnlyList<string> Amenities,
    // False only when the caller asked for taken workspaces as well.
    bool IsAvailable = true);

/// <summary>A desk or cabin on the monthly booking screen. IsAvailable is false when it is
/// booked or blocked at any point in the term, or has no monthly price.</summary>
public record MonthlyWorkspaceDto(
    int WorkspaceId,
    string WorkspaceCode,
    string WorkspaceName,
    string WorkspaceTypeName,
    string? FloorNumber,
    int Capacity,
    decimal? PricePerMonth,
    string? Equipment,
    IReadOnlyList<string> Amenities,
    bool IsAvailable,
    // "Booked", "Blocked" or "NoMonthlyPrice" when not available.
    string? UnavailableReason);

// ── Monthly desk / cabin booking ────────────────────────────────────────────

public record MonthlyBookingRequest(int WorkspaceId, DateOnly StartDate, int Months, string? Notes = null);

public record MonthlyQuoteResponse(
    int WorkspaceId,
    string WorkspaceName,
    string WorkspaceCode,
    int Months,
    DateOnly StartDate,
    DateOnly EndDate,
    DateTime StartUtc,
    DateTime EndUtc,
    decimal PricePerMonth,
    decimal BaseAmount,
    decimal CgstPercent,
    decimal CgstAmount,
    decimal SgstPercent,
    decimal SgstAmount,
    decimal TotalAmount);

public class MonthlyBookingRequestValidator : AbstractValidator<MonthlyBookingRequest>
{
    public MonthlyBookingRequestValidator()
    {
        RuleFor(x => x.WorkspaceId).GreaterThan(0);
        RuleFor(x => x.Months).Must(m => m is 1 or 3 or 6 or 12)
            .WithMessage("Choose a term of 1, 3, 6 or 12 months.");
        RuleFor(x => x.Notes).MaximumLength(500);
    }
}

public record AvailabilityCheckRequest(int WorkspaceId, DateTime StartUtc, DateTime EndUtc);

public record AvailabilityCheckResponse(bool IsAvailable, string? ConflictReason);

public class AvailabilityCheckRequestValidator : AbstractValidator<AvailabilityCheckRequest>
{
    public AvailabilityCheckRequestValidator()
    {
        RuleFor(x => x.WorkspaceId).GreaterThan(0);
        RuleFor(x => x.EndUtc).GreaterThan(x => x.StartUtc)
            .WithMessage("End must be after start.");
    }
}

// ── Quote / hold ────────────────────────────────────────────────────────────

public record QuoteRequest(int WorkspaceId, DateTime StartUtc, int DurationMinutes);

public record QuoteResponse(
    int WorkspaceId,
    string WorkspaceName,
    DateTime StartUtc,
    DateTime EndUtc,
    int DurationMinutes,
    decimal BaseAmount,
    decimal CgstPercent,
    decimal CgstAmount,
    decimal SgstPercent,
    decimal SgstAmount,
    decimal TotalAmount);

public record CreateBookingRequest(int WorkspaceId, DateTime StartUtc, int DurationMinutes, string? Notes);

public record BookingDto(
    long BookingId,
    string BookingNumber,
    long CustomerId,
    string CustomerName,
    int LocationId,
    int WorkspaceId,
    string WorkspaceCode,
    string WorkspaceName,
    string WorkspaceTypeName,
    DateTime StartUtc,
    DateTime EndUtc,
    int DurationMinutes,
    decimal BaseAmount,
    decimal TaxAmount,
    decimal TotalAmount,
    byte Status,
    byte PaymentStatus,
    byte BookingSource,
    string? Notes,
    DateTime CreatedDate,
    // When the Pending hold lapses; null once the booking is confirmed.
    DateTime? HoldExpiresUtc,
    // 1, 3, 6 or 12 for a monthly desk/cabin booking; null for an hourly one.
    byte? BillingMonths = null,
    // Pay later: confirmed unpaid; the balance is TotalAmount - AmountPaid.
    bool IsPayLater = false,
    decimal AmountPaid = 0m,
    DateTime? PaymentDueUtc = null);

public class QuoteRequestValidator : AbstractValidator<QuoteRequest>
{
    public QuoteRequestValidator()
    {
        RuleFor(x => x.WorkspaceId).GreaterThan(0);
        RuleFor(x => x.DurationMinutes).GreaterThan(0);
    }
}

public class CreateBookingRequestValidator : AbstractValidator<CreateBookingRequest>
{
    public CreateBookingRequestValidator()
    {
        RuleFor(x => x.WorkspaceId).GreaterThan(0);
        RuleFor(x => x.DurationMinutes).GreaterThan(0);
        RuleFor(x => x.Notes).MaximumLength(500);
    }
}

// ── Services ────────────────────────────────────────────────────────────────

public interface IAvailabilityService
{
    /// <summary>Day-grid slots for a location + type: available when at least one
    /// bookable workspace of that type is free for the slot.</summary>
    Task<List<SlotDto>> GetSlotsAsync(int locationId, int workspaceTypeId, DateOnly date,
        CancellationToken ct = default);

    /// <summary>Workspaces for a window, with price — the customer picks one. Only the free
    /// ones, unless <paramref name="includeBusy"/>: then the taken ones come back too, with
    /// IsAvailable = false, so the screen can show them greyed out.</summary>
    Task<List<AvailableWorkspaceDto>> GetAvailableWorkspacesAsync(int locationId, int workspaceTypeId,
        DateTime startUtc, int durationMinutes, CancellationToken ct = default, bool includeBusy = false);

    /// <summary>Every bookable desk or cabin of a type for a monthly term, free or not, with
    /// its monthly price.</summary>
    Task<List<MonthlyWorkspaceDto>> GetMonthlyWorkspacesAsync(int locationId, int workspaceTypeId,
        DateOnly startDate, int months, CancellationToken ct = default);

    /// <summary>Advisory check (no locks) — frontend convenience only. The engine
    /// re-verifies under range locks inside the booking transaction.</summary>
    Task<AvailabilityCheckResponse> CheckAsync(AvailabilityCheckRequest request, CancellationToken ct = default);
}

public interface IBookingService
{
    /// <summary>Price + GST breakdown before payment. No write.</summary>
    Task<QuoteResponse> QuoteAsync(QuoteRequest request, CancellationToken ct = default);

    /// <summary>Creates a Pending hold under a serializable transaction with
    /// UPDLOCK/HOLDLOCK range locks (Design Invariant 2). Losing the race is a
    /// documented 409 SLOT_UNAVAILABLE.</summary>
    Task<BookingDto> CreateHoldAsync(CreateBookingRequest request, CancellationToken ct = default);

    /// <summary>Price of a desk or cabin for a monthly term. No write.</summary>
    Task<MonthlyQuoteResponse> QuoteMonthlyAsync(MonthlyBookingRequest request, CancellationToken ct = default);

    /// <summary>A Pending hold on a desk or cabin for a whole monthly term; paid and
    /// confirmed through the same confirm call as an hourly booking.</summary>
    Task<BookingDto> CreateMonthlyHoldAsync(MonthlyBookingRequest request, CancellationToken ct = default);

    /// <summary>Design Invariant 4 — one serializable transaction produces the
    /// confirmed booking, the verified payment, the GST invoice, the access token
    /// and the notification. All or nothing.</summary>
    Task<ConfirmBookingResponse> ConfirmAsync(long bookingId, ConfirmBookingRequest request,
        CancellationToken ct = default);

    /// <summary>Admin books on a customer's behalf: hold + immediate confirm
    /// (wallet or cash) in one call. BookingSource = Admin.</summary>
    Task<ConfirmBookingResponse> AdminCreateAsync(AdminCreateBookingRequest request,
        CancellationToken ct = default);

    /// <summary>Before any hold: whether the signed-in customer may pay later, and for which kinds.</summary>
    Task<Finance.PayLaterStatusDto> GetPayLaterStatusAsync(CancellationToken ct = default);

    /// <summary>Whether the caller may confirm this Pending hold with "paylater", and why not.</summary>
    Task<Finance.PayLaterEligibilityDto> GetPayLaterEligibilityAsync(long bookingId, CancellationToken ct = default);

    /// <summary>A booking scoped to the caller: customers see only their own rows.</summary>
    Task<BookingDto> GetBookingAsync(long bookingId, CancellationToken ct = default);

    /// <summary>The caller's bookings by tab: upcoming | completed | cancelled.</summary>
    Task<Shared.Api.PagedResult<BookingDto>> GetMyBookingsAsync(string tab,
        Shared.Api.PagedRequest request, CancellationToken ct = default);

    /// <summary>Admin list with the common filter set.</summary>
    Task<Shared.Api.PagedResult<BookingDto>> GetBookingsAsync(BookingListRequest request,
        CancellationToken ct = default);

    /// <summary>Bookings and revenue per month of the requested year (default: this
    /// year), under the same filters as the admin list.</summary>
    Task<BookingMonthlyDto> GetMonthlyAsync(BookingMonthlyRequest request,
        CancellationToken ct = default);
}
