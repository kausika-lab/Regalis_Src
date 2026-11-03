using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Regalis.Application.Abstractions;
using Regalis.Application.Common.Exceptions;
using Regalis.Application.Finance.Billing;
using Regalis.Domain.Entities.Finance.Billing;
using Regalis.Infrastructure.Persistence;
using Regalis.Shared.Api;

namespace Regalis.Infrastructure.Services.Billing;

/// <summary>
/// Petty cash (build doc §12.3): one float, a running balance that can never go below zero
/// (also a check constraint), top-ups from a bank account, cash expenses out, and counts
/// that write an adjustment with a reason when the tin disagrees. Not the Cash Register.
/// </summary>
public class PettyCashService(RegalisDbContext db, ISettingService settings, ICurrentUser currentUser, IDateTimeProvider clock) : IPettyCashService
{
    /// <summary>Writes one entry against the latest balance. Runs inside the caller's
    /// serializable transaction, so two entries cannot both spend the same rupee.</summary>
    internal static async Task<PettyCashEntry> PostAsync(RegalisDbContext db, PettyCashEntryType type, decimal amount, DateOnly on,
        string description, long userId, string? reason, CancellationToken ct, decimal? counted = null)
    {
        await FinanceRules.EnsurePeriodOpenAsync(db, on, ct, "this petty cash entry");
        decimal balance = await db.Set<PettyCashEntry>().OrderByDescending(e => e.PettyCashEntryId)
            .Select(e => (decimal?)e.BalanceAfter).FirstOrDefaultAsync(ct) ?? 0m;
        decimal after = balance + amount;
        if (after < 0m)
        {
            throw new BusinessRuleException($"The tin only has {FinanceRules.Money(balance)}. Top it up before recording this.", "PETTY_CASH_SHORT");
        }
        var entry = new PettyCashEntry
        {
            EntryType = type,
            Amount = amount,
            HappenedOn = on,
            Description = description,
            BalanceAfter = after,
            Reason = reason,
            CountedAmount = counted,
            EnteredByUserId = userId
        };
        db.Set<PettyCashEntry>().Add(entry);
        await db.SaveChangesAsync(ct);
        return entry;
    }

    private IQueryable<PettyCashEntryDto> Project(IQueryable<PettyCashEntry> q) =>
        from e in q
        join x in db.Expenses on e.ExpenseId equals (long?)x.ExpenseId into xj
        from x in xj.DefaultIfEmpty()
        join u in db.Users on e.EnteredByUserId equals u.UserId into uj
        from u in uj.DefaultIfEmpty()
        select new PettyCashEntryDto(e.PettyCashEntryId, (byte)e.EntryType, e.HappenedOn, e.Description,
            e.Amount > 0m ? e.Amount : 0m, e.Amount < 0m ? -e.Amount : 0m, e.BalanceAfter, e.Reason, e.CountedAmount,
            e.ExpenseId, x != null ? x.ExpenseNumber : null, e.VendorPaymentId, e.BankAccountId, u != null ? u.FullName : null, e.CreatedDate,
            db.Set<BankMatchAllocation>().Any(m => m.DocType == BankMatchDocType.PettyCashTopUp && m.DocId == e.PettyCashEntryId));

    public async Task<PettyCashOverviewDto> GetOverviewAsync(PagedRequest request, CancellationToken ct = default)
    {
        IQueryable<PettyCashEntry> q = db.Set<PettyCashEntry>().AsNoTracking();
        decimal balance = await q.OrderByDescending(e => e.PettyCashEntryId).Select(e => (decimal?)e.BalanceAfter).FirstOrDefaultAsync(ct) ?? 0m;
        var lastCount = await (from e in q
                               where e.CountedAmount != null
                               orderby e.PettyCashEntryId descending
                               join u in db.Users on e.EnteredByUserId equals u.UserId into uj
                               from u in uj.DefaultIfEmpty()
                               select new { e.CreatedDate, Name = u != null ? u.FullName : null, e.Amount }).FirstOrDefaultAsync(ct);
        DateOnly today = FinanceRules.Today(clock);
        var monthStart = new DateOnly(today.Year, today.Month, 1);
        var month = await q.Where(e => e.HappenedOn >= monthStart).GroupBy(_ => 1)
            .Select(g => new { In = g.Sum(e => e.Amount > 0m ? e.Amount : 0m), Out = g.Sum(e => e.Amount < 0m ? -e.Amount : 0m) })
            .FirstOrDefaultAsync(ct);
        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            string s = request.Search.Trim();
            q = q.Where(e => e.Description.Contains(s) || (e.Reason != null && e.Reason.Contains(s)));
        }
        int total = await q.CountAsync(ct);
        var items = await Project(q.OrderByDescending(e => e.PettyCashEntryId).Skip((request.Page - 1) * request.PageSize).Take(request.PageSize)).ToListAsync(ct);
        return new PettyCashOverviewDto(balance, await settings.GetDecimalAsync("PettyCash.Float", 5000m, ct), lastCount?.CreatedDate, lastCount?.Name,
            lastCount?.Amount, month?.In ?? 0m, month?.Out ?? 0m, PagedResult<PettyCashEntryDto>.Create(items, request.Page, request.PageSize, total));
    }

    public async Task<PettyCashEntryDto> TopUpAsync(PettyCashTopUpRequest request, CancellationToken ct = default)
    {
        decimal amount = FinanceRules.Round(request.Amount);
        if (amount <= 0m) throw new BusinessRuleException("Enter the amount put into the tin.");
        FinanceRules.EnsureNotFuture(request.HappenedOn, clock, "The top-up date cannot be in the future.");
        long id = await FinanceRules.InTransactionAsync(db, async () =>
        {
            string account = await db.BankAccounts.Where(a => a.BankAccountId == request.BankAccountId && a.IsActive)
                .Select(a => a.BankName + " · " + a.AccountCode).FirstOrDefaultAsync(ct)
                ?? throw new BusinessRuleException("Choose the bank account the cash was withdrawn from.");
            PettyCashEntry e = await PostAsync(db, PettyCashEntryType.TopUp, amount, request.HappenedOn,
                string.IsNullOrWhiteSpace(request.Note) ? $"Top-up from {account}" : request.Note.Trim(), currentUser.UserId, null, ct);
            e.BankAccountId = request.BankAccountId;
            await db.SaveChangesAsync(ct);
            return e.PettyCashEntryId;
        }, ct);
        return await Project(db.Set<PettyCashEntry>().AsNoTracking().Where(e => e.PettyCashEntryId == id)).FirstAsync(ct);
    }

    public async Task<PettyCashEntryDto> CountAsync(PettyCashCountRequest request, CancellationToken ct = default)
    {
        decimal counted = FinanceRules.Round(request.CountedAmount);
        if (counted < 0m) throw new BusinessRuleException("Enter what the tin holds.");
        long id = await FinanceRules.InTransactionAsync(db, async () =>
        {
            decimal balance = await db.Set<PettyCashEntry>().OrderByDescending(e => e.PettyCashEntryId)
                .Select(e => (decimal?)e.BalanceAfter).FirstOrDefaultAsync(ct) ?? 0m;
            decimal diff = counted - balance;
            if (diff != 0m && string.IsNullOrWhiteSpace(request.Reason))
            {
                throw new BusinessRuleException(diff < 0m
                    ? $"The tin counts {FinanceRules.Money(counted)} but should hold {FinanceRules.Money(balance)}. Say what happened to the {FinanceRules.Money(-diff)}."
                    : $"The tin counts {FinanceRules.Money(counted)} but should hold {FinanceRules.Money(balance)}. Say where the extra {FinanceRules.Money(diff)} came from.");
            }
            PettyCashEntry e = await PostAsync(db, PettyCashEntryType.Adjustment, diff, FinanceRules.Today(clock),
                diff == 0m ? $"Counted {FinanceRules.Money(counted)} - agrees" : $"Counted {FinanceRules.Money(counted)} - {(diff < 0m ? "short" : "over")} by {FinanceRules.Money(Math.Abs(diff))}",
                currentUser.UserId, string.IsNullOrWhiteSpace(request.Reason) ? null : request.Reason.Trim(), ct, counted);
            return e.PettyCashEntryId;
        }, ct);
        return await Project(db.Set<PettyCashEntry>().AsNoTracking().Where(e => e.PettyCashEntryId == id)).FirstAsync(ct);
    }
}
