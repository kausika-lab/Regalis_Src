using System.Globalization;
using System.Text;
using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using Regalis.Application.Abstractions;
using Regalis.Application.Reporting.Analytics;
using Regalis.Domain.Enums;
using Regalis.Infrastructure.Persistence;
using Regalis.Shared;
using Regalis.Shared.Constants;

namespace Regalis.Infrastructure.Reporting.Analytics;

internal sealed record BuiltFile(byte[] Content, string ContentType, string FileName, int RowCount);

/// <summary>The applied-filter chips, and the same filters as one line of plain English
/// for the top of an export (spec §4.2, §11.1).</summary>
internal static class ReportChips
{
    public static async Task<List<ReportChipDto>> BuildAsync(RegalisDbContext db, ReportHandler handler,
        ReportLookups lookups, Dictionary<string, List<string>> values, DateOnly from, DateOnly to, DateOnly today,
        CancellationToken ct)
    {
        var chips = new List<ReportChipDto>();
        var defaultTo = today;
        var defaultFrom = handler.DefaultRange == "all" ? ReportFilterParser.AllTimeStart : today.AddDays(-29);
        if (from != defaultFrom || to != defaultTo)
        {
            chips.Add(new ReportChipDto("dates", from <= ReportFilterParser.AllTimeStart
                ? $"Up to {ReportMath.Day(to)}" : $"{ReportMath.Day(from)} – {ReportMath.Day(to)}"));
        }
        foreach (var filter in handler.Filters(lookups))
        {
            if (filter.Kind == "dateRange")
            {
                continue;
            }
            var chosen = values.GetValueOrDefault(filter.Key) ?? [];
            if (chosen.OrderBy(v => v).SequenceEqual(filter.Default.OrderBy(v => v)))
            {
                continue;
            }
            string label;
            switch (filter.Kind)
            {
                case "toggle":
                    label = chosen.Contains("true") ? filter.Label : $"{filter.Label}: off";
                    break;
                case "customer":
                    var id = long.TryParse(chosen.FirstOrDefault(), out var parsed) ? parsed : 0;
                    var name = await db.Users.AsNoTracking().Where(u => u.UserId == id)
                        .Select(u => u.FullName).FirstOrDefaultAsync(ct);
                    label = $"Customer: {name ?? "unknown"}";
                    break;
                default:
                    var names = chosen.Select(v => filter.Options.FirstOrDefault(o => o.Value == v)?.Label ?? v).ToList();
                    label = $"{filter.Label}: {(names.Count == 0 ? filter.AllLabel ?? "All" : string.Join(", ", names))}";
                    break;
            }
            chips.Add(new ReportChipDto(filter.Key, label));
        }
        return chips;
    }

    public static string Sentence(DateOnly from, DateOnly to, IEnumerable<ReportChipDto> chips)
    {
        var parts = new List<string>
        {
            from <= ReportFilterParser.AllTimeStart ? $"Up to {ReportMath.Day(to)}" : ReportMath.Range(from, to)
        };
        parts.AddRange(chips.Where(c => c.Key != "dates").Select(c => c.Label));
        return string.Join(" · ", parts);
    }
}

/// <summary>Builds the Excel and PDF files every report exports to — the same rows and
/// the same server totals the screen shows, so the two agree to the rupee (spec §11.3).</summary>
internal sealed class ReportFileBuilder(ISettingService settings)
{
    public const int PdfRowCap = 500;

    /// <summary>₹2,70,900.00 in Excel: Indian grouping by number format, so the cell stays a number.</summary>
    private const string RupeeFormat = "[>=10000000]\"₹\"##\\,##\\,##\\,##0.00;[>=100000]\"₹\"##\\,##\\,##0.00;\"₹\"##,##0.00";

    static ReportFileBuilder()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public static string FileName(ReportHandler handler, DateOnly from, DateOnly to, ReportFormat format)
    {
        var slug = new StringBuilder();
        foreach (var ch in handler.Name.ToLowerInvariant())
        {
            slug.Append(char.IsLetterOrDigit(ch) ? ch : '-');
        }
        var name = string.Join('-', slug.ToString().Split('-', StringSplitOptions.RemoveEmptyEntries));
        if (!name.EndsWith("report"))
        {
            name += "-report";
        }
        return $"{name}_{from:yyyy-MM-dd}_{to:yyyy-MM-dd}.{(format == ReportFormat.Pdf ? "pdf" : "xlsx")}";
    }

    public async Task<BuiltFile> BuildAsync(ReportHandler handler, ReportRunContext ctx, ReportFormat format,
        string sort, string exportedBy, string filtersText, IReadOnlyCollection<string>? visibleColumns,
        DateTime nowUtc, CancellationToken ct)
    {
        var (count, totals) = await handler.TotalsAsync(ctx, ct);
        var fileName = FileName(handler, ctx.From, ctx.To, format);
        var exportedAt = $"{IstClock.ToIst(nowUtc).ToString("d MMM yyyy, HH:mm", CultureInfo.InvariantCulture)} IST";
        if (format == ReportFormat.Pdf)
        {
            var bytes = await PdfAsync(handler, ctx, sort, count, totals, exportedBy, exportedAt, filtersText,
                visibleColumns, ct);
            return new BuiltFile(bytes, "application/pdf", fileName, count);
        }
        var xlsx = await ExcelAsync(handler, ctx, sort, count, totals, exportedBy, exportedAt, filtersText, ct);
        return new BuiltFile(xlsx, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName, count);
    }

    private static async Task<byte[]> ExcelAsync(ReportHandler handler, ReportRunContext ctx, string sort, int count,
        Dictionary<string, object?> totals, string exportedBy, string exportedAt, string filtersText, CancellationToken ct)
    {
        var columns = handler.ColumnDefinitions;
        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add(handler.Name.Length > 31 ? handler.Name[..31] : handler.Name);

        sheet.Cell(1, 1).Value = handler.Name;
        sheet.Cell(1, 1).Style.Font.SetBold().Font.SetFontSize(14);
        sheet.Cell(2, 1).Value = $"Filters: {filtersText}";
        sheet.Cell(3, 1).Value = $"Exported by {exportedBy}";
        sheet.Cell(4, 1).Value = $"Exported on {exportedAt}";
        for (var r = 2; r <= 4; r++)
        {
            sheet.Cell(r, 1).Style.Font.SetFontColor(XLColor.FromHtml("#6B655C"));
        }

        const int headerRow = 6;
        for (var c = 0; c < columns.Count; c++)
        {
            var cell = sheet.Cell(headerRow, c + 1);
            cell.Value = columns[c].Header;
            cell.Style.Font.SetBold();
            cell.Style.Fill.SetBackgroundColor(XLColor.FromHtml("#F3EEE4"));
            cell.Style.Border.BottomBorder = XLBorderStyleValues.Thin;
            if (IsNumeric(columns[c].Type))
            {
                cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
            }
        }

        var row = headerRow;
        await foreach (var data in handler.AllRowsAsync(ctx, sort, ct))
        {
            row++;
            for (var c = 0; c < columns.Count; c++)
            {
                Write(sheet.Cell(row, c + 1), columns[c].Type, data.GetValueOrDefault(columns[c].Key));
            }
        }

        // The grand total: every matching row, the same server totals as the screen.
        var totalRow = row + 1;
        sheet.Cell(totalRow, 1).Value = $"Total of all {ReportText.Plural(count, handler.RowNoun)}";
        for (var c = 0; c < columns.Count; c++)
        {
            if (columns[c].Totalled && totals.TryGetValue(columns[c].Key, out var total) && c > 0)
            {
                Write(sheet.Cell(totalRow, c + 1), columns[c].Type, total);
            }
        }
        var totalRange = sheet.Range(totalRow, 1, totalRow, Math.Max(1, columns.Count));
        totalRange.Style.Font.SetBold();
        totalRange.Style.Border.TopBorder = XLBorderStyleValues.Medium;

        sheet.SheetView.FreezeRows(headerRow);
        sheet.Range(headerRow, 1, Math.Max(headerRow + 1, row), Math.Max(1, columns.Count)).SetAutoFilter();
        sheet.Columns(1, Math.Max(1, columns.Count)).AdjustToContents(headerRow, Math.Min(row, headerRow + 400));
        foreach (var column in sheet.Columns(1, Math.Max(1, columns.Count)))
        {
            column.Width = Math.Min(Math.Max(column.Width, 10), 48);
        }

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    private static bool IsNumeric(string type) => type is "money" or "number" or "percent" or "hours";

    private static void Write(IXLCell cell, string type, object? value)
    {
        switch (value)
        {
            case null:
                return;
            case DateTime utc:
                cell.Value = IstClock.ToIst(utc);
                cell.Style.DateFormat.Format = type == "date" ? "dd mmm yyyy" : "dd mmm yyyy hh:mm";
                return;
            case DateOnly day:
                cell.Value = day.ToDateTime(TimeOnly.MinValue);
                cell.Style.DateFormat.Format = type == "month" ? "mmm yyyy" : "dd mmm yyyy";
                return;
        }
        if (IsNumeric(type) && TryNumber(value, out var number))
        {
            switch (type)
            {
                case "money":
                    cell.Value = number;
                    cell.Style.NumberFormat.Format = RupeeFormat;
                    break;
                case "percent":
                    cell.Value = number / 100m;
                    cell.Style.NumberFormat.Format = "0.0%";
                    break;
                case "hours":
                    cell.Value = number;
                    cell.Style.NumberFormat.Format = "0.0";
                    break;
                default:
                    cell.Value = number;
                    break;
            }
            return;
        }
        cell.Value = value.ToString();
    }

    private static bool TryNumber(object value, out decimal number)
    {
        switch (value)
        {
            case decimal d:
                number = d;
                return true;
            case int i:
                number = i;
                return true;
            case long l:
                number = l;
                return true;
            case double f:
                number = (decimal)f;
                return true;
            default:
                number = 0;
                return false;
        }
    }

    internal static string Text(string type, object? value) => value switch
    {
        null => "—",
        DateTime utc when type == "date" => IstClock.ToIst(utc).ToString("d MMM yyyy", CultureInfo.InvariantCulture),
        DateTime utc => IstClock.ToIst(utc).ToString("d MMM yyyy, HH:mm", CultureInfo.InvariantCulture),
        DateOnly day when type == "month" => day.ToString("MMM yyyy", CultureInfo.InvariantCulture),
        DateOnly day => ReportMath.Day(day),
        _ when TryNumber(value, out var n) && type == "money" => ReportMath.Money(n, paise: true),
        _ when TryNumber(value, out var n) && type == "percent" => ReportMath.Percent(n, 1),
        _ when TryNumber(value, out var n) && type == "hours" => ReportMath.Hours(n),
        _ when TryNumber(value, out var n) => ReportMath.Count(n),
        _ => value.ToString() is { Length: > 0 } s ? s : "—"
    };

    internal async Task<(byte[]? Logo, string Company)> LetterheadAsync(CancellationToken ct) =>
        (await PdfLogo.LoadAsync(settings, ct),
            await settings.GetStringAsync(AppSettingKeys.Company.Name, "Regalis WorkSpace", ct));

    private async Task<byte[]> PdfAsync(ReportHandler handler, ReportRunContext ctx, string sort, int count,
        Dictionary<string, object?> totals, string exportedBy, string exportedAt, string filtersText,
        IReadOnlyCollection<string>? visibleColumns, CancellationToken ct)
    {
        var all = handler.ColumnDefinitions;
        var columns = visibleColumns is { Count: > 0 }
            ? all.Where(c => visibleColumns.Contains(c.Key)).ToList()
            : all.Where(c => c.DefaultVisible).ToList();
        if (columns.Count == 0)
        {
            columns = all;
        }
        var (summary, charts) = await handler.SummaryAndChartsAsync(ctx, ct);
        var rows = new List<Dictionary<string, object?>>();
        await foreach (var data in handler.AllRowsAsync(ctx, sort, ct))
        {
            rows.Add(data);
            if (rows.Count >= PdfRowCap)
            {
                break;
            }
        }
        var (logo, company) = await LetterheadAsync(ct);
        var sentence = handler.SentenceFor(ctx, count, totals);
        var svgs = charts.Take(2).Select(c => (c.Title, Svg: SvgCharts.Render(c, 760, 190))).Where(c => c.Svg is not null).ToList();

        return Document.Create(container => container.Page(page =>
        {
            page.Size(PageSizes.A4.Landscape());
            page.Margin(26);
            page.DefaultTextStyle(s => s.FontSize(8).FontColor("#1F1B16"));
            page.Header().PaddingBottom(8).Column(col =>
            {
                col.Item().Row(row =>
                {
                    PdfLogo.Compose(row, logo);
                    row.RelativeItem().Column(c =>
                    {
                        c.Item().Text(company).FontSize(9).FontColor("#6B655C");
                        c.Item().Text(handler.Name).FontSize(15).Bold();
                    });
                    row.ConstantItem(220).AlignRight().Column(c =>
                    {
                        c.Item().AlignRight().Text($"Exported {exportedAt}").FontColor("#6B655C");
                        c.Item().AlignRight().Text($"by {exportedBy}").FontColor("#6B655C");
                    });
                });
                col.Item().PaddingTop(4).Text($"Filters: {filtersText}").FontColor("#6B655C");
                col.Item().PaddingTop(4).LineHorizontal(0.75f).LineColor("#E6E1D8");
            });

            page.Content().Column(col =>
            {
                col.Item().PaddingBottom(8).Text(sentence).FontSize(10);
                if (summary.Count > 0)
                {
                    col.Item().PaddingBottom(10).Row(row =>
                    {
                        foreach (var figure in summary)
                        {
                            row.RelativeItem().Border(0.5f).BorderColor("#E6E1D8").Padding(6).Column(c =>
                            {
                                c.Item().Text(figure.Label).FontSize(7.5f).FontColor("#6B655C");
                                c.Item().Text(SvgCharts.Format(figure.Value, figure.Format)).FontSize(12).Bold();
                                if (!string.IsNullOrEmpty(figure.Sub))
                                {
                                    c.Item().Text(figure.Sub).FontSize(7).FontColor("#6B655C");
                                }
                            });
                            row.ConstantItem(6);
                        }
                    });
                }
                foreach (var (title, svg) in svgs)
                {
                    col.Item().PaddingBottom(2).Text(title).Bold().FontSize(9);
                    col.Item().PaddingBottom(10).Svg(svg!).FitWidth();
                }
                col.Item().Table(table =>
                {
                    table.ColumnsDefinition(cd =>
                    {
                        foreach (var _ in columns)
                        {
                            cd.RelativeColumn();
                        }
                    });
                    table.Header(header =>
                    {
                        foreach (var column in columns)
                        {
                            var cell = header.Cell().Background("#F3EEE4").Padding(4);
                            if (IsNumeric(column.Type))
                            {
                                cell.AlignRight().Text(column.Header).Bold();
                            }
                            else
                            {
                                cell.Text(column.Header).Bold();
                            }
                        }
                    });
                    foreach (var data in rows)
                    {
                        foreach (var column in columns)
                        {
                            var cell = table.Cell().BorderBottom(0.5f).BorderColor("#EEE9E0").Padding(3.5f);
                            var text = Text(column.Type, data.GetValueOrDefault(column.Key));
                            if (IsNumeric(column.Type))
                            {
                                cell.AlignRight().Text(text);
                            }
                            else
                            {
                                cell.Text(text);
                            }
                        }
                    }
                    for (var c = 0; c < columns.Count; c++)
                    {
                        var cell = table.Cell().BorderTop(1.25f).BorderColor("#1F1B16").Padding(4);
                        if (c == 0)
                        {
                            cell.Text($"Total of all {ReportText.Plural(count, handler.RowNoun)}").Bold();
                        }
                        else if (columns[c].Totalled && totals.TryGetValue(columns[c].Key, out var total))
                        {
                            cell.AlignRight().Text(Text(columns[c].Type, total)).Bold();
                        }
                    }
                });
                if (count > rows.Count)
                {
                    col.Item().PaddingTop(6).Text(
                            $"This PDF shows the first {rows.Count:N0} of {count:N0} rows; the total row counts all of them. The Excel export has every row.")
                        .Italic().FontColor("#6B655C");
                }
            });

            page.Footer().AlignCenter().Text(text =>
            {
                text.Span("Page ");
                text.CurrentPageNumber();
                text.Span(" of ");
                text.TotalPages();
            });
        })).GeneratePdf();
    }
}
