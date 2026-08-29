using Regalis.Shared.Api;

namespace Regalis.Application.Administration;

public record AuditLogDto(
    long AuditLogId,
    long? UserId,
    string? UserName,
    string Action,
    string ModuleName,
    string? EntityName,
    string? EntityId,
    string? OldValue,
    string? NewValue,
    string? IpAddress,
    string? CorrelationId,
    DateTime TimestampUtc);

public class AuditListRequest : PagedRequest
{
    public long? UserId { get; set; }
    public string? Module { get; set; }
    public string? Action { get; set; }
    public string? EntityName { get; set; }
    public DateOnly? From { get; set; }
    public DateOnly? To { get; set; }
}

public record AuditExportResult(byte[] Content, string ContentType, string FileName);

/// <summary>The audit trail screen (API_PLAN §13). TRN_AuditLog is append-only —
/// this service only ever reads it.</summary>
public interface IAuditService
{
    Task<PagedResult<AuditLogDto>> GetLogsAsync(AuditListRequest request, CancellationToken ct = default);
    Task<List<string>> GetModulesAsync(CancellationToken ct = default);
    Task<AuditExportResult> ExportAsync(AuditListRequest request, CancellationToken ct = default);
}
