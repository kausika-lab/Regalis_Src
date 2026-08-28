using Regalis.Domain.Common;
using Regalis.Domain.Enums;

namespace Regalis.Domain.Entities.Operations;

/// <summary>TRN_Notification — the in-app notification centre.</summary>
public class Notification : AuditableEntity
{
    public long NotificationId { get; set; }
    /// <summary>Null ⇒ broadcast.</summary>
    public long? UserId { get; set; }
    public NotificationAudience Audience { get; set; } = NotificationAudience.SingleUser;
    public long? AudienceRefId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public NotificationCategory Category { get; set; }
    public NotificationPriority Priority { get; set; } = NotificationPriority.Normal;
    public string? ReferenceType { get; set; }
    public long? ReferenceId { get; set; }
    public string? ActionLabel { get; set; }
    public string? ActionRoute { get; set; }
    public bool IsRead { get; set; }
    public DateTime? ReadUtc { get; set; }
    public DateTime? ScheduledForUtc { get; set; }
    public DateTime? SentUtc { get; set; }
    public long? CreatedByUserId { get; set; }
}
