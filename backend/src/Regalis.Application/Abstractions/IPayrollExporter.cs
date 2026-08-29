namespace Regalis.Application.Abstractions;

public record PayrollExportRow(
    long UserId,
    string FullName,
    string Email,
    byte SubjectType,
    DateOnly AttendanceDate,
    DateTime CheckInUtc,
    DateTime? CheckOutUtc,
    int? TotalMinutes,
    bool IsCorrected);

public record PayrollExportFile(byte[] Content, string ContentType, string FileName);

/// <summary>
/// The payroll hand-off seam (A03): the payroll product is unknown, so attendance
/// leaves the system as an export file — CSV in Phase 1, XLSX joins with the
/// reporting toolkit. No direct integration.
/// </summary>
public interface IPayrollExporter
{
    string Format { get; }

    PayrollExportFile Export(IReadOnlyList<PayrollExportRow> rows, DateOnly from, DateOnly to);
}
