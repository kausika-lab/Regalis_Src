using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Regalis.Shared.Api;

namespace Regalis.Application.Finance.Billing;

// ── Statement import (build doc §8) ─────────────────────────────────────────────

/// <summary>Column headers chosen in step 2. Out/In may be one signed Amount column instead
/// (AmountColumn, positive = in). DateFormat is a .NET pattern such as dd/MM/yyyy.</summary>
public record BankColumnMappingDto(string? DateColumn, string? DescriptionColumn, string? OutColumn, string? InColumn,
    string? AmountColumn, string? ReferenceColumn, string? BalanceColumn, string DateFormat);

public record BankImportRowDto(int RowNumber, DateOnly? Date, string Description, decimal Out, decimal In, string? Reference,
    decimal? Balance, string Result, string? Message);

public record BankImportPreviewDto(int BankAccountId, string FileName, IReadOnlyList<string> Headers, int RowCount, DateOnly? FirstDate,
    DateOnly? LastDate, BankColumnMappingDto? SavedMapping, BankColumnMappingDto? AppliedMapping, IReadOnlyList<BankImportRowDto> Rows,
    int NewCount, int DuplicateCount, int ErrorCount, IReadOnlyList<string> DateFormats);

public record BankImportResultDto(long BankImportBatchId, string BatchCode, int Imported, int Skipped, int Errors, string Message,
    decimal? FileOpeningBalance, decimal? RegalisBalanceBefore, decimal? FileClosingBalance, decimal RegalisClosingBalance, bool BalancesAgree,
    string BalanceMessage);

public record BankImportBatchDto(long BankImportBatchId, string BatchCode, int BankAccountId, string AccountName, string FileName, int RowCount,
    int ImportedCount, int SkippedCount, int ErrorCount, DateOnly? FirstDate, DateOnly? LastDate, string? ImportedBy, DateTime ImportedUtc,
    bool IsUndone, int MatchedLines);

public record BankUploadInput(string FileName, Stream Content, long Length);

public interface IBankImportService
{
    Task<BankColumnMappingDto?> GetMappingAsync(int bankAccountId, CancellationToken ct = default);
    Task<BankImportPreviewDto> ParseAsync(int bankAccountId, BankUploadInput file, BankColumnMappingDto? mapping, CancellationToken ct = default);
    Task<BankImportResultDto> CommitAsync(int bankAccountId, BankUploadInput file, BankColumnMappingDto mapping, CancellationToken ct = default);
    Task<BankImportBatchDto> UndoAsync(long batchId, CancellationToken ct = default);
    Task<IReadOnlyList<BankImportBatchDto>> GetBatchesAsync(int? bankAccountId, CancellationToken ct = default);
}

// ── Matching (doc §9) ─────────────────────────────────────────────────────────────

public class BankLineListRequest : PagedRequest
{
    public int? BankAccountId { get; set; }

    /// <summary>1 credit, 2 debit.</summary>
    public byte? Direction { get; set; }

    /// <summary>1 unreconciled, 2 matched, 3 reconciled; "open" = not reconciled.</summary>
    public string? Status { get; set; }

    public DateOnly? From { get; set; }
    public DateOnly? To { get; set; }
    public string? BatchCode { get; set; }
}

public record BankLineDocDto(byte DocType, long DocId, string DocNumber, decimal Amount);

public record BankLineDifferenceDto(byte Reason, string ReasonLabel, decimal SignedAmount, string? TdsSection, string? Note);

public record BankLineDto(long BankTransactionId, int BankAccountId, string AccountName, DateOnly TransactionDate, byte Direction, decimal Amount,
    string Narration, string? ReferenceNumber, string? BankReferenceId, string? Category, byte Status, string? ImportBatchId,
    DateTime? ReconciledUtc, string? ReconciledBy, IReadOnlyList<BankLineDocDto> Documents, IReadOnlyList<BankLineDifferenceDto> Differences,
    string? LegacyMatch);

public record BankLinesResultDto(PagedResult<BankLineDto> Page, int UnreconciledCount, decimal AccountBalance);

public record MatchCandidateRowDto(byte DocType, string DocTypeLabel, long DocId, string Number, string? PartyName, long? PartyId,
    DateOnly Date, decimal Total, decimal Outstanding, bool MustBeFull, int Score, bool Suggested, string? Hint);

public record DifferenceReasonDto(byte Reason, string Code, string Label, string? Help, bool Allowed, string? RequiredPermission);

public record BankMatchContextDto(BankLineDto Line, IReadOnlyList<MatchCandidateRowDto> Candidates, IReadOnlyList<DifferenceReasonDto> Reasons,
    decimal RoundingCap, IReadOnlyList<TdsSectionOptionDto> TdsSections);

public record MatchAllocationRequest(byte DocType, long DocId, decimal Amount);

/// <summary>Amount is always positive except for Rounding, which carries its sign (positive =
/// the bank figure is higher). TargetDocType/Id ties a TDS or short-payment difference to one
/// of the ticked invoices or bills; PartyId names who an advance belongs to when nothing
/// ticked says so.</summary>
public record MatchDifferenceRequest(byte Reason, decimal Amount, string? TdsSection, byte? TargetDocType, long? TargetDocId, long? PartyId, string? Note);

public record MatchBankLineRequest(List<MatchAllocationRequest> Allocations, List<MatchDifferenceRequest> Differences);

public record UnmatchBankLineRequest(string Reason);

public interface IBankMatchService
{
    Task<BankLinesResultDto> GetLinesAsync(BankLineListRequest request, CancellationToken ct = default);
    Task<BankMatchContextDto> GetContextAsync(long bankTransactionId, string? search, CancellationToken ct = default);
    Task<BankLineDto> MatchAsync(long bankTransactionId, MatchBankLineRequest request, CancellationToken ct = default);
    Task<BankLineDto> UnmatchAsync(long bankTransactionId, UnmatchBankLineRequest request, CancellationToken ct = default);
}
