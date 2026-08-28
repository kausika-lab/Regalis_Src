namespace Regalis.Domain.Entities.Identity;

/// <summary>TRN_PasswordHistory — the dedicated table backing the "not equal to the
/// last 3 hashes" password rule (ARCHITECTURE §4). Append-only.</summary>
public class PasswordHistory
{
    public long PasswordHistoryId { get; set; }
    public long UserId { get; set; }
    public string PasswordHash { get; set; } = string.Empty;
    public DateTime ChangedUtc { get; set; }

    public User User { get; set; } = null!;
}
