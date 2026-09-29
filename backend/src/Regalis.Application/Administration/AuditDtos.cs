using Regalis.Shared.Api;

namespace Regalis.Application.Administration;

// Audit Trail Enhancements (v1.0, 28 Sep 2026): search, action/user/result filters,
// readable change summaries and a details drawer. TRN_AuditLog stays append-only —
// nothing here writes to it except the EXPORT row an export itself produces.

/// <summary>Who did it. <see cref="IsSystem"/> is true for automated jobs (no user and
/// no name on the row); a failed sign-in for an unknown address carries only the email
/// tried, as <see cref="Name"/>.</summary>
public record AuditUserDto(long? Id, string Name, string? Email, bool IsSystem);

public record AuditListItemDto(
    long Id,
    DateTime TimestampUtc,
    AuditUserDto User,
    string Action,
    string Module,
    string? EntityType,
    string? EntityId,
    string EntityLabel,
    // Success, Failed or Denied.
    string Result,
    string? FailureReason,
    // "3 fields changed: Status, Remarks, Total" — or the reason for Failed/Denied.
    string ChangeSummary,
    string? IpAddress,
    // Browser and OS parsed from the user agent, e.g. "Chrome 129 · Windows".
    string? Device);

public record AuditFieldChangeDto(string Field, string? Before, string? After, bool Changed);

public record AuditDetailDto(
    AuditListItemDto Record,
    string? UserRole,
    string? UserAgent,
    string? RequestId,
    int ChangedCount,
    // Every field; <c>Changed</c> marks the ones the drawer shows by default.
    IReadOnlyList<AuditFieldChangeDto> Fields,
    string? OldValues,
    string? NewValues,
    // A successful DELETE exists for this entity, so "Open" is hidden.
    bool EntityDeleted);

public record AuditUserOptionDto(string Value, string Name, string? Email);

public record AuditLookupsDto(
    IReadOnlyList<string> Actions,
    IReadOnlyList<string> Modules,
    IReadOnlyList<string> Results,
    IReadOnlyList<AuditUserOptionDto> Users);

public class AuditListRequest : PagedRequest
{
    // Comma-separated module names.
    public string? Module { get; set; }
    // Comma-separated actions, e.g. "DELETE,UPDATE".
    public string? Actions { get; set; }
    // Comma-separated user IDs; "system" selects automated entries.
    public string? UserIds { get; set; }
    public string? Result { get; set; }
    public DateOnly? From { get; set; }
    public DateOnly? To { get; set; }
    // "createdAt:desc" (default) or "createdAt:asc".
    public string? Sort { get; set; }
    public string? EntityName { get; set; }

    // Older single-value filters, still honoured.
    public long? UserId { get; set; }
    public string? Action { get; set; }
}

public record AuditExportResult(byte[] Content, string ContentType, string FileName);

/// <summary>The audit trail screen. TRN_AuditLog is append-only — this service only
/// reads it, apart from recording that an export happened.</summary>
public interface IAuditService
{
    Task<PagedResult<AuditListItemDto>> GetLogsAsync(AuditListRequest request, CancellationToken ct = default);
    Task<AuditDetailDto> GetDetailAsync(long id, CancellationToken ct = default);
    Task<IReadOnlyList<AuditListItemDto>> GetRelatedAsync(long id, CancellationToken ct = default);
    Task<AuditLookupsDto> GetLookupsAsync(string? userSearch, CancellationToken ct = default);
    Task<List<string>> GetModulesAsync(CancellationToken ct = default);
    Task<AuditExportResult> ExportAsync(AuditListRequest request, CancellationToken ct = default);
}
