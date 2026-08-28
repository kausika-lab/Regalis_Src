using Regalis.Domain.Enums;

namespace Regalis.Domain.Entities.Access;

/// <summary>TRN_AccessLog — append-only; records every attempt, granted or denied.
/// Invariant 9: a DeviceError result never implies a grant. Only an explicit override
/// with ACCESS.OVERRIDE produces Granted + IsManualOverride.</summary>
public class AccessLog
{
    public long AccessLogId { get; set; }
    public long? AccessTokenId { get; set; }
    public long? UserId { get; set; }
    /// <summary>What was actually presented, even if invalid.</summary>
    public string? ScannedTokenValue { get; set; }
    public int? WorkspaceId { get; set; }
    public int LocationId { get; set; }
    public AccessDirection Direction { get; set; }
    public AccessMethod AccessMethod { get; set; }
    public AccessResult Result { get; set; }
    /// <summary>TokenNotFound, TokenRevoked, OutsideValidWindow, BookingCancelled,
    /// WrongWorkspace, AlreadyCheckedOut, DeviceOffline.</summary>
    public string? DenyReason { get; set; }
    public string? DeviceId { get; set; }
    public string? DeviceStatus { get; set; }
    public bool IsManualOverride { get; set; }
    public long? OverrideByUserId { get; set; }
    public string? OverrideReason { get; set; }
    public string? IpAddress { get; set; }
    public DateTime LoggedUtc { get; set; }
}
