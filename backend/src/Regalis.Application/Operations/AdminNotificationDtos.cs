using FluentValidation;
using Regalis.Shared.Api;

namespace Regalis.Application.Operations;

/// <summary>One row of the admin Notifications feed, with this admin's read state.</summary>
public record AdminNotificationDto(
    long AdminNotificationId,
    // 1 Customer, 2 Booking, 3 Payment, 4 Wallet, 5 Support, 6 AccountChange, 7 Maintenance, 8 Purchase.
    byte Type,
    string EventCode,
    // 1 Info, 2 ActionNeeded.
    byte Severity,
    string Title,
    string Body,
    long? CustomerId,
    string? CustomerName,
    string? ReferenceType,
    long? ReferenceId,
    string? ReferenceLabel,
    string? ActionLabel,
    string? ActionRoute,
    DateTime CreatedUtc,
    bool IsRead,
    // ActionNeeded and the record it is about is not resolved yet.
    bool NeedsAction);

/// <summary>The four summary cards.</summary>
public record AdminNotificationSummaryDto(
    int Unread,
    int Today,
    int TodayTypes,
    int NeedsAction,
    int AnnouncementsSent,
    DateTime? LastAnnouncementUtc);

/// <summary>The feed page plus the per-type chip counts (under every filter except the
/// type itself) and the summary cards.</summary>
public record AdminNotificationFeedDto(
    PagedResult<AdminNotificationDto> Page,
    Dictionary<byte, int> TypeCounts,
    int AllCount,
    int UnreadCount,
    AdminNotificationSummaryDto Summary);

public class AdminNotificationListRequest : PagedRequest
{
    public byte? Type { get; set; }
    /// <summary>"unread" or "read"; anything else is all.</summary>
    public string? State { get; set; }
    public long? CustomerId { get; set; }
    /// <summary>IST dates, inclusive. With no From, the feed shows the last 90 days.</summary>
    public DateOnly? From { get; set; }
    public DateOnly? To { get; set; }
    /// <summary>Only items that still need action (the "Needs action" card).</summary>
    public bool? NeedsAction { get; set; }
}

/// <summary>One row of the Notification settings screen. In-app is always on.</summary>
public record AdminNotificationSettingDto(
    byte Type,
    string Label,
    string Description,
    bool InApp,
    bool Email,
    bool WhatsApp);

public record AdminNotificationSettingItem(byte Type, bool Email, bool WhatsApp);

public record SaveAdminNotificationSettingsRequest(List<AdminNotificationSettingItem> Items);

public class SaveAdminNotificationSettingsRequestValidator : AbstractValidator<SaveAdminNotificationSettingsRequest>
{
    public SaveAdminNotificationSettingsRequestValidator()
    {
        RuleFor(x => x.Items).NotNull();
        RuleForEach(x => x.Items).Must(i => i.Type is >= 1 and <= 8)
            .WithMessage("Unknown notification type.");
    }
}

public interface IAdminNotificationService
{
    Task<AdminNotificationFeedDto> GetFeedAsync(AdminNotificationListRequest request, CancellationToken ct = default);
    Task<int> GetUnreadCountAsync(CancellationToken ct = default);
    Task MarkReadAsync(long adminNotificationId, CancellationToken ct = default);
    Task<int> MarkAllReadAsync(CancellationToken ct = default);
    Task<List<AdminNotificationSettingDto>> GetSettingsAsync(CancellationToken ct = default);
    Task<List<AdminNotificationSettingDto>> SaveSettingsAsync(SaveAdminNotificationSettingsRequest request,
        CancellationToken ct = default);
}
