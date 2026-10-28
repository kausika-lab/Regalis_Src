using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;
using Regalis.Application.Abstractions;
using Regalis.Application.Common.Exceptions;
using Regalis.Application.Finance;
using Regalis.Domain.Entities.Finance;
using Regalis.Domain.Enums;
using Regalis.Infrastructure.Persistence;
using Regalis.Shared;
using Regalis.Shared.Api;

namespace Regalis.Infrastructure.Services;

/// <summary>
/// The collector's drawer for one IST day at one location. Opening it records the float;
/// a receipt taken with no session open opens one with a zero float. Closing records the
/// counted cash against the expected cash (float + recorded cash receipts) and needs a
/// reason for any difference. An Admin approves the close and takes the cash
/// (HandedOver). UPI and card receipts are listed per session but reconciled against the
/// bank and card-settlement statements, not the drawer.
/// </summary>
public class CashRegisterService(
    RegalisDbContext db,
    INumberSequenceService sequences,
    ICurrentUser currentUser,
    IDateTimeProvider clock,
    ILogger<CashRegisterService> logger) : ICashRegisterService
{
    /// <summary>The user's open session at a location today; opens one (zero float) if
    /// there is none. Refuses when today's session is already closed. Runs inside the
    /// caller's transaction.</summary>
    internal static async Task<CashRegisterSession> OpenSessionForAsync(
        RegalisDbContext db, INumberSequenceService sequences, int locationId, long userId, DateTime nowUtc, CancellationToken ct)
    {
        DateOnly today = IstClock.IstDateOf(nowUtc);
        CashRegisterSession? session = await db.CashRegisterSessions
            .FirstOrDefaultAsync(s => s.LocationId == locationId && s.UserId == userId && s.BusinessDate == today, ct);
        if (session != null)
        {
            if (session.Status != CashSessionStatus.Open)
            {
                throw new BusinessRuleException(
                    $"Your register for {today:dd MMM yyyy} ({session.SessionNumber}) is closed. Ask an Admin, or have another colleague take the payment.");
            }
            return session;
        }

        session = new CashRegisterSession
        {
            SessionNumber = await sequences.NextAsync(SequenceKeys.CashSession, ct),
            LocationId = locationId,
            UserId = userId,
            BusinessDate = today,
            OpeningFloat = 0m,
            OpenedUtc = nowUtc,
            Status = CashSessionStatus.Open
        };
        db.CashRegisterSessions.Add(session);
        await db.SaveChangesAsync(ct);
        return session;
    }

    public async Task<CashRegisterSessionDto?> GetCurrentAsync(int locationId, CancellationToken ct = default)
    {
        DateOnly today = IstClock.IstDateOf(clock.UtcNow);
        long? id = await db.CashRegisterSessions.AsNoTracking()
            .Where(s => s.LocationId == locationId && s.UserId == currentUser.UserId && s.BusinessDate == today)
            .Select(s => (long?)s.CashRegisterSessionId)
            .FirstOrDefaultAsync(ct);
        return id.HasValue ? await GetAsync(id.Value, ct) : null;
    }

    public async Task<CashRegisterSessionDto> OpenAsync(OpenRegisterRequest request, CancellationToken ct = default)
    {
        long id = await db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            db.ChangeTracker.Clear();
            await using IDbContextTransaction transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
            DateTime nowUtc = clock.UtcNow;
            DateOnly today = IstClock.IstDateOf(nowUtc);
            if (await db.CashRegisterSessions.AnyAsync(s => s.LocationId == request.LocationId && s.UserId == currentUser.UserId && s.BusinessDate == today, ct))
            {
                throw new BusinessRuleException("You already have a register session at this location today.");
            }
            var session = new CashRegisterSession
            {
                SessionNumber = await sequences.NextAsync(SequenceKeys.CashSession, ct),
                LocationId = request.LocationId,
                UserId = currentUser.UserId,
                BusinessDate = today,
                OpeningFloat = request.OpeningFloat,
                OpenedUtc = nowUtc,
                Status = CashSessionStatus.Open
            };
            db.CashRegisterSessions.Add(session);
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            return session.CashRegisterSessionId;
        });
        return await GetAsync(id, ct);
    }

    public async Task<CashRegisterSessionDto> CloseAsync(long sessionId, CloseRegisterRequest request, CancellationToken ct = default)
    {
        await db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            db.ChangeTracker.Clear();
            await using IDbContextTransaction transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
            CashRegisterSession session = await db.CashRegisterSessions.FirstOrDefaultAsync(s => s.CashRegisterSessionId == sessionId, ct)
                ?? throw new NotFoundException("CashRegisterSession", sessionId);
            if (session.UserId != currentUser.UserId && !currentUser.HasPermission("PAYMENT.CASH_APPROVE"))
            {
                throw new ForbiddenException("Only the collector or an Admin can close this register.");
            }
            if (session.Status != CashSessionStatus.Open)
            {
                throw new BusinessRuleException($"{session.SessionNumber} is already closed.");
            }
            decimal cash = await CashTotalAsync(sessionId, ct);
            decimal expected = session.OpeningFloat + cash;
            string? reason = string.IsNullOrWhiteSpace(request.VarianceReason) ? null : request.VarianceReason.Trim();
            if (request.CountedCash != expected && reason == null)
            {
                throw new BusinessRuleException(
                    $"Counted ₹{request.CountedCash:N2} but ₹{expected:N2} was expected. Say why before closing.");
            }
            session.ExpectedCash = expected;
            session.CountedCash = request.CountedCash;
            session.VarianceReason = reason;
            session.Status = CashSessionStatus.Closed;
            session.ClosedUtc = clock.UtcNow;
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
        });
        logger.LogInformation("Register session {SessionId} closed by {UserId}", sessionId, currentUser.UserId);
        return await GetAsync(sessionId, ct);
    }

    public async Task<CashRegisterSessionDto> ApproveAsync(long sessionId, CancellationToken ct = default)
    {
        CashRegisterSession session = await db.CashRegisterSessions.FirstOrDefaultAsync(s => s.CashRegisterSessionId == sessionId, ct)
            ?? throw new NotFoundException("CashRegisterSession", sessionId);
        if (session.Status != CashSessionStatus.Closed)
        {
            throw new BusinessRuleException(session.Status == CashSessionStatus.Open
                ? $"{session.SessionNumber} must be closed and counted first."
                : $"{session.SessionNumber} is already approved.");
        }
        session.Status = CashSessionStatus.HandedOver;
        session.HandedOverToUserId = currentUser.UserId;
        session.HandedOverUtc = clock.UtcNow;
        await db.SaveChangesAsync(ct);
        logger.LogInformation("Register session {SessionId} approved by {UserId}", sessionId, currentUser.UserId);
        return await GetAsync(sessionId, ct);
    }

    public async Task<CashRegisterSessionDto> GetAsync(long sessionId, CancellationToken ct = default)
    {
        CashRegisterSessionDto? dto = (await ToDtosAsync(db.CashRegisterSessions.AsNoTracking()
            .Where(s => s.CashRegisterSessionId == sessionId), ct)).FirstOrDefault();
        return dto ?? throw new NotFoundException("CashRegisterSession", sessionId);
    }

    public async Task<PagedResult<CashRegisterSessionDto>> GetSessionsAsync(CashRegisterListRequest request, CancellationToken ct = default)
    {
        IQueryable<CashRegisterSession> query = db.CashRegisterSessions.AsNoTracking();
        if (request.LocationId.HasValue)
        {
            query = query.Where(s => s.LocationId == request.LocationId.Value);
        }
        if (request.UserId.HasValue)
        {
            query = query.Where(s => s.UserId == request.UserId.Value);
        }
        if (request.Status.HasValue)
        {
            var st = (CashSessionStatus)request.Status.Value;
            query = query.Where(s => s.Status == st);
        }
        if (request.From.HasValue)
        {
            query = query.Where(s => s.BusinessDate >= request.From.Value);
        }
        if (request.To.HasValue)
        {
            query = query.Where(s => s.BusinessDate <= request.To.Value);
        }
        int total = await query.CountAsync(ct);
        List<CashRegisterSessionDto> items = await ToDtosAsync(query
            .OrderByDescending(s => s.BusinessDate).ThenByDescending(s => s.CashRegisterSessionId)
            .Skip((request.Page - 1) * request.PageSize).Take(request.PageSize), ct);
        return PagedResult<CashRegisterSessionDto>.Create(items, request.Page, request.PageSize, total);
    }

    private Task<decimal> CashTotalAsync(long sessionId, CancellationToken ct)
    {
        return db.CashCollections
            .Where(c => c.CashRegisterSessionId == sessionId && c.Status == CashCollectionStatus.Recorded && c.PaymentMethod == PaymentMethod.Cash)
            .SumAsync(c => c.Amount, ct);
    }

    private async Task<List<CashRegisterSessionDto>> ToDtosAsync(IQueryable<CashRegisterSession> query, CancellationToken ct)
    {
        var sessions = await query.Select(s => new
        {
            Session = s,
            UserName = db.Users.Where(u => u.UserId == s.UserId).Select(u => u.FullName).FirstOrDefault() ?? "",
            ApproverName = db.Users.Where(u => u.UserId == s.HandedOverToUserId).Select(u => u.FullName).FirstOrDefault()
        }).ToListAsync(ct);
        List<long> ids = sessions.Select(x => x.Session.CashRegisterSessionId).ToList();
        var totals = await db.CashCollections.AsNoTracking()
            .Where(c => c.CashRegisterSessionId != null && ids.Contains(c.CashRegisterSessionId.Value) && c.Status == CashCollectionStatus.Recorded)
            .GroupBy(c => new { SessionId = c.CashRegisterSessionId!.Value, c.PaymentMethod })
            .Select(g => new { g.Key.SessionId, g.Key.PaymentMethod, Count = g.Count(), Amount = g.Sum(c => c.Amount) })
            .ToListAsync(ct);

        return sessions.Select(x =>
        {
            CashRegisterSession s = x.Session;
            var mine = totals.Where(t => t.SessionId == s.CashRegisterSessionId)
                .Select(t => new CashMethodTotalDto((byte)t.PaymentMethod, t.Count, t.Amount))
                .OrderBy(t => t.PaymentMethod == (byte)PaymentMethod.Cash ? 0 : t.PaymentMethod).ToList();
            decimal cash = mine.Where(t => t.PaymentMethod == (byte)PaymentMethod.Cash).Sum(t => t.Amount);
            return new CashRegisterSessionDto(
                s.CashRegisterSessionId, s.SessionNumber, s.LocationId, s.UserId, x.UserName, s.BusinessDate,
                s.OpeningFloat, s.OpenedUtc, cash, s.ExpectedCash ?? s.OpeningFloat + cash, s.CountedCash,
                s.Variance, s.VarianceReason, (byte)s.Status, s.ClosedUtc, s.HandedOverToUserId, x.ApproverName,
                s.HandedOverUtc, mine);
        }).ToList();
    }
}
