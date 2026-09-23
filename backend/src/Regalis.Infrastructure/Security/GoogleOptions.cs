namespace Regalis.Infrastructure.Security;

/// <summary>
/// Bound from the "Authentication:Google" configuration section.
///
/// The client id is public — it ships inside the SPA bundle and Google expects it to be
/// visible. It is configuration rather than a secret, and it is pinned as the audience
/// when verifying an ID token so a token minted for a different Google application
/// cannot be replayed against this API.
///
/// No client secret appears here: the token flow never needs one server-side. Only the
/// authorization-code redirect flow would, and this API does not implement it.
/// </summary>
public class GoogleOptions
{
    public const string SectionName = "Authentication:Google";

    /// <summary>Empty disables Google sign-in; the endpoint then refuses cleanly.</summary>
    public string ClientId { get; set; } = string.Empty;
}
