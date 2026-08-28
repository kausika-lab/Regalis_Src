namespace Regalis.Domain.Entities.Identity;

/// <summary>TRN_RefreshToken — stored hashed, single-use, rotated on every refresh.
/// Reuse of a consumed token revokes the whole family (token-theft detection).</summary>
public class RefreshToken
{
    public long RefreshTokenId { get; set; }
    public long UserId { get; set; }
    /// <summary>SHA-256 of the raw token; the raw value is never stored.</summary>
    public string TokenHash { get; set; } = string.Empty;
    public Guid FamilyId { get; set; }
    public DateTime ExpiresUtc { get; set; }
    public DateTime CreatedUtc { get; set; }
    public string? CreatedByIp { get; set; }
    public DateTime? ConsumedUtc { get; set; }
    public DateTime? RevokedUtc { get; set; }
    public string? RevokedReason { get; set; }
    public string? ReplacedByTokenHash { get; set; }

    public User User { get; set; } = null!;

    public bool IsExpired(DateTime nowUtc) => nowUtc >= ExpiresUtc;
    public bool IsUsable(DateTime nowUtc) => ConsumedUtc is null && RevokedUtc is null && !IsExpired(nowUtc);
}
