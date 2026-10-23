using Microsoft.EntityFrameworkCore;
using Regalis.Application.Common.Exceptions;
using Regalis.Application.Operations;
using Regalis.Domain.Entities.Operations;
using Regalis.Domain.Enums;
using Regalis.Infrastructure.Excel;
using Regalis.Shared.Api;

namespace Regalis.Infrastructure.Services;

/// <summary>Outgoing Goods (§7): the only way stock goes down and the only way an asset
/// changes room or status. The reason chosen decides everything after it.</summary>
public partial class AssetService
{
    // ── List and read ───────────────────────────────────────────────────────

    private IQueryable<GoodsIssue> FilterIssues(GoodsIssueListRequest request)
    {
        var query = db.GoodsIssues.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var term = request.Search.Trim();
            query = query.Where(g => g.IssueNumber.Contains(term)
                                     || (g.RepairVendor != null && g.RepairVendor.Contains(term))
                                     || (g.ReportedBy != null && g.ReportedBy.Contains(term))
                                     || (g.Note != null && g.Note.Contains(term))
                                     || g.Lines.Any(l => l.InventoryItem.ItemName.Contains(term)
                                                         || l.InventoryItem.ItemCode.Contains(term))
                                     || db.Users.Any(u => u.UserId == g.PostedByUserId && u.FullName.Contains(term)));
        }
        if (request.Reason is { } reason) query = query.Where(g => (byte)g.Reason == reason);
        if (request.InventoryItemId is { } itemId) query = query.Where(g => g.Lines.Any(l => l.InventoryItemId == itemId));
        if (request.From is { } from) query = query.Where(g => g.IssueDate >= from);
        if (request.To is { } to) query = query.Where(g => g.IssueDate <= to);
        return query;
    }

    public async Task<PagedResult<GoodsIssueRowDto>> GetIssuesAsync(GoodsIssueListRequest request, CancellationToken ct = default)
    {
        var query = FilterIssues(request);
        var total = await query.CountAsync(ct);
        var issues = await query.OrderByDescending(g => g.IssueDate).ThenByDescending(g => g.GoodsIssueId)
            .Skip((request.Page - 1) * request.PageSize).Take(request.PageSize)
            .Include(g => g.Lines).ThenInclude(l => l.InventoryItem)
            .ToListAsync(ct);
        return PagedResult<GoodsIssueRowDto>.Create(await IssueRowsAsync(issues, ct), request.Page, request.PageSize, total);
    }

    private async Task<List<GoodsIssueRowDto>> IssueRowsAsync(List<GoodsIssue> issues, CancellationToken ct)
    {
        var rooms = await RoomsAsync(ct);
        var ids = issues.Select(g => g.GoodsIssueId).ToList();
        var linked = issues.SelectMany(g => new[] { g.ReversedByIssueId, g.ReversesIssueId })
            .Where(x => x != null).Select(x => x!.Value).ToList();
        var numbers = await db.GoodsIssues.AsNoTracking().Where(g => linked.Contains(g.GoodsIssueId))
            .ToDictionaryAsync(g => g.GoodsIssueId, g => g.IssueNumber, ct);
        var users = issues.Select(g => g.PostedByUserId).Distinct().ToList();
        var names = await db.Users.IgnoreQueryFilters().AsNoTracking().Where(u => users.Contains(u.UserId))
            .ToDictionaryAsync(u => u.UserId, u => u.FullName, ct);
        var openRepairs = await db.AssetRepairs.AsNoTracking().Where(r => ids.Contains(r.GoodsIssueId) && r.IsOpen)
            .Select(r => r.GoodsIssueId).ToListAsync(ct);
        string? Room(int? id) => id is { } r && rooms.TryGetValue(r, out var room) ? room.LocationName : null;
        return issues.Select(g =>
        {
            var first = g.Lines.OrderBy(l => l.GoodsIssueLineId).FirstOrDefault();
            var where = g.Reason switch
            {
                GoodsIssueReason.Moved => Room(g.ToLocationId),
                GoodsIssueReason.SentForRepair => g.RepairVendor,
                GoodsIssueReason.UsedUp => Join(Room(g.FromLocationId), g.Note),
                GoodsIssueReason.ReturnedToSupplier => Join(g.ReasonText,
                    g.CreditNoteRaised == true ? "credit note raised" : g.CreditNoteRaised == false ? "no credit note" : null),
                GoodsIssueReason.Scrapped => Join(g.ReasonText,
                    g.AmountRecovered is { } rec ? Money(rec) + " recovered" : null),
                GoodsIssueReason.Lost => Join(g.Note, g.ReportedBy is null ? null : "reported by " + g.ReportedBy),
                GoodsIssueReason.Reversal => g.Note,
                _ => g.Note
            };
            return new GoodsIssueRowDto(g.GoodsIssueId, g.IssueNumber, g.IssueDate, (byte)g.Reason, g.Lines.Count,
                first?.InventoryItemId, first?.InventoryItem.ItemName, first?.InventoryItem.ItemCode,
                first is null ? null : (byte)first.InventoryItem.Kind, first?.Quantity, first?.InventoryItem.Unit,
                where, names.GetValueOrDefault(g.PostedByUserId), g.ReversesIssueId != null, g.ReversedByIssueId != null,
                g.ReversedByIssueId is { } by ? numbers.GetValueOrDefault(by)
                    : g.ReversesIssueId is { } of ? numbers.GetValueOrDefault(of) : null,
                openRepairs.Contains(g.GoodsIssueId));
        }).ToList();
    }

    private static string? Join(params string?[] parts)
    {
        var text = string.Join(" · ", parts.Where(p => !string.IsNullOrWhiteSpace(p)));
        return text.Length == 0 ? null : text;
    }

    public async Task<GoodsIssueDto> GetIssueAsync(long issueId, CancellationToken ct = default)
    {
        var g = await db.GoodsIssues.AsNoTracking().Include(x => x.Lines).ThenInclude(l => l.InventoryItem)
            .FirstOrDefaultAsync(x => x.GoodsIssueId == issueId, ct)
            ?? throw new NotFoundException("GoodsIssue", issueId);
        var rooms = await RoomsAsync(ct);
        string? Room(int? id) => id is { } r && rooms.TryGetValue(r, out var room) ? room.LocationName : null;
        async Task<string?> NumberAsync(long? id) => id is null ? null
            : await db.GoodsIssues.Where(x => x.GoodsIssueId == id).Select(x => x.IssueNumber).FirstOrDefaultAsync(ct);
        var postedBy = await db.Users.IgnoreQueryFilters().Where(u => u.UserId == g.PostedByUserId)
            .Select(u => u.FullName).FirstOrDefaultAsync(ct);
        return new GoodsIssueDto(g.GoodsIssueId, g.IssueNumber, g.IssueDate, (byte)g.Reason, g.FromLocationId,
            Room(g.FromLocationId), g.ToLocationId, Room(g.ToLocationId), g.RepairVendor, g.ExpectedCost,
            g.CreditNoteRaised, g.AmountRecovered, g.ReportedBy, g.ReasonText, g.Note, postedBy, g.PostedUtc,
            g.ReversesIssueId, await NumberAsync(g.ReversesIssueId), g.ReversedByIssueId,
            await NumberAsync(g.ReversedByIssueId),
            g.Lines.OrderBy(l => l.GoodsIssueLineId).Select(l => new GoodsIssueLineDto(l.GoodsIssueLineId,
                l.InventoryItemId, l.InventoryItem.ItemCode, l.InventoryItem.ItemName, (byte)l.InventoryItem.Kind,
                l.InventoryItem.Unit, l.Quantity, l.UnitCost, l.Amount,
                l.PreviousStatus is null ? null : (byte)l.PreviousStatus, Room(l.PreviousLocationId),
                l.NewStatus is null ? null : (byte)l.NewStatus, Room(l.NewLocationId))).ToList());
    }

    // ── Checking a movement ─────────────────────────────────────────────────

    private sealed record IssuePlanLine(InventoryItem Item, decimal Quantity, decimal? OnHand, string? Error, bool Skip);

    /// <summary>
    /// Works out, line by line, whether the movement may happen. Shared by the preview
    /// (which only reports) and posting (which runs it again inside the transaction, so
    /// the stock it checks is the stock it writes against).
    /// </summary>
    private async Task<List<IssuePlanLine>> PlanIssueAsync(CreateGoodsIssueRequest request, bool tracked,
        CancellationToken ct)
    {
        var reason = (GoodsIssueReason)request.Reason;
        var ids = request.Lines.Select(l => l.InventoryItemId).Distinct().ToList();
        var query = db.InventoryItems.Include(i => i.AssetDetail).Where(i => ids.Contains(i.InventoryItemId));
        var items = await (tracked ? query : query.AsNoTracking()).ToDictionaryAsync(i => i.InventoryItemId, ct);
        var onHand = await ops.OnHandAsync(items.Values.Where(i => i.Kind == ItemKind.Consumable)
            .Select(i => i.InventoryItemId).ToList(), ct);
        var rooms = await RoomsAsync(ct);
        var plan = new List<IssuePlanLine>();
        var seen = new HashSet<int>();
        foreach (var line in request.Lines)
        {
            if (!items.TryGetValue(line.InventoryItemId, out var item))
            {
                throw new NotFoundException("InventoryItem", line.InventoryItemId);
            }
            if (!seen.Add(item.InventoryItemId))
            {
                throw new BusinessRuleException($"{item.ItemCode} is on the movement twice.");
            }
            string? error = null;
            var skip = false;
            if (item.ItemType == InventoryItemType.Service)
            {
                error = $"{item.ItemName} is a service — it has no stock.";
                skip = true;
            }
            else if (!item.IsActive)
            {
                error = $"{item.ItemCode} is inactive.";
            }
            else if (item.Kind == ItemKind.Asset)
            {
                var d = item.AssetDetail!;
                if (reason == GoodsIssueReason.UsedUp)
                {
                    error = "An asset is not used up — choose Scrapped or Lost.";
                    skip = true;
                }
                else if (line.Quantity != 1)
                {
                    error = "An asset is one record: the quantity is 1.";
                }
                else if (AssetRules.IsRetired(d.Status))
                {
                    error = $"{item.ItemCode} is {AssetRules.StatusName(d.Status).ToLowerInvariant()} — it has left the register.";
                    skip = true;
                }
                else if (d.Status == AssetStatus.InRepair)
                {
                    error = "This item is away for repair. Close the repair first.";
                }
                else if (reason == GoodsIssueReason.Moved && request.ToLocationId == d.AssetLocationId)
                {
                    error = $"{item.ItemCode} is already in " +
                            (rooms.TryGetValue(d.AssetLocationId, out var here) ? here.LocationName : "that room") + ".";
                    skip = true;
                }
            }
            else
            {
                if (reason is GoodsIssueReason.Moved or GoodsIssueReason.SentForRepair)
                {
                    error = reason == GoodsIssueReason.Moved
                        ? $"{item.ItemName} is a consumable — it is not moved room to room."
                        : $"{item.ItemName} is a consumable — it is not sent for repair.";
                    skip = true;
                }
                else
                {
                    var stock = onHand.GetValueOrDefault(item.InventoryItemId);
                    if (line.Quantity > stock)
                    {
                        error = $"Only {Qty(stock)} {item.Unit} on hand. Record a stock count if the figure is wrong.";
                    }
                }
            }
            plan.Add(new IssuePlanLine(item, line.Quantity,
                item.Kind == ItemKind.Consumable && item.ItemType == InventoryItemType.Goods
                    ? onHand.GetValueOrDefault(item.InventoryItemId) : null,
                error, skip));
        }
        return plan;
    }

    private List<ApiError> HeaderErrors(CreateGoodsIssueRequest request, Dictionary<int, AssetLocation> rooms)
    {
        var reason = (GoodsIssueReason)request.Reason;
        var errors = new List<ApiError>();
        if (request.IssueDate is { } date && date > Today) errors.Add(Field("Date cannot be in the future.", "issueDate"));
        switch (reason)
        {
            case GoodsIssueReason.Moved:
                if (request.ToLocationId is not { } to) errors.Add(Field("Choose the room it is going to.", "toLocationId"));
                else if (!rooms.TryGetValue(to, out var room) || !room.IsActive)
                    errors.Add(Field("That room is not available.", "toLocationId"));
                break;
            case GoodsIssueReason.SentForRepair:
                if (string.IsNullOrWhiteSpace(request.RepairVendor))
                    errors.Add(Field("Who did it go to?", "repairVendor"));
                break;
            case GoodsIssueReason.ReturnedToSupplier:
            case GoodsIssueReason.Scrapped:
                if (string.IsNullOrWhiteSpace(request.ReasonText)) errors.Add(Field("Give the reason.", "reasonText"));
                break;
            case GoodsIssueReason.Lost:
                if (string.IsNullOrWhiteSpace(request.ReportedBy)) errors.Add(Field("Who reported it?", "reportedBy"));
                break;
        }
        return errors;
    }

    public async Task<GoodsIssuePreviewDto> PreviewIssueAsync(CreateGoodsIssueRequest request, CancellationToken ct = default)
    {
        var plan = await PlanIssueAsync(request, tracked: false, ct);
        var lines = plan.Select(p =>
        {
            var i = p.Item;
            if (p.OnHand is not { } stock)
            {
                return new GoodsIssueLinePreviewDto(i.InventoryItemId, i.ItemCode, i.ItemName, (byte)i.Kind, i.Unit,
                    null, null, 0m, false, p.Error ?? "", p.Error);
            }
            var after = stock - p.Quantity;
            var below = after <= i.ReorderLevel;
            var message = p.Error ?? $"{Qty(stock)} {i.Unit} in stock — after this, {Qty(after)} {i.Unit}. " +
                (below
                    ? $"That is at or below the reorder level of {Qty(i.ReorderLevel)} — order more."
                    : $"Still above the reorder level of {Qty(i.ReorderLevel)}.");
            return new GoodsIssueLinePreviewDto(i.InventoryItemId, i.ItemCode, i.ItemName, (byte)i.Kind, i.Unit,
                stock, after, i.ReorderLevel, below && p.Error is null, message, p.Error);
        }).ToList();
        var skipped = plan.Count(p => p.Skip);
        return new GoodsIssuePreviewDto(lines, skipped, skipped == 0 ? null : SkippedMessage(plan, request));
    }

    private static string SkippedMessage(List<IssuePlanLine> plan, CreateGoodsIssueRequest request)
    {
        var skipped = plan.Where(p => p.Skip).ToList();
        var consumables = skipped.Count(p => p.Item.Kind == ItemKind.Consumable);
        var others = skipped.Count - consumables;
        var parts = new List<string>();
        if (consumables > 0)
        {
            parts.Add((GoodsIssueReason)request.Reason == GoodsIssueReason.Moved
                ? $"{consumables} consumable{(consumables == 1 ? " was" : "s were")} skipped — they have no room to move"
                : $"{consumables} consumable{(consumables == 1 ? " was" : "s were")} skipped");
        }
        if (others > 0) parts.Add($"{others} asset{(others == 1 ? " was" : "s were")} skipped — {skipped.First(p => p.Item.Kind == ItemKind.Asset).Error}");
        return string.Join("; ", parts) + ".";
    }

    // ── Posting ─────────────────────────────────────────────────────────────

    public async Task<GoodsIssueResult> CreateIssueAsync(CreateGoodsIssueRequest request, CancellationToken ct = default)
    {
        var reason = (GoodsIssueReason)request.Reason;
        if (reason == GoodsIssueReason.Reversal) throw new BusinessRuleException("Choose why it is going out.");
        var skipped = 0;
        string? message = null;
        var issueId = await InTransactionAsync(async () =>
        {
            var rooms = await RoomsAsync(ct);
            var errors = HeaderErrors(request, rooms);
            if (errors.Count > 0) throw new AppValidationException(errors);

            var plan = await PlanIssueAsync(request, tracked: true, ct);
            var blocking = plan.Where(p => p.Error is not null && !(p.Skip && request.SkipInapplicable)).ToList();
            if (blocking.Count > 0)
            {
                throw new BusinessRuleException(blocking.Count == 1
                    ? blocking[0].Error!
                    : $"{blocking.Count} lines cannot go out: " + string.Join(" ", blocking.Take(3).Select(b => $"{b.Item.ItemCode}: {b.Error}")));
            }
            var lines = plan.Where(p => p.Error is null).ToList();
            skipped = plan.Count - lines.Count;
            if (lines.Count == 0) throw new BusinessRuleException("Nothing on this movement can go out for that reason.");
            message = skipped == 0 ? null : SkippedMessage(plan, request);

            var date = request.IssueDate ?? Today;
            var single = lines.Count == 1 ? lines[0].Item : null;
            var issue = new GoodsIssue
            {
                IssueNumber = await ops.NextDocumentNumberAsync("OUT", date, ct),
                IssueDate = date,
                Reason = reason,
                FromLocationId = request.FromLocationId
                                 ?? single?.AssetDetail?.AssetLocationId ?? single?.KeptInLocationId,
                ToLocationId = reason == GoodsIssueReason.Moved ? request.ToLocationId : null,
                RepairVendor = reason == GoodsIssueReason.SentForRepair ? Trim(request.RepairVendor) : null,
                ExpectedCost = reason == GoodsIssueReason.SentForRepair ? request.ExpectedCost : null,
                CreditNoteRaised = reason == GoodsIssueReason.ReturnedToSupplier ? request.CreditNoteRaised : null,
                AmountRecovered = reason == GoodsIssueReason.Scrapped ? request.AmountRecovered : null,
                ReportedBy = reason == GoodsIssueReason.Lost ? Trim(request.ReportedBy) : null,
                ReasonText = reason is GoodsIssueReason.ReturnedToSupplier or GoodsIssueReason.Scrapped
                    ? Trim(request.ReasonText) : null,
                Note = Trim(request.Note),
                PostedByUserId = currentUser.UserId,
                PostedUtc = clock.UtcNow
            };
            db.GoodsIssues.Add(issue);
            await db.SaveChangesAsync(ct);

            string? Room(int? id) => id is { } r && rooms.TryGetValue(r, out var room) ? room.LocationName : null;
            var reorderAlerts = new List<(InventoryItem Item, decimal After)>();
            foreach (var p in lines)
            {
                var item = p.Item;
                var line = new GoodsIssueLine
                {
                    InventoryItemId = item.InventoryItemId,
                    Quantity = p.Quantity,
                    UnitCost = item.UnitCost,
                    Amount = item.UnitCost is { } cost ? Math.Round(cost * p.Quantity, 2, MidpointRounding.AwayFromZero) : null
                };
                issue.Lines.Add(line);

                if (item.Kind == ItemKind.Consumable)
                {
                    var row = await ops.LedgerRowAsync(item, InventoryTransactionType.Out, p.Quantity, date,
                        StockSource.GoodsIssue, issue.GoodsIssueId, issue.IssueNumber,
                        issue.FromLocationId ?? item.KeptInLocationId, ct, reason);
                    row.SourceOrDestination = AssetRules.ReasonName(reason);
                    row.Notes = Join(issue.ReasonText, issue.Note);
                    db.InventoryTransactions.Add(row);
                    ops.AddEvent(item.InventoryItemId, EventFor(reason), date,
                        $"{AssetRules.ReasonName(reason)}: {Qty(p.Quantity)} {item.Unit}",
                        Room(issue.FromLocationId ?? item.KeptInLocationId), issue.Note, issue.IssueNumber);
                    var after = p.OnHand!.Value - p.Quantity;
                    if (p.OnHand > item.ReorderLevel && after <= item.ReorderLevel) reorderAlerts.Add((item, after));
                    continue;
                }

                var d = item.AssetDetail!;
                line.PreviousStatus = d.Status;
                line.PreviousLocationId = d.AssetLocationId;
                var fromRoom = Room(d.AssetLocationId);
                switch (reason)
                {
                    case GoodsIssueReason.Moved:
                        var to = rooms[request.ToLocationId!.Value];
                        d.AssetLocationId = to.AssetLocationId;
                        d.Status = AssetRules.StatusForRoom(to.IsStore);
                        ops.AddEvent(item.InventoryItemId, "MOVED", date, "Moved", fromRoom, to.LocationName, issue.IssueNumber);
                        break;
                    case GoodsIssueReason.SentForRepair:
                        db.AssetRepairs.Add(new AssetRepair
                        {
                            InventoryItemId = item.InventoryItemId,
                            GoodsIssueId = issue.GoodsIssueId,
                            SentOn = date,
                            RepairVendor = issue.RepairVendor!,
                            Description = issue.Note,
                            ExpectedCost = issue.ExpectedCost,
                            ReturnStatus = d.Status
                        });
                        d.Status = AssetStatus.InRepair;
                        ops.AddEvent(item.InventoryItemId, "SENT_FOR_REPAIR", date, "Sent for repair",
                            fromRoom, issue.RepairVendor, issue.IssueNumber);
                        break;
                    default:
                        // Returned, scrapped, lost: out of the register, record kept for audit.
                        d.Status = reason switch
                        {
                            GoodsIssueReason.ReturnedToSupplier => AssetStatus.ReturnedToSupplier,
                            GoodsIssueReason.Scrapped => AssetStatus.Scrapped,
                            _ => AssetStatus.Lost
                        };
                        d.RetiredReason = Join(issue.ReasonText, issue.Note, issue.ReportedBy is null ? null : "reported by " + issue.ReportedBy);
                        if (d.RetiredReason?.Length > 300) d.RetiredReason = d.RetiredReason[..300];
                        d.RetiredValue = issue.AmountRecovered;
                        d.RetiredUtc = clock.UtcNow;
                        var row = await ops.LedgerRowAsync(item, InventoryTransactionType.Out, 1m, date,
                            StockSource.GoodsIssue, issue.GoodsIssueId, issue.IssueNumber, d.AssetLocationId, ct, reason);
                        row.SourceOrDestination = AssetRules.ReasonName(reason);
                        row.Notes = d.RetiredReason;
                        db.InventoryTransactions.Add(row);
                        ops.AddEvent(item.InventoryItemId, EventFor(reason), date, AssetRules.ReasonName(reason),
                            fromRoom, d.RetiredReason, issue.IssueNumber);
                        break;
                }
                line.NewStatus = d.Status;
                line.NewLocationId = d.AssetLocationId;
            }

            foreach (var (item, after) in reorderAlerts)
            {
                AdminAlerts.Raise(db, new AdminEvent(AdminNotificationType.Purchase, "stock.below_reorder",
                    $"Below reorder level — {item.ItemName}",
                    $"{Qty(after)} {item.Unit} left after {issue.IssueNumber}; the reorder level is {Qty(item.ReorderLevel)}.",
                    AdminNotificationSeverity.Info, null, "InventoryItem", item.InventoryItemId,
                    $"{item.ItemCode} · {item.ItemName}", "Open Current Stock", "/admin/assets/stock"),
                    clock.UtcNow, currentUser.UserId);
            }
            await db.SaveChangesAsync(ct);
            return issue.GoodsIssueId;
        }, ct);
        return new GoodsIssueResult(await GetIssueAsync(issueId, ct), skipped, message);
    }

    private static string EventFor(GoodsIssueReason reason) => reason switch
    {
        GoodsIssueReason.UsedUp => "USED_UP",
        GoodsIssueReason.Moved => "MOVED",
        GoodsIssueReason.SentForRepair => "SENT_FOR_REPAIR",
        GoodsIssueReason.ReturnedToSupplier => "RETURNED",
        GoodsIssueReason.Scrapped => "SCRAPPED",
        GoodsIssueReason.Lost => "LOST",
        _ => "REVERSED"
    };

    // ── Reverse ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Nothing on Outgoing Goods is deleted: a mistake is corrected by the opposite entry
    /// (§7.1). Consumables come back in; an asset goes back to the room and status it had —
    /// but only while it is still exactly as this movement left it.
    /// </summary>
    public async Task<GoodsIssueDto> ReverseIssueAsync(long issueId, string reason, CancellationToken ct = default)
    {
        var reversalId = await InTransactionAsync(async () =>
        {
            var issue = await db.GoodsIssues.Include(g => g.Lines).ThenInclude(l => l.InventoryItem)
                .ThenInclude(i => i.AssetDetail)
                .FirstOrDefaultAsync(g => g.GoodsIssueId == issueId, ct)
                ?? throw new NotFoundException("GoodsIssue", issueId);
            if (issue.ReversedByIssueId is not null) throw new BusinessRuleException("This movement is already reversed.");
            if (issue.Reason == GoodsIssueReason.Reversal)
                throw new BusinessRuleException("A reversal cannot itself be reversed. Record the movement again instead.");

            var today = Today;
            var note = $"Reverses {issue.IssueNumber}: {reason.Trim()}";
            var reversal = new GoodsIssue
            {
                IssueNumber = await ops.NextDocumentNumberAsync("OUT", today, ct),
                IssueDate = today,
                Reason = GoodsIssueReason.Reversal,
                FromLocationId = issue.ToLocationId,
                ToLocationId = issue.FromLocationId,
                Note = note.Length > 500 ? note[..500] : note,
                ReversesIssueId = issue.GoodsIssueId,
                PostedByUserId = currentUser.UserId,
                PostedUtc = clock.UtcNow
            };
            db.GoodsIssues.Add(reversal);
            await db.SaveChangesAsync(ct);
            var rooms = await RoomsAsync(ct);
            string? Room(int? id) => id is { } r && rooms.TryGetValue(r, out var room) ? room.LocationName : null;

            foreach (var line in issue.Lines)
            {
                var item = line.InventoryItem;
                var back = new GoodsIssueLine
                {
                    InventoryItemId = item.InventoryItemId, Quantity = line.Quantity,
                    UnitCost = line.UnitCost, Amount = line.Amount is { } a ? -a : null
                };
                reversal.Lines.Add(back);
                if (item.Kind == ItemKind.Consumable)
                {
                    var row = await ops.LedgerRowAsync(item, InventoryTransactionType.In, line.Quantity, today,
                        StockSource.GoodsIssue, reversal.GoodsIssueId, reversal.IssueNumber, issue.FromLocationId, ct,
                        GoodsIssueReason.Reversal);
                    row.SourceOrDestination = "Movement reversed";
                    row.Notes = reversal.Note;
                    db.InventoryTransactions.Add(row);
                    ops.AddEvent(item.InventoryItemId, "REVERSED", today,
                        $"{issue.IssueNumber} reversed: {Qty(line.Quantity)} {item.Unit} back in",
                        issue.IssueNumber, null, reversal.IssueNumber);
                    continue;
                }

                var d = item.AssetDetail!;
                // Only an asset's latest movement can be reversed, and only while the asset is
                // still as that movement left it: history is unwound in order, never skipped.
                var later = await db.GoodsIssueLines.AnyAsync(l => l.InventoryItemId == item.InventoryItemId
                    && l.GoodsIssueId > issue.GoodsIssueId && l.GoodsIssueId != reversal.GoodsIssueId, ct);
                if (later || d.Status != line.NewStatus || d.AssetLocationId != line.NewLocationId)
                {
                    throw new BusinessRuleException(
                        $"{item.ItemCode} has moved or changed status since {issue.IssueNumber}. Reverse the later movement first.");
                }
                back.PreviousStatus = d.Status;
                back.PreviousLocationId = d.AssetLocationId;
                if (issue.Reason == GoodsIssueReason.SentForRepair)
                {
                    var repair = await db.AssetRepairs.FirstOrDefaultAsync(
                        r => r.GoodsIssueId == issue.GoodsIssueId && r.InventoryItemId == item.InventoryItemId, ct);
                    if (repair is { IsOpen: false })
                    {
                        throw new BusinessRuleException($"The repair of {item.ItemCode} is already closed.");
                    }
                    if (repair is not null)
                    {
                        repair.IsOpen = false;
                        repair.ClosedOn = today;
                        repair.ClosedNote = "Movement reversed";
                    }
                }
                if (AssetRules.IsRetired(d.Status))
                {
                    var row = await ops.LedgerRowAsync(item, InventoryTransactionType.In, 1m, today,
                        StockSource.GoodsIssue, reversal.GoodsIssueId, reversal.IssueNumber, line.PreviousLocationId, ct,
                        GoodsIssueReason.Reversal);
                    row.SourceOrDestination = "Movement reversed";
                    row.Notes = reversal.Note;
                    db.InventoryTransactions.Add(row);
                    d.RetiredReason = null;
                    d.RetiredValue = null;
                    d.RetiredUtc = null;
                }
                var fromRoom = Room(d.AssetLocationId);
                d.Status = line.PreviousStatus ?? AssetStatus.InUse;
                d.AssetLocationId = line.PreviousLocationId ?? d.AssetLocationId;
                back.NewStatus = d.Status;
                back.NewLocationId = d.AssetLocationId;
                ops.AddEvent(item.InventoryItemId, "REVERSED", today, $"{issue.IssueNumber} reversed",
                    fromRoom, Room(d.AssetLocationId), reversal.IssueNumber);
            }
            issue.ReversedByIssueId = reversal.GoodsIssueId;
            await db.SaveChangesAsync(ct);
            return reversal.GoodsIssueId;
        }, ct);
        return await GetIssueAsync(reversalId, ct);
    }

    // ── Export ──────────────────────────────────────────────────────────────

    public async Task<InventoryExportResult?> ExportIssuesAsync(GoodsIssueListRequest request, CancellationToken ct = default)
    {
        var issues = await FilterIssues(request).OrderByDescending(g => g.IssueDate).ThenByDescending(g => g.GoodsIssueId)
            .Take(MaxExportRows).Include(g => g.Lines).ThenInclude(l => l.InventoryItem).ToListAsync(ct);
        if (issues.Count == 0) return null;
        var rows = await IssueRowsAsync(issues, ct);
        var content = ExcelWorkbookHelper.Write("Outgoing Goods", new List<ExcelColumn<GoodsIssueRowDto>>
        {
            new("Note", r => r.IssueNumber),
            new("Date", r => r.IssueDate, ExcelCellKind.Date),
            new("Reason", r => AssetRules.ReasonName((GoodsIssueReason)r.Reason)),
            new("Item", r => r.FirstItemName + (r.LineCount > 1 ? $" (+{r.LineCount - 1} more)" : "")),
            new("Code", r => r.FirstItemCode),
            new("Quantity", r => r.FirstQuantity, ExcelCellKind.Number),
            new("Unit", r => r.FirstUnit),
            new("Where it went", r => r.WhereItWent),
            new("Recorded by", r => r.PostedByName),
            new("Reversal", r => r.ReversalNumber)
        }, rows);
        return new InventoryExportResult(content, ExcelWorkbookHelper.ContentType,
            ExcelWorkbookHelper.FileName("outgoing_goods", clock.UtcNow));
    }
}
