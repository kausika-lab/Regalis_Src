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
    IReadOnlyList<string> Amenities);

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
    DateTime? HoldExpiresUtc);

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

    /// <summary>Free workspaces for a window, with price — the customer picks one.</summary>
    Task<List<AvailableWorkspaceDto>> GetAvailableWorkspacesAsync(int locationId, int workspaceTypeId,
        DateTime startUtc, int durationMinutes, CancellationToken ct = default);

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

    /// <summary>Design Invariant 4 — one serializable transaction produces the
    /// confirmed booking, the verified payment, the GST invoice, the access token
    /// and the notification. All or nothing.</summary>
    Task<ConfirmBookingResponse> ConfirmAsync(long bookingId, ConfirmBookingRequest request,
        CancellationToken ct = default);

    /// <summary>Admin books on a customer's behalf: hold + immediate confirm
    /// (wallet or cash) in one call. BookingSource = Admin.</summary>
    Task<ConfirmBookingResponse> AdminCreateAsync(AdminCreateBookingRequest request,
        CancellationToken ct = default);

    /// <summary>A booking scoped to the caller: customers see only their own rows.</summary>
    Task<BookingDto> GetBookingAsync(long bookingId, CancellationToken ct = default);

    /// <summary>The caller's bookings by tab: upcoming | completed | cancelled.</summary>
    Task<Shared.Api.PagedResult<BookingDto>> GetMyBookingsAsync(string tab,
        Shared.Api.PagedRequest request, CancellationToken ct = default);

    /// <summary>Admin list with the common filter set.</summary>
    Task<Shared.Api.PagedResult<BookingDto>> GetBookingsAsync(BookingListRequest request,
        CancellationToken ct = default);
}
