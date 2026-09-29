using FluentValidation;
using Regalis.Shared.Api;

namespace Regalis.Application.Operations;

public record NotificationDto(
    long NotificationId,
    string Title,
    string Message,
    byte Category,
    byte Priority,
    string? ActionLabel,
    string? ActionRoute,
    bool IsRead,
    DateTime? ReadUtc,
    DateTime CreatedDate,
    // "Booking", "Ticket" or "User"; with ReferenceId it names the record the alert is about.
    string? ReferenceType = null,
    long? ReferenceId = null);

public class NotificationListRequest : PagedRequest
{
    public byte? Category { get; set; }
    public bool? IsRead { get; set; }
}

public record BroadcastNotificationRequest(
    // 2 AllCustomers, 3 AllAdmins.
    byte Audience,
    string Title,
    string Message,
    byte Category,
    string? ActionLabel,
    string? ActionRoute);

public class BroadcastNotificationRequestValidator : AbstractValidator<BroadcastNotificationRequest>
{
    public BroadcastNotificationRequestValidator()
    {
        RuleFor(x => x.Audience).Must(a => a is 2 or 3)
            .WithMessage("Audience must be 2 (all customers) or 3 (all admins).");
        RuleFor(x => x.Title).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Message).NotEmpty().MaximumLength(2000);
        RuleFor(x => x.Category).InclusiveBetween((byte)1, (byte)13);
        RuleFor(x => x.ActionLabel).MaximumLength(60);
        RuleFor(x => x.ActionRoute).MaximumLength(200);
    }
}

public interface INotificationService
{
    Task<PagedResult<NotificationDto>> GetMyAsync(NotificationListRequest request, CancellationToken ct = default);
    Task<int> GetMyUnreadCountAsync(CancellationToken ct = default);
    Task MarkReadAsync(long notificationId, CancellationToken ct = default);
    Task MarkAllReadAsync(CancellationToken ct = default);
    Task DeleteAsync(long notificationId, CancellationToken ct = default);
    /// <summary>Fans a broadcast out to one row per recipient so read-state stays personal.</summary>
    Task<int> BroadcastAsync(BroadcastNotificationRequest request, CancellationToken ct = default);
}
