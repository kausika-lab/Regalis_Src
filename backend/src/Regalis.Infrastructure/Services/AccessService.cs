using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Regalis.Application.Abstractions;
using Regalis.Application.Access;
using Regalis.Application.Common.Exceptions;
using Regalis.Domain.Entities.Access;
using Regalis.Domain.Entities.Booking;
using Regalis.Domain.Entities.Identity;
using Regalis.Domain.Enums;
using Regalis.Infrastructure.Persistence;
using Regalis.Infrastructure.Security;
using Regalis.Shared.Api;
using Regalis.Shared.Constants;

namespace Regalis.Infrastructure.Services;

/// <summary>
/// The access engine (M16). All rules are evaluated HERE against the database —
/// the device adapter only opens doors. Every attempt, granted or denied, writes a
/// TRN_AccessLog row. A device failure is DeviceError, never a grant; the only path
/// to a grant while hardware is down is the ACCESS.OVERRIDE manual fallback, which
/// writes an exception log row and an audit entry (Invariant 9, A10).
/// </summary>
public class AccessService(
    RegalisDbContext db,
    IAccessControlAdapter accessControlAdapter,
    AttendanceRecorder attendanceRecorder,
    ICurrentUser currentUser,
    IDateTimeProvider clock,
    ISettingService settings,
    IOptions<JwtOptions> jwtOptions,
    ILogger<AccessService> logger) : IAccessService
{
    public async Task<AccessDecisionDto> ValidateAsync(ValidateAccessRequest request,
        CancellationToken ct = default)
    {
        var nowUtc = clock.UtcNow;
        var direction = (AccessDirection)request.Direction;
        var method = (AccessMethod)request.AccessMethod;
        var (tokenValue, signatureValid) = ParsePayload(request.Payload);

        AccessToken? token = null;
        string? denyReason = null;
        if (!signatureValid)
        {
            denyReason = DenyReasons.TokenNotFound;
        }
        else
        {
            token = await db.AccessTokens.FirstOrDefaultAsync(t => t.TokenValue == tokenValue, ct);
            denyReason = await EvaluateTokenRulesAsync(token, request.WorkspaceId, direction, nowUtc, ct);
        }

        var user = token is null
            ? null
            : await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.UserId == token.UserId, ct);
        var locationId = token?.LocationId
            ?? await db.Locations.Where(l => l.IsDefault).Select(l => l.LocationId).FirstAsync(ct);

        // Rules passed — now the hardware. Offline is DeviceError, never a grant.
        var result = AccessResult.Denied;
        string? deviceStatus = null;
        if (denyReason is null)
        {
            var device = await accessControlAdapter.RequestDoorOpenAsync(request.DeviceId, locationId, ct);
            deviceStatus = device.DeviceStatus;
            if (!device.IsDeviceOnline)
            {
                result = AccessResult.DeviceError;
                denyReason = DenyReasons.DeviceOffline;
            }
            else
            {
                result = AccessResult.Granted;
            }
        }

        var log = new AccessLog
        {
            AccessTokenId = token?.AccessTokenId,
            UserId = token?.UserId,
            ScannedTokenValue = Truncate(tokenValue, 64),
            WorkspaceId = request.WorkspaceId ?? token?.WorkspaceId,
            LocationId = locationId,
            Direction = direction,
            AccessMethod = method,
            Result = result,
            DenyReason = denyReason,
            DeviceId = request.DeviceId,
            DeviceStatus = deviceStatus,
            IpAddress = currentUser.IpAddress,
            LoggedUtc = nowUtc
        };
        db.AccessLogs.Add(log);
        await db.SaveChangesAsync(ct);

        string? reference = null;
        if (result == AccessResult.Granted && token is not null && user is not null)
        {
            reference = await ApplyGrantSideEffectsAsync(token, user, direction, method, log, nowUtc, ct);
        }

        logger.LogInformation("Access {Result} ({Direction}) for token {Token}: {Reason}",
            result, direction, Truncate(tokenValue, 12), denyReason ?? "OK");

        return new AccessDecisionDto(
            result == AccessResult.Granted, (byte)result, denyReason, log.AccessLogId,
            user?.FullName, reference, token?.ValidToUtc);
    }

    public async Task<AccessDecisionDto> ManualOverrideAsync(ManualOverrideRequest request,
        CancellationToken ct = default)
    {
        var nowUtc = clock.UtcNow;
        var direction = (AccessDirection)request.Direction;
        var user = await db.Users.AsNoTracking()
            .FirstOrDefaultAsync(u => u.UserId == request.UserId, ct)
            ?? throw new NotFoundException("User", request.UserId);
        // Pay later: no override into a booking that still owes money.
        if (direction == AccessDirection.In && request.BookingId is not null)
        {
            var pay = await db.Bookings
                .Where(b => b.BookingId == request.BookingId.Value)
                .Select(b => new { b.BookingNumber, b.IsPayLater, b.TotalAmount, b.AmountPaid })
                .FirstOrDefaultAsync(ct);
            if (pay is not null && pay.IsPayLater && pay.AmountPaid < pay.TotalAmount &&
                await settings.GetBoolAsync(PayLaterPolicy.BlockCheckInUntilPaid, true, ct))
            {
                throw new BusinessRuleException(
                    $"₹{pay.TotalAmount - pay.AmountPaid:N2} is still due on {pay.BookingNumber}. Record the payment under Cash Collections first.",
                    "PAYMENT_DUE");
            }
        }

        var log = new AccessLog
        {
            UserId = user.UserId,
            WorkspaceId = request.WorkspaceId,
            LocationId = request.LocationId,
            Direction = direction,
            AccessMethod = AccessMethod.Manual,
            Result = AccessResult.Granted,
            IsManualOverride = true,
            OverrideByUserId = currentUser.UserId,
            OverrideReason = request.Reason.Trim(),
            IpAddress = currentUser.IpAddress,
            LoggedUtc = nowUtc
        };
        db.AccessLogs.Add(log);

        // The override is a distinct, separately audited privilege (matrix §2).
        db.AuditLogs.Add(new AuditLog
        {
            UserId = currentUser.UserId,
            UserName = currentUser.FullName,
            Action = AuditActions.AccessOverride,
            ModuleName = "Access",
            EntityName = "User",
            EntityId = user.UserId.ToString(),
            NewValue = $"{{\"direction\":\"{direction}\",\"reason\":\"{request.Reason.Trim()}\"}}",
            IpAddress = currentUser.IpAddress,
            UserAgent = currentUser.UserAgent,
            CorrelationId = currentUser.CorrelationId,
            TimestampUtc = nowUtc
        });
        await db.SaveChangesAsync(ct);

        string? reference = null;
        if (direction == AccessDirection.In)
        {
            var attendance = await attendanceRecorder.RecordEntryAsync(user.UserId, user.UserType,
                request.LocationId, request.WorkspaceId, request.BookingId, AccessMethod.Manual,
                log.AccessLogId, ct);
            _ = attendance;
        }
        else
        {
            await attendanceRecorder.RecordExitAsync(user.UserId, AccessMethod.Manual,
                log.AccessLogId, ct);
        }
        if (request.BookingId is not null)
        {
            reference = await TransitionBookingForAccessAsync(request.BookingId.Value, direction, nowUtc, ct);
        }
        await db.SaveChangesAsync(ct);

        logger.LogWarning("MANUAL OVERRIDE: {Admin} granted {Direction} to user {UserId} — {Reason}",
            currentUser.UserId, direction, user.UserId, request.Reason);

        return new AccessDecisionDto(true, (byte)AccessResult.Granted, null, log.AccessLogId,
            user.FullName, reference, null);
    }

    public async Task<List<MyAccessTokenDto>> GetMyTokensAsync(CancellationToken ct = default)
    {
        var tokens = await db.AccessTokens.AsNoTracking()
            .Where(t => t.UserId == currentUser.UserId && t.Status == AccessTokenStatus.Active)
            .OrderBy(t => t.ValidFromUtc)
            .ToListAsync(ct);

        var bookingIds = tokens.Where(t => t.BookingId != null).Select(t => t.BookingId!.Value).ToList();
        var passIds = tokens.Where(t => t.CustomerPassId != null).Select(t => t.CustomerPassId!.Value).ToList();
        var workspaceIds = tokens.Where(t => t.WorkspaceId != null).Select(t => t.WorkspaceId!.Value).ToList();

        var bookings = await db.Bookings.AsNoTracking()
            .Where(b => bookingIds.Contains(b.BookingId))
            .ToDictionaryAsync(b => b.BookingId, b => b.BookingNumber, ct);
        var passes = await db.CustomerPasses.AsNoTracking()
            .Where(p => passIds.Contains(p.CustomerPassId))
            .ToDictionaryAsync(p => p.CustomerPassId, p => p.PassNumber, ct);
        var workspaces = await db.Workspaces.AsNoTracking()
            .Where(w => workspaceIds.Contains(w.WorkspaceId))
            .ToDictionaryAsync(w => w.WorkspaceId, w => w.WorkspaceName, ct);

        return tokens.Select(t => new MyAccessTokenDto(
                t.AccessTokenId, t.QrPayload, (byte)t.TokenPurpose,
                t.BookingId is not null && bookings.TryGetValue(t.BookingId.Value, out var bn) ? bn
                    : t.CustomerPassId is not null && passes.TryGetValue(t.CustomerPassId.Value, out var pn) ? pn
                    : "Access pass",
                t.WorkspaceId is not null && workspaces.TryGetValue(t.WorkspaceId.Value, out var wn) ? wn : null,
                t.ValidFromUtc, t.ValidToUtc, (byte)t.Status))
            .ToList();
    }

    public async Task<PagedResult<AccessLogDto>> GetLogsAsync(AccessLogListRequest request,
        CancellationToken ct = default)
    {
        var query = db.AccessLogs.AsNoTracking();
        if (request.Result is not null)
        {
            query = query.Where(l => (byte)l.Result == request.Result);
        }
        if (request.UserId is not null)
        {
            query = query.Where(l => l.UserId == request.UserId);
        }
        if (request.From is not null)
        {
            var fromUtc = Shared.IstClock.ToUtc(request.From.Value.ToDateTime(TimeOnly.MinValue));
            query = query.Where(l => l.LoggedUtc >= fromUtc);
        }
        if (request.To is not null)
        {
            var toUtc = Shared.IstClock.ToUtc(request.To.Value.AddDays(1).ToDateTime(TimeOnly.MinValue));
            query = query.Where(l => l.LoggedUtc < toUtc);
        }

        var totalCount = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(l => l.AccessLogId)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .GroupJoin(db.Users, l => l.UserId, u => (long?)u.UserId, (l, users) => new { l, users })
            .SelectMany(x => x.users.DefaultIfEmpty(), (x, u) => new AccessLogDto(
                x.l.AccessLogId, x.l.UserId, u == null ? null : u.FullName, x.l.WorkspaceId,
                x.l.LocationId, (byte)x.l.Direction, (byte)x.l.AccessMethod, (byte)x.l.Result,
                x.l.DenyReason, x.l.DeviceId, x.l.IsManualOverride, x.l.OverrideReason, x.l.LoggedUtc))
            .ToListAsync(ct);
        return PagedResult<AccessLogDto>.Create(items, request.Page, request.PageSize, totalCount);
    }

    public async Task RevokeTokenAsync(long accessTokenId, CancellationToken ct = default)
    {
        var token = await db.AccessTokens
            .FirstOrDefaultAsync(t => t.AccessTokenId == accessTokenId, ct)
            ?? throw new NotFoundException("AccessToken", accessTokenId);
        if (token.Status != AccessTokenStatus.Active)
        {
            throw new BusinessRuleException("Only active tokens can be revoked.");
        }
        token.Status = AccessTokenStatus.Revoked;
        token.RevokedReason = $"Revoked by admin {currentUser.UserId}";
        await db.SaveChangesAsync(ct);
    }

    // ── Rule evaluation ─────────────────────────────────────────────────────

    private async Task<string?> EvaluateTokenRulesAsync(AccessToken? token, int? workspaceId,
        AccessDirection direction, DateTime nowUtc, CancellationToken ct)
    {
        if (token is null)
        {
            return DenyReasons.TokenNotFound;
        }
        if (token.Status == AccessTokenStatus.Revoked)
        {
            return DenyReasons.TokenRevoked;
        }
        if (token.Status == AccessTokenStatus.Expired ||
            nowUtc < token.ValidFromUtc || nowUtc >= token.ValidToUtc)
        {
            if (token.Status == AccessTokenStatus.Active && nowUtc >= token.ValidToUtc)
            {
                token.Status = AccessTokenStatus.Expired;
            }
            return DenyReasons.OutsideValidWindow;
        }
        if (token.MaxUsageCount is not null && token.UsageCount >= token.MaxUsageCount)
        {
            return DenyReasons.UsageExceeded;
        }
        if (token.BookingId is not null)
        {
            var bookingStatus = await db.Bookings
                .Where(b => b.BookingId == token.BookingId)
                .Select(b => b.Status)
                .FirstOrDefaultAsync(ct);
            if (bookingStatus is BookingStatus.Cancelled or BookingStatus.Expired)
            {
                return DenyReasons.BookingCancelled;
            }
            if (workspaceId is not null && token.WorkspaceId is not null &&
                workspaceId != token.WorkspaceId)
            {
                return DenyReasons.WrongWorkspace;
            }
            if (direction == AccessDirection.In)
            {
                var pay = await db.Bookings
                    .Where(b => b.BookingId == token.BookingId)
                    .Select(b => new { b.IsPayLater, b.TotalAmount, b.AmountPaid })
                    .FirstOrDefaultAsync(ct);
                if (pay is not null && pay.IsPayLater && pay.AmountPaid < pay.TotalAmount &&
                    await settings.GetBoolAsync(PayLaterPolicy.BlockCheckInUntilPaid, true, ct))
                {
                    return DenyReasons.PaymentDue;
                }
            }
        }
        // Pay later: a day pass or membership that still owes money does not open the gate.
        if (token.CustomerPassId is not null && direction == AccessDirection.In)
        {
            var passPay = await db.CustomerPasses
                .Where(p => p.CustomerPassId == token.CustomerPassId)
                .Select(p => new { p.IsPayLater, p.TotalAmount, p.AmountPaid })
                .FirstOrDefaultAsync(ct);
            if (passPay is not null && passPay.IsPayLater && passPay.AmountPaid < passPay.TotalAmount &&
                await settings.GetBoolAsync(PayLaterPolicy.BlockCheckInUntilPaid, true, ct))
            {
                return DenyReasons.PaymentDue;
            }
        }
        // Attendance gate: exiting needs an open session; a finished day blocks
        // re-entry while multiple check-ins are disabled.
        if (direction == AccessDirection.Out)
        {
            var open = await attendanceRecorder.FindOpenAsync(token.UserId, ct);
            if (open is null)
            {
                return DenyReasons.AlreadyCheckedOut;
            }
        }
        else
        {
            var deny = await attendanceRecorder.CheckEntryEligibilityAsync(token.UserId, ct);
            if (deny is not null)
            {
                return deny;
            }
        }
        return null;
    }

    /// <summary>Token usage, attendance row and booking transition after a grant.</summary>
    private async Task<string?> ApplyGrantSideEffectsAsync(AccessToken token, User user,
        AccessDirection direction, AccessMethod method, AccessLog log, DateTime nowUtc,
        CancellationToken ct)
    {
        token.UsageCount++;
        token.FirstUsedUtc ??= nowUtc;
        token.LastUsedUtc = nowUtc;

        if (direction == AccessDirection.In)
        {
            await attendanceRecorder.RecordEntryAsync(user.UserId, user.UserType, token.LocationId,
                token.WorkspaceId, token.BookingId, method, log.AccessLogId, ct);
        }
        else
        {
            await attendanceRecorder.RecordExitAsync(user.UserId, method, log.AccessLogId, ct);
        }

        string? reference = null;
        if (token.BookingId is not null)
        {
            reference = await TransitionBookingForAccessAsync(token.BookingId.Value, direction, nowUtc, ct);
        }
        await db.SaveChangesAsync(ct);
        return reference;
    }

    private async Task<string?> TransitionBookingForAccessAsync(long bookingId,
        AccessDirection direction, DateTime nowUtc, CancellationToken ct)
    {
        var booking = await db.Bookings.FirstOrDefaultAsync(b => b.BookingId == bookingId, ct);
        if (booking is null)
        {
            return null;
        }
        if (direction == AccessDirection.In && booking.Status == BookingStatus.Confirmed)
        {
            booking.Status = BookingStatus.CheckedIn;
            booking.CheckInUtc = nowUtc;
            AddHistory(booking, BookingStatus.Confirmed, BookingStatus.CheckedIn, nowUtc, "Checked in at the gate.");
        }
        else if (direction == AccessDirection.Out && booking.Status == BookingStatus.CheckedIn
                 && booking.BillingMonths is not null && nowUtc < booking.EndUtc)
        {
            // A monthly desk or cabin stays the member's until the term ends; leaving for
            // the day records the exit but does not complete (and free) the booking.
            booking.CheckOutUtc = nowUtc;
        }
        else if (direction == AccessDirection.Out && booking.Status == BookingStatus.CheckedIn)
        {
            booking.Status = BookingStatus.Completed;
            booking.CheckOutUtc = nowUtc;
            AddHistory(booking, BookingStatus.CheckedIn, BookingStatus.Completed, nowUtc, "Checked out at the gate.");
            await AdminAlerts.BookingAsync(db, booking, "Booking completed", nowUtc, ct,
                "Checked out at the gate.");
        }
        return booking.BookingNumber;
    }

    private void AddHistory(Booking booking, BookingStatus from, BookingStatus to,
        DateTime nowUtc, string reason) =>
        booking.StatusHistory.Add(new BookingStatusHistory
        {
            BookingId = booking.BookingId,
            FromStatus = from,
            ToStatus = to,
            ChangedByUserId = currentUser.IsAuthenticated ? currentUser.UserId : 0,
            ChangedUtc = nowUtc,
            Reason = reason
        });

    /// <summary>Accepts the raw token value or the signed RGL1 payload; a payload
    /// with a bad HMAC never reaches the database lookup.</summary>
    private (string TokenValue, bool SignatureValid) ParsePayload(string payload)
    {
        var trimmed = payload.Trim();
        if (!trimmed.StartsWith("RGL1.", StringComparison.Ordinal))
        {
            // Raw token (card serial path) — 64 hex chars.
            return (Truncate(trimmed, 64), trimmed.Length == 64);
        }
        var parts = trimmed.Split('.');
        if (parts.Length != 3)
        {
            return (Truncate(trimmed, 64), false);
        }
        var expected = Convert.ToBase64String(HMACSHA256.HashData(
                Encoding.UTF8.GetBytes(jwtOptions.Value.SigningKey),
                Encoding.UTF8.GetBytes(parts[1])))
            .TrimEnd('=').Replace('+', '-').Replace('/', '_');
        var valid = CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(expected), Encoding.UTF8.GetBytes(parts[2]));
        return (parts[1], valid);
    }

    private static string Truncate(string value, int maxLength) =>
        value.Length <= maxLength ? value : value[..maxLength];
}
