using System.Security.Cryptography;
using System.Text;

namespace Regalis.Infrastructure.Security;

/// <summary>Compact signed QR payload: RGL1.{token}.{hmac}. The reader posts it to
/// /access/validate, which re-verifies the signature and the token window. One
/// implementation — booking, pass and housekeeping tokens all sign here.</summary>
internal static class QrPayloadSigner
{
    internal static string Sign(string tokenValue, string signingKey)
    {
        var signature = Convert.ToBase64String(HMACSHA256.HashData(
                Encoding.UTF8.GetBytes(signingKey),
                Encoding.UTF8.GetBytes(tokenValue)))
            .TrimEnd('=').Replace('+', '-').Replace('/', '_');
        return $"RGL1.{tokenValue}.{signature}";
    }
}
