namespace Regalis.Infrastructure.Services;

/// <summary>Leading-magic-byte checks for user uploads — a renamed executable never
/// gets stored, whatever its extension claims (used by tickets and profile photos).</summary>
internal static class FileTypeSniffer
{
    internal static bool Matches(string extension, byte[] bytes)
    {
        if (bytes.Length < 12)
        {
            return false;
        }
        return extension.ToLowerInvariant() switch
        {
            ".png" => bytes is [0x89, 0x50, 0x4E, 0x47, ..],
            ".jpg" or ".jpeg" => bytes is [0xFF, 0xD8, 0xFF, ..],
            ".webp" => bytes is [0x52, 0x49, 0x46, 0x46, _, _, _, _, 0x57, 0x45, 0x42, 0x50, ..],
            ".pdf" => bytes is [0x25, 0x50, 0x44, 0x46, ..],
            _ => false
        };
    }
}
