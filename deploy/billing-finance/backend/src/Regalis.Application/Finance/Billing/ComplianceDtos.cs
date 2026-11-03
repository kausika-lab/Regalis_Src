using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Regalis.Application.Finance.Billing;

// ── Compliance calendar, period lock, reminders (build doc §13) and the accounting
// export (§20) ──

/// <summary>Status 1 not started, 2 not done (overdue), 3 ready to file, 4 filed.</summary>
public record ComplianceItemDto(string Key, int? ObligationId, string Name, string Period, string PeriodLabel, DateOnly DueDate, int DaysToGo,
    decimal? Amount, string? AmountNote, byte Status, string StatusLabel, string? Owner, string? CompletedBy, DateOnly? CompletedOn,
    string? Reference, string? Link, bool Editable);

public record ComplianceStatusRequest(int ObligationId, string Period, byte Status, string? Reference, DateOnly? CompletedOn);

public record ComplianceObligationDto(int ComplianceObligationId, string Code, string Name, string Frequency, int DueDay, string? DueMonths,
    string? AmountSource, string? OwnerRole, int DisplayOrder, bool IsActive);

public record SaveComplianceObligationRequest(string Code, string Name, string Frequency, int DueDay, string? DueMonths, string? AmountSource,
    string? OwnerRole, int DisplayOrder, bool IsActive);

public record PeriodLockDto(string Period, string Label, bool IsLocked, string? LockedBy, DateTime? LockedUtc, string? UnlockedBy,
    DateTime? UnlockedUtc, string? UnlockReason, bool CanLock);

public record UnlockPeriodRequest(string Reason);

public record ReminderLogDto(long ReminderLogId, string ReminderType, string DocumentType, long DocumentId, string? DocumentNumber, string Recipient,
    string Channel, DateTime SentUtc, bool Delivered, string? Error);

public record ReminderRunResultDto(bool Enabled, int Sent, int Failed, IReadOnlyList<string> Notes);

public interface IComplianceService
{
    Task<IReadOnlyList<ComplianceItemDto>> GetCalendarAsync(string? fromPeriod, int months, CancellationToken ct = default);
    Task<ComplianceItemDto> SetStatusAsync(ComplianceStatusRequest request, CancellationToken ct = default);
    Task<IReadOnlyList<ComplianceObligationDto>> GetObligationsAsync(CancellationToken ct = default);
    Task<ComplianceObligationDto> SaveObligationAsync(int? id, SaveComplianceObligationRequest request, CancellationToken ct = default);
    Task<IReadOnlyList<PeriodLockDto>> GetPeriodLocksAsync(CancellationToken ct = default);
    Task<PeriodLockDto> LockAsync(string period, CancellationToken ct = default);
    Task<PeriodLockDto> UnlockAsync(string period, UnlockPeriodRequest request, CancellationToken ct = default);
    Task<IReadOnlyList<ReminderLogDto>> GetReminderLogAsync(string? documentType, long? documentId, CancellationToken ct = default);
}

public interface IFinanceReminderService
{
    /// <summary>Sends whatever is due now. Does nothing while Reminders.Enabled is false.</summary>
    Task<ReminderRunResultDto> RunAsync(bool force, CancellationToken ct = default);
}

public interface IAccountingExportService
{
    Task<byte[]> ExportAsync(DateOnly from, DateOnly to, CancellationToken ct = default);
}
