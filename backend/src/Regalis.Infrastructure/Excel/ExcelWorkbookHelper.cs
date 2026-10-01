using System.Globalization;
using ClosedXML.Excel;
using Regalis.Application.Common.Exceptions;
using Regalis.Shared;

namespace Regalis.Infrastructure.Excel;

/// <summary>How an export cell is written, so numbers stay numbers and dates stay dates.</summary>
internal enum ExcelCellKind
{
    Text,
    Number,
    Money,
    Date,
    DateTime
}

/// <summary>One export column: the readable header and how to read the value from a row.</summary>
internal sealed record ExcelColumn<T>(string Header, Func<T, object?> Value, ExcelCellKind Kind = ExcelCellKind.Text);

/// <summary>One column an import understands. <see cref="Help"/> and <see cref="Example"/>
/// feed the template's instructions sheet and its sample row.</summary>
internal sealed record ImportColumn(string Header, bool Required, string Help, object? Example,
    ExcelCellKind Kind = ExcelCellKind.Text);

/// <summary>One data row of an uploaded sheet, keyed by the canonical header.</summary>
internal sealed class ImportSheetRow(int rowNumber, Dictionary<string, object?> values)
{
    /// <summary>The row number as Excel shows it (the header is row 1).</summary>
    public int RowNumber { get; } = rowNumber;
    public IReadOnlyDictionary<string, object?> Values { get; } = values;

    public object? Raw(string header) => Values.GetValueOrDefault(header);

    /// <summary>The cell as trimmed text, or null when it is blank.</summary>
    public string? Text(string header) => ExcelWorkbookHelper.AsText(Raw(header));
}

/// <summary>An uploaded sheet after the file-level checks passed.</summary>
internal sealed record ImportSheet(List<ImportSheetRow> Rows, List<string> IgnoredColumns);

/// <summary>
/// The one place Excel files are written and read (ClosedXML). The Assets screens'
/// exports, import templates, uploads and error reports all go through here, so they
/// share the house header style, the ₹ money format, real Excel dates and the
/// upload limits.
/// </summary>
internal static class ExcelWorkbookHelper
{
    public const string ContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
    public const long MaxUploadBytes = 5 * 1024 * 1024;
    public const int MaxImportRows = 5_000;

    private const string MoneyFormat = "₹ #,##0.00";
    private const string DateFormat = "dd-mmm-yyyy";
    private const string DateTimeFormat = "dd-mmm-yyyy hh:mm";
    private static readonly XLColor HeaderFill = XLColor.FromHtml("#0B0B0D");
    private static readonly XLColor HeaderFont = XLColor.FromHtml("#C9A227");

    private static readonly string[] DateFormats =
    [
        "yyyy-MM-dd", "dd-MM-yyyy", "dd/MM/yyyy", "d/M/yyyy", "d-M-yyyy", "dd-MMM-yyyy",
        "d-MMM-yyyy", "dd MMM yyyy", "d MMM yyyy", "yyyy/MM/dd"
    ];

    /// <summary>screen_yyyy-MM-dd_HHmm.xlsx in IST, e.g. incoming_goods_2026-09-30_1430.xlsx.</summary>
    public static string FileName(string screen, DateTime utcNow) =>
        $"{screen}_{IstClock.ToIst(utcNow).ToString("yyyy-MM-dd_HHmm", CultureInfo.InvariantCulture)}.xlsx";

    // ── Writing ─────────────────────────────────────────────────────────────

    /// <summary>A one-sheet workbook with a styled, frozen, filterable header row.</summary>
    public static byte[] Write<T>(string sheetName, IReadOnlyList<ExcelColumn<T>> columns, IReadOnlyList<T> rows)
    {
        using var workbook = new XLWorkbook();
        AddSheet(workbook, sheetName, columns, rows);
        return Save(workbook);
    }

    public static IXLWorksheet AddSheet<T>(XLWorkbook workbook, string sheetName,
        IReadOnlyList<ExcelColumn<T>> columns, IReadOnlyList<T> rows)
    {
        var sheet = workbook.Worksheets.Add(sheetName);
        WriteHeader(sheet, columns.Select(c => c.Header).ToList());
        for (var r = 0; r < rows.Count; r++)
        {
            for (var c = 0; c < columns.Count; c++)
            {
                SetCell(sheet.Cell(r + 2, c + 1), columns[c].Value(rows[r]), columns[c].Kind);
            }
        }
        Finish(sheet, columns.Count, rows.Count);
        return sheet;
    }

    /// <summary>The import template: the headers, one example row and an Instructions sheet.</summary>
    public static byte[] Template(string sheetName, IReadOnlyList<ImportColumn> columns,
        IReadOnlyList<string> generalNotes)
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add(sheetName);
        WriteHeader(sheet, columns.Select(c => c.Header).ToList());
        for (var c = 0; c < columns.Count; c++)
        {
            SetCell(sheet.Cell(2, c + 1), columns[c].Example, columns[c].Kind);
            if (columns[c].Required)
            {
                sheet.Cell(1, c + 1).CreateComment().AddText("Required");
            }
        }
        Finish(sheet, columns.Count, 1);

        var help = workbook.Worksheets.Add("Instructions");
        help.Cell(1, 1).Value = "How to fill in this file";
        help.Cell(1, 1).Style.Font.SetBold().Font.SetFontSize(14);
        var line = 2;
        foreach (var note in generalNotes)
        {
            help.Cell(line++, 1).Value = "• " + note;
        }
        line++;
        string[] heads = ["Column", "Required", "Allowed values / format"];
        for (var c = 0; c < heads.Length; c++)
        {
            StyleHeader(help.Cell(line, c + 1), heads[c]);
        }
        foreach (var column in columns)
        {
            line++;
            help.Cell(line, 1).Value = column.Header;
            help.Cell(line, 2).Value = column.Required ? "Yes" : "No";
            help.Cell(line, 3).Value = column.Help;
        }
        help.Column(1).Width = 22;
        help.Column(2).Width = 10;
        help.Column(3).Width = 90;
        help.Column(3).Style.Alignment.WrapText = true;
        return Save(workbook);
    }

    /// <summary>The rejected rows exactly as uploaded, plus an Error column, so they can be
    /// fixed and uploaded again.</summary>
    public static byte[] ErrorReport(string sheetName, IReadOnlyList<ImportColumn> columns,
        IReadOnlyList<(ImportSheetRow Row, string Error)> failed)
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add(sheetName);
        var headers = columns.Select(c => c.Header).Append("Error").ToList();
        WriteHeader(sheet, headers);
        for (var r = 0; r < failed.Count; r++)
        {
            for (var c = 0; c < columns.Count; c++)
            {
                var raw = failed[r].Row.Raw(columns[c].Header);
                SetCell(sheet.Cell(r + 2, c + 1), raw, columns[c].Kind);
            }
            var error = sheet.Cell(r + 2, headers.Count);
            error.Value = failed[r].Error;
            error.Style.Font.SetFontColor(XLColor.FromHtml("#B42318"));
        }
        Finish(sheet, headers.Count, failed.Count);
        return Save(workbook);
    }

    private static void WriteHeader(IXLWorksheet sheet, IReadOnlyList<string> headers)
    {
        for (var c = 0; c < headers.Count; c++)
        {
            StyleHeader(sheet.Cell(1, c + 1), headers[c]);
        }
    }

    private static void StyleHeader(IXLCell cell, string text)
    {
        cell.Value = text;
        cell.Style.Font.SetBold();
        cell.Style.Fill.SetBackgroundColor(HeaderFill);
        cell.Style.Font.SetFontColor(HeaderFont);
    }

    private static void SetCell(IXLCell cell, object? value, ExcelCellKind kind)
    {
        switch (value)
        {
            case null:
                return;
            case string s:
                cell.Value = s;
                return;
            case DateOnly d:
                cell.Value = d.ToDateTime(TimeOnly.MinValue);
                cell.Style.DateFormat.Format = DateFormat;
                return;
            case DateTime dt:
                cell.Value = dt;
                cell.Style.DateFormat.Format = kind == ExcelCellKind.DateTime ? DateTimeFormat : DateFormat;
                return;
            case decimal m:
                cell.Value = m;
                if (kind == ExcelCellKind.Money) cell.Style.NumberFormat.Format = MoneyFormat;
                return;
            case double n:
                cell.Value = n;
                if (kind == ExcelCellKind.Money) cell.Style.NumberFormat.Format = MoneyFormat;
                return;
            case int i:
                cell.Value = i;
                return;
            case long l:
                cell.Value = l;
                return;
            case bool b:
                cell.Value = b;
                return;
            default:
                cell.Value = value.ToString();
                return;
        }
    }

    private static void Finish(IXLWorksheet sheet, int columnCount, int rowCount)
    {
        sheet.SheetView.FreezeRows(1);
        sheet.Range(1, 1, rowCount + 1, columnCount).SetAutoFilter();
        sheet.Columns(1, columnCount).AdjustToContents(1, Math.Min(rowCount + 1, 500));
        foreach (var column in sheet.Columns(1, columnCount))
        {
            if (column.Width > 60) column.Width = 60;
        }
    }

    private static byte[] Save(XLWorkbook workbook)
    {
        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    // ── Reading ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Opens an upload and runs the file-level checks: .xlsx only, at most 5 MB and
    /// 5,000 data rows, a header row carrying every required column, at least one data
    /// row. Any failure is a 422 with a message the screen shows as is. Blank rows are
    /// skipped; columns the import does not know are ignored and reported back.
    /// </summary>
    public static ImportSheet Read(Stream content, string fileName, long length, IReadOnlyList<ImportColumn> columns)
    {
        var extension = Path.GetExtension(fileName ?? "").ToLowerInvariant();
        if (extension == ".xls")
        {
            throw new BusinessRuleException(
                "Old .xls files are not supported. Open the file in Excel and save it as .xlsx, then upload it again.");
        }
        if (extension != ".xlsx")
        {
            throw new BusinessRuleException("Only Excel .xlsx files can be imported.");
        }
        if (length <= 0)
        {
            throw new BusinessRuleException("The file is empty.");
        }
        if (length > MaxUploadBytes)
        {
            throw new BusinessRuleException(
                $"The file is {length / 1024.0 / 1024.0:0.#} MB. The limit is {MaxUploadBytes / 1024 / 1024} MB.");
        }

        XLWorkbook workbook;
        try
        {
            workbook = new XLWorkbook(content);
        }
        catch (Exception)
        {
            throw new BusinessRuleException(
                "This file could not be read as an Excel workbook. Check that it is a real .xlsx file.");
        }

        using (workbook)
        {
            var sheet = workbook.Worksheets.FirstOrDefault(w => w.Visibility == XLWorksheetVisibility.Visible)
                        ?? workbook.Worksheets.First();
            var used = sheet.RangeUsed();
            if (used is null)
            {
                throw new BusinessRuleException("The file has no data. Fill in the template and upload it again.");
            }

            var lastColumn = used.LastColumn().ColumnNumber();
            var lastRow = used.LastRow().RowNumber();
            var known = columns.ToDictionary(c => Normalise(c.Header), c => c.Header);
            var map = new Dictionary<int, string>();
            var ignored = new List<string>();
            for (var c = 1; c <= lastColumn; c++)
            {
                var header = sheet.Cell(1, c).GetString().Trim();
                if (header.Length == 0) continue;
                if (known.TryGetValue(Normalise(header), out var canonical) && !map.ContainsValue(canonical))
                {
                    map[c] = canonical;
                }
                else
                {
                    ignored.Add(header);
                }
            }

            var missing = columns.Where(c => c.Required && !map.ContainsValue(c.Header))
                .Select(c => c.Header).ToList();
            if (missing.Count > 0)
            {
                throw new BusinessRuleException(
                    $"The header row is missing: {string.Join(", ", missing)}. " +
                    "Download the template to get the correct headers.");
            }

            var rows = new List<ImportSheetRow>();
            for (var r = 2; r <= lastRow; r++)
            {
                var values = new Dictionary<string, object?>();
                var any = false;
                foreach (var (col, header) in map)
                {
                    var value = CellValue(sheet.Cell(r, col));
                    if (value is string s && s.Trim().Length == 0) value = null;
                    values[header] = value;
                    any |= value is not null;
                }
                if (!any) continue;
                rows.Add(new ImportSheetRow(r, values));
                if (rows.Count > MaxImportRows)
                {
                    throw new BusinessRuleException(
                        $"The file has more than {MaxImportRows:N0} rows. Split it into smaller files.");
                }
            }
            if (rows.Count == 0)
            {
                throw new BusinessRuleException("The file has headers but no data rows.");
            }
            return new ImportSheet(rows, ignored);
        }
    }

    private static object? CellValue(IXLCell cell)
    {
        var value = cell.Value;
        return value.Type switch
        {
            XLDataType.Blank => null,
            XLDataType.Number => value.GetNumber(),
            XLDataType.DateTime => value.GetDateTime(),
            XLDataType.Boolean => value.GetBoolean(),
            XLDataType.Text => value.GetText(),
            XLDataType.TimeSpan => value.GetTimeSpan().ToString(),
            _ => cell.GetFormattedString()
        };
    }

    private static string Normalise(string header) =>
        new string(header.Where(char.IsLetterOrDigit).ToArray()).ToLowerInvariant();

    // ── Cell parsing, shared by every import ────────────────────────────────

    public static string? AsText(object? raw) => raw switch
    {
        null => null,
        string s => s.Trim().Length == 0 ? null : s.Trim(),
        double d => d.ToString("0.############", CultureInfo.InvariantCulture),
        DateTime dt => dt.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        bool b => b ? "TRUE" : "FALSE",
        _ => raw.ToString()?.Trim()
    };

    /// <summary>A number from a numeric cell or from text such as "₹ 1,250.50".</summary>
    public static bool TryDecimal(object? raw, out decimal value)
    {
        value = 0m;
        switch (raw)
        {
            case double d when !double.IsNaN(d) && !double.IsInfinity(d)
                               && Math.Abs(d) < (double)decimal.MaxValue:
                value = Math.Round((decimal)d, 6);
                return true;
            case string s:
                var cleaned = s.Replace("₹", "").Replace("Rs.", "").Replace("Rs", "").Replace(",", "").Trim();
                return decimal.TryParse(cleaned, NumberStyles.Number, CultureInfo.InvariantCulture, out value);
            default:
                return false;
        }
    }

    /// <summary>A date from a real Excel date, an Excel serial number, or text in
    /// yyyy-MM-dd, dd-MM-yyyy, dd/MM/yyyy or dd-MMM-yyyy.</summary>
    public static bool TryDate(object? raw, out DateOnly value)
    {
        value = default;
        switch (raw)
        {
            case DateTime dt:
                value = DateOnly.FromDateTime(dt);
                return true;
            case double serial when serial is > 1 and < 2_958_466:
                value = DateOnly.FromDateTime(DateTime.FromOADate(serial));
                return true;
            case string s:
                return DateOnly.TryParseExact(s.Trim(), DateFormats, CultureInfo.InvariantCulture,
                    DateTimeStyles.None, out value);
            default:
                return false;
        }
    }

    /// <summary>Digits after the decimal point, so 1.2345 can be refused for a 3-decimal column.</summary>
    public static int Scale(decimal value) => (decimal.GetBits(value)[3] >> 16) & 0xFF;
}
