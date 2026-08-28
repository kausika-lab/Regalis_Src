namespace Regalis.Application.Abstractions;

/// <summary>PBKDF2-HMAC-SHA256 password hashing behind an interface (ARCHITECTURE §4).</summary>
public interface IPasswordHasher
{
    /// <summary>Returns a single versioned string: version.iterations.salt.subkey.</summary>
    string Hash(string password);

    /// <summary>Constant-time verification.</summary>
    bool Verify(string password, string storedHash);
}
