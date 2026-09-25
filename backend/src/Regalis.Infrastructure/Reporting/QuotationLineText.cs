using System.Globalization;
using Regalis.Domain.Entities.Finance;
using Regalis.Domain.Enums;

namespace Regalis.Infrastructure.Reporting;

/// <summary>
/// The one-line description of a quoted service (specification §2.1), for example
/// "Cabin C-001 · 1 × 3 Month × ₹25,000" or "10-Day Pack · 1 × One-time × ₹6,800".
/// The quotation screen, the view screen and the PDF all print this same text; the
/// form's preview builds it with the same rules in the browser.
/// </summary>
public static class QuotationLineText
{
    public const string OneTime = "One-time";

    private static readonly CultureInfo Indian = CultureInfo.GetCultureInfo("en-IN");

    /// <summary>Header lines are captions and have no summary.</summary>
    public static string? Summary(QuotationLine line)
    {
        if (line.LineType == QuotationLineType.Header)
        {
            return null;
        }
        var quantity = line.Quantity.ToString("0.###", CultureInfo.InvariantCulture);
        var term = IsOneTime(line.DurationUnit, line.DurationValue)
            ? OneTime
            : $"{(line.DurationValue ?? 1m).ToString("0.###", CultureInfo.InvariantCulture)} {line.DurationUnit}";
        return $"{line.Description} · {quantity} × {term} × {Rupees(line.UnitPrice)}";
    }

    /// <summary>A line without a duration (or marked One-time) is charged once:
    /// quantity × rate, as the server's arithmetic already treats it.</summary>
    public static bool IsOneTime(string? unit, decimal? duration) =>
        duration is null || string.IsNullOrWhiteSpace(unit)
        || string.Equals(unit.Trim(), OneTime, StringComparison.OrdinalIgnoreCase);

    /// <summary>"₹25,000", "₹1,21,292.50" — Indian grouping, paise only when present.</summary>
    public static string Rupees(decimal amount) =>
        "₹" + amount.ToString(amount % 1m == 0m ? "#,##0" : "#,##0.00", Indian);
}
