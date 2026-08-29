using ClosedXML.Excel;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using Regalis.Application.Reporting;
using Regalis.Shared;

namespace Regalis.Infrastructure.Reporting;

/// <summary>Shared cell rendering so the grid, the Excel file and the PDF all show
/// the same value for the same cell (UTC → IST at the edge, A13).</summary>
internal static class ReportCellFormatter
{
    internal static string Format(ReportColumn column, object? value) => value switch
    {
        null => "",
        DateTime utc when column.Type == "datetime" => IstClock.ToIst(utc).ToString("dd MMM yyyy HH:mm"),
        DateTime utc when column.Type == "date" => IstClock.ToIst(utc).ToString("dd MMM yyyy"),
        DateOnly d => d.ToString("dd MMM yyyy"),
        decimal m when column.Type == "money" => $"₹ {m:N2}",
        decimal p when column.Type == "percent" => $"{p:0.#}%",
        decimal n => n.ToString("0.##"),
        _ => value.ToString() ?? ""
    };
}

/// <summary>One Excel exporter for all sixteen reports (ClosedXML).</summary>
internal static class ReportExcelExporter
{
    internal static byte[] Generate(ReportResult report)
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add(Truncate(report.Title, 31));

        sheet.Cell(1, 1).Value = report.Title;
        sheet.Cell(1, 1).Style.Font.SetBold().Font.SetFontSize(14);
        sheet.Cell(2, 1).Value =
            $"{report.From:dd MMM yyyy} – {report.To:dd MMM yyyy} · generated from Regalis WorkSpace";
        sheet.Cell(2, 1).Style.Font.SetFontColor(XLColor.Gray);

        const int headerRow = 4;
        for (var c = 0; c < report.Columns.Count; c++)
        {
            var cell = sheet.Cell(headerRow, c + 1);
            cell.Value = report.Columns[c].Header;
            cell.Style.Font.SetBold();
            cell.Style.Fill.SetBackgroundColor(XLColor.FromHtml("#0B0B0D"));
            cell.Style.Font.SetFontColor(XLColor.FromHtml("#C9A227"));
        }

        for (var r = 0; r < report.Rows.Count; r++)
        {
            for (var c = 0; c < report.Columns.Count; c++)
            {
                var column = report.Columns[c];
                var cell = sheet.Cell(headerRow + 1 + r, c + 1);
                var value = report.Rows[r].GetValueOrDefault(column.Key);
                switch (value)
                {
                    case null:
                        break;
                    case decimal m when column.Type == "money":
                        cell.Value = m;
                        cell.Style.NumberFormat.Format = "₹ #,##0.00";
                        break;
                    case decimal p when column.Type == "percent":
                        cell.Value = p;
                        cell.Style.NumberFormat.Format = "0.0\"%\"";
                        break;
                    case decimal n:
                        cell.Value = n;
                        break;
                    case int i:
                        cell.Value = i;
                        break;
                    case DateOnly d:
                        cell.Value = d.ToDateTime(TimeOnly.MinValue);
                        cell.Style.DateFormat.Format = "dd mmm yyyy";
                        break;
                    case DateTime utc:
                        cell.Value = IstClock.ToIst(utc);
                        cell.Style.DateFormat.Format =
                            column.Type == "date" ? "dd mmm yyyy" : "dd mmm yyyy hh:mm";
                        break;
                    default:
                        cell.Value = value.ToString();
                        break;
                }
            }
        }

        var summaryRow = headerRow + report.Rows.Count + 2;
        for (var s = 0; s < report.Summary.Count; s++)
        {
            sheet.Cell(summaryRow + s, 1).Value = report.Summary[s].Label;
            sheet.Cell(summaryRow + s, 1).Style.Font.SetBold();
            sheet.Cell(summaryRow + s, 2).Value = report.Summary[s].Value;
        }
        if (report.Truncated)
        {
            sheet.Cell(summaryRow + report.Summary.Count, 1).Value =
                $"Note: showing the first {report.Rows.Count} of {report.TotalRows} rows — narrow the filters.";
        }

        sheet.Columns(1, report.Columns.Count).AdjustToContents(headerRow, headerRow + Math.Min(report.Rows.Count, 100));
        sheet.SheetView.FreezeRows(headerRow);

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    private static string Truncate(string value, int maxLength) =>
        value.Length <= maxLength ? value : value[..maxLength];
}

/// <summary>One PDF exporter for all sixteen reports (QuestPDF, community license —
/// same house style as the invoice).</summary>
internal static class ReportPdfGenerator
{
    private const string Gold = "#C9A227";
    private const string Black = "#0B0B0D";

    static ReportPdfGenerator()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    internal static byte[] Generate(ReportResult report)
    {
        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4.Landscape());
                page.Margin(28);
                page.DefaultTextStyle(style => style.FontSize(8));

                page.Header().PaddingBottom(8).Row(row =>
                {
                    row.RelativeItem().Column(col =>
                    {
                        col.Item().Text(report.Title).FontSize(14).Bold().FontColor(Black);
                        col.Item().Text($"{report.From:dd MMM yyyy} – {report.To:dd MMM yyyy}")
                            .FontColor(Colors.Grey.Darken1);
                    });
                    row.ConstantItem(160).AlignRight().Text("Regalis WorkSpace")
                        .FontSize(11).Bold().FontColor(Gold);
                });

                page.Content().Column(col =>
                {
                    col.Item().Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            foreach (var _ in report.Columns)
                            {
                                columns.RelativeColumn();
                            }
                        });
                        table.Header(header =>
                        {
                            foreach (var column in report.Columns)
                            {
                                header.Cell().Background(Black).Padding(4)
                                    .Text(column.Header).Bold().FontColor(Gold);
                            }
                        });
                        for (var r = 0; r < report.Rows.Count; r++)
                        {
                            var background = r % 2 == 0 ? Colors.White : Colors.Grey.Lighten4;
                            foreach (var column in report.Columns)
                            {
                                var text = ReportCellFormatter.Format(
                                    column, report.Rows[r].GetValueOrDefault(column.Key));
                                var cell = table.Cell().Background(background)
                                    .BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2)
                                    .Padding(4);
                                if (column.Type is "money" or "number" or "percent")
                                {
                                    cell.AlignRight().Text(text);
                                }
                                else
                                {
                                    cell.Text(text);
                                }
                            }
                        }
                    });

                    if (report.Truncated)
                    {
                        col.Item().PaddingTop(6).Text(
                                $"Showing the first {report.Rows.Count} of {report.TotalRows} rows — narrow the filters for the rest.")
                            .Italic().FontColor(Colors.Grey.Darken1);
                    }

                    col.Item().PaddingTop(10).Row(row =>
                    {
                        foreach (var item in report.Summary)
                        {
                            row.RelativeItem().Border(0.5f).BorderColor(Colors.Grey.Lighten2)
                                .Padding(6).Column(c =>
                                {
                                    c.Item().Text(item.Label).FontSize(7)
                                        .FontColor(Colors.Grey.Darken1);
                                    c.Item().Text(item.Value).FontSize(10).Bold();
                                });
                        }
                    });
                });

                page.Footer().AlignCenter().Text(text =>
                {
                    text.Span("Page ");
                    text.CurrentPageNumber();
                    text.Span(" of ");
                    text.TotalPages();
                });
            });
        }).GeneratePdf();
    }
}
