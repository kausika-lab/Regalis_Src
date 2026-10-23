using System.Globalization;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Regalis.Application.Abstractions;
using Regalis.Application.Common.Exceptions;
using Regalis.Application.Operations;
using Regalis.Domain.Entities.Finance;
using Regalis.Domain.Entities.Operations;
using Regalis.Domain.Enums;
using Regalis.Infrastructure.Persistence;
using Regalis.Shared;
using Regalis.Shared.Api;
using Regalis.Shared.Constants;

namespace Regalis.Infrastructure.Services;

/// <summary>
/// The Assets ledger operations shared by the Assets screens and the purchase order
/// "Receive" action, so both create assets and move stock the same way:
/// running numbers issued under a row lock, on hand summed from the ledger, the item
/// history, and posting a receipt. Every method runs inside the caller's transaction —
/// none of them opens or commits one.
/// </summary>
public sealed class AssetOperations(
    RegalisDbContext db,
    INumberSequenceService numberSequence,
    ICurrentUser currentUser,
    IDateTimeProvider clock)
{
    public const string ErrorCostMissing = "COST_MISSING";

    public DateOnly Today => IstClock.IstDateOf(clock.UtcNow);

    // ── Running numbers ─────────────────────────────────────────────────────

    /// <summary>
    /// Locks the prefix's MST_CodeSequence row (creating it the first time) and returns
    /// its last number. The lock is held until the caller's transaction ends, so a second
    /// admin posting the same prefix waits instead of being handed the same number.
    /// </summary>
    private async Task<int> LockSequenceAsync(string prefix, CancellationToken ct)
    {
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            IF NOT EXISTS (SELECT 1 FROM dbo.MST_CodeSequence WITH (UPDLOCK, HOLDLOCK) WHERE Prefix = {prefix})
                INSERT INTO dbo.MST_CodeSequence (Prefix, LastNumber) VALUES ({prefix}, 0);
            """, ct);
        var last = await db.Database.SqlQuery<int>($"""
            SELECT LastNumber AS Value FROM dbo.MST_CodeSequence WITH (UPDLOCK, HOLDLOCK) WHERE Prefix = {prefix}
            """).ToListAsync(ct);
        return last[0];
    }

    private Task SetSequenceAsync(string prefix, int last, CancellationToken ct) =>
        db.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE dbo.MST_CodeSequence SET LastNumber = {last} WHERE Prefix = {prefix} AND LastNumber < {last}", ct);

    /// <summary>IN-2026-0007 / OUT-2026-0042 / SC-2026-0003, counted per calendar year.</summary>
    public async Task<string> NextDocumentNumberAsync(string kind, DateOnly date, CancellationToken ct)
    {
        var prefix = $"{kind}-{date.Year.ToString(CultureInfo.InvariantCulture)}";
        var next = await LockSequenceAsync(prefix, ct) + 1;
        await SetSequenceAsync(prefix, next, ct);
        return AssetRules.DocumentNumber(prefix, next);
    }

    /// <summary>
    /// The next <paramref name="count"/> unused numbers for an asset prefix, issued one at
    /// a time and never reused. A number whose code already exists (an item typed in
    /// before the sequence knew the prefix) is skipped rather than handed out twice.
    /// </summary>
    public async Task<List<int>> AllocateCodeNumbersAsync(string prefix, int count, CancellationToken ct)
    {
        var last = await LockSequenceAsync(prefix, ct);
        var numbers = await FreeNumbersAsync(prefix, last, count, ct);
        await SetSequenceAsync(prefix, numbers[^1], ct);
        return numbers;
    }

    /// <summary>What <see cref="AllocateCodeNumbersAsync"/> would hand out now, without
    /// taking anything: the receipt form's "Will create … R-2W-E-EGC-015 to -022".
    /// <paramref name="alreadyPlanned"/> carries numbers earlier lines of the same form
    /// will take.</summary>
    public async Task<List<int>> PeekCodeNumbersAsync(string prefix, int count,
        IReadOnlyDictionary<string, int>? alreadyPlanned, CancellationToken ct)
    {
        var last = await db.CodeSequences.AsNoTracking().Where(s => s.Prefix == prefix)
            .Select(s => (int?)s.LastNumber).FirstOrDefaultAsync(ct) ?? 0;
        if (alreadyPlanned is not null && alreadyPlanned.TryGetValue(prefix, out var planned) && planned > last)
        {
            last = planned;
        }
        return await FreeNumbersAsync(prefix, last, count, ct);
    }

    private async Task<List<int>> FreeNumbersAsync(string prefix, int last, int count, CancellationToken ct)
    {
        // Codes on items, deleted or not (UX_InventoryItem_Code is unfiltered), plus the
        // numbers asset records already hold under this prefix.
        var start = prefix + "-";
        var taken = (await db.InventoryItems.IgnoreQueryFilters().AsNoTracking()
                .Where(i => i.ItemCode.StartsWith(start))
                .Select(i => i.ItemCode).ToListAsync(ct))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var takenNumbers = (await db.AssetDetails.AsNoTracking()
                .Where(d => d.CodePrefix == prefix && d.CodeNumber > last)
                .Select(d => d.CodeNumber).ToListAsync(ct))
            .ToHashSet();
        var numbers = new List<int>(count);
        var candidate = last;
        while (numbers.Count < count)
        {
            candidate++;
            if (takenNumbers.Contains(candidate) || taken.Contains(AssetRules.AssetCode(prefix, candidate))) continue;
            numbers.Add(candidate);
        }
        return numbers;
    }

    // ── Stock ───────────────────────────────────────────────────────────────

    /// <summary>On hand per item: the legacy opening quantity plus the signed sum of
    /// every ledger row. Computed on read; there is no column that holds it.</summary>
    public async Task<Dictionary<int, decimal>> OnHandAsync(IReadOnlyCollection<int> itemIds, CancellationToken ct)
    {
        if (itemIds.Count == 0) return [];
        var sums = await db.InventoryTransactions.AsNoTracking()
            .Where(t => itemIds.Contains(t.InventoryItemId))
            .GroupBy(t => t.InventoryItemId)
            .Select(g => new
            {
                ItemId = g.Key,
                Total = g.Sum(t => t.TransactionType == InventoryTransactionType.Out
                                   || t.TransactionType == InventoryTransactionType.Damage
                    ? -t.Quantity
                    : t.Quantity)
            })
            .ToDictionaryAsync(x => x.ItemId, x => x.Total, ct);
        var openings = await db.InventoryItems.IgnoreQueryFilters().AsNoTracking()
            .Where(i => itemIds.Contains(i.InventoryItemId))
            .Select(i => new { i.InventoryItemId, i.OpeningQuantity })
            .ToDictionaryAsync(x => x.InventoryItemId, x => x.OpeningQuantity, ct);
        return itemIds.Distinct().ToDictionary(id => id,
            id => openings.GetValueOrDefault(id) + sums.GetValueOrDefault(id));
    }

    /// <summary>Signed effect of a ledger row on stock.</summary>
    public static decimal Signed(InventoryTransactionType type, decimal quantity) =>
        type is InventoryTransactionType.Out or InventoryTransactionType.Damage ? -quantity : quantity;

    /// <summary>A ledger row numbered from the shared ITX- sequence.</summary>
    public async Task<InventoryTransaction> LedgerRowAsync(InventoryItem item, InventoryTransactionType type,
        decimal quantity, DateOnly date, StockSource source, long sourceId, string documentNumber,
        int? roomId, CancellationToken ct, GoodsIssueReason? reason = null) =>
        new()
        {
            InventoryItem = item,
            InventoryItemId = item.InventoryItemId,
            TransactionNumber = await numberSequence.NextAsync(SequenceKeys.InventoryTransaction, ct),
            TransactionType = type,
            Quantity = quantity,
            TransactionDate = date,
            ReferenceNumber = documentNumber,
            LocationId = item.LocationId,
            AssetLocationId = roomId,
            EnteredByUserId = currentUser.UserId,
            SourceType = source,
            SourceId = sourceId,
            IssueReason = reason,
            UnitCost = item.UnitCost
        };

    // ── History ─────────────────────────────────────────────────────────────

    public void AddEvent(int itemId, string eventType, DateOnly date, string summary,
        string? from = null, string? to = null, string? documentNumber = null, object? meta = null) =>
        db.ItemEvents.Add(new ItemEvent
        {
            InventoryItemId = itemId,
            EventType = eventType,
            EventDate = date,
            Summary = Clip(summary, 300)!,
            FromText = Clip(from, 150),
            ToText = Clip(to, 150),
            DocumentNumber = documentNumber,
            MetaJson = meta is null ? null : JsonSerializer.Serialize(meta),
            ActorUserId = currentUser.IsAuthenticated ? currentUser.UserId : null,
            CreatedUtc = clock.UtcNow
        });

    private static string? Clip(string? value, int max) =>
        value is null ? null : value.Length <= max ? value : value[..max];

    /// <summary>The branch (MST_Location) every item row needs. Assets and consumables
    /// carry their room separately.</summary>
    public async Task<int> DefaultBranchIdAsync(CancellationToken ct)
    {
        if (currentUser.LocationId is { } mine && await db.Locations.AnyAsync(l => l.LocationId == mine, ct))
        {
            return mine;
        }
        return await db.Locations.OrderByDescending(l => l.IsDefault).ThenBy(l => l.LocationId)
                   .Select(l => (int?)l.LocationId).FirstOrDefaultAsync(ct)
               ?? throw new BusinessRuleException("Add a location (Masters → Locations) before adding items.");
    }

    // ── Posting a receipt ───────────────────────────────────────────────────

    /// <summary>
    /// Applies a receipt (§6): consumable lines write one ledger row each; asset lines
    /// create one asset record per unit, each with the next code under its prefix, and a
    /// ledger row and a history row for each. <paramref name="updatePurchaseOrder"/>
    /// moves the PO lines' received quantities (the Incoming Goods path; the PO's own
    /// Receive action has already done it). The caller holds the transaction.
    /// </summary>
    public async Task PostReceiptAsync(GoodsReceipt receipt, bool updatePurchaseOrder, CancellationToken ct)
    {
        if (receipt.Status != GoodsReceiptStatus.Draft)
        {
            throw new BusinessRuleException("This receipt is already posted. A posted receipt is reversed, never posted again.");
        }

        var rooms = await db.AssetLocations.IgnoreQueryFilters().AsNoTracking()
            .ToDictionaryAsync(r => r.AssetLocationId, ct);
        var supplierName = receipt.SupplierId is null
            ? null
            : await db.Suppliers.IgnoreQueryFilters().Where(s => s.SupplierId == receipt.SupplierId)
                .Select(s => s.SupplierName).FirstOrDefaultAsync(ct);
        var from = receipt.IsOpeningBalance ? "Opening balance" : supplierName;
        var type = receipt.IsOpeningBalance ? InventoryTransactionType.OpeningBalance : InventoryTransactionType.In;
        var branchId = await DefaultBranchIdAsync(ct);
        var categories = await db.AssetCategories.IgnoreQueryFilters().AsNoTracking()
            .ToDictionaryAsync(c => c.AssetCategoryId, ct);

        var created = new List<(InventoryItem Item, GoodsReceiptLine Line, int RoomId)>();
        var ledger = new List<(InventoryTransaction Row, int? RoomId)>();

        foreach (var line in receipt.Lines.OrderBy(l => l.LineNumber))
        {
            if (line.Kind == ItemKind.Consumable)
            {
                var item = await db.InventoryItems.FirstOrDefaultAsync(i => i.InventoryItemId == line.InventoryItemId, ct)
                           ?? throw new NotFoundException("InventoryItem", line.InventoryItemId ?? 0);
                if (item.Kind != ItemKind.Consumable || item.ItemType == InventoryItemType.Service)
                {
                    throw new BusinessRuleException($"{item.ItemName} is not a consumable.");
                }
                // The first receipt with a rate gives an item with no cost its rate.
                if (item.UnitCost is null && line.Rate is not null) item.UnitCost = line.Rate;
                var roomId = receipt.IntoLocationId ?? item.KeptInLocationId;
                var row = await LedgerRowAsync(item, type, line.Quantity, receipt.ReceiptDate,
                    StockSource.GoodsReceipt, receipt.GoodsReceiptId, receipt.ReceiptNumber, roomId, ct);
                row.UnitCost = line.Rate ?? item.UnitCost;
                ledger.Add((row, roomId));
                AddEvent(item.InventoryItemId, "RECEIVED", receipt.ReceiptDate,
                    $"Received {line.Quantity.ToString("0.###", CultureInfo.InvariantCulture)} {item.Unit}",
                    receipt.ReceiptNumber, roomId is { } r && rooms.TryGetValue(r, out var room) ? room.LocationName : null,
                    receipt.ReceiptNumber);
            }
            else
            {
                if (line.Quantity != decimal.Truncate(line.Quantity))
                {
                    throw new BusinessRuleException("Assets are counted one by one: the quantity must be a whole number.");
                }
                var count = (int)line.Quantity;
                var prefix = AssetRules.NormalisePrefix(line.CodePrefix)
                             ?? throw new BusinessRuleException($"Line {line.LineNumber}: the code prefix is missing.");
                var roomId = line.AssetLocationId ?? receipt.IntoLocationId
                             ?? throw new BusinessRuleException($"Line {line.LineNumber}: choose the room it goes into.");
                if (!rooms.TryGetValue(roomId, out var room)) throw new NotFoundException("AssetLocation", roomId);
                var category = line.AssetCategoryId is { } cid && categories.TryGetValue(cid, out var c) ? c : null;

                var numbers = await AllocateCodeNumbersAsync(prefix, count, ct);
                foreach (var number in numbers)
                {
                    var item = new InventoryItem
                    {
                        ItemCode = AssetRules.AssetCode(prefix, number),
                        ItemName = line.ItemName!.Trim(),
                        Kind = ItemKind.Asset,
                        ItemType = InventoryItemType.Goods,
                        AssetCategoryId = category?.AssetCategoryId,
                        Category = category?.CategoryName,
                        Unit = "Nos",
                        UnitCost = line.Rate,
                        BrandModel = line.BrandModel,
                        Description = line.Description,
                        SupplierId = receipt.SupplierId,
                        VendorName = line.VendorName ?? supplierName,
                        Remarks = line.Remarks,
                        LocationId = branchId,
                        IsNonTaxable = true,
                        AssetDetail = new AssetDetail
                        {
                            CodePrefix = prefix,
                            CodeNumber = number,
                            SerialNumber = count == 1 ? line.SerialNumber : null,
                            AssetLocationId = roomId,
                            Condition = line.Condition ?? AssetCondition.Good,
                            Status = AssetRules.StatusForRoom(room.IsStore),
                            PurchaseDate = line.PurchaseDate,
                            WarrantyUntil = line.WarrantyUntil,
                            GoodsReceiptLineId = line.GoodsReceiptLineId
                        }
                    };
                    db.InventoryItems.Add(item);
                    created.Add((item, line, roomId));
                }
                line.FirstCode = AssetRules.AssetCode(prefix, numbers[0]);
                line.LastCode = AssetRules.AssetCode(prefix, numbers[^1]);
            }

            if (updatePurchaseOrder && line.PurchaseOrderLineId is { } poLineId)
            {
                var poLine = await db.PurchaseOrderLines.FirstOrDefaultAsync(l => l.PurchaseOrderLineId == poLineId, ct)
                             ?? throw new NotFoundException("PurchaseOrderLine", poLineId);
                var pending = poLine.Quantity - poLine.ReceivedQuantity;
                if (line.Quantity > pending)
                {
                    throw new BusinessRuleException(
                        $"{poLine.ItemName}: only {pending.ToString("0.###", CultureInfo.InvariantCulture)} pending against this order.");
                }
                poLine.ReceivedQuantity += line.Quantity;
            }
        }

        // New assets need their keys before their ledger and history rows.
        if (created.Count > 0) await db.SaveChangesAsync(ct);
        foreach (var (item, _, roomId) in created)
        {
            var row = await LedgerRowAsync(item, type, 1m, receipt.ReceiptDate,
                StockSource.GoodsReceipt, receipt.GoodsReceiptId, receipt.ReceiptNumber, roomId, ct);
            ledger.Add((row, roomId));
            AddEvent(item.InventoryItemId, "RECEIVED", receipt.ReceiptDate,
                receipt.IsOpeningBalance ? "Opening balance" : "Received",
                receipt.ReceiptNumber, rooms[roomId].LocationName, receipt.ReceiptNumber);
        }
        foreach (var (row, _) in ledger)
        {
            row.SourceOrDestination = from;
            row.SupplierId = receipt.SupplierId;
            row.PurchaseOrderId = receipt.PurchaseOrderId;
            row.PurchaseInvoiceNumber = receipt.InvoiceNumber;
            row.Notes = receipt.Notes;
            db.InventoryTransactions.Add(row);
        }

        if (updatePurchaseOrder && receipt.PurchaseOrderId is { } poId)
        {
            await RefreshPurchaseOrderStatusAsync(poId, $"Received on {receipt.ReceiptNumber}", ct);
        }

        receipt.TotalAmount = receipt.Lines.Sum(l => l.Amount);
        receipt.Status = receipt.Source == GoodsReceiptSource.Import ? GoodsReceiptStatus.Imported : GoodsReceiptStatus.Posted;
        receipt.PostedByUserId = currentUser.UserId;
        receipt.PostedUtc = clock.UtcNow;
        await db.SaveChangesAsync(ct);
    }

    /// <summary>Sets a purchase order to Received / Partially received / Sent from its
    /// lines, with a status history row when it changes.</summary>
    public async Task RefreshPurchaseOrderStatusAsync(long purchaseOrderId, string action, CancellationToken ct)
    {
        var order = await db.PurchaseOrders.Include(p => p.Lines)
            .FirstOrDefaultAsync(p => p.PurchaseOrderId == purchaseOrderId, ct)
            ?? throw new NotFoundException("PurchaseOrder", purchaseOrderId);
        var status = order.Lines.All(l => l.ReceivedQuantity >= l.Quantity)
            ? PurchaseOrderStatus.Received
            : order.Lines.Any(l => l.ReceivedQuantity > 0)
                ? PurchaseOrderStatus.PartiallyReceived
                : PurchaseOrderStatus.Sent;
        if (status == order.Status) return;
        db.PurchaseOrderStatusHistory.Add(new PurchaseOrderStatusHistory
        {
            PurchaseOrderId = order.PurchaseOrderId,
            PurchaseOrder = order,
            FromStatus = order.Status,
            ToStatus = status,
            Action = action,
            ChangedByUserId = currentUser.UserId,
            ChangedUtc = clock.UtcNow
        });
        order.Status = status;
    }

    /// <summary>A purchase order goods can be received against.</summary>
    public static bool IsReceivable(PurchaseOrderStatus status) =>
        status is PurchaseOrderStatus.Sent or PurchaseOrderStatus.PartiallyReceived;

    public void RequirePermission(string permission, string message)
    {
        if (!currentUser.HasPermission(permission)) throw new ForbiddenException(message);
    }

    public long UserId => currentUser.UserId;
    public DateTime UtcNow => clock.UtcNow;
}
