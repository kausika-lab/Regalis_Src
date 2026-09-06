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
            .Select(g => new { Category = g.Key, Amount = g.Sum(e => e.Amount), Count = g.Count() })
            .OrderByDescending(row => row.Amount)
            .ToListAsync(ct);

        var byCategory = totals
            .Select(row => new ExpenseCategoryTotalDto(row.Category, row.Amount, row.Count))
            .ToList();

        return new ExpenseSummaryDto(
            byCategory.Sum(c => c.Amount),
            byCategory.Sum(c => c.Count),
            byCategory);
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
        db.Expenses.Remove(expense); // interceptor converts to soft delete
        await db.SaveChangesAsync(ct);
    }

    public async Task<ExpenseExportResult> ExportAsync(ExpenseListRequest request,
        CancellationToken ct = default)
    {
        var rows = await Shape(Filter(request)
                .OrderByDescending(e => e.ExpenseDate).ThenByDescending(e => e.ExpenseId))
            .ToListAsync(ct);

        var csv = new StringBuilder();
        csv.AppendLine("Voucher,Date,Category,Description,Amount,Payment Mode,Remarks," +
                       "Location,Entered By,Supplier,PO Number,Reconciled");
        foreach (var row in rows)
        {
            csv.AppendLine(string.Join(',', new[]
            {
                Csv(row.ExpenseNumber),
                Csv(row.ExpenseDate.ToString("dd-MM-yyyy", CultureInfo.InvariantCulture)),
                Csv(row.Category),
                Csv(row.Description),
                row.Amount.ToString("0.00", CultureInfo.InvariantCulture),
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
            e.CreatedDate));

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
