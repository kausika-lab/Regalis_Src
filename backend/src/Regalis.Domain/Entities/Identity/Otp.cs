using Regalis.Domain.Enums;

namespace Regalis.Domain.Entities.Identity;

/// <summary>TRN_Otp — 6 digits, hashed at rest, 10-minute TTL, max 5 verification attempts.</summary>
public class Otp
{
    public long OtpId { get; set; }
    public long? UserId { get; set; }
    /// <summary>Email address or mobile number the code was sent to.</summary>
    public string Destination { get; set; } = string.Empty;
    public OtpChannel Channel { get; set; }
    public OtpPurpose Purpose { get; set; }
    public string CodeHash { get; set; } = string.Empty;
    public DateTime ExpiresUtc { get; set; }
    public int AttemptCount { get; set; }
    public int MaxAttempts { get; set; }
    public DateTime? ConsumedUtc { get; set; }
    public DateTime CreatedUtc { get; set; }
}
