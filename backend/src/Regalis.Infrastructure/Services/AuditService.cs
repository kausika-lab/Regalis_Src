using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Regalis.Application.Abstractions;
using Regalis.Application.Administration;
using Regalis.Application.Common.Exceptions;
using Regalis.Domain.Entities.Identity;
using Regalis.Infrastructure.Persistence;
using Regalis.Shared;
using Regalis.Shared.Api;
using Regalis.Shared.Constants;

namespace Regalis.Infrastructure.Services;

/// <summary>
/// The audit trail screen (Audit Trail Enhancements v1.0). TRN_AuditLog is append-only and
/// written by the save-changes interceptor, the sign-in code and the Failed/Denied
/// recorder — this service only reads it, except for the EXPORT row an export writes.
///
/// Filtering, sorting and paging run in SQL. Only the rows of the requested page are
/// materialised, and their JSON is read on the server to build the change summary; the
/// list response never carries the full before/after values (§5.2) — the drawer's
/// detail call does.
/// </summary>
public partial class AuditService(RegalisDbContext db, ICurrentUser currentUser, IDateTimeProvider clock) : IAuditService
{
    private const int MaxExportRows = 50_000;
    private const string SystemUser = "system";

    private static readonly HashSet<string> IgnoredFields = new(StringComparer.OrdinalIgnoreCase)
    {
        "ModifiedBy", "ModifiedDate"
    };

    private static readonly string[] KnownActions =
    [
        AuditActions.Create, AuditActions.Update, AuditActions.Delete, AuditActions.Login, AuditActions.Logout,
        AuditActions.LoginFailed, AuditActions.Export, AuditActions.Import, AuditActions.Approve, AuditActions.Reject
    ];

    // ── List ────────────────────────────────────────────────────────────────

    public async Task<PagedResult<AuditListItemDto>> GetLogsAsync(AuditListRequest request, CancellationToken ct = default)
    {
        var query = Filter(request);
        var totalCount = await query.CountAsync(ct);
        var rows = await Project(Sort(query, request).Skip((request.Page - 1) * request.PageSize).Take(request.PageSize))
            .ToListAsync(ct);
        return PagedResult<AuditListItemDto>.Create(rows.Select(ToItem).ToList(), request.Page, request.PageSize, totalCount);
    }

    public async Task<List<string>> GetModulesAsync(CancellationToken ct = default) =>
        await db.AuditLogs.AsNoTracking().Select(l => l.ModuleName).Distinct().OrderBy(m => m).ToListAsync(ct);

    public async Task<AuditLookupsDto> GetLookupsAsync(string? userSearch, CancellationToken ct = default)
    {
        var actions = await db.AuditLogs.AsNoTracking().Select(l => l.Action).Distinct().ToListAsync(ct);
        var modules = await GetModulesAsync(ct);

        var users = from u in db.Users.AsNoTracking()
                    where db.AuditLogs.Any(l => l.UserId == u.UserId)
                    select new { u.UserId, u.FullName, u.Email };
        if (!string.IsNullOrWhiteSpace(userSearch))
        {
            var term = userSearch.Trim();
            users = users.Where(u => u.FullName.Contains(term) || (u.Email != null && u.Email.Contains(term)));
        }
        var found = await users.OrderBy(u => u.FullName).Take(50).ToListAsync(ct);

        var options = new List<AuditUserOptionDto> { new(SystemUser, "System (automated jobs)", null) };
        options.AddRange(found.Select(u => new AuditUserOptionDto(u.UserId.ToString(CultureInfo.InvariantCulture), u.FullName, u.Email)));
        return new AuditLookupsDto(
            KnownActions.Concat(actions).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(a => a).ToList(),
            modules, AuditResults.All, options);
    }

    // ── Details drawer ──────────────────────────────────────────────────────

    public async Task<AuditDetailDto> GetDetailAsync(long id, CancellationToken ct = default)
    {
        var row = await Project(db.AuditLogs.AsNoTracking().Where(l => l.AuditLogId == id)).FirstOrDefaultAsync(ct)
                  ?? throw new NotFoundException("AuditLog", id);
        var extra = await db.AuditLogs.AsNoTracking().Where(l => l.AuditLogId == id)
            .Select(l => new { l.UserAgent, l.CorrelationId }).FirstAsync(ct);

        string? role = null;
        if (row.UserId is not null)
        {
            var roles = await db.UserRoles.AsNoTracking().Where(ur => ur.UserId == row.UserId)
                .Select(ur => ur.Role!.RoleName).ToListAsync(ct);
            role = roles.Count == 0 ? null : string.Join(", ", roles);
        }

        var entityDeleted = row.EntityName is not null && row.EntityId is not null
            && await db.AuditLogs.AsNoTracking().AnyAsync(l => l.EntityName == row.EntityName && l.EntityId == row.EntityId
                                                               && l.Action == AuditActions.Delete && l.Result == AuditResults.Success, ct);

        var (fields, changedCount) = Fields(row);
        return new AuditDetailDto(ToItem(row), role, extra.UserAgent, extra.CorrelationId, changedCount, fields,
            Pretty(row.OldValue), Pretty(row.NewValue), entityDeleted);
    }

    public async Task<IReadOnlyList<AuditListItemDto>> GetRelatedAsync(long id, CancellationToken ct = default)
    {
        var anchor = await db.AuditLogs.AsNoTracking().Where(l => l.AuditLogId == id)
                         .Select(l => new { l.EntityName, l.EntityId }).FirstOrDefaultAsync(ct)
                     ?? throw new NotFoundException("AuditLog", id);
        if (anchor.EntityName is null || anchor.EntityId is null || anchor.EntityId.StartsWith('-'))
        {
            return [];
        }
        var rows = await Project(db.AuditLogs.AsNoTracking()
                .Where(l => l.AuditLogId != id && l.EntityName == anchor.EntityName && l.EntityId == anchor.EntityId)
                .OrderByDescending(l => l.TimestampUtc).ThenByDescending(l => l.AuditLogId).Take(50))
            .ToListAsync(ct);
        return rows.Select(ToItem).ToList();
    }

    // ── Export ──────────────────────────────────────────────────────────────

    public async Task<AuditExportResult> ExportAsync(AuditListRequest request, CancellationToken ct = default)
    {
        var rows = await Project(Sort(Filter(request), request).Take(MaxExportRows)).ToListAsync(ct);

        var csv = new StringBuilder();
        csv.AppendLine("AuditId,When (IST),User,Email,Action,Module,Entity,EntityId,Result,FailureReason,ChangeSummary,IP,Device,RequestId,OldValues,NewValues");
        foreach (var r in rows)
        {
            var item = ToItem(r);
            csv.AppendJoin(',', [
                item.Id.ToString(CultureInfo.InvariantCulture),
                IstClock.ToIst(r.TimestampUtc).ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
                Escape(item.User.Name), Escape(item.User.Email), Escape(item.Action), Escape(item.Module),
                Escape(item.EntityType), Escape(item.EntityId), Escape(item.Result), Escape(item.FailureReason),
                Escape(item.ChangeSummary), Escape(item.IpAddress), Escape(item.Device), Escape(r.CorrelationId),
                Escape(r.OldValue), Escape(r.NewValue)
            ]).AppendLine();
        }

        // §4.6: exporting is itself recorded, with what was exported.
        db.AuditLogs.Add(new AuditLog
        {
            UserId = currentUser.IsAuthenticated ? currentUser.UserId : null,
            UserName = currentUser.FullName,
            Action = AuditActions.Export,
            ModuleName = "Audit",
            EntityName = "AuditLog",
            NewValue = JsonSerializer.Serialize(new
            {
                Rows = rows.Count,
                Filters = new
                {
                    request.Search, request.Module, request.Actions, request.UserIds, request.Result,
                    From = request.From?.ToString("yyyy-MM-dd"), To = request.To?.ToString("yyyy-MM-dd")
                }
            }),
            IpAddress = currentUser.IpAddress,
            UserAgent = currentUser.UserAgent is { Length: > 300 } ua ? ua[..300] : currentUser.UserAgent,
            CorrelationId = currentUser.CorrelationId,
            TimestampUtc = clock.UtcNow,
            Result = AuditResults.Success
        });
        await db.SaveChangesAsync(ct);

        var content = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(csv.ToString())).ToArray();
        return new AuditExportResult(content, "text/csv", $"audit_{IstClock.IstDateOf(clock.UtcNow):yyyyMMdd}.csv");
    }

    // ── Query building ──────────────────────────────────────────────────────

    private IQueryable<AuditLog> Filter(AuditListRequest request)
    {
        var query = db.AuditLogs.AsNoTracking();

        var modules = Csv(request.Module);
        if (modules.Count > 0) query = query.Where(l => modules.Contains(l.ModuleName));

        var actions = Csv(request.Actions).Concat(Csv(request.Action)).ToList();
        if (actions.Count > 0) query = query.Where(l => actions.Contains(l.Action));

        var userValues = Csv(request.UserIds);
        var includeSystem = userValues.Any(v => v.Equals(SystemUser, StringComparison.OrdinalIgnoreCase));
        var userIds = userValues.Select(v => long.TryParse(v, out var n) ? n : (long?)null).Where(n => n is not null)
            .Select(n => n!.Value).ToList();
        if (request.UserId is not null) userIds.Add(request.UserId.Value);
        if (includeSystem || userIds.Count > 0)
        {
            query = query.Where(l => (l.UserId != null && userIds.Contains(l.UserId.Value))
                                     || (includeSystem && l.UserId == null && l.UserName == null));
        }

        if (!string.IsNullOrWhiteSpace(request.Result)) query = query.Where(l => l.Result == request.Result);
        if (!string.IsNullOrWhiteSpace(request.EntityName)) query = query.Where(l => l.EntityName == request.EntityName);

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

        return Search(query, request.Search);
    }

    /// <summary>§4.1: case-insensitive, partial, across user name and email, entity,
    /// entity ID, module, IP and the change content. "Quotation 11" / "Quotation #11"
    /// means that one quotation — the ID must match exactly, or amounts like 1100 in
    /// other quotations' values would match too. Every other word must match somewhere.</summary>
    private IQueryable<AuditLog> Search(IQueryable<AuditLog> query, string? search)
    {
        var term = search?.Trim() ?? "";
        if (term.Length < 2) return query;

        var entityAndId = EntityAndIdPattern().Match(term);
        if (entityAndId.Success)
        {
            var name = entityAndId.Groups["name"].Value.Replace(" ", "");
            var id = entityAndId.Groups["id"].Value;
            return query.Where(l => l.EntityName == name && l.EntityId == id);
        }

        foreach (var raw in term.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var word = raw.TrimStart('#');
            if (word.Length == 0) continue;
            if (long.TryParse(word, out var number))
            {
                query = query.Where(l => l.EntityId == word || l.AuditLogId == number
                                         || (l.IpAddress != null && l.IpAddress.Contains(word))
                                         || (l.UserName != null && l.UserName.Contains(word)));
                continue;
            }
            query = query.Where(l =>
                (l.UserName != null && l.UserName.Contains(word))
                || (l.EntityName != null && l.EntityName.Contains(word))
                || l.ModuleName.Contains(word)
                || l.Action.Contains(word)
                || (l.IpAddress != null && l.IpAddress.Contains(word))
                || (l.FailureReason != null && l.FailureReason.Contains(word))
                || (l.ChangedFields != null && l.ChangedFields.Contains(word))
                || (l.OldValue != null && l.OldValue.Contains(word))
                || (l.NewValue != null && l.NewValue.Contains(word))
                || db.Users.Any(u => u.UserId == l.UserId && u.Email != null && u.Email.Contains(word)));
        }
        return query;
    }

    [GeneratedRegex(@"^(?<name>[A-Za-z][A-Za-z ]*?)\s*#?\s*(?<id>\d+)$")]
    private static partial Regex EntityAndIdPattern();

    private static IQueryable<AuditLog> Sort(IQueryable<AuditLog> query, AuditListRequest request)
    {
        var ascending = request.Sort?.EndsWith(":asc", StringComparison.OrdinalIgnoreCase) == true
                        || (request.Sort is null && !request.IsDescending);
        return ascending
            ? query.OrderBy(l => l.TimestampUtc).ThenBy(l => l.AuditLogId)
            : query.OrderByDescending(l => l.TimestampUtc).ThenByDescending(l => l.AuditLogId);
    }

    private IQueryable<Row> Project(IQueryable<AuditLog> query) =>
        query.Select(l => new Row
        {
            AuditLogId = l.AuditLogId,
            TimestampUtc = l.TimestampUtc,
            UserId = l.UserId,
            UserName = l.UserName,
            UserFullName = db.Users.Where(u => u.UserId == l.UserId).Select(u => u.FullName).FirstOrDefault(),
            UserEmail = db.Users.Where(u => u.UserId == l.UserId).Select(u => u.Email).FirstOrDefault(),
            Action = l.Action,
            ModuleName = l.ModuleName,
            EntityName = l.EntityName,
            EntityId = l.EntityId,
            Result = l.Result,
            FailureReason = l.FailureReason,
            ChangedFields = l.ChangedFields,
            OldValue = l.OldValue,
            NewValue = l.NewValue,
            IpAddress = l.IpAddress,
            UserAgent = l.UserAgent,
            CorrelationId = l.CorrelationId
        });

    private sealed class Row
    {
        public long AuditLogId { get; init; }
        public DateTime TimestampUtc { get; init; }
        public long? UserId { get; init; }
        public string? UserName { get; init; }
        public string? UserFullName { get; init; }
        public string? UserEmail { get; init; }
        public string Action { get; init; } = "";
        public string ModuleName { get; init; } = "";
        public string? EntityName { get; init; }
        public string? EntityId { get; init; }
        public string Result { get; init; } = AuditResults.Success;
        public string? FailureReason { get; init; }
        public string? ChangedFields { get; init; }
        public string? OldValue { get; init; }
        public string? NewValue { get; init; }
        public string? IpAddress { get; init; }
        public string? UserAgent { get; init; }
        public string? CorrelationId { get; init; }
    }

    // ── Presentation ────────────────────────────────────────────────────────

    private static AuditListItemDto ToItem(Row r)
    {
        var isSystem = r.UserId is null && r.UserName is null;
        var user = new AuditUserDto(r.UserId,
            isSystem ? SystemUser : r.UserName ?? r.UserFullName ?? $"User #{r.UserId}",
            isSystem ? null : r.UserEmail ?? (r.Action == AuditActions.LoginFailed ? EmailTried(r.NewValue) : null),
            isSystem);
        return new AuditListItemDto(r.AuditLogId, DateTime.SpecifyKind(r.TimestampUtc, DateTimeKind.Utc), user, r.Action,
            r.ModuleName, r.EntityName, r.EntityId, EntityLabel(r.EntityName, r.EntityId), r.Result, r.FailureReason,
            Summary(r), r.IpAddress, Device(r.UserAgent));
    }


    private static string Summary(Row r)
    {
        if (r.Result != AuditResults.Success && !string.IsNullOrWhiteSpace(r.FailureReason)) return r.FailureReason!;
        var entity = Humanize(r.EntityName ?? "Record");
        switch (r.Action)
        {
            case AuditActions.Create:
                return $"{entity} created · {Count(r.NewValue)} fields";
            case AuditActions.Delete:
                return r.OldValue is null ? $"{entity} removed" : $"{entity} removed · {Count(r.OldValue)} fields captured";
            case AuditActions.Update:
            case AuditActions.Approve:
            case AuditActions.Reject:
                var changed = ChangedList(r);
                if (changed.Count == 0) return r.Action == AuditActions.Update ? $"{entity} updated" : $"{entity} {r.Action.ToLowerInvariant()}d";
                var shown = string.Join(", ", changed.Take(4)) + (changed.Count > 4 ? $" +{changed.Count - 4} more" : "");
                return $"{changed.Count} field{(changed.Count == 1 ? "" : "s")} changed: {shown}";
            case AuditActions.Login: return "Signed in";
            case AuditActions.Logout: return "Signed out";
            case AuditActions.LoginFailed: return "Sign-in failed";
            case AuditActions.PasswordChange: return "Password changed";
            case AuditActions.Export:
                return ReadInt(r.NewValue, "Rows") is { } rows ? $"Exported {rows} row{(rows == 1 ? "" : "s")} to CSV" : "Exported to CSV";
            case AuditActions.AuthorizationFailed:
                return $"Missing permission: {r.EntityId}";
            default:
                var words = r.Action.Replace('_', ' ').ToLowerInvariant();
                return char.ToUpperInvariant(words[0]) + words[1..];
        }
    }

    /// <summary>ChangedFields when the row has it; for rows written before it existed,
    /// the difference between the before and after snapshots.</summary>
    private static List<string> ChangedList(Row r)
    {
        if (!string.IsNullOrWhiteSpace(r.ChangedFields))
        {
            return r.ChangedFields.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
        }
        var before = Parse(r.OldValue);
        var after = Parse(r.NewValue);
        if (before is null || after is null) return [];
        return after.Keys.Where(k => !IgnoredFields.Contains(k)
                                     && (!before.TryGetValue(k, out var b) || b.GetRawText() != after[k].GetRawText()))
            .ToList();
    }

    private static (IReadOnlyList<AuditFieldChangeDto> Fields, int Changed) Fields(Row r)
    {
        var before = Parse(r.OldValue);
        var after = Parse(r.NewValue);
        if (before is null && after is null) return ([], 0);
        var changed = before is not null && after is not null ? ChangedList(r).ToHashSet(StringComparer.OrdinalIgnoreCase) : null;
        var keys = (before?.Keys.AsEnumerable() ?? []).Concat(after?.Keys.AsEnumerable() ?? []).Distinct().ToList();
        var fields = keys.Select(k => new AuditFieldChangeDto(k,
                before is not null && before.TryGetValue(k, out var b) ? b.GetRawText() : null,
                after is not null && after.TryGetValue(k, out var a) ? a.GetRawText() : null,
                changed is null || changed.Contains(k)))
            .ToList();
        return (fields, fields.Count(f => f.Changed));
    }

    private static Dictionary<string, JsonElement>? Parse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try
        {
            using var doc = JsonDocument.Parse(json);
            return doc.RootElement.ValueKind != JsonValueKind.Object
                ? null
                : doc.RootElement.EnumerateObject().ToDictionary(p => p.Name, p => p.Value.Clone());
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static int Count(string? json) => Parse(json)?.Count ?? 0;

    private static int? ReadInt(string? json, string key) =>
        Parse(json) is { } d && d.TryGetValue(key, out var v) && v.TryGetInt32(out var n) ? n : null;

    private static string? EmailTried(string? json) =>
        Parse(json) is { } d && d.TryGetValue("Email", out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

    private static string? Pretty(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try
        {
            using var doc = JsonDocument.Parse(json);
            return JsonSerializer.Serialize(doc.RootElement, new JsonSerializerOptions { WriteIndented = true });
        }
        catch (JsonException)
        {
            return json;
        }
    }

    private static string EntityLabel(string? entity, string? id)
    {
        if (entity is null) return "—";
        var name = Humanize(entity);
        if (string.IsNullOrEmpty(id)) return name;
        // Rows written before §6 was fixed hold EF's temporary key, not the real one.
        if (id.StartsWith('-')) return $"{name} (new — ID not recorded)";
        return long.TryParse(id, out _) ? $"{name} #{id}" : $"{name} {id}";
    }

    /// <summary>"PurchaseOrder" → "Purchase Order".</summary>
    private static string Humanize(string name) => HumanizePattern().Replace(name, " $1").Trim();

    [GeneratedRegex(@"(?<=[a-z])([A-Z])")]
    private static partial Regex HumanizePattern();

    /// <summary>"Chrome 129 · Windows" from a user agent.</summary>
    internal static string? Device(string? ua)
    {
        if (string.IsNullOrWhiteSpace(ua)) return null;
        string browser =
            Version(ua, "Edg/") is { } edge ? $"Edge {edge}" :
            Version(ua, "OPR/") is { } opera ? $"Opera {opera}" :
            Version(ua, "Firefox/") is { } ff ? $"Firefox {ff}" :
            Version(ua, "Chrome/") is { } chrome ? $"Chrome {chrome}" :
            ua.Contains("Safari/", StringComparison.Ordinal) ? "Safari" :
            ua.StartsWith("Mozilla", StringComparison.Ordinal) ? "Browser" : ua.Split('/')[0];
        string os =
            ua.Contains("Windows", StringComparison.Ordinal) ? "Windows" :
            ua.Contains("Android", StringComparison.Ordinal) ? "Android" :
            ua.Contains("iPhone", StringComparison.Ordinal) ? "iPhone" :
            ua.Contains("iPad", StringComparison.Ordinal) ? "iPad" :
            ua.Contains("Mac OS X", StringComparison.Ordinal) ? "macOS" :
            ua.Contains("Linux", StringComparison.Ordinal) ? "Linux" : "";
        return os.Length == 0 ? browser : $"{browser} · {os}";
    }

    private static string? Version(string ua, string marker)
    {
        var at = ua.IndexOf(marker, StringComparison.Ordinal);
        if (at < 0) return null;
        var digits = new string(ua[(at + marker.Length)..].TakeWhile(char.IsDigit).ToArray());
        return digits.Length == 0 ? null : digits;
    }

    private static List<string> Csv(string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? []
            : value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Distinct().ToList();

    private static string Escape(string? value)
    {
        var text = value ?? "";
        if (text.Length > 0 && "=+-@".Contains(text[0])) text = "'" + text;
        return text.IndexOfAny([',', '"', '\n', '\r']) >= 0 ? $"\"{text.Replace("\"", "\"\"")}\"" : text;
    }
}
