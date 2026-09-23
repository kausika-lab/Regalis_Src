namespace Regalis.Application.Abstractions;

/// <summary>
/// The verified contents of a Google ID token. Only produced after the signature,
/// issuer, audience and expiry have all been checked — never from an unverified token.
/// </summary>
/// <param name="Subject">Google's <c>sub</c> claim. Stable for the life of the Google
/// account and never reused, which is why it, not the email, is the identity we store.</param>
/// <param name="Email">Lower-cased email address from the token.</param>
/// <param name="EmailVerified">Whether Google has verified that address. An unverified
/// address must never be matched against an existing account.</param>
/// <param name="Name">Display name, when Google supplies one.</param>
public record GoogleIdentity(string Subject, string Email, bool EmailVerified, string? Name);

/// <summary>
/// Verifies a Google ID token (Google Identity Services token flow, ARCHITECTURE §4).
/// Kept behind an interface so the sign-in branches can be tested without reaching
/// Google, and so the Google SDK stays inside Infrastructure (Invariant 5).
/// </summary>
public interface IGoogleTokenVerifier
{
    /// <summary>True when a client id is configured; false disables the endpoint.</summary>
    bool IsConfigured { get; }

    /// <summary>
    /// Validates the token against Google's published keys with the audience pinned to
    /// our own client id. Returns null when the token is missing, malformed, expired,
    /// signed by someone else, or minted for a different Google application.
    /// </summary>
    Task<GoogleIdentity?> VerifyAsync(string idToken, CancellationToken ct = default);
}
