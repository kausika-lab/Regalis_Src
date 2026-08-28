namespace Regalis.Infrastructure.Security;

/// <summary>Bound from the "Jwt" configuration section. The signing key comes from
/// environment variables / user-secrets — never from a committed file.</summary>
public class JwtOptions
{
    public const string SectionName = "Jwt";

    public string Issuer { get; set; } = string.Empty;
    public string Audience { get; set; } = string.Empty;
    /// <summary>Minimum 32 bytes. Empty in committed appsettings.</summary>
    public string SigningKey { get; set; } = string.Empty;
    public int AccessTokenMinutes { get; set; } = 15;
    public int RefreshTokenDays { get; set; } = 7;
}
