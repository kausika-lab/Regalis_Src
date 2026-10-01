using QuestPDF.Fluent;
using Regalis.Application.Abstractions;

namespace Regalis.Infrastructure.Reporting;

/// <summary>The company logo on every PDF letterhead: a PNG or JPG file named by the
/// Company.LogoPath setting. No setting, a missing file or another format means no
/// logo, and the letterhead prints the company name alone.</summary>
public static class PdfLogo
{
    public static async Task<byte[]?> LoadAsync(ISettingService settings, CancellationToken ct = default)
    {
        var path = await settings.GetStringAsync("Company.LogoPath", "", ct);
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)
            || Path.GetExtension(path).ToLowerInvariant() is not (".png" or ".jpg" or ".jpeg"))
        {
            return null;
        }
        return await File.ReadAllBytesAsync(path, ct);
    }

    /// <summary>Puts the logo at the start of a letterhead row, with a gap after it.</summary>
    public static void Compose(RowDescriptor row, byte[]? logo)
    {
        if (logo is not { Length: > 0 }) return;
        row.ConstantItem(46).Height(46).Image(logo).FitArea();
        row.ConstantItem(10);
    }
}
