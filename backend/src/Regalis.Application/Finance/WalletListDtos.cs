using FluentValidation;
using Regalis.Shared.Api;

namespace Regalis.Application.Finance;

/// <summary>GET /api/admin/wallets. Search matches name, email or mobile.</summary>
public class WalletListRequest : PagedRequest
{
    /// <summary>"all" (default), "with-balance", "zero", "low" (below Wallet.LowBalanceThreshold).</summary>
    public string? Filter { get; set; }

    /// <summary>"balance-desc" (default), "balance-asc", "name", "last-activity", "spent".</summary>
    public string? Sort { get; set; }

    /// <summary>"all" (default), "30d" / "90d" (activity in that many days), "inactive-90d".</summary>
    public string? Activity { get; set; }
}

/// <summary>One customer's wallet. Credit, debit and balance are summed from the
/// append-only ledger (TRN_WalletTransaction), never read from stored counters, so
/// Balance is always TotalCredit − TotalDebit.</summary>
public record WalletListItemDto(
    long CustomerId,
    string FullName,
    string Email,
    string? MobileNumber,
    decimal TotalCredit,
    decimal TotalDebit,
    decimal Balance,
    DateTime? LastActivityUtc,
    bool IsLowBalance);

/// <summary>The four cards: every wallet, whatever the filters.</summary>
public record WalletSummaryDto(
    decimal BalanceHeld,
    decimal TotalRecharged,
    decimal TotalSpent,
    int CustomerCount,
    int LowBalanceCount,
    decimal LowBalanceThreshold);

/// <summary>The table's totals row: every wallet matching the current filters, across all pages.</summary>
public record WalletTotalsDto(int CustomerCount, decimal TotalCredit, decimal TotalDebit, decimal Balance);

public record WalletListResultDto(
    PagedResult<WalletListItemDto> Wallets,
    WalletSummaryDto Summary,
    WalletTotalsDto FilteredTotals);

public interface IWalletLedgerQueries
{
    Task<WalletListResultDto> GetWalletsAsync(WalletListRequest request, CancellationToken ct = default);

    /// <summary>The filtered list as CSV (every page), for the Export button.</summary>
    Task<byte[]> ExportWalletsAsync(WalletListRequest request, CancellationToken ct = default);
}

// ── Screen 2: one customer's wallet ─────────────────────────────────────────

/// <summary>GET /api/admin/wallets/{customerId}. Search matches the txn number or narration.</summary>
public class WalletLedgerRequest : PagedRequest
{
    /// <summary>"credit" or "debit"; empty for both.</summary>
    public string? Direction { get; set; }

    /// <summary>"recharge", "booking", "refund", "day-pass", "monthly-pass", "service",
    /// "deposit" or "adjustment"; empty for all.</summary>
    public string? Category { get; set; }

    /// <summary>IST calendar dates, inclusive.</summary>
    public DateOnly? From { get; set; }
    public DateOnly? To { get; set; }
}

public record WalletLedgerEntryDto(
    long WalletTransactionId,
    string TransactionNumber,
    // 1 credit, 2 debit.
    byte Direction,
    byte Category,
    decimal Amount,
    decimal BalanceAfter,
    string Narration,
    DateTime TransactionUtc,
    // Set only on Recharge rows backed by a successful payment: the Receipt button.
    long? ReceiptPaymentId,
    string? PaymentNumber,
    string? PerformedByName);

public record WalletCustomerDto(
    long CustomerId,
    string FullName,
    string Email,
    string? MobileNumber,
    DateTime CustomerSinceUtc,
    DateTime? WalletOpenedUtc,
    // The active pass or membership, if any.
    string? MembershipName,
    DateOnly? MembershipValidTo);

/// <summary>Totals are the whole wallet, summed from the ledger; the transaction page
/// follows the filters. LedgerVerified compares the ledger to the stored wallet balance.</summary>
public record WalletDetailDto(
    WalletCustomerDto Customer,
    decimal TotalCredit,
    decimal TotalDebit,
    decimal Balance,
    bool IsLowBalance,
    decimal LowBalanceThreshold,
    bool LedgerVerified,
    PagedResult<WalletLedgerEntryDto> Transactions,
    string NextPaymentNumber);

/// <summary>Money collected at the desk. Creates the PAY- payment and the Recharge
/// credit in one database transaction.</summary>
public record AdminAddMoneyRequest(
    decimal Amount,
    // PaymentMethod: 2 UPI, 3 credit card, 4 debit card, 5 bank transfer, 6 cash, 7 other.
    byte PaymentMethod,
    string? Reference,
    DateOnly? ReceivedOn,
    string? Narration,
    bool NotifyCustomer,
    // The PAY- number the form showed; if another payment took it meanwhile, the
    // narration is rewritten to the number actually issued.
    string? ExpectedPaymentNumber = null);

public record AdminAddMoneyResponse(
    string PaymentNumber,
    long PaymentId,
    string TransactionNumber,
    decimal Balance,
    string? NotificationNote);

/// <summary>A correction entry. Reason goes to the audit trail with the admin's name;
/// Narration is what the customer reads on their statement.</summary>
public record AdminAdjustRequest(string Direction, decimal Amount, string Reason, string Narration);

public class AdminAddMoneyRequestValidator : AbstractValidator<AdminAddMoneyRequest>
{
    public AdminAddMoneyRequestValidator()
    {
        RuleFor(x => x.Amount).GreaterThan(0).LessThanOrEqualTo(10_00_000m)
            .WithMessage("Enter an amount between ₹0.01 and ₹10,00,000.");
        RuleFor(x => x.PaymentMethod).Must(m => m is 2 or 3 or 4 or 5 or 6 or 7)
            .WithMessage("Choose how the money was paid.");
        RuleFor(x => x.Reference).MaximumLength(80);
        RuleFor(x => x.Narration).MaximumLength(240);
        RuleFor(x => x.ReceivedOn).NotNull().WithMessage("Enter the date the money was received.");
        RuleFor(x => x.Amount).PrecisionScale(12, 2, true).WithMessage("At most 2 decimal places.");
    }
}

public class AdminAdjustRequestValidator : AbstractValidator<AdminAdjustRequest>
{
    public AdminAdjustRequestValidator()
    {
        RuleFor(x => x.Direction).Must(d => d is "credit" or "debit").WithMessage("Choose credit or debit.");
        RuleFor(x => x.Amount).GreaterThan(0).LessThanOrEqualTo(10_00_000m)
            .PrecisionScale(12, 2, true).WithMessage("Enter a positive amount with at most 2 decimals.");
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(120);
        RuleFor(x => x.Narration).NotEmpty().WithMessage("Enter the text the customer will see.").MaximumLength(200);
    }
}

public interface IWalletDeskService
{
    Task<WalletDetailDto> GetWalletAsync(long customerId, WalletLedgerRequest request, CancellationToken ct = default);

    /// <summary>"pdf" or "csv"; honours the From/To dates (a statement is a period).</summary>
    Task<(byte[] Content, string ContentType, string FileName)> ExportStatementAsync(long customerId,
        WalletLedgerRequest request, string format, CancellationToken ct = default);

    Task<AdminAddMoneyResponse> AddMoneyAsync(long customerId, AdminAddMoneyRequest request, CancellationToken ct = default);

    Task<WalletDetailDto> AdjustAsync(long customerId, AdminAdjustRequest request, CancellationToken ct = default);
}
