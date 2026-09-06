using System.Globalization;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Regalis.Application.Abstractions;
using Regalis.Application.Common.Exceptions;
using Regalis.Application.Finance;
using Regalis.Domain.Entities.Finance;
using Regalis.Domain.Enums;
using Regalis.Infrastructure.Persistence;
using Regalis.Shared;
using Regalis.Shared.Api;
using Regalis.Shared.Constants;

namespace Regalis.Infrastructure.Services;

/// <summary>
/// The bank book (document §13–§14).
///
/// Reconciliation state lives on TRN_BankTransaction and nowhere else. Matching a bank
/// line to a payment or an expense writes only to the bank row — TRN_Payment,
/// TRN_Invoice and TRN_Expense are never touched — so cash received keeps exactly one
/// owner and a reconciliation can be undone without disturbing the books.
///
/// Matching also only ever points at a record that already exists. Nothing here creates
/// a payment or an expense, which is what stops a statement import from duplicating
/// what the system already knows.
///
/// An account's balance is derived from its ledger (opening + credits − debits), the
/// same way inventory stock is derived, so there is no stored balance to drift.
/// </summary>
public class BankService(
    RegalisDbContext db,
    ICurrentUser currentUser,
    IDateTimeProvider clock) : IBankService
{
    /// <summary>How far either side of the bank date to look for a candidate.</summary>
    private const int CandidateWindowDays = 7;

    // ── Accounts ────────────────────────────────────────────────────────────

    public async Task<List<BankAccountDto>> GetAccountsAsync(CancellationToken ct = default)
    {
        var accounts = await db.BankAccounts.AsNoTracking()
            .OrderBy(a => a.BankName).ThenBy(a => a.AccountCode)
            .ToListAsync(ct);
        return await ShapeAccountsAsync(accounts, ct);
    }

    public async Task<BankAccountDto> GetAccountAsync(int bankAccountId,
        CancellationToken ct = default)
    {
        var account = await db.BankAccounts.AsNoTracking()
            .FirstOrDefaultAsync(a => a.BankAccountId == bankAccountId, ct)
            ?? throw new NotFoundException("BankAccount", bankAccountId);
        return (await ShapeAccountsAsync([account], ct)).Single();
    }

    public async Task<BankAccountDto> CreateAccountAsync(SaveBankAccountRequest request,
        CancellationToken ct = default)
    {
        var code = request.AccountCode.Trim().ToUpperInvariant();
        if (await db.BankAccounts.AnyAsync(a => a.AccountCode == code, ct))
        {
            throw new ConflictException($"Bank account code {code} is already in use.",
                ErrorCodes.DuplicateRecord);
        }
        await GuardLocationAsync(request.LocationId, ct);

        var account = new BankAccount { AccountCode = code };
        ApplyAccount(account, request);
        db.BankAccounts.Add(account);
        await db.SaveChangesAsync(ct);
        return await GetAccountAsync(account.BankAccountId, ct);
    }

    public async Task<BankAccountDto> UpdateAccountAsync(int bankAccountId,
        SaveBankAccountRequest request, CancellationToken ct = default)
    {
        var account = await db.BankAccounts
            .FirstOrDefaultAsync(a => a.BankAccountId == bankAccountId, ct)
            ?? throw new NotFoundException("BankAccount", bankAccountId);
        await GuardLocationAsync(request.LocationId, ct);

        // The opening balance is the anchor every later balance is derived from;
        // changing it once the ledger has rows would silently restate history.
        var hasLedger = await db.BankTransactions.AnyAsync(t => t.BankAccountId == bankAccountId, ct);
        if (hasLedger && account.OpeningBalance != request.OpeningBalance)
        {
            throw new BusinessRuleException(
                "This account has transactions. Correct the balance with a bank line, "
                + "not by editing the opening balance.");
        }

        var code = request.AccountCode.Trim().ToUpperInvariant();
        if (code != account.AccountCode
            && await db.BankAccounts.AnyAsync(a => a.AccountCode == code
                                                  && a.BankAccountId != bankAccountId, ct))
        {
            throw new ConflictException($"Bank account code {code} is already in use.",
                ErrorCodes.DuplicateRecord);
        }
        account.AccountCode = code;
        ApplyAccount(account, request);
        await db.SaveChangesAsync(ct);
        return await GetAccountAsync(bankAccountId, ct);
    }

    public async Task DeleteAccountAsync(int bankAccountId, CancellationToken ct = default)
    {
        var account = await db.BankAccounts
            .FirstOrDefaultAsync(a => a.BankAccountId == bankAccountId, ct)
            ?? throw new NotFoundException("BankAccount", bankAccountId);
        if (await db.BankTransactions.AnyAsync(t => t.BankAccountId == bankAccountId, ct))
        {
            throw new BusinessRuleException(
                "This account has transactions and cannot be deleted — deactivate it instead.");
        }
        db.BankAccounts.Remove(account); // interceptor converts to soft delete
        await db.SaveChangesAsync(ct);
    }

    // ── Transactions ────────────────────────────────────────────────────────

    public async Task<PagedResult<BankTransactionDto>> GetTransactionsAsync(
        BankTransactionListRequest request, CancellationToken ct = default)
    {
        var query = Filter(request);
        var totalCount = await query.CountAsync(ct);
        var rows = await Shape(query
                .OrderByDescending(t => t.TransactionDate).ThenByDescending(t => t.BankTransactionId)
                .Skip((request.Page - 1) * request.PageSize)
                .Take(request.PageSize))
            .ToListAsync(ct);
        return PagedResult<BankTransactionDto>.Create(rows, request.Page, request.PageSize, totalCount);
    }

    public async Task<BankTransactionDto> GetTransactionAsync(long bankTransactionId,
        CancellationToken ct = default) =>
        await Shape(db.BankTransactions.AsNoTracking()
                .Where(t => t.BankTransactionId == bankTransactionId))
            .FirstOrDefaultAsync(ct)
        ?? throw new NotFoundException("BankTransaction", bankTransactionId);

    public async Task<BankTransactionDto> CreateTransactionAsync(SaveBankTransactionRequest request,
        CancellationToken ct = default)
    {
        await GuardTransactionAsync(request, ct);

        // Re-importing the same statement line must not duplicate it. The filtered
        // unique index enforces this too; catching it here gives a readable message.
        var bankReference = request.BankReferenceId?.Trim();
        if (!string.IsNullOrWhiteSpace(bankReference)
            && await db.BankTransactions.AnyAsync(t => t.BankAccountId == request.BankAccountId
                                                       && t.BankReferenceId == bankReference, ct))
        {
            throw new ConflictException(
                $"A line with bank reference {bankReference} is already recorded on this account.",
                ErrorCodes.DuplicateRecord);
        }

        var entry = new BankTransaction
        {
            BankAccountId = request.BankAccountId,
            TransactionDate = request.TransactionDate ?? IstClock.IstDateOf(clock.UtcNow),
            EnteredByUserId = currentUser.UserId,
            ReconciliationStatus = ReconciliationStatus.Unreconciled
        };
        ApplyTransaction(entry, request);
        db.BankTransactions.Add(entry);
        await db.SaveChangesAsync(ct);
        return await GetTransactionAsync(entry.BankTransactionId, ct);
    }

    public async Task<BankTransactionDto> UpdateTransactionAsync(long bankTransactionId,
        SaveBankTransactionRequest request, CancellationToken ct = default)
    {
        var entry = await db.BankTransactions
            .FirstOrDefaultAsync(t => t.BankTransactionId == bankTransactionId, ct)
            ?? throw new NotFoundException("BankTransaction", bankTransactionId);

        if (entry.ReconciliationStatus != ReconciliationStatus.Unreconciled)
        {
            throw new BusinessRuleException(
                "This line is matched. Unmatch it before editing the amount or narration.");
        }
        await GuardTransactionAsync(request, ct);
        entry.TransactionDate = request.TransactionDate ?? entry.TransactionDate;
        ApplyTransaction(entry, request);
        await db.SaveChangesAsync(ct);
        return await GetTransactionAsync(bankTransactionId, ct);
    }

    public async Task DeleteTransactionAsync(long bankTransactionId, CancellationToken ct = default)
    {
        var entry = await db.BankTransactions
            .FirstOrDefaultAsync(t => t.BankTransactionId == bankTransactionId, ct)
            ?? throw new NotFoundException("BankTransaction", bankTransactionId);
        if (entry.ReconciliationStatus != ReconciliationStatus.Unreconciled)
        {
            throw new BusinessRuleException(
                "This line is matched. Unmatch it before deleting.");
        }
        db.BankTransactions.Remove(entry); // interceptor converts to soft delete
        await db.SaveChangesAsync(ct);
    }

    // ── Matching ────────────────────────────────────────────────────────────

    public async Task<List<MatchCandidateDto>> GetMatchCandidatesAsync(long bankTransactionId,
        CancellationToken ct = default)
    {
        var entry = await db.BankTransactions.AsNoTracking()
            .FirstOrDefaultAsync(t => t.BankTransactionId == bankTransactionId, ct)
            ?? throw new NotFoundException("BankTransaction", bankTransactionId);

        var from = entry.TransactionDate.AddDays(-CandidateWindowDays);
        var to = entry.TransactionDate.AddDays(CandidateWindowDays);

        // A credit is money in, so it can only be a customer payment; a debit is money
        // out, so it can only be a company expense.
        if (entry.Direction == BankTransactionDirection.Credit)
        {
            var fromUtc = IstClock.ToUtc(from.ToDateTime(TimeOnly.MinValue));
            var toUtc = IstClock.ToUtc(to.ToDateTime(TimeOnly.MaxValue));

            var payments = await db.Payments.AsNoTracking()
                .Where(p => p.Status == PaymentStatus.Success
                            && p.PaidUtc != null
                            && p.PaidUtc >= fromUtc && p.PaidUtc <= toUtc)
                .Select(p => new
                {
                    p.PaymentId,
                    p.PaymentNumber,
                    p.Amount,
                    p.PaidUtc,
                    CustomerName = db.Users.Where(u => u.UserId == p.CustomerId)
                        .Select(u => u.FullName).FirstOrDefault(),
                    InvoiceNumber = db.Invoices.Where(i => i.InvoiceId == p.InvoiceId)
                        .Select(i => i.InvoiceNumber).FirstOrDefault(),
                    Taken = db.BankTransactions.Any(b => b.MatchedPaymentId == p.PaymentId)
                })
                .ToListAsync(ct);

            return payments
                .Select(p => new MatchCandidateDto("Payment", p.PaymentId, p.PaymentNumber,
                    $"{p.CustomerName}{(p.InvoiceNumber is null ? "" : $" · {p.InvoiceNumber}")}",
                    p.Amount, IstClock.IstDateOf(p.PaidUtc!.Value),
                    p.Amount == entry.Amount, p.Taken))
                .OrderByDescending(c => c.AmountMatches).ThenBy(c => c.AlreadyMatched)
                .ThenByDescending(c => c.Date)
                .ToList();
        }

        var expenses = await db.Expenses.AsNoTracking()
            .Where(e => e.ExpenseDate >= from && e.ExpenseDate <= to)
            .Select(e => new
            {
                e.ExpenseId,
                e.ExpenseNumber,
                e.Amount,
                e.ExpenseDate,
                e.Category,
                e.Description,
                Taken = db.BankTransactions.Any(b => b.MatchedExpenseId == e.ExpenseId)
            })
            .ToListAsync(ct);

        return expenses
            .Select(e => new MatchCandidateDto("Expense", e.ExpenseId, e.ExpenseNumber,
                $"{e.Category} · {e.Description}", e.Amount, e.ExpenseDate,
                e.Amount == entry.Amount, e.Taken))
            .OrderByDescending(c => c.AmountMatches).ThenBy(c => c.AlreadyMatched)
            .ThenByDescending(c => c.Date)
            .ToList();
    }

    public async Task<BankTransactionDto> MatchAsync(long bankTransactionId,
        MatchBankTransactionRequest request, CancellationToken ct = default)
    {
        var entry = await db.BankTransactions
            .FirstOrDefaultAsync(t => t.BankTransactionId == bankTransactionId, ct)
            ?? throw new NotFoundException("BankTransaction", bankTransactionId);

        if (entry.ReconciliationStatus == ReconciliationStatus.Reconciled)
        {
            throw new BusinessRuleException(
                "This line is already reconciled. Unmatch it first to change the match.");
        }

        if (request.PaymentId is not null)
        {
            if (entry.Direction != BankTransactionDirection.Credit)
            {
                throw new BusinessRuleException(
                    "A customer payment is money in, so it can only match a credit line.");
            }
            var payment = await db.Payments.AsNoTracking()
                .FirstOrDefaultAsync(p => p.PaymentId == request.PaymentId, ct)
                ?? throw new NotFoundException("Payment", request.PaymentId);
            if (payment.Status != PaymentStatus.Success)
            {
                throw new BusinessRuleException(
                    "Only a successful payment can be reconciled against the bank.");
            }
            if (await db.BankTransactions.AnyAsync(b => b.MatchedPaymentId == payment.PaymentId
                                                        && b.BankTransactionId != bankTransactionId, ct))
            {
                throw new ConflictException(
                    $"Payment {payment.PaymentNumber} is already matched to another bank line.",
                    ErrorCodes.DuplicateRecord);
            }
            entry.MatchedPaymentId = payment.PaymentId;
            entry.MatchedExpenseId = null;
        }
        else
        {
            if (entry.Direction != BankTransactionDirection.Debit)
            {
                throw new BusinessRuleException(
                    "An expense is money out, so it can only match a debit line.");
            }
            var expense = await db.Expenses.AsNoTracking()
                .FirstOrDefaultAsync(e => e.ExpenseId == request.ExpenseId, ct)
                ?? throw new NotFoundException("Expense", request.ExpenseId!);
            if (await db.BankTransactions.AnyAsync(b => b.MatchedExpenseId == expense.ExpenseId
                                                        && b.BankTransactionId != bankTransactionId, ct))
            {
                throw new ConflictException(
                    $"Expense {expense.ExpenseNumber} is already matched to another bank line.",
                    ErrorCodes.DuplicateRecord);
            }
            entry.MatchedExpenseId = expense.ExpenseId;
            entry.MatchedPaymentId = null;
        }

        entry.ReconciliationStatus = ReconciliationStatus.Matched;
        entry.MatchedUtc = clock.UtcNow;
        entry.MatchedByUserId = currentUser.UserId;
        // Nothing is written to the payment or the expense. That is the whole point.
        await db.SaveChangesAsync(ct);
        return await GetTransactionAsync(bankTransactionId, ct);
    }

    public async Task<BankTransactionDto> UnmatchAsync(long bankTransactionId,
        CancellationToken ct = default)
    {
        var entry = await db.BankTransactions
            .FirstOrDefaultAsync(t => t.BankTransactionId == bankTransactionId, ct)
            ?? throw new NotFoundException("BankTransaction", bankTransactionId);

        if (entry.ReconciliationStatus == ReconciliationStatus.Unreconciled)
        {
            throw new BusinessRuleException("This line is not matched to anything.");
        }

        entry.MatchedPaymentId = null;
        entry.MatchedExpenseId = null;
        entry.MatchedUtc = null;
        entry.MatchedByUserId = null;
        entry.ReconciledUtc = null;
        entry.ReconciledByUserId = null;
        entry.ReconciliationStatus = ReconciliationStatus.Unreconciled;
        await db.SaveChangesAsync(ct);
        return await GetTransactionAsync(bankTransactionId, ct);
    }

    public async Task<BankTransactionDto> ReconcileAsync(long bankTransactionId,
        CancellationToken ct = default)
    {
        var entry = await db.BankTransactions
            .FirstOrDefaultAsync(t => t.BankTransactionId == bankTransactionId, ct)
            ?? throw new NotFoundException("BankTransaction", bankTransactionId);

        if (entry.ReconciliationStatus != ReconciliationStatus.Matched)
        {
            throw new BusinessRuleException(
                entry.ReconciliationStatus == ReconciliationStatus.Reconciled
                    ? "This line is already reconciled."
                    : "Match the line to a payment or an expense before reconciling it.");
        }

        entry.ReconciliationStatus = ReconciliationStatus.Reconciled;
        entry.ReconciledUtc = clock.UtcNow;
        entry.ReconciledByUserId = currentUser.UserId;
        await db.SaveChangesAsync(ct);
        return await GetTransactionAsync(bankTransactionId, ct);
    }

    public async Task<BankExportResult> ExportAsync(BankTransactionListRequest request,
        CancellationToken ct = default)
    {
        var rows = await Shape(Filter(request)
                .OrderBy(t => t.TransactionDate).ThenBy(t => t.BankTransactionId))
            .ToListAsync(ct);

        var csv = new StringBuilder();
        csv.AppendLine("Date,Bank,Account,Type,Amount,Narration,Reference,Bank reference," +
                       "Category,Status,Matched to,Remarks");
        foreach (var row in rows)
        {
            csv.AppendLine(string.Join(',', new[]
            {
                Csv(row.TransactionDate.ToString("dd-MM-yyyy", CultureInfo.InvariantCulture)),
                Csv(row.BankName),
                Csv(row.AccountCode),
                row.Direction == 1 ? "Credit" : "Debit",
                row.Amount.ToString("0.00", CultureInfo.InvariantCulture),
                Csv(row.Narration),
                Csv(row.ReferenceNumber),
                Csv(row.BankReferenceId),
                Csv(row.Category),
                Csv(StatusName(row.ReconciliationStatus)),
                Csv(row.MatchedPaymentNumber ?? row.MatchedExpenseNumber),
                Csv(row.Remarks)
            }));
        }

        var fileName = $"bank-statement-{IstClock.IstDateOf(clock.UtcNow):yyyy-MM-dd}.csv";
        return new BankExportResult(Encoding.UTF8.GetBytes(csv.ToString()), "text/csv", fileName);
    }

    // ── helpers ─────────────────────────────────────────────────────────────

    private IQueryable<BankTransaction> Filter(BankTransactionListRequest request)
    {
        var query = db.BankTransactions.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var term = request.Search.Trim();
            query = query.Where(t => t.Narration.Contains(term)
                                     || (t.ReferenceNumber != null && t.ReferenceNumber.Contains(term))
                                     || (t.BankReferenceId != null && t.BankReferenceId.Contains(term)));
        }
        if (request.BankAccountId is not null)
        {
            query = query.Where(t => t.BankAccountId == request.BankAccountId);
        }
        if (request.Direction is not null)
        {
            query = query.Where(t => (byte)t.Direction == request.Direction);
        }
        if (request.ReconciliationStatus is not null)
        {
            query = query.Where(t => (byte)t.ReconciliationStatus == request.ReconciliationStatus);
        }
        if (request.LocationId is not null)
        {
            query = query.Where(t => t.LocationId == request.LocationId);
        }
        if (request.From is not null)
        {
            query = query.Where(t => t.TransactionDate >= request.From);
        }
        if (request.To is not null)
        {
            query = query.Where(t => t.TransactionDate <= request.To);
        }
        return query;
    }

    private IQueryable<BankTransactionDto> Shape(IQueryable<BankTransaction> query) =>
        query.Select(t => new BankTransactionDto(
            t.BankTransactionId, t.BankAccountId,
            db.BankAccounts.Where(a => a.BankAccountId == t.BankAccountId)
                .Select(a => a.BankName).FirstOrDefault() ?? string.Empty,
            db.BankAccounts.Where(a => a.BankAccountId == t.BankAccountId)
                .Select(a => a.AccountCode).FirstOrDefault() ?? string.Empty,
            t.TransactionDate, (byte)t.Direction, t.Amount, t.Narration, t.ReferenceNumber,
            t.BankReferenceId, t.Category, (byte)t.ReconciliationStatus,
            t.MatchedPaymentId,
            db.Payments.Where(p => p.PaymentId == t.MatchedPaymentId)
                .Select(p => p.PaymentNumber).FirstOrDefault(),
            t.MatchedExpenseId,
            db.Expenses.Where(e => e.ExpenseId == t.MatchedExpenseId)
                .Select(e => e.ExpenseNumber).FirstOrDefault(),
            t.MatchedUtc,
            db.Users.Where(u => u.UserId == t.MatchedByUserId)
                .Select(u => u.FullName).FirstOrDefault(),
            t.ReconciledUtc,
            db.Users.Where(u => u.UserId == t.ReconciledByUserId)
                .Select(u => u.FullName).FirstOrDefault(),
            t.LocationId, t.Remarks, t.ImportBatchId, t.CreatedDate));

    private async Task<List<BankAccountDto>> ShapeAccountsAsync(List<BankAccount> accounts,
        CancellationToken ct)
    {
        var ids = accounts.Select(a => a.BankAccountId).ToList();
        var sums = await db.BankTransactions.AsNoTracking()
            .Where(t => ids.Contains(t.BankAccountId))
            .GroupBy(t => new { t.BankAccountId, t.Direction })
            .Select(g => new { g.Key.BankAccountId, g.Key.Direction, Total = g.Sum(t => t.Amount) })
            .ToListAsync(ct);
        var unreconciled = await db.BankTransactions.AsNoTracking()
            .Where(t => ids.Contains(t.BankAccountId)
                        && t.ReconciliationStatus != ReconciliationStatus.Reconciled)
            .GroupBy(t => t.BankAccountId)
            .Select(g => new { BankAccountId = g.Key, Count = g.Count() })
            .ToListAsync(ct);
        var locations = await db.Locations.AsNoTracking()
            .Select(l => new { l.LocationId, l.LocationName })
            .ToListAsync(ct);

        return accounts.Select(a =>
        {
            var credits = sums.Where(s => s.BankAccountId == a.BankAccountId
                                          && s.Direction == BankTransactionDirection.Credit)
                .Sum(s => s.Total);
            var debits = sums.Where(s => s.BankAccountId == a.BankAccountId
                                         && s.Direction == BankTransactionDirection.Debit)
                .Sum(s => s.Total);
            return new BankAccountDto(
                a.BankAccountId, a.AccountCode, a.BankName, a.AccountName, a.AccountNumberMasked,
                a.Ifsc, a.Branch, a.AccountType, a.OpeningBalance, a.OpeningBalanceAsOn,
                a.LocationId,
                locations.FirstOrDefault(l => l.LocationId == a.LocationId)?.LocationName,
                a.Notes,
                a.OpeningBalance + credits - debits, credits, debits,
                unreconciled.FirstOrDefault(u => u.BankAccountId == a.BankAccountId)?.Count ?? 0,
                a.IsActive);
        }).ToList();
    }

    private async Task GuardLocationAsync(int? locationId, CancellationToken ct)
    {
        if (locationId is not null
            && !await db.Locations.AnyAsync(l => l.LocationId == locationId, ct))
        {
            throw new NotFoundException("Location", locationId);
        }
    }

    private async Task GuardTransactionAsync(SaveBankTransactionRequest request, CancellationToken ct)
    {
        if (!await db.BankAccounts.AnyAsync(a => a.BankAccountId == request.BankAccountId, ct))
        {
            throw new NotFoundException("BankAccount", request.BankAccountId);
        }
        if (!await db.Locations.AnyAsync(l => l.LocationId == request.LocationId, ct))
        {
            throw new NotFoundException("Location", request.LocationId);
        }
    }

    private static void ApplyAccount(BankAccount account, SaveBankAccountRequest request)
    {
        account.BankName = request.BankName.Trim();
        account.AccountName = request.AccountName.Trim();
        account.AccountNumberMasked = request.AccountNumberMasked.Trim();
        account.Ifsc = request.Ifsc?.Trim().ToUpperInvariant();
        account.Branch = request.Branch?.Trim();
        account.AccountType = request.AccountType?.Trim();
        account.OpeningBalance = request.OpeningBalance;
        account.OpeningBalanceAsOn = request.OpeningBalanceAsOn;
        account.LocationId = request.LocationId;
        account.Notes = request.Notes?.Trim();
    }

    private static void ApplyTransaction(BankTransaction entry, SaveBankTransactionRequest request)
    {
        entry.BankAccountId = request.BankAccountId;
        entry.Direction = (BankTransactionDirection)request.Direction;
        entry.Amount = request.Amount;
        entry.Narration = request.Narration.Trim();
        entry.ReferenceNumber = request.ReferenceNumber?.Trim();
        entry.BankReferenceId = request.BankReferenceId?.Trim();
        entry.Category = request.Category?.Trim();
        entry.LocationId = request.LocationId;
        entry.Remarks = request.Remarks?.Trim();
    }

    private static string StatusName(byte status) => status switch
    {
        1 => "Unreconciled",
        2 => "Matched",
        _ => "Reconciled"
    };

    /// <summary>Quote a CSV field so a comma in a narration cannot shift columns.</summary>
    private static string Csv(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }
        var escaped = value.Replace("\"", "\"\"");
        return $"\"{escaped}\"";
    }
}
