using System.Runtime.CompilerServices;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Regalis.Application.Abstractions;
using Regalis.Domain.Common;
using Regalis.Domain.Entities.Identity;
using Regalis.Shared.Constants;

namespace Regalis.Infrastructure.Persistence.Interceptors;

/// <summary>
/// Sets the audit columns on every insert/update, converts hard deletes of
/// soft-deletable entities into soft deletes, and writes a TRN_AuditLog row for every
/// change to an auditable entity (ARCHITECTURE §9). Sensitive fields are redacted
/// before serialisation.
///
/// CREATE rows are written only after the insert succeeds (Audit Trail Enhancements §6):
/// before the save, an identity key holds EF's temporary placeholder, which is how
/// entries like "Quotation #-9223372036854774805" got into the log. The rows are held
/// back per context and added by a second, audit-only save in the same transaction.
/// </summary>
public class AuditSaveChangesInterceptor(ICurrentUser currentUser, IDateTimeProvider clock)
    : SaveChangesInterceptor
{
    private static readonly HashSet<string> RedactedProperties = new(StringComparer.OrdinalIgnoreCase)
    {
        "PasswordHash", "TokenHash", "CodeHash", "ReplacedByTokenHash", "GatewaySignature",
        "Token", "RawToken", "RefreshToken", "Secret", "ClientSecret", "ApiKey", "Otp", "OtpCode"
    };

    /// <summary>Shown with only the last four characters.</summary>
    private static readonly HashSet<string> MaskedProperties = new(StringComparer.OrdinalIgnoreCase)
    {
        "AccountNumber", "AccountNo", "BankAccountNumber", "CardNumber"
    };

    /// <summary>Bookkeeping that never counts as a change on its own.</summary>
    private static readonly HashSet<string> IgnoredForChanges = new(StringComparer.OrdinalIgnoreCase)
    {
        nameof(IAuditableEntity.ModifiedBy), nameof(IAuditableEntity.ModifiedDate)
    };

    /// <summary>What a sign-in touches on MST_User. The LOGIN / LOGIN_FAILED row already
    /// records the event, so an UPDATE changing only these is noise (the "system" UPDATE
    /// on User #3 after every login, §6).</summary>
    private static readonly HashSet<string> LoginBookkeeping = new(StringComparer.OrdinalIgnoreCase)
    {
        "LastLoginUtc", "FailedLoginCount", "LockoutEndUtc"
    };

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = false };

    private readonly ConditionalWeakTable<DbContext, List<(EntityEntry Entry, AuditLog Row)>> _pendingCreates = new();

    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData, InterceptionResult<int> result)
    {
        ApplyAudit(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData, InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        ApplyAudit(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    public override int SavedChanges(SaveChangesCompletedEventData eventData, int result)
    {
        if (TakePendingCreates(eventData.Context) is { } context)
        {
            context.SaveChanges();
        }
        return base.SavedChanges(eventData, result);
    }

    public override async ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData eventData,
        int result, CancellationToken cancellationToken = default)
    {
        if (TakePendingCreates(eventData.Context) is { } context)
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        return await base.SavedChangesAsync(eventData, result, cancellationToken);
    }

    public override void SaveChangesFailed(DbContextErrorEventData eventData)
    {
        if (eventData.Context is not null) _pendingCreates.Remove(eventData.Context);
        base.SaveChangesFailed(eventData);
    }

    public override Task SaveChangesFailedAsync(DbContextErrorEventData eventData,
        CancellationToken cancellationToken = default)
    {
        if (eventData.Context is not null) _pendingCreates.Remove(eventData.Context);
        return base.SaveChangesFailedAsync(eventData, cancellationToken);
    }

    /// <summary>Stamps the real keys onto held-back CREATE rows and stages them.
    /// Returns the context when there is something to save.</summary>
    private DbContext? TakePendingCreates(DbContext? context)
    {
        if (context is null || !_pendingCreates.TryGetValue(context, out var pending) || pending.Count == 0)
        {
            return null;
        }
        _pendingCreates.Remove(context);
        foreach (var (entry, row) in pending)
        {
            row.EntityId = DescribePrimaryKey(entry);
            row.NewValue = SerializeCurrent(entry);
        }
        context.Set<AuditLog>().AddRange(pending.Select(p => p.Row));
        return context;
    }

    private void ApplyAudit(DbContext? context)
    {
        if (context is null)
        {
            return;
        }

        var nowUtc = clock.UtcNow;
        long? userId = currentUser.IsAuthenticated ? currentUser.UserId : null;
        var auditRows = new List<AuditLog>();
        var creates = new List<(EntityEntry, AuditLog)>();

        foreach (var entry in context.ChangeTracker.Entries().ToList())
        {
            // The audit log itself is never audited and never mutated.
            if (entry.Entity is AuditLog)
            {
                continue;
            }

            switch (entry.State)
            {
                case EntityState.Added:
                    if (entry.Entity is IAuditableEntity added)
                    {
                        added.CreatedBy = userId;
                        added.CreatedDate = nowUtc;
                    }
                    if (entry.Entity is AuditableEntity)
                    {
                        creates.Add((entry, BuildRow(entry, AuditActions.Create, nowUtc, userId,
                            oldValues: null, newValues: null, changedFields: null)));
                    }
                    break;

                case EntityState.Modified:
                    if (entry.Entity is IAuditableEntity modified)
                    {
                        modified.ModifiedBy = userId;
                        modified.ModifiedDate = nowUtc;
                    }
                    if (entry.Entity is AuditableEntity)
                    {
                        var isSoftDelete = entry.Entity is ISoftDeletableEntity { IsDeleted: true } &&
                                           entry.Property(nameof(ISoftDeletableEntity.IsDeleted)).IsModified;
                        var changed = ChangedProperties(entry);
                        if (!isSoftDelete)
                        {
                            if (changed.Count == 0) break;
                            if (entry.Entity is User && changed.All(LoginBookkeeping.Contains)) break;
                        }
                        auditRows.Add(BuildRow(entry, isSoftDelete ? AuditActions.Delete : AuditActions.Update,
                            nowUtc, userId, SerializeOriginal(entry), SerializeCurrent(entry),
                            isSoftDelete ? null : Join(changed)));
                    }
                    break;

                case EntityState.Deleted when entry.Entity is ISoftDeletableEntity softDeletable:
                    // Nothing in Phase 1 issues a hard DELETE against a business table.
                    entry.State = EntityState.Modified;
                    softDeletable.IsDeleted = true;
                    softDeletable.IsActive = false;
                    softDeletable.DeletedBy = userId;
                    softDeletable.DeletedDate = nowUtc;
                    if (entry.Entity is IAuditableEntity onDelete)
                    {
                        onDelete.ModifiedBy = userId;
                        onDelete.ModifiedDate = nowUtc;
                    }
                    auditRows.Add(BuildRow(entry, AuditActions.Delete, nowUtc, userId,
                        SerializeOriginal(entry), SerializeCurrent(entry), changedFields: null));
                    break;
            }
        }

        if (auditRows.Count > 0)
        {
            context.Set<AuditLog>().AddRange(auditRows);
        }
        if (creates.Count > 0)
        {
            _pendingCreates.GetValue(context, _ => []).AddRange(creates);
        }
    }

    private AuditLog BuildRow(EntityEntry entry, string action, DateTime nowUtc, long? userId,
        string? oldValues, string? newValues, string? changedFields) => new()
    {
        UserId = userId,
        UserName = currentUser.FullName,
        Action = action,
        ModuleName = entry.Entity.GetType().Namespace?.Split('.').Last() ?? "Unknown",
        EntityName = entry.Entity.GetType().Name,
        EntityId = DescribePrimaryKey(entry),
        OldValue = oldValues,
        NewValue = newValues,
        IpAddress = currentUser.IpAddress,
        UserAgent = Truncate(currentUser.UserAgent, 300),
        CorrelationId = currentUser.CorrelationId,
        TimestampUtc = nowUtc,
        Result = AuditResults.Success,
        ChangedFields = changedFields
    };

    private static List<string> ChangedProperties(EntityEntry entry) =>
        entry.Properties
            .Where(p => p.IsModified && !p.Metadata.IsShadowProperty()
                        && !IgnoredForChanges.Contains(p.Metadata.Name)
                        && !Equals(p.OriginalValue, p.CurrentValue))
            .Select(p => p.Metadata.Name)
            .ToList();

    private static string? Join(List<string> names)
    {
        if (names.Count == 0) return null;
        var joined = string.Join(",", names);
        return joined.Length <= 1000 ? joined : joined[..1000];
    }

    private static string? Truncate(string? value, int max) =>
        value is null || value.Length <= max ? value : value[..max];

    private static string? DescribePrimaryKey(EntityEntry entry)
    {
        var key = entry.Metadata.FindPrimaryKey();
        if (key is null)
        {
            return null;
        }
        var values = key.Properties.Select(p => entry.Property(p.Name).CurrentValue?.ToString());
        return string.Join(",", values);
    }

    private static string SerializeCurrent(EntityEntry entry) =>
        Serialize(entry, p => p.CurrentValue);

    private static string SerializeOriginal(EntityEntry entry) =>
        Serialize(entry, p => p.OriginalValue);

    private static string Serialize(EntityEntry entry, Func<PropertyEntry, object?> valueSelector)
    {
        var payload = new Dictionary<string, object?>();
        foreach (var property in entry.Properties)
        {
            if (property.Metadata.IsShadowProperty())
            {
                continue;
            }
            var name = property.Metadata.Name;
            var value = valueSelector(property);
            payload[name] = RedactedProperties.Contains(name)
                ? "***REDACTED***"
                : MaskedProperties.Contains(name) && value is string s && s.Length > 4
                    ? new string('X', s.Length - 4) + s[^4..]
                    : value;
        }
        return JsonSerializer.Serialize(payload, JsonOptions);
    }
}
