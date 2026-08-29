using FluentValidation;
using Regalis.Shared.Api;

namespace Regalis.Application.Access;

public record AttendanceDto(
    long AttendanceId,
    long UserId,
    string PersonName,
    byte SubjectType,
    int LocationId,
    int? WorkspaceId,
    long? BookingId,
    DateOnly AttendanceDate,
    DateTime CheckInUtc,
    DateTime? CheckOutUtc,
    int? TotalMinutes,
    byte Status,
    byte CheckInMethod,
    bool IsCorrected,
    string? CorrectionReason,
    DateTime? OriginalCheckInUtc,
    DateTime? OriginalCheckOutUtc);

public class AttendanceListRequest : PagedRequest
{
    public DateOnly? Date { get; set; }
    public DateOnly? From { get; set; }
    public DateOnly? To { get; set; }
    public long? UserId { get; set; }
    public int? LocationId { get; set; }
}

public record CorrectAttendanceRequest(DateTime CheckInUtc, DateTime? CheckOutUtc, string Reason);

public record SelfCheckRequest(int? WorkspaceId);

public class CorrectAttendanceRequestValidator : AbstractValidator<CorrectAttendanceRequest>
{
    public CorrectAttendanceRequestValidator()
    {
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(500)
            .WithMessage("A reason is mandatory — corrections keep the original values.");
        RuleFor(x => x.CheckOutUtc).GreaterThan(x => x.CheckInUtc)
            .When(x => x.CheckOutUtc is not null)
            .WithMessage("Check-out must be after check-in.");
    }
}

public interface IAttendanceService
{
    /// <summary>Self check-in from My Work Status (AccessMethod App). Requires a
    /// live basis: a booking occupying now or a pass valid today.</summary>
    Task<AttendanceDto> SelfCheckInAsync(SelfCheckRequest request, CancellationToken ct = default);

    Task<AttendanceDto> SelfCheckOutAsync(CancellationToken ct = default);

    Task<PagedResult<AttendanceDto>> GetMyAsync(PagedRequest request, CancellationToken ct = default);

    /// <summary>The caller's open attendance row today, if any.</summary>
    Task<AttendanceDto?> GetMyOpenAsync(CancellationToken ct = default);

    Task<PagedResult<AttendanceDto>> GetAttendanceAsync(AttendanceListRequest request,
        CancellationToken ct = default);

    /// <summary>Correction keeps the original values (payroll stays auditable).</summary>
    Task<AttendanceDto> CorrectAsync(long attendanceId, CorrectAttendanceRequest request,
        CancellationToken ct = default);

    /// <summary>The payroll hand-off file (A03) via IPayrollExporter.</summary>
    Task<PayrollExportResult> ExportPayrollAsync(DateOnly from, DateOnly to, int? locationId,
        CancellationToken ct = default);
}

public record PayrollExportResult(byte[] Content, string ContentType, string FileName);
