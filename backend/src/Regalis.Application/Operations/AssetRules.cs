using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Regalis.Domain.Enums;

namespace Regalis.Application.Operations;

/// <summary>The Assets rules that are pure arithmetic or text, kept apart so they can be
/// unit-tested and shared by the service, the import and the PO receive path.</summary>
public static partial class AssetRules
{
    /// <summary>"Never below 5% of cost while the asset is in use" (§11.3).</summary>
    public const decimal BookValueFloor = 0.05m;

    /// <summary>
    /// Straight-line book value (§11.3): cost × (1 − age ÷ useful life), age in years =
    /// days ÷ 365, never below 5% of cost while the asset is still in the register.
    /// Scrapped, lost and returned assets are worth 0. No cost → null (shown as a dash,
    /// never a zero). No useful life (a consumable category) or no purchase date → the
    /// cost itself, undepreciated.
    /// </summary>
    public static decimal? BookValue(decimal? cost, DateOnly? purchaseDate, decimal? usefulLifeYears,
        AssetStatus status, DateOnly today)
    {
        if (cost is null) return null;
        if (IsRetired(status)) return 0m;
        if (usefulLifeYears is not > 0 || purchaseDate is null) return cost;
        var age = AgeYears(purchaseDate.Value, today);
        var value = cost.Value * (1m - age / usefulLifeYears.Value);
        var floor = cost.Value * BookValueFloor;
        return Math.Round(Math.Max(value, floor), 2, MidpointRounding.AwayFromZero);
    }

    public static decimal AgeYears(DateOnly purchaseDate, DateOnly today) =>
        Math.Max(0, today.DayNumber - purchaseDate.DayNumber) / 365m;

    public static bool IsRetired(AssetStatus status) =>
        status is AssetStatus.Scrapped or AssetStatus.Lost or AssetStatus.ReturnedToSupplier;

    /// <summary>R-2W-E-EAC + 1 → R-2W-E-EAC-001. Three digits, more when the prefix
    /// passes 999 — a number is never reused.</summary>
    public static string AssetCode(string prefix, int number) =>
        $"{prefix}-{number.ToString("000", CultureInfo.InvariantCulture)}";

    /// <summary>IN-2026 + 7 → IN-2026-0007.</summary>
    public static string DocumentNumber(string prefix, int number) =>
        $"{prefix}-{number.ToString("0000", CultureInfo.InvariantCulture)}";

    /// <summary>"R-2W-E-EGC-015 to -022" — the way the receipt form and the import say it.</summary>
    public static string CodeRange(IReadOnlyList<string> codes)
    {
        if (codes.Count == 0) return "";
        if (codes.Count == 1) return codes[0];
        var last = codes[^1];
        var dash = last.LastIndexOf('-');
        return $"{codes[0]} to {(dash >= 0 ? last[dash..] : last)}";
    }

    /// <summary>
    /// The prefix as Regalis writes it, tidied the way the import needs: trimmed (the
    /// sheet's "R-2W-E-ES " must not become a second prefix), upper case, inner spaces
    /// removed, no trailing hyphen. Null when nothing usable is left.
    /// </summary>
    public static string? NormalisePrefix(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        var cleaned = WhitespaceRegex().Replace(raw.Trim().ToUpperInvariant(), "").TrimEnd('-');
        return cleaned.Length == 0 ? null : cleaned;
    }

    /// <summary>Letters, digits, hyphens and slashes; starts with a letter or digit.</summary>
    public static bool IsValidPrefix(string prefix) =>
        prefix.Length is > 0 and <= 30 && PrefixRegex().IsMatch(prefix);

    /// <summary>
    /// The plain text the QR code carries (§9.1), one field per line, ASCII only. Not a
    /// URL: any phone camera reads it with no app, no internet and no login. Brand and
    /// Bought are skipped when they are not known.
    /// </summary>
    public static string QrText(string code, string itemName, string? brand, string? where,
        DateOnly? bought, string ownerName, string contactEmail)
    {
        var lines = new List<string>
        {
            "REGALIS WS ASSET",
            "Code: " + code,
            "Item: " + itemName
        };
        if (!string.IsNullOrWhiteSpace(brand)) lines.Add("Brand: " + brand.Trim());
        lines.Add("Where: " + (string.IsNullOrWhiteSpace(where) ? "-" : where.Trim()));
        if (bought is { } date) lines.Add("Bought: " + date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
        lines.Add("Property of " + ownerName);
        lines.Add("If found, contact " + contactEmail);
        return Ascii(string.Join("\n", lines));
    }

    /// <summary>The code inside a scanned sticker ("Code: R-2W-E-EAC-001" line), or the
    /// text itself when it is a bare code typed or scanned by a USB reader.</summary>
    public static string? CodeFromScan(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        foreach (var raw in text.Split('\n'))
        {
            var line = raw.Trim();
            if (line.StartsWith("Code:", StringComparison.OrdinalIgnoreCase))
            {
                var value = line[5..].Trim();
                return value.Length == 0 ? null : value.ToUpperInvariant();
            }
        }
        var single = text.Trim();
        return single.Contains('\n') ? null : single.ToUpperInvariant();
    }

    /// <summary>
    /// ASCII only, so every scanner shows the same text: long dashes become "-", the
    /// rupee sign "Rs.", smart quotes plain ones, accents are dropped and anything else
    /// outside ASCII becomes "?".
    /// </summary>
    public static string Ascii(string text)
    {
        var replaced = text
            .Replace('–', '-').Replace('—', '-').Replace('−', '-')
            .Replace("₹", "Rs.").Replace('×', 'x')
            .Replace('‘', '\'').Replace('’', '\'')
            .Replace('“', '"').Replace('”', '"')
            .Replace(' ', ' ');
        var decomposed = replaced.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposed.Length);
        foreach (var ch in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(ch) == UnicodeCategory.NonSpacingMark) continue;
            builder.Append(ch is '\n' || (ch >= ' ' && ch <= '~') ? ch : '?');
        }
        return builder.ToString();
    }

    /// <summary>An asset received or moved into a store room is In store; anywhere else
    /// it is In use.</summary>
    public static AssetStatus StatusForRoom(bool isStore) => isStore ? AssetStatus.InStore : AssetStatus.InUse;

    public static string StatusName(AssetStatus status) => status switch
    {
        AssetStatus.InStore => "In store",
        AssetStatus.InUse => "In use",
        AssetStatus.InRepair => "In repair",
        AssetStatus.Scrapped => "Scrapped",
        AssetStatus.Lost => "Lost",
        AssetStatus.ReturnedToSupplier => "Returned to supplier",
        _ => status.ToString()
    };

    public static string ConditionName(AssetCondition condition) => condition switch
    {
        AssetCondition.Good => "Good",
        AssetCondition.Fair => "Fair",
        AssetCondition.NeedsAttention => "Needs attention",
        AssetCondition.Damaged => "Damaged",
        _ => condition.ToString()
    };

    public static string ReasonName(GoodsIssueReason reason) => reason switch
    {
        GoodsIssueReason.UsedUp => "Used up",
        GoodsIssueReason.Moved => "Moved",
        GoodsIssueReason.SentForRepair => "Sent for repair",
        GoodsIssueReason.ReturnedToSupplier => "Returned to supplier",
        GoodsIssueReason.Scrapped => "Scrapped",
        GoodsIssueReason.Lost => "Lost",
        GoodsIssueReason.Reversal => "Reversal",
        _ => reason.ToString()
    };

    /// <summary>Good / Fair / Needs attention / Damaged from free text (the import).</summary>
    public static AssetCondition? ParseCondition(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        var t = text.Trim().ToLowerInvariant();
        if (t.StartsWith("good") || t is "ok" or "new" or "working") return AssetCondition.Good;
        if (t.StartsWith("fair") || t.StartsWith("average")) return AssetCondition.Fair;
        if (t.Contains("attention") || t.Contains("repair") || t.StartsWith("poor")) return AssetCondition.NeedsAttention;
        if (t.StartsWith("damage") || t.StartsWith("broken") || t.StartsWith("bad")) return AssetCondition.Damaged;
        return null;
    }

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRegex();

    [GeneratedRegex(@"^[A-Z0-9][A-Z0-9\-/]*$")]
    private static partial Regex PrefixRegex();
}
