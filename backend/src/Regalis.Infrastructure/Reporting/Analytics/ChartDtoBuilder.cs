using System.Globalization;
using Regalis.Application.Reporting.Analytics;

namespace Regalis.Infrastructure.Reporting.Analytics;

/// <summary>Shapes chart series on the server, so the browser only draws (spec §14.3).</summary>
internal static class ChartDtoBuilder
{
    internal sealed record Heat(
        List<DayOfWeek> Weekdays,
        List<int> Hours,
        List<List<(decimal? Percent, decimal People)>> Cells,
        (DayOfWeek Day, int Hour, decimal Percent)? Quietest,
        (DayOfWeek Day, int Hour, decimal Percent)? Busiest);

    public static ChartDto Heatmap(string id, string title, string? subtitle, Heat heat) => new()
    {
        Id = id,
        Kind = "heatmap",
        Title = title,
        Subtitle = subtitle,
        ValueFormat = "percent",
        RowLabels = heat.Weekdays.Select(OccupancyCalculator.WeekdayName).ToList(),
        ColLabels = heat.Hours.Select(OccupancyCalculator.HourLabel).ToList(),
        Cells = heat.Cells.Select(row => row
            .Select(c => new HeatCellDto(c.Percent, c.People, OccupancyCalculator.Step(c.Percent)))
            .ToList()).ToList()
    };

    public static string? QuietestSentence(Heat heat) => heat.Quietest is { } q
        ? $"Quietest: {FullWeekday(q.Day)} {OccupancyCalculator.HourLabel(q.Hour)}–{OccupancyCalculator.HourLabel(q.Hour + 1)}, at {ReportMath.Percent(q.Percent)}."
        : null;

    public static string FullWeekday(DayOfWeek day) => CultureInfo.InvariantCulture.DateTimeFormat.GetDayName(day);

    /// <summary>Slices with whole-number shares that add to 100.</summary>
    public static List<ChartSliceDto> Slices(IEnumerable<(string Label, decimal Value, int Slot)> parts)
    {
        var list = parts.ToList();
        var shares = ReportMath.SharesTo100(list.Select(p => Math.Max(0, p.Value)).ToList());
        return list.Select((p, i) => new ChartSliceDto(p.Label, p.Value, shares[i], p.Slot)).ToList();
    }

    /// <summary>One column per day across the whole range — a quiet day is a zero, not a gap.</summary>
    public static List<ChartPointDto> DailyPoints(DateOnly from, DateOnly to,
        IReadOnlyDictionary<DateOnly, (decimal Value, decimal Value2)> byDay)
    {
        var points = new List<ChartPointDto>();
        for (var day = from; day <= to; day = day.AddDays(1))
        {
            var (value, value2) = byDay.GetValueOrDefault(day);
            points.Add(new ChartPointDto(DayLabel(day), day.ToString("yyyy-MM-dd"), value, value2));
        }
        return points;
    }

    public static string DayLabel(DateOnly day) => day.ToString("d MMM", CultureInfo.InvariantCulture);

    public static string MonthLabel(DateOnly month) => month.ToString("MMM yyyy", CultureInfo.InvariantCulture);

    /// <summary>Index of the largest value, for the label above the busiest column.</summary>
    public static int? Highest(IReadOnlyList<ChartPointDto> points)
    {
        int? best = null;
        for (var i = 0; i < points.Count; i++)
        {
            if ((points[i].Value ?? 0) > 0 && (best is null || points[i].Value > points[best.Value].Value))
            {
                best = i;
            }
        }
        return best;
    }

    /// <summary>Groups a daily range into columns: by day under three weeks, else by week.</summary>
    public static (List<(DateOnly Start, DateOnly End, string Label)> Buckets, bool ByWeek) Buckets(DateOnly from, DateOnly to)
    {
        var buckets = new List<(DateOnly, DateOnly, string)>();
        var byWeek = ReportMath.DaysIn(from, to) >= 21;
        if (!byWeek)
        {
            for (var day = from; day <= to; day = day.AddDays(1))
            {
                buckets.Add((day, day, DayLabel(day)));
            }
            return (buckets, false);
        }
        for (var start = ReportMath.WeekStart(from); start <= to; start = start.AddDays(7))
        {
            var a = start < from ? from : start;
            var b = start.AddDays(6) > to ? to : start.AddDays(6);
            buckets.Add((a, b, DayLabel(a)));
        }
        return (buckets, true);
    }
}
