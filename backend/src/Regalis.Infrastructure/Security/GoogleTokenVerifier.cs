using Google.Apis.Auth;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Regalis.Application.Abstractions;

namespace Regalis.Infrastructure.Security;

/// <summary>
/// Verifies Google ID tokens with <see cref="GoogleJsonWebSignature"/>, which checks the
/// signature, issuer, audience and expiry against Google's published JWKS and caches the
/// keys between calls.
///
/// The token is never decoded by hand. An ID token that has not been verified is just
/// attacker-controlled JSON, and trusting its `sub` or `email` would hand over any
/// account named in it.
/// </summary>
public class GoogleTokenVerifier(
    IOptions<GoogleOptions> options,
    ILogger<GoogleTokenVerifier> logger) : IGoogleTokenVerifier
{
    private readonly string _clientId = options.Value.ClientId?.Trim() ?? string.Empty;

    public bool IsConfigured => !string.IsNullOrWhiteSpace(_clientId);

    public async Task<GoogleIdentity?> VerifyAsync(string idToken, CancellationToken ct = default)
    {
        if (!IsConfigured || string.IsNullOrWhiteSpace(idToken))
        {
            return null;
        }

        try
        {
            // Audience pinned to our own client id: a token issued for another Google
            // application is rejected here rather than accepted as one of ours.
            var payload = await GoogleJsonWebSignature.ValidateAsync(
                idToken,
                new GoogleJsonWebSignature.ValidationSettings
                {
                    Audience = [_clientId]
                });

            if (string.IsNullOrWhiteSpace(payload.Subject) || string.IsNullOrWhiteSpace(payload.Email))
            {
                return null;
            }

            return new GoogleIdentity(
                payload.Subject,
                payload.Email.Trim().ToLowerInvariant(),
                payload.EmailVerified,
                string.IsNullOrWhiteSpace(payload.Name) ? null : payload.Name.Trim());
        }
        catch (InvalidJwtException ex)
        {
            // Expected for an expired, tampered or wrong-audience token. Logged at
            // information level so a stream of them is visible without being alarming.
            logger.LogInformation("Google ID token rejected: {Reason}", ex.Message);
            return null;
        }
        catch (Exception ex)
        {
            // Reaching Google's JWKS endpoint can fail. That is an outage, not a bad
            // token, so it is logged as an error — but the caller still sees "could not
            // be verified" rather than being signed in.
            logger.LogError(ex, "Google ID token could not be validated.");
            return null;
        }
    }
}
