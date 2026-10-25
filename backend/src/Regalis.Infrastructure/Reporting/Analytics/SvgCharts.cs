using System.Globalization;
using System.Net;
using System.Text;
using Regalis.Application.Reporting.Analytics;

namespace Regalis.Infrastructure.Reporting.Analytics;

/// <summary>
/// Draws a server-shaped chart as SVG for the PDF exports and the scheduled emails,
/// which are built with no browser. Same palette and rules as the screen (spec §10):
/// five fixed colours by slot, grey for "Other", one warm scale for occupancy, light
/// recessive gridlines, value axes from zero, never two y-axes.
/// </summary>
internal static class SvgCharts
{
    public static readonly string[] Palette = ["#9A958C", "#B07215", "#2E6DB4", "#C2452A", "#0F7D55", "#9B3D8C"];
    public static readonly string[] HeatSteps = ["#F7EDD6", "#EAD5A4", "#D9B468", "#C2912C", "#9A6A12"];
    private const string Ink = "#1F1B16";
    private const string Muted = "#6B655C";
    private const string Grid = "#E6E1D8";
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    public static string Colour(int slot) => slot is >= 1 and <= 5 ? Palette[slot] : Palette[0];

    public static string BandColour(string? band) => band switch
    {
        "high" => HeatSteps[4],
        "mid" => HeatSteps[3],
        _ => HeatSteps[1]
    };

    public static string? Render(ChartDto chart, int width = 760, int height = 220) => chart.Kind switch
    {
        "columns" => Columns(chart, width, height),
        "line" => Line(chart, width, height),
        "hbars" => HBars(chart, width),
        "doughnut" => Doughnut(chart, width),
        "stackedbar" => StackedBar(chart, width),
        "stackedcolumns" => StackedColumns(chart, width, height),
        "heatmap" => Heatmap(chart, width),
        _ => null
    };

    public static string Format(decimal? value, string format) => format switch
    {
        "money" => ReportMath.Money(value),
        "percent" => ReportMath.Percent(value),
        "hours" => ReportMath.Hours(value),
        _ => ReportMath.Count(value)
    };

    /// <summary>Axis ticks: 0 and three more, the top a clean 1 / 2 / 2.5 / 5 × 10ⁿ.</summary>
    private static decimal NiceTop(decimal max)
    {
        if (max <= 0)
        {
            return 1;
        }
        var magnitude = (decimal)Math.Pow(10, Math.Floor(Math.Log10((double)max)));
        foreach (var step in new[] { 1m, 2m, 2.5m, 5m, 10m })
        {
            if (max <= step * magnitude)
            {
                return step * magnitude;
            }
        }
        return 10 * magnitude;
    }

    private static string Short(decimal value, string format)
    {
        if (format == "percent")
        {
            return $"{value:0}%";
        }
        var prefix = format == "money" ? "₹" : "";
        return value switch
        {
            >= 10_000_000 => $"{prefix}{value / 10_000_000:0.#}Cr",
            >= 100_000 => $"{prefix}{value / 100_000:0.#}L",
            >= 1000 => $"{prefix}{value / 1000:0.#}k",
            _ => $"{prefix}{value:0.##}"
        };
    }

    private static string Esc(string text) => WebUtility.HtmlEncode(text);

    private static string N(decimal value) => value.ToString("0.##", Inv);
    private static string N(double value) => value.ToString("0.##", Inv);

    private static StringBuilder Open(int width, int height) =>
        new StringBuilder().Append($"<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"{width}\" height=\"{height}\" viewBox=\"0 0 {width} {height}\" font-family=\"Helvetica, Arial, sans-serif\">");

    private static void YAxis(StringBuilder svg, decimal top, string format, int left, int right, int plotTop, int plotHeight)
    {
        for (var i = 0; i <= 3; i++)
        {
            var y = plotTop + plotHeight - plotHeight * i / 3m;
            svg.Append($"<line x1=\"{left}\" x2=\"{right}\" y1=\"{N(y)}\" y2=\"{N(y)}\" stroke=\"{Grid}\" stroke-width=\"1\"/>");
            svg.Append($"<text x=\"{left - 6}\" y=\"{N(y + 3)}\" font-size=\"9\" fill=\"{Muted}\" text-anchor=\"end\">{Esc(Short(top * i / 3m, format))}</text>");
        }
    }

    private static string Columns(ChartDto chart, int width, int height)
    {
        var points = chart.Points ?? [];
        var svg = Open(width, height);
        const int left = 52, top = 18, bottom = 24;
        var plotHeight = height - top - bottom;
        var plotWidth = width - left - 8;
        var max = NiceTop(points.Count == 0 ? 0 : points.Max(p => p.Value ?? 0));
        YAxis(svg, max, chart.ValueFormat, left, width - 8, top, plotHeight);
        var slot = points.Count == 0 ? 1 : plotWidth / (decimal)points.Count;
        var barWidth = Math.Max(1, slot * 0.7m);
        var every = Math.Max(1, (int)Math.Ceiling(points.Count / 10m));
        for (var i = 0; i < points.Count; i++)
        {
            var value = points[i].Value ?? 0;
            var h = max == 0 ? 0 : value / max * plotHeight;
            var x = left + slot * i + (slot - barWidth) / 2;
            svg.Append($"<rect x=\"{N(x)}\" y=\"{N(top + plotHeight - h)}\" width=\"{N(barWidth)}\" height=\"{N(h)}\" fill=\"{Colour(1)}\" rx=\"1\"/>");
            if (chart.HighlightIndex == i && value > 0)
            {
                svg.Append($"<text x=\"{N(x + barWidth / 2)}\" y=\"{N(top + plotHeight - h - 4)}\" font-size=\"9\" fill=\"{Ink}\" text-anchor=\"middle\">{Esc(Format(value, chart.ValueFormat))}</text>");
            }
            if (i % every == 0)
            {
                svg.Append($"<text x=\"{N(x + barWidth / 2)}\" y=\"{height - 8}\" font-size=\"9\" fill=\"{Muted}\" text-anchor=\"middle\">{Esc(points[i].Label)}</text>");
            }
        }
        return svg.Append("</svg>").ToString();
    }

    private static string Line(ChartDto chart, int width, int height)
    {
        var points = chart.Points ?? [];
        var previous = chart.Previous ?? [];
        var svg = Open(width, height);
        const int left = 52, top = 18, bottom = 24;
        var plotHeight = height - top - bottom;
        var plotWidth = width - left - 12;
        var max = NiceTop(points.Concat(previous).Select(p => p.Value ?? 0).DefaultIfEmpty(0).Max());
        YAxis(svg, max, chart.ValueFormat, left, width - 8, top, plotHeight);
        var step = points.Count > 1 ? plotWidth / (decimal)(points.Count - 1) : 0;
        string Path(List<ChartPointDto> series) => string.Join(" ", series.Select((p, i) =>
            $"{(i == 0 ? "M" : "L")}{N(left + step * i)} {N(top + plotHeight - (max == 0 ? 0 : (p.Value ?? 0) / max * plotHeight))}"));
        if (previous.Count > 1)
        {
            svg.Append($"<path d=\"{Path(previous.Take(points.Count).ToList())}\" fill=\"none\" stroke=\"{Palette[0]}\" stroke-width=\"1.5\" stroke-dasharray=\"4 3\"/>");
        }
        if (points.Count > 1)
        {
            var line = Path(points);
            svg.Append($"<path d=\"{line} L{N(left + step * (points.Count - 1))} {top + plotHeight} L{left} {top + plotHeight} Z\" fill=\"{Colour(1)}\" fill-opacity=\"0.12\"/>");
            svg.Append($"<path d=\"{line}\" fill=\"none\" stroke=\"{Colour(1)}\" stroke-width=\"2\"/>");
        }
        var every = Math.Max(1, (int)Math.Ceiling(points.Count / 8m));
        for (var i = 0; i < points.Count; i += every)
        {
            svg.Append($"<text x=\"{N(left + step * i)}\" y=\"{height - 8}\" font-size=\"9\" fill=\"{Muted}\" text-anchor=\"middle\">{Esc(points[i].Label)}</text>");
        }
        return svg.Append("</svg>").ToString();
    }

    private static string HBars(ChartDto chart, int width)
    {
        var slices = (chart.Slices ?? []).Take(12).ToList();
        const int row = 22, labelWidth = 200, valueWidth = 150;
        var height = Math.Max(row, slices.Count * row) + 4;
        var svg = Open(width, height);
        var track = width - labelWidth - valueWidth;
        var max = chart.ValueFormat == "percent" ? 100m : slices.Select(s => s.Value).DefaultIfEmpty(0).Max();
        for (var i = 0; i < slices.Count; i++)
        {
            var s = slices[i];
            var y = i * row + 4;
            var w = max <= 0 ? 0 : Math.Max(0, s.Value) / max * track;
            var colour = s.Band is not null ? BandColour(s.Band) : Colour(s.Slot);
            svg.Append($"<text x=\"0\" y=\"{y + 11}\" font-size=\"9.5\" fill=\"{Ink}\">{Esc(Clip(s.Label, 34))}</text>");
            svg.Append($"<rect x=\"{labelWidth}\" y=\"{y + 2}\" width=\"{track}\" height=\"12\" fill=\"{Grid}\" rx=\"2\"/>");
            svg.Append($"<rect x=\"{labelWidth}\" y=\"{y + 2}\" width=\"{N(w)}\" height=\"12\" fill=\"{colour}\" rx=\"2\"/>");
            var text = chart.ValueFormat == "percent" ? Format(s.Value, "percent") : $"{Format(s.Value, chart.ValueFormat)} · {s.Share:0}%";
            svg.Append($"<text x=\"{labelWidth + track + 8}\" y=\"{y + 11}\" font-size=\"9.5\" fill=\"{Ink}\">{Esc(text)}</text>");
        }
        return svg.Append("</svg>").ToString();
    }

    private static string Doughnut(ChartDto chart, int width)
    {
        var slices = chart.Slices ?? [];
        const int size = 150;
        var height = Math.Max(size, slices.Count * 18 + 10);
        var svg = Open(width, height);
        var total = slices.Sum(s => Math.Max(0, s.Value));
        const double cx = 75, cy = 75, r = 56;
        var angle = -Math.PI / 2;
        foreach (var s in slices.Where(s => s.Value > 0))
        {
            var sweep = (double)(s.Value / total) * Math.PI * 2;
            if (sweep >= Math.PI * 2 - 0.001)
            {
                svg.Append($"<circle cx=\"{cx}\" cy=\"{cy}\" r=\"{r}\" fill=\"none\" stroke=\"{Colour(s.Slot)}\" stroke-width=\"22\"/>");
                break;
            }
            var x1 = cx + r * Math.Cos(angle);
            var y1 = cy + r * Math.Sin(angle);
            var x2 = cx + r * Math.Cos(angle + sweep - 0.02);
            var y2 = cy + r * Math.Sin(angle + sweep - 0.02);
            svg.Append($"<path d=\"M{N(x1)} {N(y1)} A{r} {r} 0 {(sweep > Math.PI ? 1 : 0)} 1 {N(x2)} {N(y2)}\" fill=\"none\" stroke=\"{Colour(s.Slot)}\" stroke-width=\"22\"/>");
            angle += sweep;
        }
        svg.Append($"<text x=\"{cx}\" y=\"{cy + 5}\" font-size=\"15\" font-weight=\"bold\" fill=\"{Ink}\" text-anchor=\"middle\">{Esc(Format(chart.Total ?? total, chart.ValueFormat))}</text>");
        for (var i = 0; i < slices.Count; i++)
        {
            var y = 14 + i * 18;
            svg.Append($"<rect x=\"180\" y=\"{y - 8}\" width=\"10\" height=\"10\" fill=\"{Colour(slices[i].Slot)}\" rx=\"2\"/>");
            svg.Append($"<text x=\"196\" y=\"{y + 1}\" font-size=\"10\" fill=\"{Ink}\">{Esc(slices[i].Label)} — {Esc(Format(slices[i].Value, chart.ValueFormat))} ({slices[i].Share:0}%)</text>");
        }
        return svg.Append("</svg>").ToString();
    }

    private static string StackedBar(ChartDto chart, int width)
    {
        var slices = chart.Slices ?? [];
        var svg = Open(width, 46);
        var total = slices.Sum(s => Math.Max(0, s.Value));
        decimal x = 0;
        foreach (var s in slices.Where(s => s.Value > 0))
        {
            var w = total == 0 ? 0 : s.Value / total * width;
            svg.Append($"<rect x=\"{N(x)}\" y=\"0\" width=\"{N(Math.Max(0, w - 2))}\" height=\"16\" fill=\"{Colour(s.Slot)}\"/>");
            x += w;
        }
        var labels = string.Join("   ", slices.Select(s => $"{s.Label} {s.Share:0}%"));
        svg.Append($"<text x=\"0\" y=\"34\" font-size=\"10\" fill=\"{Ink}\">{Esc(labels)}</text>");
        return svg.Append("</svg>").ToString();
    }

    private static string StackedColumns(ChartDto chart, int width, int height)
    {
        var stacks = chart.Stacks ?? [];
        var series = chart.Series ?? [];
        var svg = Open(width, height + 18);
        const int left = 52, top = 30, bottom = 24;
        var plotHeight = height - top - bottom;
        var plotWidth = width - left - 8;
        var max = NiceTop(stacks.Select(s => s.Total).DefaultIfEmpty(0).Max());
        var lx = left;
        foreach (var s in series)
        {
            svg.Append($"<rect x=\"{lx}\" y=\"4\" width=\"10\" height=\"10\" fill=\"{Colour(s.Slot)}\" rx=\"2\"/>");
            svg.Append($"<text x=\"{lx + 14}\" y=\"13\" font-size=\"10\" fill=\"{Ink}\">{Esc(s.Name)}</text>");
            lx += 24 + s.Name.Length * 6;
        }
        YAxis(svg, max, chart.ValueFormat, left, width - 8, top, plotHeight);
        var slot = stacks.Count == 0 ? 1 : plotWidth / (decimal)stacks.Count;
        var barWidth = Math.Max(2, slot * 0.6m);
        var every = Math.Max(1, (int)Math.Ceiling(stacks.Count / 10m));
        for (var i = 0; i < stacks.Count; i++)
        {
            var x = left + slot * i + (slot - barWidth) / 2;
            var y = (decimal)(top + plotHeight);
            for (var j = 0; j < stacks[i].Values.Count && j < series.Count; j++)
            {
                var h = max == 0 ? 0 : stacks[i].Values[j] / max * plotHeight;
                if (h <= 0)
                {
                    continue;
                }
                // A 2px gap keeps the segments apart on a printed page (spec §3.5).
                svg.Append($"<rect x=\"{N(x)}\" y=\"{N(y - h)}\" width=\"{N(barWidth)}\" height=\"{N(Math.Max(0, h - 2))}\" fill=\"{Colour(series[j].Slot)}\"/>");
                y -= h;
            }
            if (stacks[i].Total > 0)
            {
                svg.Append($"<text x=\"{N(x + barWidth / 2)}\" y=\"{N(y - 4)}\" font-size=\"9\" fill=\"{Ink}\" text-anchor=\"middle\">{Esc(Short(stacks[i].Total, chart.ValueFormat))}</text>");
            }
            if (i % every == 0)
            {
                svg.Append($"<text x=\"{N(x + barWidth / 2)}\" y=\"{height + 10}\" font-size=\"9\" fill=\"{Muted}\" text-anchor=\"middle\">{Esc(stacks[i].Label)}</text>");
            }
        }
        return svg.Append("</svg>").ToString();
    }

    private static string Heatmap(ChartDto chart, int width)
    {
        var rows = chart.RowLabels ?? [];
        var cols = chart.ColLabels ?? [];
        var cells = chart.Cells ?? [];
        const int left = 40, top = 16, cellHeight = 20;
        var legendWidth = 120;
        var cellWidth = cols.Count == 0 ? 0 : (width - left - legendWidth) / (decimal)cols.Count;
        var height = top + rows.Count * cellHeight + 6;
        var svg = Open(width, height);
        for (var c = 0; c < cols.Count; c++)
        {
            svg.Append($"<text x=\"{N(left + cellWidth * c + cellWidth / 2)}\" y=\"10\" font-size=\"8.5\" fill=\"{Muted}\" text-anchor=\"middle\">{Esc(cols[c])}</text>");
        }
        for (var r = 0; r < rows.Count; r++)
        {
            var y = top + r * cellHeight;
            svg.Append($"<text x=\"0\" y=\"{y + 13}\" font-size=\"9.5\" fill=\"{Ink}\">{Esc(rows[r])}</text>");
            for (var c = 0; c < cols.Count && r < cells.Count && c < cells[r].Count; c++)
            {
                var cell = cells[r][c];
                var fill = cell.Step < 0 ? "#FFFFFF" : HeatSteps[cell.Step];
                svg.Append($"<rect x=\"{N(left + cellWidth * c + 1)}\" y=\"{y + 1}\" width=\"{N(cellWidth - 2)}\" height=\"{cellHeight - 2}\" fill=\"{fill}\" stroke=\"{Grid}\" stroke-width=\"0.5\" rx=\"2\"/>");
            }
        }
        string[] steps = ["0–20%", "20–45%", "45–65%", "65–80%", "80%+"];
        var lx = width - legendWidth + 14;
        for (var i = 0; i < steps.Length; i++)
        {
            svg.Append($"<rect x=\"{lx}\" y=\"{top + i * 16}\" width=\"12\" height=\"12\" fill=\"{HeatSteps[i]}\" rx=\"2\"/>");
            svg.Append($"<text x=\"{lx + 18}\" y=\"{top + i * 16 + 10}\" font-size=\"9\" fill=\"{Ink}\">{steps[i]}</text>");
        }
        return svg.Append("</svg>").ToString();
    }

    private static string Clip(string text, int max) => text.Length <= max ? text : text[..(max - 1)] + "…";
}
