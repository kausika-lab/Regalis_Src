using System.Text;
using Microsoft.EntityFrameworkCore;
using Regalis.Application.Abstractions;
using Regalis.Application.Administration;
using Regalis.Domain.Entities.Identity;
using Regalis.Infrastructure.Persistence;
using Regalis.Shared;
using Regalis.Shared.Api;

namespace Regalis.Infrastructure.Services;

/// <summary>The audit trail screen (M25 slice). TRN_AuditLog is append-only and
/// written by the save-changes interceptor and the explicit exception paths —
/// this service only ever reads it.</summary>
public class AuditService(RegalisDbContext db, IDateTimeProvider clock) : IAuditService
{
    public async Task<PagedResult<AuditLogDto>> GetLogsAsync(AuditListRequest request,
        CancellationToken ct = default)
    {
        var query = Filter(request);
        var totalCount = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(l => l.AuditLogId)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(l => ToDto(l))
            .ToListAsync(ct);
        return PagedResult<AuditLogDto>.Create(items, request.Page, request.PageSize, totalCount);
    }

    public async Task<List<string>> GetModulesAsync(CancellationToken ct = default) =>
        await db.AuditLogs.AsNoTracking()
            .Select(l => l.ModuleName)
            .Distinct()
            .OrderBy(m => m)
            .ToListAsync(ct);

    public async Task<AuditExportResult> ExportAsync(AuditListRequest request,
        CancellationToken ct = default)
    {
        var rows = await Filter(request)
            .OrderBy(l => l.AuditLogId)
            .Take(10_000)
            .Select(l => ToDto(l))
            .ToListAsync(ct);

        var csv = new StringBuilder();
        csv.AppendLine("TimestampIST,User,Action,Module,Entity,EntityId,OldValue,NewValue,Ip,CorrelationId");
        foreach (var r in rows)
        {
            csv.Append(IstClock.ToIst(r.TimestampUtc).ToString("yyyy-MM-dd HH:mm:ss")).Append(',')
                .Append(Escape(r.UserName ?? r.UserId?.ToString() ?? "system")).Append(',')
                .Append(Escape(r.Action)).Append(',')
                .Append(Escape(r.ModuleName)).Append(',')
                .Append(Escape(r.EntityName ?? "")).Append(',')
                .Append(Escape(r.EntityId ?? "")).Append(',')
                .Append(Escape(r.OldValue ?? "")).Append(',')
                .Append(Escape(r.NewValue ?? "")).Append(',')
                .Append(Escape(r.IpAddress ?? "")).Append(',')
                .Append(Escape(r.CorrelationId ?? ""))
                .AppendLine();
        }
        var content = Encoding.UTF8.GetPreamble()
            .Concat(Encoding.UTF8.GetBytes(csv.ToString())).ToArray();
        return new AuditExportResult(content, "text/csv",
            $"audit_{IstClock.IstDateOf(clock.UtcNow):yyyyMMdd}.csv");
    }

    private IQueryable<AuditLog> Filter(AuditListRequest request)
    {
        var query = db.AuditLogs.AsNoTracking();
        if (request.UserId is not null)
        {
            query = query.Where(l => l.UserId == request.UserId);
        }
        if (!string.IsNullOrWhiteSpace(request.Module))
        {
            query = query.Where(l => l.ModuleName == request.Module);
        }
        if (!string.IsNullOrWhiteSpace(request.Action))
        {
            query = query.Where(l => l.Action == request.Action);
        }
        if (!string.IsNullOrWhiteSpace(request.EntityName))
        {
            query = query.Where(l => l.EntityName == request.EntityName);
        }
        if (request.From is not null)
        {
            var fromUtc = IstClock.ToUtc(request.From.Value.ToDateTime(TimeOnly.MinValue));
            query = query.Where(l => l.TimestampUtc >= fromUtc);
        }
        if (request.To is not null)
        {
            var toUtc = IstClock.ToUtc(request.To.Value.AddDays(1).ToDateTime(TimeOnly.MinValue));
            query = query.Where(l => l.TimestampUtc < toUtc);
        }
        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var term = request.Search.Trim();
            query = query.Where(l =>
                (l.UserName != null && l.UserName.Contains(term)) ||
                (l.EntityId != null && l.EntityId.Contains(term)) ||
                l.Action.Contains(term));
        }
        return query;
    }

    private static AuditLogDto ToDto(AuditLog l) => new(
        l.AuditLogId, l.UserId, l.UserName, l.Action, l.ModuleName, l.EntityName, l.EntityId,
        l.OldValue, l.NewValue, l.IpAddress, l.CorrelationId, l.TimestampUtc);

    private static string Escape(string value) =>
        value.Contains(',') || value.Contains('"') || value.Contains('\n')
            ? $"\"{value.Replace("\"", "\"\"")}\""
            : value;
}
