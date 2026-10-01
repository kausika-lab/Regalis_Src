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
/// Daily company expenses (document §5). Description and Remarks are required — the
/// document calls that out explicitly. An expense that a bank statement line has
/// already been matched to is frozen: editing its amount afterwards would silently
/// invalidate the reconciliation without touching the bank row that points at it.
/// </summary>
public class ExpenseService(
    RegalisDbContext db,
    INumberSequenceService numberSequence,
    ICurrentUser currentUser,
    IDateTimeProvider clock) : IExpenseService
{
    public async Task<PagedResult<ExpenseDto>> GetExpensesAsync(ExpenseListRequest request,
        CancellationToken ct = default)
    {
        var query = Filter(request);
        var totalCount = await query.CountAsync(ct);
        var rows = await Shape(query
                .OrderByDescending(e => e.ExpenseDate).ThenByDescending(e => e.ExpenseId)
                .Skip((request.Page - 1) * request.PageSize)
                .Take(request.PageSize))
            .ToListAsync(ct);
        return PagedResult<ExpenseDto>.Create(rows, request.Page, request.PageSize, totalCount);
    }

    public async Task<ExpenseDto> GetExpenseAsync(long expenseId, CancellationToken ct = default) =>
        await Shape(db.Expenses.AsNoTracking().Where(e => e.ExpenseId == expenseId))
            .FirstOrDefaultAsync(ct)
        ?? throw new NotFoundException("Expense", expenseId);

    public async Task<ExpenseSummaryDto> GetSummaryAsync(ExpenseListRequest request,
        CancellationToken ct = default)
    {
        var query = Filter(request);
        // Order over an anonymous type, not over ExpenseCategoryTotalDto: EF cannot
        // bind `Amount` back through a positional record's constructor to the g.Sum()
        // behind it, so ordering on the DTO pushes the whole GroupBy to client
        // evaluation and throws. The DTO is built once the rows are materialised.
        var totals = await query
            .GroupBy(e => e.Category)
            .Select(g => new
            {
                Category = g.Key,
                Amount = g.Sum(e => e.Amount + e.AdjustmentTotal),
                Gross = g.Sum(e => e.Amount),
                Count = g.Count()
            })
            .OrderByDescending(row => row.Amount)
            .ToListAsync(ct);

        var byCategory = totals
            .Select(row => new ExpenseCategoryTotalDto(row.Category, row.Amount, row.Count))
            .ToList();

        var net = byCategory.Sum(c => c.Amount);
        var gross = totals.Sum(row => row.Gross);
        return new ExpenseSummaryDto(net, byCategory.Sum(c => c.Count), byCategory,
            gross, net - gross);
    }

    public async Task<ExpenseMonthlyDto> GetMonthlyAsync(ExpenseMonthlyRequest request,
        CancellationToken ct = default)
    {
        var year = request.Year ?? IstClock.IstDateOf(clock.UtcNow).Year;
        if (year is < 2000 or > 2100)
        {
            throw new BusinessRuleException("Choose a year between 2000 and 2100.");
        }

        var yearStart = new DateOnly(year, 1, 1);
        var yearEnd = new DateOnly(year, 12, 31);
        var rows = await Filter(request)
            .Where(e => e.ExpenseDate >= yearStart && e.ExpenseDate <= yearEnd)
            .GroupBy(e => e.ExpenseDate.Month)
            .Select(g => new { Month = g.Key, Amount = g.Sum(e => e.Amount + e.AdjustmentTotal), Count = g.Count() })
            .ToListAsync(ct);

        var months = Enumerable.Range(1, 12)
            .Select(m => rows.FirstOrDefault(r => r.Month == m) is { } row
                ? new ExpenseMonthTotalDto(m, row.Amount, row.Count)
                : new ExpenseMonthTotalDto(m, 0m, 0))
            .ToList();
        return new ExpenseMonthlyDto(year, months.Sum(m => m.Amount), months);
    }

    public async Task<List<string>> GetCategoriesAsync(CancellationToken ct = default) =>
        await db.Expenses.AsNoTracking()
            .Select(e => e.Category)
            .Distinct()
            .OrderBy(c => c)
            .ToListAsync(ct);

    public async Task<ExpenseDto> CreateAsync(SaveExpenseRequest request,
        CancellationToken ct = default)
    {
        await GuardReferencesAsync(request, ct);

        var expense = new Expense
        {
            ExpenseDate = request.ExpenseDate ?? IstClock.IstDateOf(clock.UtcNow),
            EnteredByUserId = currentUser.UserId
        };
        Apply(expense, request);

        // Voucher number and row commit together, so a rollback leaves no gap.
        var strategy = db.Database.CreateExecutionStrategy();
        var newId = await strategy.ExecuteAsync(async () =>
        {
            await using var dbTransaction = await db.Database.BeginTransactionAsync(ct);
            expense.ExpenseNumber = await numberSequence.NextAsync(SequenceKeys.Expense, ct);
            db.Expenses.Add(expense);
            await db.SaveChangesAsync(ct);
            await dbTransaction.CommitAsync(ct);
            return expense.ExpenseId;
        });

        return await GetExpenseAsync(newId, ct);
    }

    public async Task<ExpenseDto> UpdateAsync(long expenseId, SaveExpenseRequest request,
        CancellationToken ct = default)
    {
        var expense = await db.Expenses.FirstOrDefaultAsync(e => e.ExpenseId == expenseId, ct)
            ?? throw new NotFoundException("Expense", expenseId);

        await GuardNotReconciledAsync(expenseId, "edited", ct);
        await GuardReferencesAsync(request, ct);
        if (request.Amount + expense.AdjustmentTotal < 0)
        {
            throw new BusinessRuleException(
                $"This expense carries {expense.AdjustmentTotal:N2} of adjustments, so its amount " +
                $"cannot be less than {-expense.AdjustmentTotal:N2}. Change the adjustments first.");
        }

        expense.ExpenseDate = request.ExpenseDate ?? expense.ExpenseDate;
        Apply(expense, request);
        await db.SaveChangesAsync(ct);
        return await GetExpenseAsync(expenseId, ct);
    }

    public async Task DeleteAsync(long expenseId, CancellationToken ct = default)
    {
        var expense = await db.Expenses.FirstOrDefaultAsync(e => e.ExpenseId == expenseId, ct)
            ?? throw new NotFoundException("Expense", expenseId);

        await GuardNotReconciledAsync(expenseId, "deleted", ct);
        // The adjustments go with the voucher, so none is left pointing at a hidden row.
        var adjustments = await db.ExpenseAdjustments
            .Where(a => a.ExpenseId == expenseId).ToListAsync(ct);
        db.ExpenseAdjustments.RemoveRange(adjustments);
        db.Expenses.Remove(expense); // interceptor converts both to soft deletes
        await db.SaveChangesAsync(ct);
    }

    public async Task<ExpenseExportResult> ExportAsync(ExpenseListRequest request,
        CancellationToken ct = default)
    {
        var rows = await Shape(Filter(request)
                .OrderByDescending(e => e.ExpenseDate).ThenByDescending(e => e.ExpenseId))
            .ToListAsync(ct);

        var csv = new StringBuilder();
        csv.AppendLine("Voucher,Date,Category,Description,Amount,Adjustments,Net Amount," +
                       "Payment Mode,Remarks,Location,Entered By,Supplier,PO Number,Reconciled");
        foreach (var row in rows)
        {
            csv.AppendLine(string.Join(',', new[]
            {
                Csv(row.ExpenseNumber),
                Csv(row.ExpenseDate.ToString("dd-MM-yyyy", CultureInfo.InvariantCulture)),
                Csv(row.Category),
                Csv(row.Description),
                row.Amount.ToString("0.00", CultureInfo.InvariantCulture),
                row.AdjustmentTotal.ToString("0.00", CultureInfo.InvariantCulture),
                row.NetAmount.ToString("0.00", CultureInfo.InvariantCulture),
                Csv(PaymentMethodName(row.PaymentMethod)),
                Csv(row.Remarks),
                Csv(row.LocationName),
                Csv(row.EnteredByName),
                Csv(row.SupplierName),
                Csv(row.PoNumber),
                row.IsReconciled ? "Yes" : "No"
            }));
        }

        var fileName = $"expenses-{IstClock.IstDateOf(clock.UtcNow):yyyy-MM-dd}.csv";
        return new ExpenseExportResult(Encoding.UTF8.GetBytes(csv.ToString()), "text/csv", fileName);
    }

    // ── adjustments ─────────────────────────────────────────────────────────

    public async Task<PagedResult<ExpenseAdjustmentDto>> GetAdjustmentsAsync(
        ExpenseAdjustmentListRequest request, CancellationToken ct = default)
    {
        // Joined to Expenses so the soft-delete filter hides adjustments of deleted vouchers.
        var query =
            from a in db.ExpenseAdjustments.AsNoTracking()
            join e in db.Expenses.AsNoTracking() on a.ExpenseId equals e.ExpenseId
            select new { a, e };

        if (request.ExpenseId is not null)
        {
            query = query.Where(x => x.a.ExpenseId == request.ExpenseId);
        }
        if (request.AdjustmentType is not null)
        {
            query = query.Where(x => (byte)x.a.AdjustmentType == request.AdjustmentType);
        }
        if (!string.IsNullOrWhiteSpace(request.Category))
        {
            query = query.Where(x => x.e.Category == request.Category);
        }
        if (request.From is not null)
        {
            query = query.Where(x => x.a.AdjustmentDate >= request.From);
        }
        if (request.To is not null)
        {
            query = query.Where(x => x.a.AdjustmentDate <= request.To);
        }
        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var term = request.Search.Trim();
            query = query.Where(x => x.e.ExpenseNumber.Contains(term)
                                     || x.a.Reason.Contains(term)
                                     || (x.a.Remarks != null && x.a.Remarks.Contains(term)));
        }

        var totalCount = await query.CountAsync(ct);
        var rows = await ShapeAdjustments(query
                .OrderByDescending(x => x.a.AdjustmentDate)
                .ThenByDescending(x => x.a.ExpenseAdjustmentId)
                .Skip((request.Page - 1) * request.PageSize)
                .Take(request.PageSize)
                .Select(x => x.a))
            .ToListAsync(ct);
        return PagedResult<ExpenseAdjustmentDto>.Create(rows, request.Page, request.PageSize, totalCount);
    }

    public async Task<ExpenseAdjustmentDto> GetAdjustmentAsync(long adjustmentId,
        CancellationToken ct = default) =>
        await ShapeAdjustments(db.ExpenseAdjustments.AsNoTracking()
                .Where(a => a.ExpenseAdjustmentId == adjustmentId
                            && db.Expenses.Any(e => e.ExpenseId == a.ExpenseId)))
            .FirstOrDefaultAsync(ct)
        ?? throw new NotFoundException("ExpenseAdjustment", adjustmentId);

    public async Task<ExpenseAdjustmentDto> CreateAdjustmentAsync(
        SaveExpenseAdjustmentRequest request, CancellationToken ct = default)
    {
        var adjustment = new ExpenseAdjustment { ExpenseId = request.ExpenseId };
        return await SaveAdjustmentAsync(adjustment, request, isNew: true, ct);
    }

    public async Task<ExpenseAdjustmentDto> UpdateAdjustmentAsync(long adjustmentId,
        SaveExpenseAdjustmentRequest request, CancellationToken ct = default)
    {
        var adjustment = await db.ExpenseAdjustments
                             .FirstOrDefaultAsync(a => a.ExpenseAdjustmentId == adjustmentId, ct)
                         ?? throw new NotFoundException("ExpenseAdjustment", adjustmentId);
        if (adjustment.ExpenseId != request.ExpenseId)
        {
            throw new BusinessRuleException(
                "An adjustment cannot be moved to another expense. Delete it and record a new one.");
        }
        return await SaveAdjustmentAsync(adjustment, request, isNew: false, ct);
    }

    public async Task DeleteAdjustmentAsync(long adjustmentId, CancellationToken ct = default)
    {
        var adjustment = await db.ExpenseAdjustments
                             .FirstOrDefaultAsync(a => a.ExpenseAdjustmentId == adjustmentId, ct)
                         ?? throw new NotFoundException("ExpenseAdjustment", adjustmentId);
        var expense = await db.Expenses.AsNoTracking()
                          .FirstOrDefaultAsync(e => e.ExpenseId == adjustment.ExpenseId, ct)
                      ?? throw new NotFoundException("Expense", adjustment.ExpenseId);

        // Removing an increase lowers the total, so it could take the expense below zero.
        var others = await OtherAdjustmentsTotalAsync(adjustment.ExpenseId, adjustmentId, ct);
        GuardNetNotNegative(expense.Amount + others, "deleted");

        var strategy = db.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            await using var dbTransaction = await db.Database.BeginTransactionAsync(ct);
            db.ExpenseAdjustments.Remove(adjustment); // interceptor converts to soft delete
            await db.SaveChangesAsync(ct);
            await RecomputeAdjustmentTotalAsync(adjustment.ExpenseId, ct);
            await dbTransaction.CommitAsync(ct);
        });
    }

    private async Task<ExpenseAdjustmentDto> SaveAdjustmentAsync(ExpenseAdjustment adjustment,
        SaveExpenseAdjustmentRequest request, bool isNew, CancellationToken ct)
    {
        var expense = await db.Expenses.AsNoTracking()
                          .FirstOrDefaultAsync(e => e.ExpenseId == request.ExpenseId, ct)
                      ?? throw new NotFoundException("Expense", request.ExpenseId);

        var type = (ExpenseAdjustmentType)request.AdjustmentType;
        var signed = type == ExpenseAdjustmentType.Increase ? request.Amount : -request.Amount;
        var others = await OtherAdjustmentsTotalAsync(expense.ExpenseId,
            isNew ? null : adjustment.ExpenseAdjustmentId, ct);
        GuardNetNotNegative(expense.Amount + others + signed, isNew ? "recorded" : "saved");

        adjustment.AdjustmentType = type;
        adjustment.Amount = request.Amount;
        adjustment.AdjustmentDate = request.AdjustmentDate ?? IstClock.IstDateOf(clock.UtcNow);
        adjustment.Reason = request.Reason.Trim();
        adjustment.Remarks = string.IsNullOrWhiteSpace(request.Remarks) ? null : request.Remarks.Trim();

        // The row and the expense's running total commit together.
        var strategy = db.Database.CreateExecutionStrategy();
        var id = await strategy.ExecuteAsync(async () =>
        {
            await using var dbTransaction = await db.Database.BeginTransactionAsync(ct);
            if (isNew)
            {
                db.ExpenseAdjustments.Add(adjustment);
            }
            await db.SaveChangesAsync(ct);
            await RecomputeAdjustmentTotalAsync(expense.ExpenseId, ct);
            await dbTransaction.CommitAsync(ct);
            return adjustment.ExpenseAdjustmentId;
        });
        return await GetAdjustmentAsync(id, ct);
    }

    private async Task<decimal> OtherAdjustmentsTotalAsync(long expenseId, long? excludeId,
        CancellationToken ct) =>
        await db.ExpenseAdjustments.AsNoTracking()
            .Where(a => a.ExpenseId == expenseId && a.ExpenseAdjustmentId != excludeId)
            .SumAsync(a => (decimal?)(a.AdjustmentType == ExpenseAdjustmentType.Increase
                ? a.Amount : -a.Amount), ct) ?? 0m;

    private static void GuardNetNotNegative(decimal net, string verb)
    {
        if (net < 0)
        {
            throw new BusinessRuleException(
                $"The adjustment cannot be {verb}: it would take the expense below zero " +
                $"(net {net:N2}). An expense can be reduced to nil at most.");
        }
    }

    /// <summary>One UPDATE that re-sums the live adjustments, so the stored total can
    /// never drift from its rows; CK_Expense_NetAmount backs up the guard above.</summary>
    private Task<int> RecomputeAdjustmentTotalAsync(long expenseId, CancellationToken ct) =>
        db.Expenses.Where(e => e.ExpenseId == expenseId)
            .ExecuteUpdateAsync(s => s.SetProperty(e => e.AdjustmentTotal,
                e => db.ExpenseAdjustments
                    .Where(a => a.ExpenseId == e.ExpenseId)
                    .Sum(a => (decimal?)(a.AdjustmentType == ExpenseAdjustmentType.Increase
                        ? a.Amount : -a.Amount)) ?? 0m), ct);

    private IQueryable<ExpenseAdjustmentDto> ShapeAdjustments(IQueryable<ExpenseAdjustment> query) =>
        from a in query
        join e in db.Expenses on a.ExpenseId equals e.ExpenseId
        select new ExpenseAdjustmentDto(
            a.ExpenseAdjustmentId, a.ExpenseId, e.ExpenseNumber, e.Category, e.ExpenseDate,
            e.Amount, e.Amount + e.AdjustmentTotal,
            (byte)a.AdjustmentType, a.Amount,
            a.AdjustmentType == ExpenseAdjustmentType.Increase ? a.Amount : -a.Amount,
            a.AdjustmentDate, a.Reason, a.Remarks,
            db.Users.Where(u => u.UserId == a.CreatedBy).Select(u => u.FullName).FirstOrDefault(),
            a.CreatedDate, a.ModifiedDate);

    // ── helpers ─────────────────────────────────────────────────────────────

    private IQueryable<Expense> Filter(ExpenseListRequest request)
    {
        var query = db.Expenses.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var term = request.Search.Trim();
            query = query.Where(e => e.ExpenseNumber.Contains(term)
                                     || e.Description.Contains(term)
                                     || e.Remarks.Contains(term));
        }
        if (!string.IsNullOrWhiteSpace(request.Category))
        {
            query = query.Where(e => e.Category == request.Category);
        }
        if (request.PaymentMethod is not null)
        {
            query = query.Where(e => (byte)e.PaymentMethod == request.PaymentMethod);
        }
        if (request.LocationId is not null)
        {
            query = query.Where(e => e.LocationId == request.LocationId);
        }
        if (request.SupplierId is not null)
        {
            query = query.Where(e => e.SupplierId == request.SupplierId);
        }
        if (request.From is not null)
        {
            query = query.Where(e => e.ExpenseDate >= request.From);
        }
        if (request.To is not null)
        {
            query = query.Where(e => e.ExpenseDate <= request.To);
        }
        return query;
    }

    private IQueryable<ExpenseDto> Shape(IQueryable<Expense> query) =>
        query.Select(e => new ExpenseDto(
            e.ExpenseId, e.ExpenseNumber, e.ExpenseDate, e.Category, e.Description, e.Amount,
            (byte)e.PaymentMethod, e.Remarks, e.AttachmentPath, e.LocationId,
            db.Locations.Where(l => l.LocationId == e.LocationId)
                .Select(l => l.LocationName).FirstOrDefault(),
            e.EnteredByUserId,
            db.Users.Where(u => u.UserId == e.EnteredByUserId)
                .Select(u => u.FullName).FirstOrDefault() ?? string.Empty,
            e.SupplierId,
            db.Suppliers.Where(s => s.SupplierId == e.SupplierId)
                .Select(s => s.SupplierName).FirstOrDefault(),
            e.PurchaseOrderId,
            db.PurchaseOrders.Where(p => p.PurchaseOrderId == e.PurchaseOrderId)
                .Select(p => p.PoNumber).FirstOrDefault(),
            db.BankTransactions.Where(b => b.MatchedExpenseId == e.ExpenseId)
                .Select(b => (long?)b.BankTransactionId).FirstOrDefault(),
            db.BankTransactions.Any(b => b.MatchedExpenseId == e.ExpenseId),
            e.CreatedDate,
            e.AdjustmentTotal,
            e.Amount + e.AdjustmentTotal,
            db.ExpenseAdjustments.Count(a => a.ExpenseId == e.ExpenseId)));

    private async Task GuardReferencesAsync(SaveExpenseRequest request, CancellationToken ct)
    {
        if (!await db.Locations.AnyAsync(l => l.LocationId == request.LocationId, ct))
        {
            throw new NotFoundException("Location", request.LocationId);
        }
        if (request.SupplierId is not null
            && !await db.Suppliers.AnyAsync(s => s.SupplierId == request.SupplierId, ct))
        {
            throw new NotFoundException("Supplier", request.SupplierId);
        }
        if (request.PurchaseOrderId is not null
            && !await db.PurchaseOrders.AnyAsync(p => p.PurchaseOrderId == request.PurchaseOrderId, ct))
        {
            throw new NotFoundException("PurchaseOrder", request.PurchaseOrderId);
        }
    }

    private async Task GuardNotReconciledAsync(long expenseId, string verb, CancellationToken ct)
    {
        if (await db.BankTransactions.AnyAsync(b => b.MatchedExpenseId == expenseId, ct))
        {
            throw new BusinessRuleException(
                $"A bank transaction is matched to this expense, so it cannot be {verb}. " +
                "Unmatch the bank line first.");
        }
    }

    private static void Apply(Expense expense, SaveExpenseRequest request)
    {
        expense.Category = request.Category.Trim();
        expense.Description = request.Description.Trim();
        expense.Amount = request.Amount;
        expense.PaymentMethod = (PaymentMethod)request.PaymentMethod;
        expense.Remarks = request.Remarks.Trim();
        expense.AttachmentPath = request.AttachmentPath?.Trim();
        expense.LocationId = request.LocationId;
        expense.SupplierId = request.SupplierId;
        expense.PurchaseOrderId = request.PurchaseOrderId;
    }

    private static string PaymentMethodName(byte method) => method switch
    {
        1 => "Wallet",
        2 => "UPI",
        3 => "Credit card",
        4 => "Debit card",
        5 => "Net banking",
        6 => "Cash",
        _ => "Other"
    };

    /// <summary>Quote a CSV field so a comma or quote in free text cannot shift columns.</summary>
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
