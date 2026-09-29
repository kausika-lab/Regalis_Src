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

// ── Announcements ───────────────────────────────────────────────────────────

/// <summary>One announcement, as the Announcements tab and its View screen show it.</summary>
public record AnnouncementDto(
    long AnnouncementId,
    string Title,
    string Message,
    // 1 AllCustomers, 2 CompaniesOnly, 3 IndividualMembers, 4 OneCustomer.
    byte Audience,
    long? TargetCustomerId,
    string? TargetCustomerName,
    bool SendEmail,
    bool SendWhatsApp,
    // 1 Draft, 2 Scheduled, 3 Sent.
    byte Status,
    DateTime? ScheduledForUtc,
    DateTime? SentUtc,
    string? SentByName,
    int RecipientCount,
    int ReadCount,
    int EmailSentCount,
    int WhatsAppSentCount,
    // WhatsApp was chosen but no provider is configured: it did NOT go out.
    bool WhatsAppManualShare,
    // Email / WhatsApp copies not handled yet (the worker sends them).
    bool ChannelsPending,
    string? DeliveryNote,
    DateTime CreatedUtc);

public class AnnouncementListRequest : PagedRequest
{
    public byte? Status { get; set; }
    public byte? Audience { get; set; }
    /// <summary>IST dates, inclusive, on when it was sent / is scheduled / was created.</summary>
    public DateOnly? From { get; set; }
    public DateOnly? To { get; set; }
}

/// <summary>What the form saves. Action: "draft" keeps it editable, "send" sends it now,
/// "schedule" sends it at ScheduledForUtc.</summary>
public record SaveAnnouncementRequest(
    string Title,
    string Message,
    byte Audience,
    long? TargetCustomerId,
    bool SendEmail,
    bool SendWhatsApp,
    DateTime? ScheduledForUtc,
    string Action);

public class SaveAnnouncementRequestValidator : AbstractValidator<SaveAnnouncementRequest>
{
    public SaveAnnouncementRequestValidator()
    {
        RuleFor(x => x.Title).NotEmpty().WithMessage("Give the announcement a title.").MaximumLength(200);
        RuleFor(x => x.Message).NotEmpty().WithMessage("Write the message customers will see.")
            .MaximumLength(500).WithMessage("Keep the message to 500 characters.");
        RuleFor(x => x.Audience).InclusiveBetween((byte)1, (byte)4).WithMessage("Pick who it goes to.");
        RuleFor(x => x.TargetCustomerId).NotNull().GreaterThan(0)
            .When(x => x.Audience == 4).WithMessage("Pick the customer.");
        RuleFor(x => x.Action).Must(a => a is "draft" or "send" or "schedule")
            .WithMessage("Action must be draft, send or schedule.");
        RuleFor(x => x.ScheduledForUtc).NotNull().When(x => x.Action == "schedule")
            .WithMessage("Pick when to send it.");
    }
}

/// <summary>Which extra channels can really deliver on this server, so the form can say
/// so before sending instead of failing silently.</summary>
public record AnnouncementChannelsDto(bool EmailConfigured, bool WhatsAppConfigured);

public interface IAnnouncementService
{
    Task<PagedResult<AnnouncementDto>> ListAsync(AnnouncementListRequest request, CancellationToken ct = default);
    Task<Dictionary<byte, int>> StatusCountsAsync(CancellationToken ct = default);
    Task<AnnouncementDto> GetAsync(long announcementId, CancellationToken ct = default);
    Task<AnnouncementDto> CreateAsync(SaveAnnouncementRequest request, CancellationToken ct = default);
    /// <summary>Drafts and scheduled announcements only: a sent one is never edited.</summary>
    Task<AnnouncementDto> UpdateAsync(long announcementId, SaveAnnouncementRequest request, CancellationToken ct = default);
    /// <summary>Drafts and scheduled announcements only.</summary>
    Task DeleteAsync(long announcementId, CancellationToken ct = default);
    Task<int> AudienceCountAsync(byte audience, long? customerId, CancellationToken ct = default);
    AnnouncementChannelsDto Channels();
}
