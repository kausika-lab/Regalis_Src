namespace Regalis.Domain.Entities.Identity;

/// <summary>TRN_LoginHistory — every attempt, successful or not.</summary>
public class LoginHistory
{
    public long LoginHistoryId { get; set; }
    public long? UserId { get; set; }
    public string AttemptedEmail { get; set; } = string.Empty;
    public bool IsSuccess { get; set; }
    public string? FailureReason { get; set; }
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
    public DateTime AttemptedUtc { get; set; }
}
