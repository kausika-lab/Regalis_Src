using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Regalis.Application.Abstractions;
using Regalis.Application.Common.Exceptions;
using Regalis.Application.Finance;
using Regalis.Application.Finance.Billing;
using Regalis.Domain.Entities.Finance;
using Regalis.Domain.Entities.Finance.Billing;
using Regalis.Domain.Enums;
using Regalis.Infrastructure.Persistence;
using Regalis.Shared.Api;
using Regalis.Shared.Constants;

namespace Regalis.Infrastructure.Services.Billing;

/// <summary>
/// The four additions to Expenses (build doc §12.2): approval by limit (settings, not code;
/// the approver is never the person who raised it), receipts (up to five, required above a
/// set amount), department, recurring drafts that a person confirms, and reimbursement of
/// staff who paid from their own pocket. TRN_Expense keeps the voucher as before; this
/// side lives in TRN_ExpenseControl, which a trigger creates with every new expense.
/// </summary>
public class ExpenseControlService(
    RegalisDbContext db,
    INumberSequenceService sequences,
    ISettingService settings,
    ICurrentUser currentUser,
    IDateTimeProvider clock,
    IOptions<StorageOptions> storage) : IExpenseControlService
{
    private const int MaxReceipts = 5;

    public async Task<ExpenseRulesDto> GetRulesAsync(CancellationToken ct = default)
    {
        return new ExpenseRulesDto(await settings.GetDecimalAsync("Expense.ApprovalLimitNone", 2000m, ct),
            await settings.GetDecimalAsync("Expense.ApprovalLimitManager", 25000m, ct),
            await settings.GetDecimalAsync("Expense.ReceiptRequiredAbove", 2000m, ct), MaxReceipts);
    }

    private static byte LevelFor(decimal amount, ExpenseRulesDto rules) =>
        amount <= rules.NoApprovalUpTo ? (byte)0 : amount <= rules.ManagerUpTo ? (byte)1 : (byte)2;

    private bool CanDecide(byte level) =>
        currentUser.HasPermission("EXPENSE.APPROVE") && (level < 2 || currentUser.Roles.Contains(RoleCodes.Admin));

    private static string Label(ExpenseControl c) =>
        c.IsRecurringDraft ? "Recurring draft - confirm"
        : c.ApprovalStatus == ExpenseApprovalStatus.AwaitingApproval ? "Awaiting approval"
        : c.ApprovalStatus == ExpenseApprovalStatus.Rejected ? "Rejected"
        : c.IsReimbursement && c.ReimbursedUtc == null ? "Reimbursement due"
        : c.IsReimbursement ? "Approved · reimbursed"
        : c.PaidFrom == ExpensePaidFrom.PettyCash ? "Approved · petty cash"
        : "Approved · paid";

    public async Task<IReadOnlyList<ExpenseControlDto>> GetControlsAsync(IReadOnlyList<long> expenseIds, CancellationToken ct = default)
    {
        var ids = expenseIds.Distinct().Take(200).ToList();
        if (ids.Count == 0) return Array.Empty<ExpenseControlDto>();
        ExpenseRulesDto rules = await GetRulesAsync(ct);
        var expenses = await db.Expenses.IgnoreQueryFilters().AsNoTracking().Where(e => ids.Contains(e.ExpenseId))
            .Select(e => new { e.ExpenseId, Net = e.Amount + e.AdjustmentTotal, e.EnteredByUserId }).ToListAsync(ct);
        var controls = await db.Set<ExpenseControl>().AsNoTracking().Where(c => ids.Contains(c.ExpenseId)).ToDictionaryAsync(c => c.ExpenseId, ct);
        var receipts = await db.Set<ExpenseReceipt>().AsNoTracking().Where(r => ids.Contains(r.ExpenseId) && !r.IsDeleted)
            .OrderBy(r => r.UploadedUtc).ToListAsync(ct);
        var departments = await db.Set<Department>().IgnoreQueryFilters().AsNoTracking().ToDictionaryAsync(d => d.DepartmentId, d => d.Name, ct);
        var userIds = controls.Values.SelectMany(c => new[] { c.DecidedByUserId, c.ReimburseToUserId }).Where(x => x != null).Select(x => x!.Value).Distinct().ToList();
        var names = await db.Users.IgnoreQueryFilters().Where(u => userIds.Contains(u.UserId)).ToDictionaryAsync(u => u.UserId, u => u.FullName, ct);
        var matched = await db.BankTransactions.Where(t => t.MatchedExpenseId != null && ids.Contains(t.MatchedExpenseId.Value)).Select(t => t.MatchedExpenseId!.Value)
            .Union(db.Set<BankMatchAllocation>().Where(m => m.DocType == BankMatchDocType.Expense && ids.Contains(m.DocId)).Select(m => m.DocId))
            .ToListAsync(ct);

        return expenses.Select(e =>
        {
            ExpenseControl c = controls.GetValueOrDefault(e.ExpenseId) ?? new ExpenseControl { ExpenseId = e.ExpenseId, ApprovalStatus = ExpenseApprovalStatus.Approved };
            var files = receipts.Where(r => r.ExpenseId == e.ExpenseId)
                .Select(r => new ExpenseReceiptDto(r.ExpenseReceiptId, r.FileName, r.ContentType, r.SizeBytes, r.UploadedUtc)).ToList();
            bool required = e.Net > rules.ReceiptRequiredAbove;
            bool mine = e.EnteredByUserId == currentUser.UserId;
            return new ExpenseControlDto(e.ExpenseId, c.DepartmentId, c.DepartmentId != null ? departments.GetValueOrDefault(c.DepartmentId.Value) : null,
                (byte)c.ApprovalStatus, c.ApprovalLevel, Label(c), c.DecidedByUserId != null ? names.GetValueOrDefault(c.DecidedByUserId.Value) : null,
                c.DecidedUtc, c.DecisionComment, (byte)c.PaidFrom, c.IsReimbursement, c.ReimburseToUserId,
                c.ReimburseToUserId != null ? names.GetValueOrDefault(c.ReimburseToUserId.Value) : null, c.ReimbursedUtc, c.ReimbursedVia,
                c.IsRecurringDraft, c.RecurringExpenseId, files, required, required && files.Count == 0,
                c.ApprovalStatus == ExpenseApprovalStatus.AwaitingApproval && !c.IsRecurringDraft && !mine && CanDecide(c.ApprovalLevel),
                mine, matched.Contains(e.ExpenseId));
        }).ToList();
    }

    private async Task<ExpenseControlDto> OneAsync(long expenseId, CancellationToken ct) =>
        (await GetControlsAsync(new[] { expenseId }, ct)).FirstOrDefault() ?? throw new NotFoundException("Expense", expenseId);

    /// <summary>Loads the expense and its control row (creating one for an expense that predates the trigger).</summary>
    private async Task<(Expense Expense, ExpenseControl Control)> LoadAsync(long expenseId, CancellationToken ct)
    {
        Expense expense = await db.Expenses.FirstOrDefaultAsync(e => e.ExpenseId == expenseId, ct) ?? throw new NotFoundException("Expense", expenseId);
        ExpenseControl? control = await db.Set<ExpenseControl>().FirstOrDefaultAsync(c => c.ExpenseId == expenseId, ct);
        if (control == null)
        {
            control = new ExpenseControl { ExpenseId = expenseId, ApprovalStatus = ExpenseApprovalStatus.Approved };
            db.Set<ExpenseControl>().Add(control);
        }
        return (expense, control);
    }

    private async Task<bool> IsMatchedAsync(long expenseId, CancellationToken ct) =>
        await db.BankTransactions.AnyAsync(t => t.MatchedExpenseId == expenseId, ct)
        || await db.Set<BankMatchAllocation>().AnyAsync(m => m.DocType == BankMatchDocType.Expense && m.DocId == expenseId, ct);

    private async Task TakeFromPettyCashAsync(Expense expense, ExpenseControl control, CancellationToken ct)
    {
        if (control.PettyCashEntryId != null) return;
        PettyCashEntry entry = await PettyCashService.PostAsync(db, PettyCashEntryType.Expense, -(expense.Amount + expense.AdjustmentTotal),
            expense.ExpenseDate, $"{expense.ExpenseNumber} · {expense.Category} · {expense.Description}", currentUser.UserId, null, ct);
        entry.ExpenseId = expense.ExpenseId;
        control.PettyCashEntryId = entry.PettyCashEntryId;
    }

    public async Task<ExpenseControlDto> SaveControlAsync(long expenseId, SaveExpenseControlRequest request, CancellationToken ct = default)
    {
        if (request.PaidFrom < 1 || request.PaidFrom > 3) throw new BusinessRuleException("Say where the money came from.");
        await FinanceRules.InTransactionAsync(db, async () =>
        {
            var (expense, control) = await LoadAsync(expenseId, ct);
            if (await IsMatchedAsync(expenseId, ct)) throw new BusinessRuleException("This expense is matched to a bank line and is locked. Unmatch it first.");
            if (request.DepartmentId != null && !await db.Set<Department>().AnyAsync(d => d.DepartmentId == request.DepartmentId && d.IsActive, ct))
                throw new BusinessRuleException("Choose a department from the list.");
            var paidFrom = (ExpensePaidFrom)request.PaidFrom;
            bool reimburse = request.IsReimbursement || paidFrom == ExpensePaidFrom.Staff;
            if (reimburse && request.ReimburseToUserId == null) throw new BusinessRuleException("Choose who paid, so they can be reimbursed.");
            if (control.PettyCashEntryId != null && paidFrom != ExpensePaidFrom.PettyCash)
                throw new BusinessRuleException("This expense has already been taken from petty cash.");
            if (control.ReimbursedUtc != null && !reimburse)
                throw new BusinessRuleException("This expense has already been reimbursed.");
            control.DepartmentId = request.DepartmentId;
            control.PaidFrom = reimburse ? ExpensePaidFrom.Staff : paidFrom;
            control.IsReimbursement = reimburse;
            control.ReimburseToUserId = reimburse ? request.ReimburseToUserId : null;
            if (control.ApprovalStatus == ExpenseApprovalStatus.Approved && control.PaidFrom == ExpensePaidFrom.PettyCash && !control.IsRecurringDraft)
            {
                await TakeFromPettyCashAsync(expense, control, ct);
            }
            await db.SaveChangesAsync(ct);
            return true;
        }, ct);
        return await OneAsync(expenseId, ct);
    }

    public async Task<ExpenseControlDto> ApproveAsync(long expenseId, ExpenseDecisionRequest request, CancellationToken ct = default)
    {
        ExpenseRulesDto rules = await GetRulesAsync(ct);
        await FinanceRules.InTransactionAsync(db, async () =>
        {
            var (expense, control) = await LoadAsync(expenseId, ct);
            if (control.ApprovalStatus != ExpenseApprovalStatus.AwaitingApproval || control.IsRecurringDraft)
                throw new BusinessRuleException("This expense is not waiting for approval.");
            if (expense.EnteredByUserId == currentUser.UserId)
                throw new BusinessRuleException("You raised this expense, so you cannot approve it.", "SAME_PERSON");
            decimal net = expense.Amount + expense.AdjustmentTotal;
            byte level = LevelFor(net, rules);
            if (!CanDecide(level))
                throw new ForbiddenException($"An expense above {FinanceRules.Money(rules.ManagerUpTo)} needs the owner's approval.");
            if (net > rules.ReceiptRequiredAbove && !await db.Set<ExpenseReceipt>().AnyAsync(r => r.ExpenseId == expenseId && !r.IsDeleted, ct))
                throw new BusinessRuleException($"A receipt is required for anything above {FinanceRules.Money(rules.ReceiptRequiredAbove)}. Attach the bill.", "RECEIPT_REQUIRED");
            control.ApprovalStatus = ExpenseApprovalStatus.Approved;
            control.ApprovalLevel = level;
            control.DecidedByUserId = currentUser.UserId;
            control.DecidedUtc = clock.UtcNow;
            control.DecisionComment = string.IsNullOrWhiteSpace(request.Comment) ? null : request.Comment.Trim();
            if (control.PaidFrom == ExpensePaidFrom.PettyCash) await TakeFromPettyCashAsync(expense, control, ct);
            await db.SaveChangesAsync(ct);
            return true;
        }, ct);
        return await OneAsync(expenseId, ct);
    }

    public async Task<ExpenseControlDto> RejectAsync(long expenseId, ExpenseDecisionRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.Comment)) throw new BusinessRuleException("Say why this expense is being rejected.");
        ExpenseRulesDto rules = await GetRulesAsync(ct);
        await FinanceRules.InTransactionAsync(db, async () =>
        {
            var (expense, control) = await LoadAsync(expenseId, ct);
            if (control.ApprovalStatus != ExpenseApprovalStatus.AwaitingApproval || control.IsRecurringDraft)
                throw new BusinessRuleException("This expense is not waiting for approval.");
            if (expense.EnteredByUserId == currentUser.UserId)
                throw new BusinessRuleException("You raised this expense, so you cannot decide on it.", "SAME_PERSON");
            if (!CanDecide(LevelFor(expense.Amount + expense.AdjustmentTotal, rules)))
                throw new ForbiddenException($"An expense above {FinanceRules.Money(rules.ManagerUpTo)} needs the owner's decision.");
            control.ApprovalStatus = ExpenseApprovalStatus.Rejected;
            control.DecidedByUserId = currentUser.UserId;
            control.DecidedUtc = clock.UtcNow;
            control.DecisionComment = request.Comment.Trim();
            await db.SaveChangesAsync(ct);
            return true;
        }, ct);
        return await OneAsync(expenseId, ct);
    }

    /// <summary>Re-applies the limits after a correction: below the first limit it is approved by itself.</summary>
    private async Task SubmitAsync(Expense expense, ExpenseControl control, ExpenseRulesDto rules, CancellationToken ct)
    {
        decimal net = expense.Amount + expense.AdjustmentTotal;
        control.ApprovalLevel = LevelFor(net, rules);
        control.DecidedByUserId = null;
        control.DecidedUtc = null;
        control.DecisionComment = null;
        if (control.ApprovalLevel == 0)
        {
            control.ApprovalStatus = ExpenseApprovalStatus.Approved;
            if (control.PaidFrom == ExpensePaidFrom.PettyCash) await TakeFromPettyCashAsync(expense, control, ct);
        }
        else
        {
            control.ApprovalStatus = ExpenseApprovalStatus.AwaitingApproval;
        }
    }

    public async Task<ExpenseControlDto> ResubmitAsync(long expenseId, CancellationToken ct = default)
    {
        ExpenseRulesDto rules = await GetRulesAsync(ct);
        await FinanceRules.InTransactionAsync(db, async () =>
        {
            var (expense, control) = await LoadAsync(expenseId, ct);
            if (control.ApprovalStatus != ExpenseApprovalStatus.Rejected) throw new BusinessRuleException("Only a rejected expense is resubmitted.");
            await SubmitAsync(expense, control, rules, ct);
            await db.SaveChangesAsync(ct);
            return true;
        }, ct);
        return await OneAsync(expenseId, ct);
    }

    public async Task<ExpenseControlDto> ConfirmRecurringAsync(long expenseId, ConfirmRecurringRequest request, CancellationToken ct = default)
    {
        ExpenseRulesDto rules = await GetRulesAsync(ct);
        await FinanceRules.InTransactionAsync(db, async () =>
        {
            var (expense, control) = await LoadAsync(expenseId, ct);
            if (!control.IsRecurringDraft) throw new BusinessRuleException("This expense is not a recurring draft.");
            await FinanceRules.EnsurePeriodOpenAsync(db, expense.ExpenseDate, ct, "this expense");
            if (request.Amount != null)
            {
                decimal amount = FinanceRules.Round(request.Amount.Value);
                if (amount <= 0m) throw new BusinessRuleException("Enter the amount on this month's bill.");
                expense.Amount = amount;
            }
            control.IsRecurringDraft = false;
            await SubmitAsync(expense, control, rules, ct);
            await db.SaveChangesAsync(ct);
            return true;
        }, ct);
        return await OneAsync(expenseId, ct);
    }

    public async Task<ExpenseControlDto> ReimburseAsync(long expenseId, ReimburseRequest request, CancellationToken ct = default)
    {
        string via = (request.Via ?? "").Trim().ToLowerInvariant();
        if (via is not ("bank" or "petty")) throw new BusinessRuleException("Choose bank transfer or petty cash.");
        if (via == "bank" && (request.BankAccountId == null || string.IsNullOrWhiteSpace(request.Reference)))
            throw new BusinessRuleException("Choose the bank account and enter the UTR of the transfer.");
        DateOnly on = request.PaidOn ?? FinanceRules.Today(clock);
        FinanceRules.EnsureNotFuture(on, clock, "The reimbursement date cannot be in the future.");
        await FinanceRules.InTransactionAsync(db, async () =>
        {
            var (expense, control) = await LoadAsync(expenseId, ct);
            if (!control.IsReimbursement) throw new BusinessRuleException("This expense is not owed to a member of staff.");
            if (control.ApprovalStatus != ExpenseApprovalStatus.Approved) throw new BusinessRuleException("Approve the expense before reimbursing it.");
            if (control.ReimbursedUtc != null) throw new BusinessRuleException("This expense has already been reimbursed.");
            await FinanceRules.EnsurePeriodOpenAsync(db, on, ct, "this reimbursement");
            if (via == "petty")
            {
                string name = await db.Users.Where(u => u.UserId == control.ReimburseToUserId).Select(u => u.FullName).FirstOrDefaultAsync(ct) ?? "staff";
                PettyCashEntry entry = await PettyCashService.PostAsync(db, PettyCashEntryType.Expense, -(expense.Amount + expense.AdjustmentTotal), on,
                    $"Reimbursed {name} for {expense.ExpenseNumber}", currentUser.UserId, null, ct);
                entry.ExpenseId = expense.ExpenseId;
                control.PettyCashEntryId = entry.PettyCashEntryId;
            }
            control.ReimbursedUtc = clock.UtcNow;
            control.ReimbursedByUserId = currentUser.UserId;
            control.ReimbursedVia = via == "bank" ? "Bank transfer" : "Petty cash";
            control.ReimbursementReference = via == "bank" ? request.Reference!.Trim() : null;
            await db.SaveChangesAsync(ct);
            return true;
        }, ct);
        return await OneAsync(expenseId, ct);
    }

    public async Task<PagedResult<ExpenseQueueItemDto>> GetQueueAsync(ExpenseQueueRequest request, CancellationToken ct = default)
    {
        var q = from c in db.Set<ExpenseControl>().AsNoTracking()
                join e in db.Expenses on c.ExpenseId equals e.ExpenseId
                select new { c, e };
        q = request.Queue switch
        {
            "reimbursement" => q.Where(x => x.c.IsReimbursement && x.c.ApprovalStatus == ExpenseApprovalStatus.Approved && x.c.ReimbursedUtc == null),
            "drafts" => q.Where(x => x.c.IsRecurringDraft),
            _ => q.Where(x => x.c.ApprovalStatus == ExpenseApprovalStatus.AwaitingApproval && !x.c.IsRecurringDraft)
        };
        int total = await q.CountAsync(ct);
        var rows = await (from x in q.OrderBy(x => x.e.ExpenseDate).ThenBy(x => x.e.ExpenseId).Skip((request.Page - 1) * request.PageSize).Take(request.PageSize)
                          join u in db.Users on x.e.EnteredByUserId equals u.UserId into uj
                          from u in uj.DefaultIfEmpty()
                          join d in db.Set<Department>() on x.c.DepartmentId equals (int?)d.DepartmentId into dj
                          from d in dj.DefaultIfEmpty()
                          join r in db.Users on x.c.ReimburseToUserId equals (long?)r.UserId into rj
                          from r in rj.DefaultIfEmpty()
                          select new
                          {
                              x.c, x.e, Entered = u != null ? u.FullName : null, Dept = d != null ? d.Name : null, To = r != null ? r.FullName : null,
                              Receipts = db.Set<ExpenseReceipt>().Count(f => f.ExpenseId == x.e.ExpenseId && !f.IsDeleted)
                          }).ToListAsync(ct);
        var items = rows.Select(x => new ExpenseQueueItemDto(x.e.ExpenseId, x.e.ExpenseNumber, x.e.ExpenseDate, x.e.Category, x.e.Description,
            x.e.Amount + x.e.AdjustmentTotal, x.Entered, x.e.EnteredByUserId, x.Dept, x.c.ApprovalLevel, x.Receipts, Label(x.c), x.To,
            x.c.ApprovalStatus == ExpenseApprovalStatus.AwaitingApproval && !x.c.IsRecurringDraft && x.e.EnteredByUserId != currentUser.UserId && CanDecide(x.c.ApprovalLevel)))
            .ToList();
        return PagedResult<ExpenseQueueItemDto>.Create(items, request.Page, request.PageSize, total);
    }

    public async Task<PagedResult<ExpenseDto>> GetRegisterAsync(ExpenseRegisterRequest request, CancellationToken ct = default)
    {
        var q = from x in db.Expenses.AsNoTracking()
                join c in db.Set<ExpenseControl>() on x.ExpenseId equals c.ExpenseId into cj
                from c in cj.DefaultIfEmpty()
                select new { x, c };
        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            string s = request.Search.Trim();
            q = q.Where(r => r.x.ExpenseNumber.Contains(s) || r.x.Description.Contains(s) || r.x.Remarks.Contains(s));
        }
        if (!string.IsNullOrWhiteSpace(request.Category)) q = q.Where(r => r.x.Category == request.Category);
        if (request.From != null) q = q.Where(r => r.x.ExpenseDate >= request.From);
        if (request.To != null) q = q.Where(r => r.x.ExpenseDate <= request.To);
        if (request.DepartmentId != null) q = q.Where(r => r.c != null && r.c.DepartmentId == request.DepartmentId);
        q = request.Status switch
        {
            "awaiting" => q.Where(r => r.c != null && r.c.ApprovalStatus == ExpenseApprovalStatus.AwaitingApproval && !r.c.IsRecurringDraft),
            "approved" => q.Where(r => r.c == null || r.c.ApprovalStatus == ExpenseApprovalStatus.Approved),
            "rejected" => q.Where(r => r.c != null && r.c.ApprovalStatus == ExpenseApprovalStatus.Rejected),
            "reimbursement-due" => q.Where(r => r.c != null && r.c.IsReimbursement && r.c.ApprovalStatus == ExpenseApprovalStatus.Approved && r.c.ReimbursedUtc == null),
            "petty-cash" => q.Where(r => r.c != null && r.c.PaidFrom == ExpensePaidFrom.PettyCash),
            "recurring-draft" => q.Where(r => r.c != null && r.c.IsRecurringDraft),
            _ => q
        };
        int total = await q.CountAsync(ct);
        var items = await (from r in q.OrderByDescending(r => r.x.ExpenseDate).ThenByDescending(r => r.x.ExpenseId).Skip((request.Page - 1) * request.PageSize).Take(request.PageSize)
                           join l in db.Locations on r.x.LocationId equals l.LocationId into lj
                           from l in lj.DefaultIfEmpty()
                           join u in db.Users on r.x.EnteredByUserId equals u.UserId into uj
                           from u in uj.DefaultIfEmpty()
                           join sp in db.Suppliers on r.x.SupplierId equals (int?)sp.SupplierId into sj
                           from sp in sj.DefaultIfEmpty()
                           join po in db.PurchaseOrders on r.x.PurchaseOrderId equals (long?)po.PurchaseOrderId into pj
                           from po in pj.DefaultIfEmpty()
                           select new ExpenseDto(r.x.ExpenseId, r.x.ExpenseNumber, r.x.ExpenseDate, r.x.Category, r.x.Description, r.x.Amount, (byte)r.x.PaymentMethod,
                               r.x.Remarks, r.x.AttachmentPath, r.x.LocationId, l != null ? l.LocationName : null, r.x.EnteredByUserId, u != null ? u.FullName : "",
                               r.x.SupplierId, sp != null ? sp.SupplierName : null, r.x.PurchaseOrderId, po != null ? po.PoNumber : null,
                               db.BankTransactions.Where(t => t.MatchedExpenseId == r.x.ExpenseId).Select(t => (long?)t.BankTransactionId).FirstOrDefault()
                                   ?? db.Set<BankMatchAllocation>().Where(m => m.DocType == BankMatchDocType.Expense && m.DocId == r.x.ExpenseId).Select(m => (long?)m.BankTransactionId).FirstOrDefault(),
                               db.BankTransactions.Any(t => t.MatchedExpenseId == r.x.ExpenseId) || db.Set<BankMatchAllocation>().Any(m => m.DocType == BankMatchDocType.Expense && m.DocId == r.x.ExpenseId),
                               r.x.CreatedDate, r.x.AdjustmentTotal, r.x.Amount + r.x.AdjustmentTotal, db.ExpenseAdjustments.Count(a => a.ExpenseId == r.x.ExpenseId)))
            .ToListAsync(ct);
        return PagedResult<ExpenseDto>.Create(items, request.Page, request.PageSize, total);
    }

    public async Task<ExpenseControlDto> AddReceiptAsync(long expenseId, AttachmentUploadInput file, CancellationToken ct = default)
    {
        if (!await db.Expenses.AnyAsync(e => e.ExpenseId == expenseId, ct)) throw new NotFoundException("Expense", expenseId);
        if (await db.Set<ExpenseReceipt>().CountAsync(r => r.ExpenseId == expenseId && !r.IsDeleted, ct) >= MaxReceipts)
            throw new BusinessRuleException($"An expense can have up to {MaxReceipts} receipts.");
        var (path, contentType) = await FinanceRules.SaveUploadAsync(storage.Value.UploadRoot, "expense-receipts", expenseId, file.FileName, file.Content, file.Length, ct);
        db.Set<ExpenseReceipt>().Add(new ExpenseReceipt
        {
            ExpenseId = expenseId,
            FileName = Path.GetFileName(file.FileName),
            StoredPath = path,
            ContentType = contentType,
            SizeBytes = file.Length,
            UploadedByUserId = currentUser.UserId,
            UploadedUtc = clock.UtcNow
        });
        Expense expense = await db.Expenses.FirstAsync(e => e.ExpenseId == expenseId, ct);
        expense.AttachmentPath ??= path;
        await db.SaveChangesAsync(ct);
        return await OneAsync(expenseId, ct);
    }

    public async Task<ExpenseControlDto> RemoveReceiptAsync(long expenseId, long receiptId, CancellationToken ct = default)
    {
        ExpenseReceipt r = await db.Set<ExpenseReceipt>().FirstOrDefaultAsync(x => x.ExpenseId == expenseId && x.ExpenseReceiptId == receiptId && !x.IsDeleted, ct)
            ?? throw new NotFoundException("Receipt", receiptId);
        ExpenseControl? c = await db.Set<ExpenseControl>().AsNoTracking().FirstOrDefaultAsync(x => x.ExpenseId == expenseId, ct);
        if (c != null && c.ApprovalStatus == ExpenseApprovalStatus.Approved && c.ApprovalLevel > 0)
            throw new BusinessRuleException("This expense was approved with this receipt; it cannot be removed.");
        r.IsDeleted = true;
        await db.SaveChangesAsync(ct);
        return await OneAsync(expenseId, ct);
    }

    public async Task<AttachmentFileDto> GetReceiptAsync(long expenseId, long receiptId, CancellationToken ct = default)
    {
        ExpenseReceipt r = await db.Set<ExpenseReceipt>().AsNoTracking().FirstOrDefaultAsync(x => x.ExpenseId == expenseId && x.ExpenseReceiptId == receiptId, ct)
            ?? throw new NotFoundException("Receipt", receiptId);
        if (!File.Exists(r.StoredPath)) throw new NotFoundException("Receipt file", receiptId);
        return new AttachmentFileDto(await File.ReadAllBytesAsync(r.StoredPath, ct), r.ContentType, r.FileName);
    }

    public async Task<IReadOnlyList<DepartmentDto>> GetDepartmentsAsync(bool includeInactive, CancellationToken ct = default)
    {
        return await db.Set<Department>().AsNoTracking().Where(d => includeInactive || d.IsActive)
            .OrderBy(d => d.DisplayOrder).ThenBy(d => d.Name)
            .Select(d => new DepartmentDto(d.DepartmentId, d.Name, d.DisplayOrder, d.IsActive)).ToListAsync(ct);
    }

    public async Task<DepartmentDto> SaveDepartmentAsync(int? id, SaveDepartmentRequest request, CancellationToken ct = default)
    {
        string name = request.Name?.Trim() ?? "";
        if (name.Length == 0 || name.Length > 60) throw new BusinessRuleException("Give the department a name of up to 60 characters.");
        if (await db.Set<Department>().AnyAsync(d => d.Name == name && d.DepartmentId != (id ?? 0), ct))
            throw new ConflictException($"There is already a department called {name}.", "DUPLICATE_DEPARTMENT");
        Department d;
        if (id == null)
        {
            d = new Department();
            db.Set<Department>().Add(d);
        }
        else
        {
            d = await db.Set<Department>().FirstOrDefaultAsync(x => x.DepartmentId == id, ct) ?? throw new NotFoundException("Department", id.Value);
        }
        d.Name = name;
        d.DisplayOrder = request.DisplayOrder;
        d.IsActive = request.IsActive;
        await db.SaveChangesAsync(ct);
        return new DepartmentDto(d.DepartmentId, d.Name, d.DisplayOrder, d.IsActive);
    }

    private static DateOnly? NextDue(RecurringExpense r, DateOnly today)
    {
        if (!r.IsActive) return null;
        for (int i = 0; i < 24; i++)
        {
            DateOnly month = new DateOnly(today.Year, today.Month, 1).AddMonths(i - 1);
            DateOnly due = new DateOnly(month.Year, month.Month, r.DayOfMonth);
            if (due < r.StartOn) continue;
            if (r.EndOn != null && due > r.EndOn) return null;
            if (r.LastRaisedFor != null && month <= r.LastRaisedFor) continue;
            return due;
        }
        return null;
    }

    public async Task<IReadOnlyList<RecurringExpenseDto>> GetRecurringAsync(CancellationToken ct = default)
    {
        DateOnly today = FinanceRules.Today(clock);
        var rows = await (from r in db.Set<RecurringExpense>().AsNoTracking()
                          join d in db.Set<Department>() on r.DepartmentId equals (int?)d.DepartmentId into dj
                          from d in dj.DefaultIfEmpty()
                          join s in db.Suppliers on r.SupplierId equals (int?)s.SupplierId into sj
                          from s in sj.DefaultIfEmpty()
                          orderby r.DayOfMonth, r.Category
                          select new { r, Dept = d != null ? d.Name : null, Supplier = s != null ? s.SupplierName : null }).ToListAsync(ct);
        return rows.Select(x => new RecurringExpenseDto(x.r.RecurringExpenseId, x.r.Category, x.r.Description, x.r.DepartmentId, x.Dept,
            x.r.SupplierId, x.Supplier, x.r.LocationId, x.r.Amount, x.r.PaymentMethod, x.r.DayOfMonth, x.r.StartOn, x.r.EndOn,
            x.r.LastRaisedFor, NextDue(x.r, today), x.r.IsActive)).ToList();
    }

    public async Task<RecurringExpenseDto> SaveRecurringAsync(long? id, SaveRecurringExpenseRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.Category) || string.IsNullOrWhiteSpace(request.Description))
            throw new BusinessRuleException("Enter the category and a description.");
        if (request.Amount <= 0m) throw new BusinessRuleException("Enter the monthly amount.");
        if (request.DayOfMonth < 1 || request.DayOfMonth > 28) throw new BusinessRuleException("Choose a day of the month between 1 and 28.");
        if (request.EndOn != null && request.EndOn < request.StartOn) throw new BusinessRuleException("The end date cannot be before the start date.");
        if (!Enum.IsDefined(typeof(PaymentMethod), request.PaymentMethod)) throw new BusinessRuleException("Choose how it is paid.");
        if (!await db.Locations.AnyAsync(l => l.LocationId == request.LocationId, ct)) throw new BusinessRuleException("Choose the location.");
        RecurringExpense r;
        if (id == null)
        {
            r = new RecurringExpense();
            db.Set<RecurringExpense>().Add(r);
        }
        else
        {
            r = await db.Set<RecurringExpense>().FirstOrDefaultAsync(x => x.RecurringExpenseId == id, ct) ?? throw new NotFoundException("Recurring expense", id.Value);
        }
        r.Category = request.Category.Trim();
        r.Description = request.Description.Trim();
        r.DepartmentId = request.DepartmentId;
        r.SupplierId = request.SupplierId;
        r.LocationId = request.LocationId;
        r.Amount = FinanceRules.Round(request.Amount);
        r.PaymentMethod = request.PaymentMethod;
        r.DayOfMonth = request.DayOfMonth;
        r.StartOn = request.StartOn;
        r.EndOn = request.EndOn;
        r.IsActive = request.IsActive;
        await db.SaveChangesAsync(ct);
        return (await GetRecurringAsync(ct)).First(x => x.RecurringExpenseId == r.RecurringExpenseId);
    }

    public async Task<int> RaiseRecurringDraftsAsync(CancellationToken ct = default)
    {
        DateOnly today = FinanceRules.Today(clock);
        List<RecurringExpense> due = await db.Set<RecurringExpense>().Where(r => r.IsActive).ToListAsync(ct);
        int raised = 0;
        foreach (RecurringExpense r in due)
        {
            DateOnly? next = NextDue(r, today);
            if (next == null || next.Value.AddDays(-3) > today) continue;
            if (await db.Set<PeriodLock>().AnyAsync(p => p.Period == FinanceRules.Period(next.Value) && p.IsLocked, ct)) continue;
            await FinanceRules.InTransactionAsync(db, async () =>
            {
                RecurringExpense row = await db.Set<RecurringExpense>().FirstAsync(x => x.RecurringExpenseId == r.RecurringExpenseId, ct);
                var expense = new Expense
                {
                    ExpenseNumber = await sequences.NextAsync(SequenceKeys.Expense, ct),
                    ExpenseDate = next.Value,
                    Category = row.Category,
                    Description = row.Description,
                    Amount = row.Amount,
                    PaymentMethod = (PaymentMethod)row.PaymentMethod,
                    Remarks = "Recurring draft - confirm the amount before it is paid.",
                    LocationId = row.LocationId,
                    EnteredByUserId = row.CreatedBy ?? 1,
                    SupplierId = row.SupplierId
                };
                db.Expenses.Add(expense);
                await db.SaveChangesAsync(ct);
                ExpenseControl control = await db.Set<ExpenseControl>().FirstOrDefaultAsync(c => c.ExpenseId == expense.ExpenseId, ct)
                    ?? db.Set<ExpenseControl>().Add(new ExpenseControl { ExpenseId = expense.ExpenseId }).Entity;
                control.IsRecurringDraft = true;
                control.ApprovalStatus = ExpenseApprovalStatus.AwaitingApproval;
                control.RecurringExpenseId = row.RecurringExpenseId;
                control.DepartmentId = row.DepartmentId;
                row.LastRaisedFor = new DateOnly(next.Value.Year, next.Value.Month, 1);
                await db.SaveChangesAsync(ct);
                return true;
            }, ct);
            raised++;
        }
        return raised;
    }
}
