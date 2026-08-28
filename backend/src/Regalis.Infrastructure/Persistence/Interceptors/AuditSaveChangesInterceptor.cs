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
/// </summary>
public class AuditSaveChangesInterceptor(ICurrentUser currentUser, IDateTimeProvider clock)
    : SaveChangesInterceptor
{
    private static readonly HashSet<string> RedactedProperties = new(StringComparer.OrdinalIgnoreCase)
    {
        "PasswordHash", "TokenHash", "CodeHash", "ReplacedByTokenHash", "GatewaySignature"
    };

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = false };

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

    private void ApplyAudit(DbContext? context)
    {
        if (context is null)
        {
            return;
        }

        var nowUtc = clock.UtcNow;
        long? userId = currentUser.IsAuthenticated ? currentUser.UserId : null;
        var auditRows = new List<AuditLog>();

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
                        auditRows.Add(BuildRow(entry, AuditActions.Create, nowUtc, userId, oldValues: null,
                            newValues: SerializeCurrent(entry)));
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
                        auditRows.Add(BuildRow(entry, isSoftDelete ? AuditActions.Delete : AuditActions.Update,
                            nowUtc, userId, SerializeOriginal(entry), SerializeCurrent(entry)));
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
                        SerializeOriginal(entry), SerializeCurrent(entry)));
                    break;
            }
        }

        if (auditRows.Count > 0)
        {
            context.Set<AuditLog>().AddRange(auditRows);
        }
    }

    private AuditLog BuildRow(EntityEntry entry, string action, DateTime nowUtc, long? userId,
        string? oldValues, string? newValues) => new()
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
        UserAgent = currentUser.UserAgent,
        CorrelationId = currentUser.CorrelationId,
        TimestampUtc = nowUtc
    };

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
            payload[property.Metadata.Name] = RedactedProperties.Contains(property.Metadata.Name)
                ? "***REDACTED***"
                : valueSelector(property);
        }
        return JsonSerializer.Serialize(payload, JsonOptions);
    }
}
