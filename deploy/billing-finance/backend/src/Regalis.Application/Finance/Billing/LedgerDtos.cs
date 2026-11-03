using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Regalis.Application.Finance.Billing;

// ── Party ledgers, outstanding and ageing (build doc §7) - one shape for customers and
// suppliers. Party type is "customer" or "supplier". Nothing here is stored. ──

public record LedgerRowDto(DateOnly Date, string Kind, string Reference, string? Detail, decimal Debit, decimal Credit, decimal Balance,
    string? DocType, long? DocId);

/// <summary>Balance is what the party owes Regalis (customer) or what Regalis owes them (supplier).</summary>
public record PartyLedgerDto(string PartyType, long PartyId, string PartyName, string? Gstin, string? TaxId, string? PaymentTerms,
    string? Email, DateOnly From, DateOnly To, decimal OpeningBalance, decimal TotalDebit, decimal TotalCredit, decimal ClosingBalance,
    IReadOnlyList<LedgerRowDto> Rows);

public record OutstandingPartyDto(long PartyId, string PartyName, decimal Total, int Documents, DateOnly? OldestUnpaid, int OldestDays,
    decimal Advance, decimal Balance);

public record AgeingRowDto(long PartyId, string PartyName, decimal NotDue, decimal D1To30, decimal D31To60, decimal D61To90, decimal Over90, decimal Total);

public record AgeingDto(string PartyType, DateOnly AsOn, IReadOnlyList<AgeingRowDto> Rows, AgeingRowDto Totals);

public record AgeingDocumentDto(string DocType, long DocId, string Number, long PartyId, string PartyName, DateOnly DocumentDate,
    DateOnly DueDate, int DaysOverdue, decimal Total, decimal Balance);

public record EmailStatementRequest(DateOnly? From, DateOnly? To, string? ToAddress);

public record PartyOptionDto(long PartyId, string Name, string? Detail);

public interface IPartyLedgerService
{
    Task<IReadOnlyList<PartyOptionDto>> SearchPartiesAsync(string partyType, string? search, CancellationToken ct = default);
    Task<PartyLedgerDto> GetLedgerAsync(string partyType, long partyId, DateOnly? from, DateOnly? to, CancellationToken ct = default);
    Task<IReadOnlyList<OutstandingPartyDto>> GetOutstandingAsync(string partyType, CancellationToken ct = default);
    Task<AgeingDto> GetAgeingAsync(string partyType, CancellationToken ct = default);
    Task<IReadOnlyList<AgeingDocumentDto>> GetAgeingDocumentsAsync(string partyType, long? partyId, string? bucket, CancellationToken ct = default);
    Task<(byte[] Content, string ContentType, string FileName)> ExportLedgerAsync(string partyType, long partyId, DateOnly? from, DateOnly? to, string format, CancellationToken ct = default);
    Task<byte[]> ExportAgeingAsync(string partyType, CancellationToken ct = default);
    Task<string> EmailStatementAsync(string partyType, long partyId, EmailStatementRequest request, CancellationToken ct = default);
}
