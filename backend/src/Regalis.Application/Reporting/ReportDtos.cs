namespace Regalis.Application.Reporting;

/// <summary>The common filter set every report accepts (API_PLAN §12). A report
/// simply ignores filters that do not apply to it.</summary>
public class ReportFilterRequest
{
    public DateOnly? From { get; set; }
    public DateOnly? To { get; set; }
    public int? LocationId { get; set; }
    public int? WorkspaceTypeId { get; set; }
    public int? WorkspaceId { get; set; }
    public long? CustomerId { get; set; }
    public byte? BookingStatus { get; set; }
    public byte? PaymentStatus { get; set; }
    /// <summary>Expense, inventory or bank-line category (document §15).</summary>
    public string? Category { get; set; }
    /// <summary>1 Wallet, 2 UPI, 3 Credit card, 4 Debit card, 5 Net banking, 6 Cash, 7 Other.</summary>
    public byte? PaymentMethod { get; set; }
}

/// <summary>Column type drives frontend rendering and export formatting:
/// string | number | money | percent | date | datetime.</summary>
public record ReportColumn(string Key, string Header, string Type);

public record ReportSummaryItem(string Label, string Value);

/// <summary>One tabular payload drives the grid, the Excel export and the PDF
/// export — a report is defined once, rendered three ways.</summary>
public record ReportResult(
    string Key,
    string Title,
    string Description,
    DateOnly From,
    DateOnly To,
    List<ReportColumn> Columns,
    List<Dictionary<string, object?>> Rows,
    List<ReportSummaryItem> Summary,
    int TotalRows,
    bool Truncated);

public record ReportInfo(string Key, string Title, string Description, bool IsAvailable);

public record ReportExportResult(byte[] Content, string ContentType, string FileName);

public interface IReportService
{
    /// <summary>The 16-report catalogue for the landing grid; not-yet-built reports
    /// are listed but flagged unavailable.</summary>
    List<ReportInfo> GetCatalogue();

    Task<ReportResult> GetAsync(string key, ReportFilterRequest filters, CancellationToken ct = default);

    /// <summary>format: xlsx | pdf.</summary>
    Task<ReportExportResult> ExportAsync(string key, string format, ReportFilterRequest filters,
        CancellationToken ct = default);
}
