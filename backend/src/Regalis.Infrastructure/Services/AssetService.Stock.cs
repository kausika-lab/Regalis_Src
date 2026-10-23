using Microsoft.EntityFrameworkCore;
using Regalis.Application.Common.Exceptions;
using Regalis.Application.Operations;
using Regalis.Domain.Entities.Operations;
using Regalis.Domain.Enums;
using Regalis.Infrastructure.Excel;
using Regalis.Shared.Api;

namespace Regalis.Infrastructure.Services;

/// <summary>Current Stock (§8): every figure worked out from the movements, none stored.</summary>
public partial class AssetService
{
    // ── Consumables tab ─────────────────────────────────────────────────────

    public async Task<StockSheetDto> GetStockAsync(DateOnly? from, DateOnly? to, int? locationId, bool? belowReorder,
        CancellationToken ct = default)
    {
        var today = Today;
        var periodFrom = from ?? new DateOnly(today.Year, today.Month, 1);
        var periodTo = to ?? today;
        if (periodTo < periodFrom) throw new BusinessRuleException("The period ends before it starts.");

        var rows = await StockRowsAsync(periodFrom, periodTo, locationId, ct);
        if (belowReorder == true) rows = rows.Where(r => r.OrderNow).ToList();

        var monthStart = new DateOnly(today.Year, today.Month, 1);
        var month = await db.InventoryTransactions.AsNoTracking()
            .Where(t => t.TransactionDate >= monthStart && t.TransactionDate <= today)
            .GroupBy(_ => 1)
            .Select(g => new
            {
                In = g.Count(t => t.TransactionType == InventoryTransactionType.In
                                  || t.TransactionType == InventoryTransactionType.OpeningBalance
                                  || (t.TransactionType == InventoryTransactionType.Adjustment && t.Quantity > 0)),
                Out = g.Count(t => t.TransactionType == InventoryTransactionType.Out
                                   || t.TransactionType == InventoryTransactionType.Damage
                                   || (t.TransactionType == InventoryTransactionType.Adjustment && t.Quantity < 0))
            })
            .FirstOrDefaultAsync(ct);
        var lastCount = await db.StockCounts.AsNoTracking().Where(c => c.Status == StockCountStatus.Confirmed)
            .OrderByDescending(c => c.CountDate).Select(c => (DateOnly?)c.CountDate).FirstOrDefaultAsync(ct);

        return new StockSheetDto(clock.UtcNow, periodFrom, periodTo,
            rows.Sum(r => r.Value ?? 0m), rows.Count(r => r.OrderNow),
            (month?.In ?? 0) + (month?.Out ?? 0), month?.In ?? 0, month?.Out ?? 0, lastCount, rows);
    }

    private async Task<List<StockRowDto>> StockRowsAsync(DateOnly from, DateOnly to, int? locationId, CancellationToken ct)
    {
        var itemsQuery = db.InventoryItems.AsNoTracking().Include(i => i.AssetCategory)
            .Where(i => i.IsActive && i.Kind == ItemKind.Consumable && i.ItemType == InventoryItemType.Goods);
        if (locationId is { } roomId) itemsQuery = itemsQuery.Where(i => i.KeptInLocationId == roomId);
        var items = await itemsQuery.OrderBy(i => i.ItemName).ToListAsync(ct);
        var ids = items.Select(i => i.InventoryItemId).ToList();
        var sums = await db.InventoryTransactions.AsNoTracking()
            .Where(t => ids.Contains(t.InventoryItemId) && t.TransactionDate <= to)
            .GroupBy(t => t.InventoryItemId)
            .Select(g => new
            {
                ItemId = g.Key,
                Before = g.Sum(t => t.TransactionDate < from
                    ? (t.TransactionType == InventoryTransactionType.Out || t.TransactionType == InventoryTransactionType.Damage
                        ? -t.Quantity : t.Quantity)
                    : 0m),
                In = g.Sum(t => t.TransactionDate >= from
                                && (t.TransactionType == InventoryTransactionType.In
                                    || t.TransactionType == InventoryTransactionType.OpeningBalance
                                    || (t.TransactionType == InventoryTransactionType.Adjustment && t.Quantity > 0))
                    ? t.Quantity : 0m),
                Out = g.Sum(t => t.TransactionDate >= from
                    ? (t.TransactionType == InventoryTransactionType.Out || t.TransactionType == InventoryTransactionType.Damage
                        ? t.Quantity
                        : t.TransactionType == InventoryTransactionType.Adjustment && t.Quantity < 0 ? -t.Quantity : 0m)
                    : 0m)
            })
            .ToDictionaryAsync(x => x.ItemId, ct);
        var rooms = await RoomsAsync(ct);
        return items.Select(i =>
        {
            var s = sums.GetValueOrDefault(i.InventoryItemId);
            var opening = i.OpeningQuantity + (s?.Before ?? 0m);
            var inQty = s?.In ?? 0m;
            var outQty = s?.Out ?? 0m;
            var onHand = opening + inQty - outQty;
            return new StockRowDto(i.InventoryItemId, i.ItemCode, i.ItemName, i.Unit,
                i.AssetCategory?.CategoryName ?? i.Category,
                i.KeptInLocationId is { } k && rooms.TryGetValue(k, out var room) ? room.LocationName : null,
                opening, inQty, outQty, onHand, i.ReorderLevel, i.UnitCost,
                i.UnitCost is { } cost ? Math.Round(onHand * cost, 2) : null, onHand <= i.ReorderLevel);
        }).ToList();
    }

    // ── Assets by location ──────────────────────────────────────────────────

    /// <summary>One row per room: how many assets, what they are worth, how many are in
    /// repair. No "on hand" for assets — each is counted individually.</summary>
    public async Task<List<AssetsByLocationRowDto>> GetAssetsByLocationAsync(CancellationToken ct = default)
    {
        var assets = await db.AssetDetails.AsNoTracking()
            .Where(d => d.InventoryItem.IsActive && d.Status != AssetStatus.Scrapped && d.Status != AssetStatus.Lost
                        && d.Status != AssetStatus.ReturnedToSupplier)
            .Select(d => new
            {
                d.AssetLocationId, d.Status, d.PurchaseDate, d.InventoryItem.UnitCost,
                Life = d.InventoryItem.AssetCategory != null ? d.InventoryItem.AssetCategory.UsefulLifeYears : null
            })
            .ToListAsync(ct);
        var rooms = await RoomsAsync(ct);
        var workspaceIds = rooms.Values.Where(r => r.WorkspaceId != null).Select(r => r.WorkspaceId!.Value).ToList();
        var workspaces = await db.Workspaces.AsNoTracking().Where(w => workspaceIds.Contains(w.WorkspaceId))
            .ToDictionaryAsync(w => w.WorkspaceId, w => w.WorkspaceName, ct);
        var today = Today;
        return assets.GroupBy(a => a.AssetLocationId)
            .Select(g =>
            {
                var room = rooms.GetValueOrDefault(g.Key);
                return new AssetsByLocationRowDto(g.Key, room?.LocationName ?? "(unknown room)",
                    room?.WorkspaceId is { } w ? workspaces.GetValueOrDefault(w) : null,
                    g.Count(),
                    g.Sum(a => AssetRules.BookValue(a.UnitCost, a.PurchaseDate, a.Life, a.Status, today) ?? 0m),
                    g.Sum(a => a.UnitCost ?? 0m),
                    g.Count(a => a.Status == AssetStatus.InRepair),
                    g.Count(a => a.UnitCost == null));
            })
            .OrderBy(r => rooms.GetValueOrDefault(r.LocationId ?? 0)?.DisplayOrder ?? int.MaxValue)
            .ThenBy(r => r.LocationName)
            .ToList();
    }

    // ── Movement ledger ─────────────────────────────────────────────────────

    /// <summary>A ledger row with its running balance, read straight from SQL: the window
    /// runs over every row of the item, before any filter, so the balance is exact on
    /// any page.</summary>
    public sealed class LedgerSqlRow
    {
        public long InventoryTransactionId { get; set; }
        public string TransactionNumber { get; set; } = "";
        public DateOnly TransactionDate { get; set; }
        public int InventoryItemId { get; set; }
        public string ItemCode { get; set; } = "";
        public string ItemName { get; set; } = "";
        public byte Kind { get; set; }
        public string Unit { get; set; } = "";
        public byte TransactionType { get; set; }
        public decimal Change { get; set; }
        public decimal Balance { get; set; }
        public string? ReferenceNumber { get; set; }
        public byte? SourceType { get; set; }
        public long? SourceId { get; set; }
        public byte? IssueReason { get; set; }
        public string? SourceOrDestination { get; set; }
        public string? LocationName { get; set; }
        public string? EnteredByName { get; set; }
        public DateTime CreatedDate { get; set; }
    }

    public async Task<PagedResult<StockLedgerRowDto>> GetLedgerAsync(StockLedgerRequest request, CancellationToken ct = default)
    {
        var query = db.Database.SqlQuery<LedgerSqlRow>($"""
            SELECT t.InventoryTransactionId, t.TransactionNumber, t.TransactionDate, t.InventoryItemId,
                   i.ItemCode, i.ItemName, i.Kind, i.Unit, t.TransactionType,
                   CASE WHEN t.TransactionType IN (2, 3) THEN -t.Quantity ELSE t.Quantity END AS Change,
                   i.OpeningQuantity + SUM(CASE WHEN t.TransactionType IN (2, 3) THEN -t.Quantity ELSE t.Quantity END)
                       OVER (PARTITION BY t.InventoryItemId ORDER BY t.TransactionDate, t.InventoryTransactionId
                             ROWS UNBOUNDED PRECEDING) AS Balance,
                   t.ReferenceNumber, t.SourceType, t.SourceId, t.IssueReason, t.SourceOrDestination,
                   l.LocationName, u.FullName AS EnteredByName, t.CreatedDate
              FROM dbo.TRN_InventoryTransaction t
              JOIN dbo.MST_InventoryItem i ON i.InventoryItemId = t.InventoryItemId
              LEFT JOIN dbo.MST_AssetLocation l ON l.AssetLocationId = t.AssetLocationId
              LEFT JOIN dbo.MST_User u ON u.UserId = t.EnteredByUserId
             WHERE t.IsDeleted = 0
            """);
        if (request.InventoryItemId is { } itemId) query = query.Where(r => r.InventoryItemId == itemId);
        if (request.Kind is { } kind) query = query.Where(r => r.Kind == kind);
        if (request.From is { } from) query = query.Where(r => r.TransactionDate >= from);
        if (request.To is { } to) query = query.Where(r => r.TransactionDate <= to);
        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var term = request.Search.Trim();
            query = query.Where(r => r.ItemName.Contains(term) || r.ItemCode.Contains(term)
                                     || r.TransactionNumber.Contains(term)
                                     || (r.ReferenceNumber != null && r.ReferenceNumber.Contains(term)));
        }
        var total = await query.CountAsync(ct);
        var rows = await query.OrderByDescending(r => r.TransactionDate).ThenByDescending(r => r.InventoryTransactionId)
            .Skip((request.Page - 1) * request.PageSize).Take(request.PageSize).ToListAsync(ct);
        var dtos = rows.Select(r => new StockLedgerRowDto(r.InventoryTransactionId, r.TransactionNumber, r.TransactionDate,
            r.InventoryItemId, r.ItemCode, r.ItemName, r.Kind, r.Unit, r.TransactionType, r.Change, r.Balance,
            LedgerWhat(r), r.ReferenceNumber, r.SourceType, r.SourceId, r.LocationName, r.EnteredByName,
            r.CreatedDate)).ToList();
        return PagedResult<StockLedgerRowDto>.Create(dtos, request.Page, request.PageSize, total);
    }

    private static string LedgerWhat(LedgerSqlRow r) =>
        ((StockSource?)r.SourceType, (InventoryTransactionType)r.TransactionType) switch
        {
            (StockSource.GoodsReceipt, InventoryTransactionType.OpeningBalance) => "Opening balance",
            (StockSource.GoodsReceipt, InventoryTransactionType.Out) => "Receipt reversed",
            (StockSource.GoodsReceipt, _) => "Received" + (r.SourceOrDestination is { } s ? " from " + s : ""),
            (StockSource.GoodsIssue, _) when r.IssueReason == (byte)GoodsIssueReason.Reversal => "Movement reversed",
            (StockSource.GoodsIssue, _) => r.IssueReason is { } reason ? AssetRules.ReasonName((GoodsIssueReason)reason) : "Issued",
            (StockSource.StockCount, _) => "Stock count adjustment",
            (_, InventoryTransactionType.In) => "Received" + (r.SourceOrDestination is { } s ? " from " + s : ""),
            (_, InventoryTransactionType.Out) => "Issued" + (r.SourceOrDestination is { } s ? " — " + s : ""),
            (_, InventoryTransactionType.Damage) => "Damaged",
            (_, InventoryTransactionType.OpeningBalance) => "Opening balance",
            _ => "Adjustment"
        };

    // ── Stock counts (§8.4) ─────────────────────────────────────────────────

    public async Task<List<StockCountRowDto>> GetStockCountsAsync(CancellationToken ct = default)
    {
        var rooms = await RoomsAsync(ct);
        var counts = await db.StockCounts.AsNoTracking().OrderByDescending(c => c.StockCountId).Take(50)
            .Select(c => new
            {
                c.StockCountId, c.CountNumber, c.CountDate, c.AssetLocationId, c.CountedBy, c.Status,
                Lines = c.Lines.Count,
                Differences = c.Lines.Count(l => l.Difference != null && l.Difference != 0)
            }).ToListAsync(ct);
        return counts.Select(c => new StockCountRowDto(c.StockCountId, c.CountNumber, c.CountDate,
            c.AssetLocationId is { } r && rooms.TryGetValue(r, out var room) ? room.LocationName : "Every consumable",
            c.CountedBy, (byte)c.Status, c.Lines, c.Differences)).ToList();
    }

    public async Task<StockCountDto> GetStockCountAsync(long countId, CancellationToken ct = default)
    {
        var count = await db.StockCounts.AsNoTracking().Include(c => c.Lines)
            .FirstOrDefaultAsync(c => c.StockCountId == countId, ct)
            ?? throw new NotFoundException("StockCount", countId);
        var rooms = await RoomsAsync(ct);
        var ids = count.Lines.Select(l => l.InventoryItemId).ToList();
        var items = await db.InventoryItems.IgnoreQueryFilters().AsNoTracking()
            .Where(i => ids.Contains(i.InventoryItemId)).ToDictionaryAsync(i => i.InventoryItemId, ct);
        // The counter writes what is on the shelf; the recorded figure stays hidden until
        // the count is submitted.
        var reveal = count.Status is StockCountStatus.Submitted or StockCountStatus.Confirmed;
        var lines = count.Lines.Select(l =>
        {
            var item = items[l.InventoryItemId];
            return new StockCountLineDto(l.StockCountLineId, l.InventoryItemId, item.ItemCode, item.ItemName, item.Unit,
                item.KeptInLocationId is { } k && rooms.TryGetValue(k, out var room) ? room.LocationName : null,
                l.CountedQuantity, reveal ? l.RecordedQuantity : null, reveal ? l.Difference : null);
        }).OrderBy(l => l.KeptIn).ThenBy(l => l.ItemName).ToList();
        return new StockCountDto(count.StockCountId, count.CountNumber, count.CountDate, count.AssetLocationId,
            count.AssetLocationId is { } r && rooms.TryGetValue(r, out var countRoom) ? countRoom.LocationName : null,
            count.CountedBy, (byte)count.Status, count.Notes, count.SubmittedUtc, count.ConfirmedUtc,
            reveal ? lines.Count(l => l.Difference is { } d && d != 0) : 0, lines);
    }

    public async Task<StockCountDto> StartStockCountAsync(StartStockCountRequest request, CancellationToken ct = default)
    {
        var open = await db.StockCounts.AsNoTracking()
            .Where(c => c.Status == StockCountStatus.Open || c.Status == StockCountStatus.Submitted)
            .Select(c => c.CountNumber).FirstOrDefaultAsync(ct);
        if (open is not null) throw new BusinessRuleException($"Finish or cancel stock count {open} first.");
        var date = request.CountDate ?? Today;
        if (date > Today) throw new BusinessRuleException("A count cannot be dated in the future.");
        await EnsureRoomAsync(request.LocationId, ct);

        var itemsQuery = db.InventoryItems.AsNoTracking()
            .Where(i => i.IsActive && i.Kind == ItemKind.Consumable && i.ItemType == InventoryItemType.Goods);
        if (request.LocationId is { } roomId) itemsQuery = itemsQuery.Where(i => i.KeptInLocationId == roomId);
        var itemIds = await itemsQuery.OrderBy(i => i.ItemName).Select(i => i.InventoryItemId).ToListAsync(ct);
        if (itemIds.Count == 0) throw new BusinessRuleException("There are no consumables to count there.");

        var id = await InTransactionAsync(async () =>
        {
            var count = new StockCount
            {
                CountNumber = await ops.NextDocumentNumberAsync("SC", date, ct),
                CountDate = date,
                AssetLocationId = request.LocationId,
                CountedBy = Trim(request.CountedBy) ?? currentUser.FullName ?? currentUser.Email ?? "Admin",
                Notes = Trim(request.Notes)
            };
            foreach (var itemId in itemIds) count.Lines.Add(new StockCountLine { InventoryItemId = itemId });
            db.StockCounts.Add(count);
            await db.SaveChangesAsync(ct);
            return count.StockCountId;
        }, ct);
        return await GetStockCountAsync(id, ct);
    }

    /// <summary>Takes the counted figures and shows the differences against the ledger:
    /// "Coffee — recorded 7, counted 5, short 2". Nothing is written to the ledger yet.</summary>
    public async Task<StockCountDto> SubmitStockCountAsync(long countId, SubmitStockCountRequest request,
        CancellationToken ct = default)
    {
        var count = await db.StockCounts.Include(c => c.Lines).FirstOrDefaultAsync(c => c.StockCountId == countId, ct)
                    ?? throw new NotFoundException("StockCount", countId);
        if (count.Status is not (StockCountStatus.Open or StockCountStatus.Submitted))
        {
            throw new BusinessRuleException("This count is already finished.");
        }
        var counted = request.Lines.GroupBy(l => l.InventoryItemId).ToDictionary(g => g.Key, g => g.Last().CountedQuantity);
        var missing = count.Lines.Count(l => !counted.ContainsKey(l.InventoryItemId));
        if (missing > 0)
        {
            throw new BusinessRuleException($"{missing} item(s) have no count. Write 0 for anything not on the shelf.");
        }
        var recorded = await ops.OnHandAsync(count.Lines.Select(l => l.InventoryItemId).ToList(), ct);
        foreach (var line in count.Lines)
        {
            line.CountedQuantity = counted[line.InventoryItemId];
            line.RecordedQuantity = recorded[line.InventoryItemId];
            line.Difference = line.CountedQuantity - line.RecordedQuantity;
        }
        count.Status = StockCountStatus.Submitted;
        count.SubmittedUtc = clock.UtcNow;
        await db.SaveChangesAsync(ct);
        return await GetStockCountAsync(countId, ct);
    }

    /// <summary>Writes one adjustment movement per difference, with the reason "stock
    /// count", the date and who counted (§8.4). The recorded figure is never overwritten:
    /// it is corrected by a movement, like everything else.</summary>
    public async Task<StockCountDto> ConfirmStockCountAsync(long countId, CancellationToken ct = default)
    {
        await InTransactionAsync(async () =>
        {
            var count = await db.StockCounts.Include(c => c.Lines).FirstOrDefaultAsync(c => c.StockCountId == countId, ct)
                        ?? throw new NotFoundException("StockCount", countId);
            if (count.Status != StockCountStatus.Submitted)
            {
                throw new BusinessRuleException(count.Status == StockCountStatus.Open
                    ? "Submit the counted figures first, to see the differences."
                    : "This count is already finished.");
            }
            // Recorded again inside the transaction: a movement posted since the count was
            // submitted is part of the recorded figure the difference is measured against.
            var recorded = await ops.OnHandAsync(count.Lines.Select(l => l.InventoryItemId).ToList(), ct);
            var items = await db.InventoryItems.Where(i => count.Lines.Select(l => l.InventoryItemId).Contains(i.InventoryItemId))
                .ToDictionaryAsync(i => i.InventoryItemId, ct);
            foreach (var line in count.Lines)
            {
                line.RecordedQuantity = recorded[line.InventoryItemId];
                line.Difference = (line.CountedQuantity ?? 0m) - line.RecordedQuantity;
                if (line.Difference == 0) continue;
                var item = items[line.InventoryItemId];
                var row = await ops.LedgerRowAsync(item, InventoryTransactionType.Adjustment, line.Difference!.Value,
                    count.CountDate, StockSource.StockCount, count.StockCountId, count.CountNumber,
                    count.AssetLocationId ?? item.KeptInLocationId, ct);
                row.SourceOrDestination = "Stock count";
                row.Notes = $"Stock count {count.CountNumber} by {count.CountedBy}: recorded {Qty(line.RecordedQuantity!.Value)}, " +
                            $"counted {Qty(line.CountedQuantity ?? 0m)}";
                db.InventoryTransactions.Add(row);
                var diff = line.Difference.Value;
                ops.AddEvent(item.InventoryItemId, "COUNTED", count.CountDate,
                    $"Stock count: recorded {Qty(line.RecordedQuantity!.Value)}, counted {Qty(line.CountedQuantity ?? 0m)}, " +
                    (diff < 0 ? $"short {Qty(-diff)}" : $"over {Qty(diff)}"),
                    null, null, count.CountNumber);
            }
            count.Status = StockCountStatus.Confirmed;
            count.ConfirmedUtc = clock.UtcNow;
            count.ConfirmedByUserId = currentUser.UserId;
            await db.SaveChangesAsync(ct);
            return true;
        }, ct);
        return await GetStockCountAsync(countId, ct);
    }

    public async Task<StockCountDto> CancelStockCountAsync(long countId, CancellationToken ct = default)
    {
        var count = await db.StockCounts.FirstOrDefaultAsync(c => c.StockCountId == countId, ct)
                    ?? throw new NotFoundException("StockCount", countId);
        if (count.Status is not (StockCountStatus.Open or StockCountStatus.Submitted))
        {
            throw new BusinessRuleException("This count is already finished.");
        }
        count.Status = StockCountStatus.Cancelled;
        await db.SaveChangesAsync(ct);
        return await GetStockCountAsync(countId, ct);
    }

    // ── Exports ─────────────────────────────────────────────────────────────

    public async Task<InventoryExportResult?> ExportStockAsync(DateOnly? from, DateOnly? to, int? locationId,
        CancellationToken ct = default)
    {
        var sheet = await GetStockAsync(from, to, locationId, null, ct);
        if (sheet.Rows.Count == 0) return null;
        var content = ExcelWorkbookHelper.Write("Current Stock", new List<ExcelColumn<StockRowDto>>
        {
            new("Item", r => r.ItemName),
            new("Code", r => r.ItemCode),
            new("Unit", r => r.Unit),
            new("Kept in", r => r.KeptIn),
            new($"Opening {sheet.From:dd-MMM-yyyy}", r => r.Opening, ExcelCellKind.Number),
            new("In", r => r.In, ExcelCellKind.Number),
            new("Out", r => r.Out, ExcelCellKind.Number),
            new($"On hand {sheet.To:dd-MMM-yyyy}", r => r.OnHand, ExcelCellKind.Number),
            new("Reorder at", r => r.ReorderLevel, ExcelCellKind.Number),
            new("Unit cost", r => r.UnitCost, ExcelCellKind.Money),
            new("Value", r => r.Value, ExcelCellKind.Money),
            new("Flag", r => r.OrderNow ? "Order now" : "Fine")
        }, sheet.Rows);
        return new InventoryExportResult(content, ExcelWorkbookHelper.ContentType,
            ExcelWorkbookHelper.FileName("current_stock", clock.UtcNow));
    }

    /// <summary>Every filtered row, in the column order of the team's own asset sheet,
    /// then the fields the app adds.</summary>
    public async Task<InventoryExportResult?> ExportItemsAsync(AssetItemListRequest request, CancellationToken ct = default)
    {
        var query = await FilterAsync(request, ct);
        var items = await Sort(query, request).Include(i => i.AssetDetail).Include(i => i.AssetCategory)
            .Take(MaxExportRows).ToListAsync(ct);
        if (items.Count == 0) return null;
        var rows = await RowsAsync(items, ct);
        var byId = items.ToDictionary(i => i.InventoryItemId);
        var serial = 0;
        var content = ExcelWorkbookHelper.Write("Items", new List<ExcelColumn<AssetItemRowDto>>
        {
            new("Sr. No.", _ => ++serial),
            new("Asset ID", r => r.ItemCode),
            new("Item Name", r => r.ItemName),
            new("Category", r => r.CategoryName),
            new("Description", r => byId[r.InventoryItemId].Description),
            new("Brand / Model", r => r.BrandModel),
            new("Serial No.", r => r.SerialNumber),
            new("Purchase Date", r => r.PurchaseDate, ExcelCellKind.Date),
            new("Vendor", r => byId[r.InventoryItemId].VendorName),
            new("Quantity", r => r.Kind == (byte)ItemKind.Asset ? 1m : r.OnHand, ExcelCellKind.Number),
            new("Purchase Amount", r => r.UnitCost, ExcelCellKind.Money),
            new("Location", r => r.LocationName),
            new("Assigned To", _ => null),
            new("Status", r => r.Status is { } s ? AssetRules.StatusName((AssetStatus)s) : r.IsService ? "Service" : r.BelowReorder ? "Below reorder" : "In stock"),
            new("Condition", r => r.Condition is { } c ? AssetRules.ConditionName((AssetCondition)c) : null),
            new("Remarks", r => byId[r.InventoryItemId].Remarks),
            new("Kind", r => r.IsService ? "Service" : r.Kind == (byte)ItemKind.Asset ? "Asset" : "Consumable"),
            new("Unit", r => r.Unit),
            new("Reorder level", r => r.Kind == (byte)ItemKind.Consumable && !r.IsService ? r.ReorderLevel : null, ExcelCellKind.Number),
            new("Value", r => r.Value, ExcelCellKind.Money),
            new("Warranty until", r => r.WarrantyUntil, ExcelCellKind.Date),
            new("QR label printed", r => r.LabelPrintedUtc, ExcelCellKind.DateTime)
        }, rows);
        return new InventoryExportResult(content, ExcelWorkbookHelper.ContentType,
            ExcelWorkbookHelper.FileName("assets_items", clock.UtcNow));
    }
}
