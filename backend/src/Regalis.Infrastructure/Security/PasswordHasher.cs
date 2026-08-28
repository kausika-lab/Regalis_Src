using System.Security.Cryptography;
using Microsoft.AspNetCore.Cryptography.KeyDerivation;
using Regalis.Application.Abstractions;

namespace Regalis.Infrastructure.Security;

/// <summary>
/// PBKDF2-HMAC-SHA256, 210 000 iterations, 128-bit salt, 256-bit subkey, stored as a
/// single versioned string "v1.{iterations}.{salt}.{subkey}" so parameters can be
/// raised later without invalidating existing hashes (ARCHITECTURE §4).
/// </summary>
public class PasswordHasher : IPasswordHasher
{
    private const string Version = "v1";
    private const int Iterations = 210_000;
    private const int SaltSizeBytes = 16;
    private const int SubkeySizeBytes = 32;

    public string Hash(string password)
    {
        ArgumentException.ThrowIfNullOrEmpty(password);

        var salt = RandomNumberGenerator.GetBytes(SaltSizeBytes);
        var subkey = Derive(password, salt, Iterations);
        return $"{Version}.{Iterations}.{Convert.ToBase64String(salt)}.{Convert.ToBase64String(subkey)}";
    }

    public bool Verify(string password, string storedHash)
    {
        if (string.IsNullOrEmpty(password) || string.IsNullOrEmpty(storedHash))
        {
            return false;
        }

        var parts = storedHash.Split('.');
        if (parts.Length != 4 || parts[0] != Version || !int.TryParse(parts[1], out var iterations))
        {
            return false;
        }

        byte[] salt;
        byte[] expectedSubkey;
        try
        {
            salt = Convert.FromBase64String(parts[2]);
            expectedSubkey = Convert.FromBase64String(parts[3]);
        }
        catch (FormatException)
        {
            return false;
        }

        var actualSubkey = Derive(password, salt, iterations);
        return CryptographicOperations.FixedTimeEquals(actualSubkey, expectedSubkey);
    }

    private static byte[] Derive(string password, byte[] salt, int iterations) =>
        KeyDerivation.Pbkdf2(password, salt, KeyDerivationPrf.HMACSHA256, iterations, SubkeySizeBytes);
}
