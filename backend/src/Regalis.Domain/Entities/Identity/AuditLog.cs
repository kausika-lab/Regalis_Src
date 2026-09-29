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
    /// <summary>The request's correlation ID — links every row one request wrote.</summary>
    public string? CorrelationId { get; set; }
    public DateTime TimestampUtc { get; set; }
    /// <summary>One of <c>Regalis.Shared.Constants.AuditResults</c>: Success, Failed, Denied.</summary>
    public string Result { get; set; } = "Success";
    /// <summary>Why a Failed or Denied action did not happen. Never a stack trace.</summary>
    public string? FailureReason { get; set; }
    /// <summary>Comma-separated property names an UPDATE changed; drives the summary.</summary>
    public string? ChangedFields { get; set; }
}
