using FluentValidation;

namespace Regalis.Application.Bookings;

/// <summary>One occupancy entry on the calendar. Customers receive anonymized
/// titles ("Reserved") for other customers' bookings; admins see numbers + names.</summary>
public record CalendarItemDto(
    string Kind, // "booking" | "block"
    long ReferenceId,
    DateTime StartUtc,
    DateTime EndUtc,
    string Title,
    byte? BookingStatus,
    byte? BlockReason,
    bool IsOwn);

public record CalendarWorkspaceDto(
    int WorkspaceId,
    string WorkspaceCode,
    string WorkspaceName,
    string WorkspaceTypeName,
    List<CalendarItemDto> Items);

public record CalendarResponse(
    string View,
    DateTime RangeStartUtc,
    DateTime RangeEndUtc,
    List<CalendarWorkspaceDto> Workspaces);

public record CreateBlockRequest(
    int WorkspaceId,
    DateTime StartUtc,
    DateTime EndUtc,
    byte BlockReason,
    string? ReasonNotes);

public record BlockDto(
    long WorkspaceBlockId,
    int WorkspaceId,
    string WorkspaceName,
    DateTime StartUtc,
    DateTime EndUtc,
    byte BlockReason,
    string? ReasonNotes,
    bool IsActive);

public class CreateBlockRequestValidator : AbstractValidator<CreateBlockRequest>
{
    public CreateBlockRequestValidator()
    {
        RuleFor(x => x.WorkspaceId).GreaterThan(0);
        RuleFor(x => x.EndUtc).GreaterThan(x => x.StartUtc).WithMessage("End must be after start.");
        RuleFor(x => x.BlockReason).InclusiveBetween((byte)1, (byte)7);
        RuleFor(x => x.ReasonNotes).MaximumLength(500);
    }
}

public interface ICalendarService
{
    /// <summary>The single calendar grid used by both portals (Design Invariant 1).
    /// view = day | week | month; date anchors the range (IST).</summary>
    Task<CalendarResponse> GetCalendarAsync(string view, DateOnly date, int? locationId,
        int? workspaceTypeId, int? workspaceId, CancellationToken ct = default);

    /// <summary>Creates a block — refused when a confirmed booking overlaps.</summary>
    Task<BlockDto> BlockAsync(CreateBlockRequest request, CancellationToken ct = default);

    /// <summary>Releases a block, keeping the row (never deleted).</summary>
    Task<BlockDto> UnblockAsync(long blockId, CancellationToken ct = default);
}
