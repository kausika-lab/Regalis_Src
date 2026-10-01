using Microsoft.EntityFrameworkCore;
using Regalis.Application.Abstractions;
using Regalis.Application.Access;
using Regalis.Application.Common.Exceptions;
using Regalis.Domain.Entities.Access;
using Regalis.Domain.Entities.Booking;
using Regalis.Domain.Enums;
using Regalis.Infrastructure.Persistence;
using Regalis.Shared;
using Regalis.Shared.Api;
using Regalis.Shared.Constants;

namespace Regalis.Infrastructure.Services;

/// <summary>
/// Shared attendance mutations — the gate (AccessService) and the app
/// (My Work Status) write attendance through the same code path.
/// </summary>
public class AttendanceRecorder(RegalisDbContext db, ISettingService settings, IDateTimeProvider clock)
{
    /// <summary>Null when entry is allowed; otherwise the documented deny reason.
    /// An open row means re-entry — allowed, no duplicate row.</summary>
    public async Task<string?> CheckEntryEligibilityAsync(long userId, CancellationToken ct)
    {
        var today = IstClock.IstDateOf(clock.UtcNow);
        var open = await FindOpenAsync(userId, ct);
        if (open is not null)
        {
            return null; // re-entry against the open session
        }
        var allowMultiple = await settings.GetBoolAsync(
            AppSettingKeys.Attendance.AllowMultipleCheckInPerDay, false, ct);
        if (!allowMultiple &&
            await db.Attendances.AnyAsync(a => a.UserId == userId && a.AttendanceDate == today, ct))
        {
            return DenyReasons.AlreadyCheckedOut;
        }
        return null;
    }

    /// <summary>Creates (or reuses) today's attendance row. Caller saves.</summary>
    public async Task<Attendance> RecordEntryAsync(long userId, UserType userType, int locationId,
        int? workspaceId, long? bookingId, AccessMethod method, long? accessLogId,
        CancellationToken ct)
    {
        var open = await FindOpenAsync(userId, ct);
        if (open is not null)
        {
            return open;
        }
        var nowUtc = clock.UtcNow;
        var attendance = new Attendance
        {
            UserId = userId,
            SubjectType = userType == UserType.Customer
                ? AttendanceSubjectType.Customer
                : AttendanceSubjectType.Staff,
            LocationId = locationId,
            WorkspaceId = workspaceId,
            BookingId = bookingId,
            AttendanceDate = IstClock.IstDateOf(nowUtc),
            CheckInUtc = nowUtc,
            Status = AttendanceStatus.CheckedIn,
            CheckInMethod = method,
            CheckInAccessLogId = accessLogId
        };
        db.Attendances.Add(attendance);
        return attendance;
    }

    /// <summary>Closes the open row; null when there is nothing to close. Caller saves.</summary>
    public async Task<Attendance?> RecordExitAsync(long userId, AccessMethod method,
        long? accessLogId, CancellationToken ct)
    {
        var open = await FindOpenAsync(userId, ct);
        if (open is null)
        {
            return null;
        }
        var nowUtc = clock.UtcNow;
        open.CheckOutUtc = nowUtc;
        open.TotalMinutes = (int)Math.Round((nowUtc - open.CheckInUtc).TotalMinutes);
        open.Status = AttendanceStatus.CheckedOut;
        open.CheckOutMethod = method;
        open.CheckOutAccessLogId = accessLogId;
        return open;
    }

    public Task<Attendance?> FindOpenAsync(long userId, CancellationToken ct) =>
        db.Attendances
            .Where(a => a.UserId == userId && a.Status == AttendanceStatus.CheckedIn)
            .OrderByDescending(a => a.AttendanceId)
            .FirstOrDefaultAsync(ct);
}

public class AttendanceService(
    RegalisDbContext db,
    AttendanceRecorder recorder,
    IPayrollExporter payrollExporter,
    ICurrentUser currentUser,
    IDateTimeProvider clock) : IAttendanceService
{
    public async Task<AttendanceDto> SelfCheckInAsync(SelfCheckRequest request,
        CancellationToken ct = default)
    {
        var nowUtc = clock.UtcNow;
        var todayIst = IstClock.IstDateOf(nowUtc);
        var userId = currentUser.UserId;

        // A live basis is required: a booking occupying now, or a pass valid today.
        var booking = await db.Bookings
            .Where(b => b.CustomerId == userId &&
                        (b.Status == BookingStatus.Confirmed || b.Status == BookingStatus.CheckedIn) &&
                        b.StartUtc <= nowUtc.AddMinutes(15) && nowUtc < b.EndUtc)
            .OrderBy(b => b.StartUtc)
            .FirstOrDefaultAsync(ct);
        var pass = booking is null
            ? await db.CustomerPasses.FirstOrDefaultAsync(p =>
                p.CustomerId == userId && p.Status == CustomerPassStatus.Active &&
                p.ValidFrom <= todayIst && todayIst <= p.ValidTo, ct)
            : null;
        if (booking is null && pass is null && currentUser.UserType == UserType.Customer)
        {
            throw new BusinessRuleException(
                "Check-in needs a booking running now or a pass valid today.");
        }

        var deny = await recorder.CheckEntryEligibilityAsync(userId, ct);
        if (deny is not null)
        {
            throw new BusinessRuleException("You have already completed today's session.");
        }

        var locationId = booking?.LocationId ?? pass?.LocationId
            ?? await db.Locations.Where(l => l.IsDefault).Select(l => l.LocationId).FirstAsync(ct);
        var attendance = await recorder.RecordEntryAsync(userId, currentUser.UserType!.Value,
            locationId, request.WorkspaceId ?? booking?.WorkspaceId, booking?.BookingId,
            AccessMethod.App, accessLogId: null, ct);

        if (booking is { Status: BookingStatus.Confirmed })
        {
            TransitionBooking(booking, BookingStatus.CheckedIn, nowUtc, "Self check-in from the app.");
            booking.CheckInUtc = nowUtc;
        }
        await db.SaveChangesAsync(ct);
        return await ToDtoAsync(attendance.AttendanceId, ct);
    }

    public async Task<AttendanceDto> SelfCheckOutAsync(CancellationToken ct = default)
    {
        var nowUtc = clock.UtcNow;
        var attendance = await recorder.RecordExitAsync(currentUser.UserId, AccessMethod.App, null, ct)
            ?? throw new BusinessRuleException("You are not checked in.");

        if (attendance.BookingId is not null)
        {
            var booking = await db.Bookings.FirstOrDefaultAsync(
                b => b.BookingId == attendance.BookingId, ct);
            if (booking is { Status: BookingStatus.CheckedIn, BillingMonths: not null } && nowUtc < booking.EndUtc)
            {
                // A monthly term runs on: checking out for the day does not free the workspace.
                booking.CheckOutUtc = nowUtc;
            }
            else if (booking is { Status: BookingStatus.CheckedIn })
            {
                TransitionBooking(booking, BookingStatus.Completed, nowUtc, "Self check-out from the app.");
                booking.CheckOutUtc = nowUtc;
                await AdminAlerts.BookingAsync(db, booking, "Booking completed", nowUtc, ct,
                    "Self check-out from the app.");
            }
        }
        await db.SaveChangesAsync(ct);
        return await ToDtoAsync(attendance.AttendanceId, ct);
    }

    public async Task<PagedResult<AttendanceDto>> GetMyAsync(PagedRequest request,
        CancellationToken ct = default) =>
        await PageAsync(db.Attendances.AsNoTracking()
            .Where(a => a.UserId == currentUser.UserId), request, ct);

    public async Task<AttendanceDto?> GetMyOpenAsync(CancellationToken ct = default)
    {
        var open = await recorder.FindOpenAsync(currentUser.UserId, ct);
        return open is null ? null : await ToDtoAsync(open.AttendanceId, ct);
    }

    public async Task<PagedResult<AttendanceDto>> GetAttendanceAsync(AttendanceListRequest request,
        CancellationToken ct = default)
    {
        var query = db.Attendances.AsNoTracking();
        if (request.Date is not null)
        {
            query = query.Where(a => a.AttendanceDate == request.Date);
        }
        if (request.From is not null)
        {
            query = query.Where(a => a.AttendanceDate >= request.From);
        }
        if (request.To is not null)
        {
            query = query.Where(a => a.AttendanceDate <= request.To);
        }
        if (request.UserId is not null)
        {
            query = query.Where(a => a.UserId == request.UserId);
        }
        if (request.LocationId is not null)
        {
            query = query.Where(a => a.LocationId == request.LocationId);
        }
        return await PageAsync(query, request, ct);
    }

    public async Task<AttendanceDto> CorrectAsync(long attendanceId, CorrectAttendanceRequest request,
        CancellationToken ct = default)
    {
        var attendance = await db.Attendances
            .FirstOrDefaultAsync(a => a.AttendanceId == attendanceId, ct)
            ?? throw new NotFoundException("Attendance", attendanceId);

        // The first correction snapshots the originals; later ones keep them.
        attendance.OriginalCheckInUtc ??= attendance.CheckInUtc;
        attendance.OriginalCheckOutUtc ??= attendance.CheckOutUtc;
        attendance.CheckInUtc = request.CheckInUtc;
        attendance.CheckOutUtc = request.CheckOutUtc;
        attendance.TotalMinutes = request.CheckOutUtc is null
            ? null
            : (int)Math.Round((request.CheckOutUtc.Value - request.CheckInUtc).TotalMinutes);
        attendance.Status = request.CheckOutUtc is null
            ? AttendanceStatus.CheckedIn
            : AttendanceStatus.CheckedOut;
        attendance.IsCorrected = true;
        attendance.CorrectedByUserId = currentUser.UserId;
        attendance.CorrectionReason = request.Reason.Trim();
        await db.SaveChangesAsync(ct);
        return await ToDtoAsync(attendanceId, ct);
    }

    public async Task<PayrollExportResult> ExportPayrollAsync(DateOnly from, DateOnly to,
        int? locationId, CancellationToken ct = default)
    {
        var query = db.Attendances.AsNoTracking()
            .Where(a => a.AttendanceDate >= from && a.AttendanceDate <= to);
        if (locationId is not null)
        {
            query = query.Where(a => a.LocationId == locationId);
        }
        var rows = await query
            .OrderBy(a => a.AttendanceDate).ThenBy(a => a.UserId)
            .Join(db.Users, a => a.UserId, u => u.UserId, (a, u) => new PayrollExportRow(
                a.UserId, u.FullName, u.Email, (byte)a.SubjectType, a.AttendanceDate,
                a.CheckInUtc, a.CheckOutUtc, a.TotalMinutes, a.IsCorrected))
            .ToListAsync(ct);

        var file = payrollExporter.Export(rows, from, to);
        return new PayrollExportResult(file.Content, file.ContentType, file.FileName);
    }

    private void TransitionBooking(Domain.Entities.Booking.Booking booking, BookingStatus to,
        DateTime nowUtc, string reason)
    {
        var from = booking.Status;
        booking.Status = to;
        booking.StatusHistory.Add(new BookingStatusHistory
        {
            BookingId = booking.BookingId,
            FromStatus = from,
            ToStatus = to,
            ChangedByUserId = currentUser.UserId,
            ChangedUtc = nowUtc,
            Reason = reason
        });
    }

    private async Task<PagedResult<AttendanceDto>> PageAsync(IQueryable<Attendance> query,
        PagedRequest request, CancellationToken ct)
    {
        var totalCount = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(a => a.AttendanceId)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .Join(db.Users, a => a.UserId, u => u.UserId, (a, u) => new { a, u.FullName })
            .Select(x => ToDto(x.a, x.FullName))
            .ToListAsync(ct);
        return PagedResult<AttendanceDto>.Create(items, request.Page, request.PageSize, totalCount);
    }

    private async Task<AttendanceDto> ToDtoAsync(long attendanceId, CancellationToken ct)
    {
        var row = await db.Attendances.AsNoTracking()
            .Where(a => a.AttendanceId == attendanceId)
            .Join(db.Users, a => a.UserId, u => u.UserId, (a, u) => new { a, u.FullName })
            .FirstAsync(ct);
        return ToDto(row.a, row.FullName);
    }

    private static AttendanceDto ToDto(Attendance a, string personName) => new(
        a.AttendanceId, a.UserId, personName, (byte)a.SubjectType, a.LocationId, a.WorkspaceId,
        a.BookingId, a.AttendanceDate, a.CheckInUtc, a.CheckOutUtc, a.TotalMinutes,
        (byte)a.Status, (byte)a.CheckInMethod, a.IsCorrected, a.CorrectionReason,
        a.OriginalCheckInUtc, a.OriginalCheckOutUtc);
}
