namespace Regalis.Domain.Entities.Identity;

/// <summary>TRN_AuditLog — append-only; no update or delete path exists.</summary>
public class AuditLog
{
    public long AuditLogId { get; set; }
    public long? UserId { get; set; }
    public string? UserName { get; set; }
    /// <summary>One of <c>Regalis.Shared.Constants.AuditActions</c>.</summary>
    public string Action { get; set; } = string.Empty;
    public string ModuleName { get; set; } = string.Empty;
    public string? EntityName { get; set; }
    public string? EntityId { get; set; }
    public string? OldValue { get; set; }
    public string? NewValue { get; set; }
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
    public string? CorrelationId { get; set; }
    public DateTime TimestampUtc { get; set; }
}
