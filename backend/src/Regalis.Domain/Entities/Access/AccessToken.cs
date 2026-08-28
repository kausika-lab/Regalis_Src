using Regalis.Domain.Common;
using Regalis.Domain.Enums;

namespace Regalis.Domain.Entities.Access;

/// <summary>TRN_AccessToken. TokenValue is cryptographically random and opaque —
/// never a guessable booking id.</summary>
public class AccessToken : AuditableEntity
{
    public long AccessTokenId { get; set; }
    public string TokenValue { get; set; } = string.Empty;
    /// <summary>Signed compact payload rendered as the QR.</summary>
    public string QrPayload { get; set; } = string.Empty;
    public long UserId { get; set; }
    public long? BookingId { get; set; }
    public long? CustomerPassId { get; set; }
    public long? HousekeepingScheduleId { get; set; }
    public int? WorkspaceId { get; set; }
    public int LocationId { get; set; }
    public AccessTokenPurpose TokenPurpose { get; set; }
    public DateTime ValidFromUtc { get; set; }
    public DateTime ValidToUtc { get; set; }
    public AccessTokenStatus Status { get; set; } = AccessTokenStatus.Active;
    public int UsageCount { get; set; }
    /// <summary>Null = unlimited within the validity window.</summary>
    public int? MaxUsageCount { get; set; }
    public DateTime? FirstUsedUtc { get; set; }
    public DateTime? LastUsedUtc { get; set; }
    public string? RevokedReason { get; set; }
}
