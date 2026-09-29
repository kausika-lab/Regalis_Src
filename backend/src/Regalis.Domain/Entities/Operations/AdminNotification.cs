using Regalis.Domain.Common;
using Regalis.Domain.Enums;

namespace Regalis.Domain.Entities.Operations;

/// <summary>
/// TRN_AdminNotification — one row per event in the space, shown on the admin
/// Notifications page. Read state lives in <see cref="AdminNotificationRead"/>, one row
/// per admin, so one admin reading an item never clears it for the others. Rows are
/// never deleted: the page hides anything older than 90 days from its default view.
/// </summary>
public class AdminNotification
{
    public long AdminNotificationId { get; set; }
    public AdminNotificationType Type { get; set; }
    /// <summary>"customer.registered", "payment.failed", … — the specific event within
    /// the type; new events need no migration.</summary>
    public string EventCode { get; set; } = string.Empty;
    public AdminNotificationSeverity Severity { get; set; } = AdminNotificationSeverity.Info;
    public string Title { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    /// <summary>The customer the event is about, when there is one.</summary>
    public long? CustomerId { get; set; }
    /// <summary>Booking, Payment, Invoice, WalletTransaction, Ticket, MaintenanceWork,
    /// PurchaseOrder or User — with ReferenceId, the record to deep-link to.</summary>
    public string? ReferenceType { get; set; }
    public long? ReferenceId { get; set; }
    /// <summary>"Booking REG-000052" — shown under the body.</summary>
    public string? ReferenceLabel { get; set; }
    public string? ActionLabel { get; set; }
    public string? ActionRoute { get; set; }
    public DateTime CreatedUtc { get; set; }
    /// <summary>Set once the email / WhatsApp copies (if any admin wants them) are done.</summary>
    public DateTime? DeliveredUtc { get; set; }
    /// <summary>What delivery did, e.g. "Email to 2 admins; WhatsApp: manual share".</summary>
    public string? DeliveryNote { get; set; }

    public ICollection<AdminNotificationRead> Reads { get; set; } = new List<AdminNotificationRead>();
}

/// <summary>TRN_AdminNotificationRead — one admin has read one notification.</summary>
public class AdminNotificationRead
{
    public long AdminNotificationId { get; set; }
    public long UserId { get; set; }
    public DateTime ReadUtc { get; set; }

    public AdminNotification Notification { get; set; } = null!;
}

/// <summary>MST_AdminNotificationSetting — one admin's extra channels for one type.
/// In-app is always on and not stored. No row means the defaults apply.</summary>
public class AdminNotificationSetting
{
    public long UserId { get; set; }
    public AdminNotificationType Type { get; set; }
    public bool Email { get; set; }
    public bool WhatsApp { get; set; }
    public DateTime ModifiedUtc { get; set; }
}

/// <summary>TRN_Announcement — what the admin sent (or will send) to customers. Each
/// recipient's copy is a TRN_Notification with ReferenceType "Announcement", whose read
/// state gives the read count. A sent announcement is never edited or unsent.</summary>
public class Announcement : AuditableEntity
{
    public long AnnouncementId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public AnnouncementAudience Audience { get; set; } = AnnouncementAudience.AllCustomers;
    /// <summary>For <see cref="AnnouncementAudience.OneCustomer"/>.</summary>
    public long? TargetCustomerId { get; set; }
    public bool SendEmail { get; set; }
    public bool SendWhatsApp { get; set; }
    public AnnouncementStatus Status { get; set; } = AnnouncementStatus.Draft;
    public DateTime? ScheduledForUtc { get; set; }
    public DateTime? SentUtc { get; set; }
    public long? SentByUserId { get; set; }
    public int RecipientCount { get; set; }
    public int EmailSentCount { get; set; }
    public int WhatsAppSentCount { get; set; }
    /// <summary>No WhatsApp provider was configured when it was sent, so the WhatsApp
    /// copies were NOT delivered — the admin shares the message by hand.</summary>
    public bool WhatsAppManualShare { get; set; }
    /// <summary>Set once the email / WhatsApp copies have been handled (at once when
    /// only in-app was chosen). Until then the worker picks it up.</summary>
    public DateTime? ChannelsDeliveredUtc { get; set; }
    /// <summary>What the channels did, e.g. "Email NOT sent: no mail server configured".</summary>
    public string? DeliveryNote { get; set; }
}
