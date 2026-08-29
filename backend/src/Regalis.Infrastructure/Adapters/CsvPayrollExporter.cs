using System.Text;
using Regalis.Application.Abstractions;
using Regalis.Shared;

namespace Regalis.Infrastructure.Adapters;

/// <summary>CSV payroll hand-off (Payroll:Format = "Csv", A03). Times are exported
/// in IST because the payroll team works in local time; the UTC source stays in
/// TRN_Attendance untouched.</summary>
public class CsvPayrollExporter : IPayrollExporter
{
    public string Format => "Csv";

    public PayrollExportFile Export(IReadOnlyList<PayrollExportRow> rows, DateOnly from, DateOnly to)
    {
        var csv = new StringBuilder();
        csv.AppendLine("UserId,FullName,Email,SubjectType,Date,CheckInIST,CheckOutIST,TotalMinutes,Corrected");
        foreach (var row in rows)
        {
            csv.Append(row.UserId).Append(',')
                .Append(Escape(row.FullName)).Append(',')
                .Append(Escape(row.Email)).Append(',')
                .Append(row.SubjectType == 2 ? "Staff" : "Customer").Append(',')
                .Append(row.AttendanceDate.ToString("yyyy-MM-dd")).Append(',')
                .Append(IstClock.ToIst(row.CheckInUtc).ToString("yyyy-MM-dd HH:mm")).Append(',')
                .Append(row.CheckOutUtc is null
                    ? ""
                    : IstClock.ToIst(row.CheckOutUtc.Value).ToString("yyyy-MM-dd HH:mm")).Append(',')
                .Append(row.TotalMinutes?.ToString() ?? "").Append(',')
                .Append(row.IsCorrected ? "Yes" : "No")
                .AppendLine();
        }
        return new PayrollExportFile(
            Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(csv.ToString())).ToArray(),
            "text/csv",
            $"payroll_{from:yyyyMMdd}_{to:yyyyMMdd}.csv");
    }

    private static string Escape(string value) =>
        value.Contains(',') || value.Contains('"')
            ? $"\"{value.Replace("\"", "\"\"")}\""
            : value;
}
