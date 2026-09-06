using FluentValidation;
using Regalis.Shared.Api;

namespace Regalis.Application.Finance;

public record BankAccountDto(
    int BankAccountId,
    string AccountCode,
    string BankName,
    string AccountName,
    string AccountNumberMasked,
    string? Ifsc,
    string? Branch,
    string? AccountType,
    decimal OpeningBalance,
    DateOnly? OpeningBalanceAsOn,
    int? LocationId,
    string? LocationName,
    string? Notes,
    // Derived from the ledger the same way inventory stock is: opening + credits
    // - debits. Never a stored column, so it cannot drift from the transactions.
    decimal CurrentBalance,
    decimal TotalCredits,
    decimal TotalDebits,
    int UnreconciledCount,
    bool IsActive);

public record SaveBankAccountRequest(
    string AccountCode,
    string BankName,
    string AccountName,
    string AccountNumberMasked,
    string? Ifsc,
    string? Branch,
    string? AccountType,
    decimal OpeningBalance,
    DateOnly? OpeningBalanceAsOn,
    int? LocationId,
    string? Notes);

public record BankTransactionDto(
    long BankTransactionId,
    int BankAccountId,
    string BankName,
    string AccountCode,
    DateOnly TransactionDate,
    // 1 Credit, 2 Debit.
    byte Direction,
    decimal Amount,
    string Narration,
    string? ReferenceNumber,
    string? BankReferenceId,
    string? Category,
    // 1 Unreconciled, 2 Matched, 3 Reconciled.
    byte ReconciliationStatus,
    long? MatchedPaymentId,
    string? MatchedPaymentNumber,
    long? MatchedExpenseId,
    string? MatchedExpenseNumber,
    DateTime? MatchedUtc,
    string? MatchedByName,
    DateTime? ReconciledUtc,
    string? ReconciledByName,
    int LocationId,
    string? Remarks,
    string? ImportBatchId,
    DateTime CreatedDate);

public record SaveBankTransactionRequest(
    int BankAccountId,
    DateOnly? TransactionDate,
    byte Direction,
    decimal Amount,
    string Narration,
    string? ReferenceNumber,
    string? BankReferenceId,
    string? Category,
    int LocationId,
    string? Remarks);

/// <summary>
/// Links a bank line to a record that already exists. Exactly one of the two ids is
/// given: a credit matches a customer payment, a debit matches a company expense.
/// Matching never creates either — that is the document's no-duplicates rule (§14).
/// </summary>
public record MatchBankTransactionRequest(long? PaymentId, long? ExpenseId);

/// <summary>A candidate the admin can match a bank line to, with why it was suggested.</summary>
public record MatchCandidateDto(
    string Kind,
    long Id,
    string Number,
    string Description,
    decimal Amount,
    DateOnly Date,
    bool AmountMatches,
    bool AlreadyMatched);

public class BankTransactionListRequest : PagedRequest
{
    public int? BankAccountId { get; set; }
    public byte? Direction { get; set; }
    public byte? ReconciliationStatus { get; set; }
    public int? LocationId { get; set; }
    public DateOnly? From { get; set; }
    public DateOnly? To { get; set; }
}

public record BankExportResult(byte[] Content, string ContentType, string FileName);

public class SaveBankAccountRequestValidator : AbstractValidator<SaveBankAccountRequest>
{
    public SaveBankAccountRequestValidator()
    {
        RuleFor(x => x.AccountCode).NotEmpty().MaximumLength(30);
        RuleFor(x => x.BankName).NotEmpty().MaximumLength(150);
        RuleFor(x => x.AccountName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.AccountNumberMasked).NotEmpty().MaximumLength(40);
        RuleFor(x => x.Ifsc).Length(11)
            .Matches("^[A-Z]{4}0[A-Z0-9]{6}$")
            .WithMessage("Enter a valid 11-character IFSC.")
            .When(x => !string.IsNullOrWhiteSpace(x.Ifsc));
        RuleFor(x => x.Branch).MaximumLength(150);
        RuleFor(x => x.AccountType).MaximumLength(40);
        RuleFor(x => x.LocationId).GreaterThan(0).When(x => x.LocationId is not null);
        RuleFor(x => x.Notes).MaximumLength(500);
    }
}

public class SaveBankTransactionRequestValidator : AbstractValidator<SaveBankTransactionRequest>
{
    public SaveBankTransactionRequestValidator()
    {
        RuleFor(x => x.BankAccountId).GreaterThan(0);
        RuleFor(x => x.Direction).InclusiveBetween((byte)1, (byte)2)
            .WithMessage("A bank line is either a credit (1) or a debit (2).");
        RuleFor(x => x.Amount).GreaterThan(0);
        RuleFor(x => x.Narration).NotEmpty().MaximumLength(500);
        RuleFor(x => x.ReferenceNumber).MaximumLength(80);
        RuleFor(x => x.BankReferenceId).MaximumLength(120);
        RuleFor(x => x.Category).MaximumLength(100);
        RuleFor(x => x.LocationId).GreaterThan(0);
        RuleFor(x => x.Remarks).MaximumLength(500);
    }
}

public class MatchBankTransactionRequestValidator : AbstractValidator<MatchBankTransactionRequest>
{
    public MatchBankTransactionRequestValidator()
    {
        RuleFor(x => x)
            .Must(x => (x.PaymentId is not null) ^ (x.ExpenseId is not null))
            .WithMessage("Match the line to either a payment or an expense, not both.")
            .WithName(nameof(MatchBankTransactionRequest.PaymentId));
    }
}

/// <summary>
/// The bank book (document §13–§14). Two rules define it. Reconciliation state lives
/// only here: matching a bank line never writes to TRN_Payment, TRN_Invoice or
/// TRN_Expense, so cash received keeps exactly one owner. And matching only ever
/// points at a record that already exists — it never creates a payment or an expense,
/// which is what stops a statement import from duplicating the books.
/// </summary>
public interface IBankService
{
    Task<List<BankAccountDto>> GetAccountsAsync(CancellationToken ct = default);

    Task<BankAccountDto> GetAccountAsync(int bankAccountId, CancellationToken ct = default);

    Task<BankAccountDto> CreateAccountAsync(SaveBankAccountRequest request,
        CancellationToken ct = default);

    Task<BankAccountDto> UpdateAccountAsync(int bankAccountId, SaveBankAccountRequest request,
        CancellationToken ct = default);

    Task DeleteAccountAsync(int bankAccountId, CancellationToken ct = default);

    Task<PagedResult<BankTransactionDto>> GetTransactionsAsync(BankTransactionListRequest request,
        CancellationToken ct = default);

    Task<BankTransactionDto> GetTransactionAsync(long bankTransactionId,
        CancellationToken ct = default);

    Task<BankTransactionDto> CreateTransactionAsync(SaveBankTransactionRequest request,
        CancellationToken ct = default);

    /// <summary>Editable only while the line is still unreconciled.</summary>
    Task<BankTransactionDto> UpdateTransactionAsync(long bankTransactionId,
        SaveBankTransactionRequest request, CancellationToken ct = default);

    Task DeleteTransactionAsync(long bankTransactionId, CancellationToken ct = default);

    /// <summary>Payments and expenses that plausibly correspond to this bank line —
    /// same direction, near the same date, ranked by an exact amount match.</summary>
    Task<List<MatchCandidateDto>> GetMatchCandidatesAsync(long bankTransactionId,
        CancellationToken ct = default);

    Task<BankTransactionDto> MatchAsync(long bankTransactionId,
        MatchBankTransactionRequest request, CancellationToken ct = default);

    /// <summary>Breaks a match. The payment or expense is untouched, because the match
    /// never wrote to it in the first place.</summary>
    Task<BankTransactionDto> UnmatchAsync(long bankTransactionId, CancellationToken ct = default);

    /// <summary>Matched to Reconciled: a human confirming the match is correct.</summary>
    Task<BankTransactionDto> ReconcileAsync(long bankTransactionId, CancellationToken ct = default);

    Task<BankExportResult> ExportAsync(BankTransactionListRequest request,
        CancellationToken ct = default);
}
