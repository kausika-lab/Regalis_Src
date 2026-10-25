using System.Globalization;
using Regalis.Shared;

namespace Regalis.Infrastructure.Reporting.Analytics;

/// <summary>
/// The rules every number follows (Reports &amp; Analysis spec §8), in one place so the
/// screen, the Excel file, the PDF and the scheduled email can never disagree.
/// </summary>
public static class ReportMath
{
    private static readonly CultureInfo India = CultureInfo.GetCultureInfo("en-IN");

    /// <summary>(now − before) ÷ before × 100, one decimal (§8.6). Before zero: "none"
    /// (no comparison — never infinity, never 100%). Before negative: the absolute change.</summary>
    public static (string? Kind, decimal? Change) Compare(decimal? now, decimal? before)
    {
        if (now is null || before is null)
        {
            return (null, null);
        }
        if (before == 0)
        {
            return ("none", null);
        }
        if (before < 0)
        {
            return ("absolute", now - before);
        }
        return ("percent", Math.Round((now.Value - before.Value) / before.Value * 100m, 1,
            MidpointRounding.AwayFromZero));
    }

    /// <summary>Whole-number shares that add to exactly 100; the rounding remainder goes
    /// to the largest share (§8.6). All zero → all zero.</summary>
    public static List<decimal> SharesTo100(IReadOnlyList<decimal> values)
    {
        var total = values.Sum();
        if (values.Count == 0 || total <= 0)
        {
            return values.Select(_ => 0m).ToList();
        }
        var shares = values.Select(v => Math.Round(v / total * 100m, 0, MidpointRounding.AwayFromZero)).ToList();
        var remainder = 100m - shares.Sum();
        if (remainder != 0)
        {
            var largest = 0;
            for (var i = 1; i < values.Count; i++)
            {
                if (values[i] > values[largest])
                {
                    largest = i;
                }
            }
            shares[largest] += remainder;
        }
        return shares;
    }

    /// <summary>The previous period: the same number of days immediately before (§8.3).</summary>
    public static (DateOnly From, DateOnly To) PreviousPeriod(DateOnly from, DateOnly to)
    {
        var days = to.DayNumber - from.DayNumber + 1;
        return (from.AddDays(-days), from.AddDays(-1));
    }

    /// <summary>Inclusive at both ends: 1–30 Sep is 30 days.</summary>
    public static int DaysIn(DateOnly from, DateOnly to) => to.DayNumber - from.DayNumber + 1;

    /// <summary>The UTC instant an IST day starts.</summary>
    public static DateTime IstStartUtc(DateOnly day) => IstClock.ToUtc(day.ToDateTime(TimeOnly.MinValue));

    /// <summary>The Indian financial year containing a day: 1 April – 31 March.</summary>
    public static (DateOnly From, DateOnly To) FinancialYear(DateOnly day)
    {
        var startYear = day.Month >= 4 ? day.Year : day.Year - 1;
        return (new DateOnly(startYear, 4, 1), new DateOnly(startYear + 1, 3, 31));
    }

    /// <summary>₹2,70,900 — Indian grouping, whole rupees unless paise matter.</summary>
    public static string Money(decimal? value, bool paise = false)
    {
        if (value is null)
        {
            return "—";
        }
        var rounded = paise ? Math.Round(value.Value, 2, MidpointRounding.AwayFromZero)
            : Math.Round(value.Value, 0, MidpointRounding.AwayFromZero);
        var text = Math.Abs(rounded).ToString(paise ? "#,##,##0.00" : "#,##,##0", India);
        return (rounded < 0 ? "−₹" : "₹") + text;
    }

    public static string Count(decimal? value) =>
        value is null ? "—" : Math.Round(value.Value, 0).ToString("#,##,##0", India);

    public static string Hours(decimal? value) =>
        value is null ? "—" : Math.Round(value.Value, 1).ToString("#,##,##0.#", India) + " h";

    public static string Percent(decimal? value, int decimals = 0) =>
        value is null ? "—" : Math.Round(value.Value, decimals, MidpointRounding.AwayFromZero)
            .ToString(decimals == 0 ? "0" : "0.0", India) + "%";

    /// <summary>"12 Oct 2026".</summary>
    public static string Day(DateOnly day) => day.ToString("d MMM yyyy", CultureInfo.InvariantCulture);

    /// <summary>"1 September – 12 October 2026", dropping the repeated year.</summary>
    public static string Range(DateOnly from, DateOnly to)
    {
        var inv = CultureInfo.InvariantCulture;
        if (from == to)
        {
            return to.ToString("d MMMM yyyy", inv);
        }
        return from.Year == to.Year
            ? $"{from.ToString("d MMMM", inv)} – {to.ToString("d MMMM yyyy", inv)}"
            : $"{from.ToString("d MMMM yyyy", inv)} – {to.ToString("d MMMM yyyy", inv)}";
    }

    /// <summary>"Today, 21:00" / "Mon, 19 Oct" / "1 Nov 2026" (spec §5.4), in IST.</summary>
    public static string FriendlyIst(DateTime utc, DateTime nowUtc)
    {
        var inv = CultureInfo.InvariantCulture;
        var ist = IstClock.ToIst(utc);
        var today = IstClock.IstDateOf(nowUtc);
        var day = DateOnly.FromDateTime(ist);
        if (day == today)
        {
            return $"Today, {ist:HH:mm}";
        }
        if (day == today.AddDays(1))
        {
            return $"Tomorrow, {ist:HH:mm}";
        }
        return day.DayNumber - today.DayNumber is > 0 and < 7
            ? ist.ToString("ddd, d MMM", inv)
            : ist.ToString("d MMM yyyy", inv);
    }

    /// <summary>Money stored as decimal rupees → whole paise for RPT_DailyMetric.</summary>
    public static long ToPaise(decimal rupees) =>
        (long)Math.Round(rupees * 100m, 0, MidpointRounding.AwayFromZero);

    public static decimal FromPaise(long paise) => paise / 100m;

    /// <summary>The Monday a day's week starts on.</summary>
    public static DateOnly WeekStart(DateOnly day) => day.AddDays(-(((int)day.DayOfWeek + 6) % 7));
}
