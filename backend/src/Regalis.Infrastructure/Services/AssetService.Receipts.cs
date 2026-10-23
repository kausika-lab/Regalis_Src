using Microsoft.EntityFrameworkCore;
using Regalis.Application.Common.Exceptions;
using Regalis.Application.Operations;
using Regalis.Domain.Entities.Operations;
using Regalis.Domain.Enums;
using Regalis.Infrastructure.Excel;
using Regalis.Shared.Api;
using Regalis.Shared.Constants;

namespace Regalis.Infrastructure.Services;

/// <summary>Incoming Goods (§6): the only way stock goes up and the only way an asset
/// record is born.</summary>
public partial class AssetService
{
    // ── List and read ───────────────────────────────────────────────────────

    private IQueryable<GoodsReceipt> FilterReceipts(GoodsReceiptListRequest request)
    {
        var query = db.GoodsReceipts.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var term = request.Search.Trim();
            query = query.Where(r => r.ReceiptNumber.Contains(term)
                                     || (r.InvoiceNumber != null && r.InvoiceNumber.Contains(term))
                                     || db.Suppliers.Any(s => s.SupplierId == r.SupplierId && s.SupplierName.Contains(term))
                                     || r.Lines.Any(l => (l.ItemName != null && l.ItemName.Contains(term))
                                                         || (l.FirstCode != null && l.FirstCode.Contains(term))
                                                         || db.InventoryItems.Any(i => i.InventoryItemId == l.InventoryItemId
                                                             && (i.ItemName.Contains(term) || i.ItemCode.Contains(term)))));
        }
        if (request.SupplierId is { } supplierId) query = query.Where(r => r.SupplierId == supplierId);
        if (request.Status is { } status) query = query.Where(r => (byte)r.Status == status);
        if (request.From is { } from) query = query.Where(r => r.ReceiptDate >= from);
        if (request.To is { } to) query = query.Where(r => r.ReceiptDate <= to);
        return query;
    }

    private IQueryable<GoodsReceiptRowDto> ShapeReceipts(IQueryable<GoodsReceipt> query) =>
        query.Select(r => new GoodsReceiptRowDto(
            r.GoodsReceiptId, r.ReceiptNumber, r.ReceiptDate, (byte)r.Status, (byte)r.Source, r.IsOpeningBalance,
            r.SupplierId,
            db.Suppliers.IgnoreQueryFilters().Where(s => s.SupplierId == r.SupplierId).Select(s => s.SupplierName).FirstOrDefault(),
            r.InvoiceNumber,
            db.PurchaseOrders.Where(p => p.PurchaseOrderId == r.PurchaseOrderId).Select(p => p.PoNumber).FirstOrDefault(),
            r.Lines.Count,
            (int)r.Lines.Where(l => l.Kind == ItemKind.Asset).Sum(l => l.Quantity),
            r.Lines.Count(l => l.Kind == ItemKind.Consumable),
            r.TotalAmount,
            r.ReversesReceiptId != null,
            r.ReversedByReceiptId != null,
            r.ReversedByReceiptId != null
                ? db.GoodsReceipts.Where(x => x.GoodsReceiptId == r.ReversedByReceiptId).Select(x => x.ReceiptNumber).FirstOrDefault()
                : db.GoodsReceipts.Where(x => x.GoodsReceiptId == r.ReversesReceiptId).Select(x => x.ReceiptNumber).FirstOrDefault()));

    public async Task<PagedResult<GoodsReceiptRowDto>> GetReceiptsAsync(GoodsReceiptListRequest request,
        CancellationToken ct = default)
    {
        var query = FilterReceipts(request);
        var total = await query.CountAsync(ct);
        var rows = await ShapeReceipts(query.OrderByDescending(r => r.ReceiptDate).ThenByDescending(r => r.GoodsReceiptId)
                .Skip((request.Page - 1) * request.PageSize).Take(request.PageSize))
            .ToListAsync(ct);
        return PagedResult<GoodsReceiptRowDto>.Create(rows, request.Page, request.PageSize, total);
    }

    public async Task<GoodsReceiptDto> GetReceiptAsync(long receiptId, CancellationToken ct = default)
    {
        var r = await db.GoodsReceipts.AsNoTracking().Include(x => x.Lines)
            .FirstOrDefaultAsync(x => x.GoodsReceiptId == receiptId, ct)
            ?? throw new NotFoundException("GoodsReceipt", receiptId);
        var rooms = await RoomsAsync(ct);
        var itemIds = r.Lines.Where(l => l.InventoryItemId != null).Select(l => l.InventoryItemId!.Value).ToList();
        var items = await db.InventoryItems.IgnoreQueryFilters().AsNoTracking()
            .Where(i => itemIds.Contains(i.InventoryItemId))
            .ToDictionaryAsync(i => i.InventoryItemId, ct);
        string? Number(long? id) => id is null ? null
            : db.GoodsReceipts.Where(x => x.GoodsReceiptId == id).Select(x => x.ReceiptNumber).FirstOrDefault();
        var supplier = r.SupplierId is null ? null
            : await db.Suppliers.IgnoreQueryFilters().Where(s => s.SupplierId == r.SupplierId)
                .Select(s => s.SupplierName).FirstOrDefaultAsync(ct);
        var po = r.PurchaseOrderId is null ? null
            : await db.PurchaseOrders.Where(p => p.PurchaseOrderId == r.PurchaseOrderId)
                .Select(p => p.PoNumber).FirstOrDefaultAsync(ct);
        var postedBy = r.PostedByUserId is null ? null
            : await db.Users.IgnoreQueryFilters().Where(u => u.UserId == r.PostedByUserId)
                .Select(u => u.FullName).FirstOrDefaultAsync(ct);
        var lines = r.Lines.OrderBy(l => l.LineNumber).Select(l =>
        {
            var item = l.InventoryItemId is { } id && items.TryGetValue(id, out var it) ? it : null;
            var roomId = l.AssetLocationId ?? (l.Kind == ItemKind.Asset ? r.IntoLocationId : null);
            return new GoodsReceiptLineDto(l.GoodsReceiptLineId, l.LineNumber, (byte)l.Kind, l.InventoryItemId,
                l.Kind == ItemKind.Consumable ? item?.ItemCode : l.FirstCode,
                l.Kind == ItemKind.Asset ? l.ItemName ?? item?.ItemName ?? "" : item?.ItemName ?? "",
                l.Kind == ItemKind.Asset ? "Nos" : item?.Unit ?? "",
                l.Quantity, l.Rate, l.Amount, l.PurchaseOrderLineId, l.AssetCategoryId, l.CodePrefix, l.BrandModel,
                l.Description, l.SerialNumber, roomId,
                roomId is { } rid && rooms.TryGetValue(rid, out var room) ? room.LocationName : null,
                l.Condition is null ? null : (byte)l.Condition, l.PurchaseDate, l.WarrantyUntil, l.VendorName,
                l.Remarks, l.FirstCode, l.LastCode);
        }).ToList();
        return new GoodsReceiptDto(r.GoodsReceiptId, r.ReceiptNumber, r.ReceiptDate, (byte)r.Status, (byte)r.Source,
            r.IsOpeningBalance, r.SupplierId, supplier, r.InvoiceNumber, r.PurchaseOrderId, po, r.IntoLocationId,
            r.IntoLocationId is { } into && rooms.TryGetValue(into, out var intoRoom) ? intoRoom.LocationName : null,
            r.TotalAmount, r.Notes, postedBy, r.PostedUtc, r.ReversesReceiptId, Number(r.ReversesReceiptId),
            r.ReversedByReceiptId, Number(r.ReversedByReceiptId), lines);
    }

    // ── Save, post, delete ──────────────────────────────────────────────────

    public Task<GoodsReceiptDto> CreateReceiptAsync(SaveGoodsReceiptRequest request, CancellationToken ct = default) =>
        SaveReceiptAsync(null, request, GoodsReceiptSource.Screen, ct);

    public Task<GoodsReceiptDto> UpdateReceiptAsync(long receiptId, SaveGoodsReceiptRequest request,
        CancellationToken ct = default) =>
        SaveReceiptAsync(receiptId, request, GoodsReceiptSource.Screen, ct);

    private async Task<GoodsReceiptDto> SaveReceiptAsync(long? receiptId, SaveGoodsReceiptRequest request,
        GoodsReceiptSource source, CancellationToken ct)
    {
        var id = await InTransactionAsync(() => SaveReceiptCoreAsync(receiptId, request, source, ct), ct);
        return await GetReceiptAsync(id, ct);
    }

    /// <summary>Saves (and with <c>Post</c>, posts) a receipt inside the caller's transaction.</summary>
    private async Task<long> SaveReceiptCoreAsync(long? receiptId, SaveGoodsReceiptRequest request,
        GoodsReceiptSource source, CancellationToken ct)
    {
        {
            GoodsReceipt receipt;
            if (receiptId is null)
            {
                receipt = new GoodsReceipt
                {
                    Source = source,
                    ReceiptNumber = await ops.NextDocumentNumberAsync("IN", request.ReceiptDate, ct)
                };
                db.GoodsReceipts.Add(receipt);
            }
            else
            {
                receipt = await db.GoodsReceipts.FirstOrDefaultAsync(r => r.GoodsReceiptId == receiptId, ct)
                          ?? throw new NotFoundException("GoodsReceipt", receiptId);
                if (receipt.Status != GoodsReceiptStatus.Draft)
                {
                    throw new BusinessRuleException("A posted receipt can never be edited — only reversed.");
                }
                var old = await db.GoodsReceiptLines.Where(l => l.GoodsReceiptId == receipt.GoodsReceiptId).ToListAsync(ct);
                db.GoodsReceiptLines.RemoveRange(old);
                await db.SaveChangesAsync(ct);
                foreach (var line in old) db.Entry(line).State = EntityState.Detached;
                receipt.Lines.Clear();
            }

            receipt.ReceiptDate = request.ReceiptDate;
            receipt.IsOpeningBalance = request.IsOpeningBalance;
            receipt.SupplierId = request.IsOpeningBalance ? null : request.SupplierId;
            receipt.InvoiceNumber = Trim(request.InvoiceNumber);
            receipt.PurchaseOrderId = request.IsOpeningBalance ? null : request.PurchaseOrderId;
            receipt.IntoLocationId = request.IntoLocationId;
            receipt.Notes = Trim(request.Notes);
            foreach (var line in await BuildReceiptLinesAsync(request, source, ct))
            {
                receipt.Lines.Add(line);
            }
            receipt.TotalAmount = receipt.Lines.Sum(l => l.Amount);
            await db.SaveChangesAsync(ct);

            if (request.Post)
            {
                await ValidateReceiptForPostAsync(receipt, request.ConfirmMissingCost, ct);
                await ops.PostReceiptAsync(receipt, updatePurchaseOrder: true, ct);
            }
            return receipt.GoodsReceiptId;
        }
    }

    /// <summary>Turns the form's lines into receipt lines. An asset line that names an
    /// existing asset ("more of these") takes its name, category, prefix, brand and vendor
    /// wherever the form left them blank.</summary>
    private async Task<List<GoodsReceiptLine>> BuildReceiptLinesAsync(SaveGoodsReceiptRequest request,
        GoodsReceiptSource source, CancellationToken ct)
    {
        var ids = request.Lines.Where(l => l.InventoryItemId != null).Select(l => l.InventoryItemId!.Value).Distinct().ToList();
        var items = await db.InventoryItems.AsNoTracking().Include(i => i.AssetDetail)
            .Where(i => ids.Contains(i.InventoryItemId)).ToDictionaryAsync(i => i.InventoryItemId, ct);
        var categories = await db.AssetCategories.AsNoTracking().ToDictionaryAsync(c => c.AssetCategoryId, ct);
        var lines = new List<GoodsReceiptLine>();
        var number = 0;
        foreach (var l in request.Lines)
        {
            number++;
            var kind = (ItemKind)l.Kind;
            InventoryItem? item = null;
            if (l.InventoryItemId is { } itemId && !items.TryGetValue(itemId, out item))
            {
                throw new NotFoundException("InventoryItem", itemId);
            }
            if (kind == ItemKind.Consumable)
            {
                if (item is null)
                {
                    throw new AppValidationException([Field($"Line {number}: choose the item.", "lines")]);
                }
                var rate = l.Rate ?? item.UnitCost;
                lines.Add(new GoodsReceiptLine
                {
                    LineNumber = number,
                    Kind = kind,
                    InventoryItemId = item.InventoryItemId,
                    Quantity = l.Quantity,
                    Rate = rate,
                    Amount = rate is null ? 0m : Math.Round(l.Quantity * rate.Value, 2, MidpointRounding.AwayFromZero),
                    PurchaseOrderLineId = l.PurchaseOrderLineId
                });
                continue;
            }

            var template = item is { Kind: ItemKind.Asset } ? item : null;
            if (item is not null && template is null)
            {
                throw new BusinessRuleException($"Line {number}: {item.ItemName} is a consumable, not an asset.");
            }
            var categoryId = l.AssetCategoryId ?? template?.AssetCategoryId;
            var category = categoryId is { } cid && categories.TryGetValue(cid, out var c) ? c : null;
            lines.Add(new GoodsReceiptLine
            {
                LineNumber = number,
                Kind = ItemKind.Asset,
                InventoryItemId = template?.InventoryItemId,
                Quantity = l.Quantity,
                Rate = l.Rate,
                Amount = l.Rate is null ? 0m : Math.Round(l.Quantity * l.Rate.Value, 2, MidpointRounding.AwayFromZero),
                PurchaseOrderLineId = l.PurchaseOrderLineId,
                ItemName = Trim(l.ItemName) ?? template?.ItemName,
                AssetCategoryId = categoryId,
                CodePrefix = AssetRules.NormalisePrefix(l.CodePrefix) ?? template?.AssetDetail?.CodePrefix ?? category?.CodePrefix,
                BrandModel = Trim(l.BrandModel) ?? template?.BrandModel,
                Description = Trim(l.Description) ?? template?.Description,
                SerialNumber = l.Quantity == 1 ? Trim(l.SerialNumber) : null,
                AssetLocationId = l.LocationId,
                Condition = l.Condition is null ? null : (AssetCondition)l.Condition,
                // Bought when it arrived, unless the form says otherwise. The import keeps a
                // blank date blank: the team's sheet only knows 7 of 43.
                PurchaseDate = l.PurchaseDate ?? (source == GoodsReceiptSource.Import ? null : request.ReceiptDate),
                WarrantyUntil = l.WarrantyUntil,
                VendorName = Trim(l.VendorName) ?? template?.VendorName,
                Remarks = Trim(l.Remarks)
            });
        }
        return lines;
    }

    /// <summary>Everything posting needs, checked before anything moves.</summary>
    private async Task ValidateReceiptForPostAsync(GoodsReceipt receipt, bool confirmMissingCost, CancellationToken ct)
    {
        var errors = new List<ApiError>();
        if (receipt.ReceiptDate > Today) errors.Add(Field("Date cannot be in the future.", "receiptDate"));
        if (!receipt.IsOpeningBalance && receipt.SupplierId is null)
            errors.Add(Field("Choose the supplier, or Opening balance.", "supplierId"));
        if (receipt.Source == GoodsReceiptSource.Screen && receipt.IntoLocationId is null)
            errors.Add(Field("Choose where it went.", "intoLocationId"));
        var lines = receipt.Lines.Where(l => !l.IsDeleted).ToList();
        if (lines.Count == 0) errors.Add(Field("Add at least one line: what arrived.", "lines"));

        var rooms = await RoomsAsync(ct);
        var categories = await db.AssetCategories.AsNoTracking().ToDictionaryAsync(c => c.AssetCategoryId, ct);
        var itemIds = lines.Where(l => l.InventoryItemId != null).Select(l => l.InventoryItemId!.Value).ToList();
        var items = await db.InventoryItems.AsNoTracking().Where(i => itemIds.Contains(i.InventoryItemId))
            .ToDictionaryAsync(i => i.InventoryItemId, ct);
        if (receipt.IntoLocationId is { } into && (!rooms.TryGetValue(into, out var intoRoom) || !intoRoom.IsActive))
            errors.Add(Field("That room is not available.", "intoLocationId"));
        if (receipt.SupplierId is { } sid && !await db.Suppliers.AnyAsync(s => s.SupplierId == sid, ct))
            errors.Add(Field("That supplier is not available.", "supplierId"));

        if (receipt.PurchaseOrderId is { } poId)
        {
            var po = await db.PurchaseOrders.AsNoTracking().Include(p => p.Lines)
                .FirstOrDefaultAsync(p => p.PurchaseOrderId == poId, ct);
            if (po is null) errors.Add(Field("That purchase order does not exist.", "purchaseOrderId"));
            else
            {
                if (!AssetOperations.IsReceivable(po.Status))
                    errors.Add(Field($"{po.PoNumber} is not open for receiving — send it to the supplier first.", "purchaseOrderId"));
                if (po.SupplierId != receipt.SupplierId)
                    errors.Add(Field($"{po.PoNumber} is for a different supplier.", "purchaseOrderId"));
                foreach (var line in lines.Where(l => l.PurchaseOrderLineId != null))
                {
                    if (po.Lines.All(pl => pl.PurchaseOrderLineId != line.PurchaseOrderLineId))
                        errors.Add(Field($"Line {line.LineNumber} is not on {po.PoNumber}.", "lines"));
                }
            }
        }
        else if (lines.Any(l => l.PurchaseOrderLineId != null))
        {
            errors.Add(Field("Choose the purchase order these lines belong to.", "purchaseOrderId"));
        }

        foreach (var line in lines)
        {
            var at = $"Line {line.LineNumber}: ";
            if (line.Kind == ItemKind.Consumable)
            {
                if (line.InventoryItemId is not { } id || !items.TryGetValue(id, out var item))
                    errors.Add(Field(at + "choose the item.", "lines"));
                else if (item.Kind != ItemKind.Consumable || item.ItemType != InventoryItemType.Goods)
                    errors.Add(Field(at + item.ItemName + " is not a consumable.", "lines"));
                else if (!item.IsActive)
                    errors.Add(Field(at + item.ItemName + " is inactive.", "lines"));
                continue;
            }
            if (line.Quantity != decimal.Truncate(line.Quantity) || line.Quantity > 500)
                errors.Add(Field(at + "assets are counted one by one — a whole number up to 500.", "lines"));
            if (string.IsNullOrWhiteSpace(line.ItemName)) errors.Add(Field(at + "the item name is missing.", "lines"));
            if (line.AssetCategoryId is not { } cid || !categories.ContainsKey(cid))
                errors.Add(Field(at + "choose a category.", "lines"));
            if (line.CodePrefix is null || !AssetRules.IsValidPrefix(line.CodePrefix))
                errors.Add(Field(at + "the code prefix is missing or has characters a code cannot hold.", "lines"));
            var roomId = line.AssetLocationId ?? receipt.IntoLocationId;
            if (roomId is null) errors.Add(Field(at + "choose the room it goes into.", "lines"));
            else if (!rooms.TryGetValue(roomId.Value, out var room) || !room.IsActive)
                errors.Add(Field(at + "that room is not available.", "lines"));
            if (line.PurchaseDate is { } bought && bought > receipt.ReceiptDate)
                errors.Add(Field(at + "it cannot be bought after it arrived.", "lines"));
        }
        if (errors.Count > 0) throw new AppValidationException(errors);

        var withoutCost = lines.Where(l => l.Kind == ItemKind.Asset && l.Rate is null).Sum(l => (int)l.Quantity);
        if (withoutCost > 0 && !confirmMissingCost)
        {
            var all = lines.Where(l => l.Kind == ItemKind.Asset).Sum(l => (int)l.Quantity);
            throw new BusinessRuleException(MissingCostWarning(withoutCost, all), AssetOperations.ErrorCostMissing);
        }
    }

    private static string MissingCostWarning(int withoutCost, int all) =>
        $"{withoutCost} of these {all} assets have no purchase cost. They will show a dash for book value. Post anyway?";

    public async Task<GoodsReceiptDto> PostReceiptAsync(long receiptId, bool confirmMissingCost,
        CancellationToken ct = default)
    {
        await InTransactionAsync(async () =>
        {
            var receipt = await db.GoodsReceipts.Include(r => r.Lines)
                .FirstOrDefaultAsync(r => r.GoodsReceiptId == receiptId, ct)
                ?? throw new NotFoundException("GoodsReceipt", receiptId);
            await ValidateReceiptForPostAsync(receipt, confirmMissingCost, ct);
            await ops.PostReceiptAsync(receipt, updatePurchaseOrder: true, ct);
            return true;
        }, ct);
        return await GetReceiptAsync(receiptId, ct);
    }

    public async Task DeleteDraftReceiptAsync(long receiptId, CancellationToken ct = default)
    {
        var receipt = await db.GoodsReceipts.Include(r => r.Lines)
            .FirstOrDefaultAsync(r => r.GoodsReceiptId == receiptId, ct)
            ?? throw new NotFoundException("GoodsReceipt", receiptId);
        if (receipt.Status != GoodsReceiptStatus.Draft)
        {
            throw new BusinessRuleException("Only a draft can be deleted. A posted receipt is reversed.");
        }
        db.GoodsReceiptLines.RemoveRange(receipt.Lines);
        db.GoodsReceipts.Remove(receipt);
        await db.SaveChangesAsync(ct);
    }

    // ── Reverse ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Writes the opposite receipt and keeps both (§6.1). Consumables go back out — never
    /// below zero. Assets it created are taken out of the register and made inactive, but
    /// only while they are still exactly as they arrived; their codes stay retired.
    /// </summary>
    public async Task<GoodsReceiptDto> ReverseReceiptAsync(long receiptId, string reason, CancellationToken ct = default)
    {
        var reversalId = await InTransactionAsync(async () =>
        {
            var receipt = await db.GoodsReceipts.Include(r => r.Lines)
                .FirstOrDefaultAsync(r => r.GoodsReceiptId == receiptId, ct)
                ?? throw new NotFoundException("GoodsReceipt", receiptId);
            if (receipt.Status == GoodsReceiptStatus.Draft)
                throw new BusinessRuleException("A draft changed nothing — edit or delete it instead.");
            if (receipt.ReversedByReceiptId is not null)
                throw new BusinessRuleException("This receipt is already reversed.");
            if (receipt.Source == GoodsReceiptSource.Reversal)
                throw new BusinessRuleException("A reversal cannot itself be reversed. Record the goods again instead.");

            var today = Today;
            var note = $"Reverses {receipt.ReceiptNumber}: {reason.Trim()}";
            var reversal = new GoodsReceipt
            {
                ReceiptNumber = await ops.NextDocumentNumberAsync("IN", today, ct),
                ReceiptDate = today,
                Status = GoodsReceiptStatus.Posted,
                Source = GoodsReceiptSource.Reversal,
                IsOpeningBalance = receipt.IsOpeningBalance,
                SupplierId = receipt.SupplierId,
                InvoiceNumber = receipt.InvoiceNumber,
                PurchaseOrderId = receipt.PurchaseOrderId,
                IntoLocationId = receipt.IntoLocationId,
                Notes = note.Length > 500 ? note[..500] : note,
                ReversesReceiptId = receipt.GoodsReceiptId,
                TotalAmount = -receipt.TotalAmount,
                PostedByUserId = currentUser.UserId,
                PostedUtc = clock.UtcNow
            };
            db.GoodsReceipts.Add(reversal);
            await db.SaveChangesAsync(ct);

            var lines = receipt.Lines.OrderBy(l => l.LineNumber).ToList();
            var consumableIds = lines.Where(l => l.Kind == ItemKind.Consumable).Select(l => l.InventoryItemId!.Value).ToList();
            var onHand = await ops.OnHandAsync(consumableIds, ct);
            var now = clock.UtcNow;
            foreach (var line in lines)
            {
                reversal.Lines.Add(new GoodsReceiptLine
                {
                    LineNumber = line.LineNumber, Kind = line.Kind, InventoryItemId = line.InventoryItemId,
                    Quantity = line.Quantity, Rate = line.Rate, Amount = -line.Amount,
                    PurchaseOrderLineId = line.PurchaseOrderLineId, ItemName = line.ItemName,
                    AssetCategoryId = line.AssetCategoryId, CodePrefix = line.CodePrefix, BrandModel = line.BrandModel,
                    AssetLocationId = line.AssetLocationId, FirstCode = line.FirstCode, LastCode = line.LastCode
                });

                if (line.Kind == ItemKind.Consumable)
                {
                    var item = await db.InventoryItems.FirstAsync(i => i.InventoryItemId == line.InventoryItemId, ct);
                    var left = onHand[item.InventoryItemId] - line.Quantity;
                    if (left < 0)
                    {
                        throw new BusinessRuleException(
                            $"Only {Qty(onHand[item.InventoryItemId])} {item.Unit} of {item.ItemName} on hand — " +
                            "reversing would take it below zero. Record a stock count if the figure is wrong.");
                    }
                    onHand[item.InventoryItemId] = left;
                    var row = await ops.LedgerRowAsync(item, InventoryTransactionType.Out, line.Quantity, today,
                        StockSource.GoodsReceipt, reversal.GoodsReceiptId, reversal.ReceiptNumber,
                        receipt.IntoLocationId, ct);
                    row.SourceOrDestination = "Receipt reversed";
                    row.Notes = reversal.Notes;
                    db.InventoryTransactions.Add(row);
                    ops.AddEvent(item.InventoryItemId, "REVERSED", today,
                        $"Receipt {receipt.ReceiptNumber} reversed: {Qty(line.Quantity)} {item.Unit} out",
                        receipt.ReceiptNumber, null, reversal.ReceiptNumber);
                }
                else
                {
                    var assets = await db.AssetDetails.Include(d => d.InventoryItem)
                        .Where(d => d.GoodsReceiptLineId == line.GoodsReceiptLineId).ToListAsync(ct);
                    foreach (var d in assets)
                    {
                        var moved = await db.GoodsIssueLines.AnyAsync(g => g.InventoryItemId == d.InventoryItemId, ct);
                        if (moved || d.Status is not (AssetStatus.InUse or AssetStatus.InStore) || !d.InventoryItem.IsActive)
                        {
                            throw new BusinessRuleException(
                                $"{d.InventoryItem.ItemCode} has moved or changed status since it was received. " +
                                "Reverse that movement first.");
                        }
                        d.InventoryItem.IsActive = false;
                        d.RetiredReason = $"Receipt {receipt.ReceiptNumber} reversed: {reason.Trim()}";
                        if (d.RetiredReason.Length > 300) d.RetiredReason = d.RetiredReason[..300];
                        d.RetiredUtc = now;
                        var row = await ops.LedgerRowAsync(d.InventoryItem, InventoryTransactionType.Out, 1m, today,
                            StockSource.GoodsReceipt, reversal.GoodsReceiptId, reversal.ReceiptNumber, d.AssetLocationId, ct);
                        row.SourceOrDestination = "Receipt reversed";
                        row.Notes = reversal.Notes;
                        db.InventoryTransactions.Add(row);
                        ops.AddEvent(d.InventoryItemId, "REVERSED", today,
                            $"Receipt {receipt.ReceiptNumber} reversed — taken out of the register",
                            receipt.ReceiptNumber, null, reversal.ReceiptNumber);
                    }
                }

                if (line.PurchaseOrderLineId is { } poLineId)
                {
                    var poLine = await db.PurchaseOrderLines.FirstAsync(l => l.PurchaseOrderLineId == poLineId, ct);
                    poLine.ReceivedQuantity = Math.Max(0, poLine.ReceivedQuantity - line.Quantity);
                }
            }
            receipt.ReversedByReceiptId = reversal.GoodsReceiptId;
            if (receipt.PurchaseOrderId is { } poId)
            {
                await ops.RefreshPurchaseOrderStatusAsync(poId, $"Receipt {receipt.ReceiptNumber} reversed", ct);
            }
            await db.SaveChangesAsync(ct);
            return reversal.GoodsReceiptId;
        }, ct);
        return await GetReceiptAsync(reversalId, ct);
    }

    // ── Preview ─────────────────────────────────────────────────────────────

    /// <summary>"Under every line the form states, in plain words, exactly what posting
    /// will cause" (§6.3): the actual codes an asset line will take, and the before and
    /// after stock of a consumable line. Nothing is saved or reserved.</summary>
    public async Task<GoodsReceiptPreviewDto> PreviewReceiptAsync(SaveGoodsReceiptRequest request,
        CancellationToken ct = default)
    {
        var result = new List<GoodsReceiptLinePreviewDto>();
        var ids = request.Lines.Where(l => l.InventoryItemId != null).Select(l => l.InventoryItemId!.Value).Distinct().ToList();
        var items = await db.InventoryItems.AsNoTracking().Include(i => i.AssetDetail)
            .Where(i => ids.Contains(i.InventoryItemId)).ToDictionaryAsync(i => i.InventoryItemId, ct);
        var categories = await db.AssetCategories.AsNoTracking().ToDictionaryAsync(c => c.AssetCategoryId, ct);
        var onHand = await ops.OnHandAsync(items.Values.Where(i => i.Kind == ItemKind.Consumable)
            .Select(i => i.InventoryItemId).ToList(), ct);
        var planned = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        int assets = 0, withoutCost = 0;
        decimal total = 0;
        for (var index = 0; index < request.Lines.Count; index++)
        {
            var l = request.Lines[index];
            var amount = l.Rate is null ? 0m : Math.Round(l.Quantity * l.Rate.Value, 2, MidpointRounding.AwayFromZero);
            var item = l.InventoryItemId is { } id && items.TryGetValue(id, out var it) ? it : null;
            if (l.Kind == (byte)ItemKind.Consumable)
            {
                if (item is null || item.Kind != ItemKind.Consumable || item.ItemType != InventoryItemType.Goods)
                {
                    result.Add(new GoodsReceiptLinePreviewDto(index, l.Kind, "Choose the item.", [], null, null, null, false,
                        "Choose a consumable."));
                    continue;
                }
                var rate = l.Rate ?? item.UnitCost;
                amount = rate is null ? 0m : Math.Round(l.Quantity * rate.Value, 2, MidpointRounding.AwayFromZero);
                total += amount;
                var before = onHand.GetValueOrDefault(item.InventoryItemId);
                var after = before + (l.Quantity > 0 ? l.Quantity : 0);
                onHand[item.InventoryItemId] = after;
                result.Add(new GoodsReceiptLinePreviewDto(index, l.Kind,
                    $"Stock goes from {Qty(before)} {item.Unit} to {Qty(after)} {item.Unit}. No labels.",
                    [], before, after, item.Unit, false, null));
                continue;
            }

            total += amount;
            var template = item is { Kind: ItemKind.Asset } ? item : null;
            var categoryId = l.AssetCategoryId ?? template?.AssetCategoryId;
            var prefix = AssetRules.NormalisePrefix(l.CodePrefix) ?? template?.AssetDetail?.CodePrefix
                         ?? (categoryId is { } cid && categories.TryGetValue(cid, out var cat) ? cat.CodePrefix : null);
            var name = Trim(l.ItemName) ?? template?.ItemName;
            string? error = null;
            if (name is null) error = "Name the item.";
            else if (categoryId is null) error = "Choose a category.";
            else if (prefix is null || !AssetRules.IsValidPrefix(prefix)) error = "The code prefix is missing.";
            else if (l.Quantity <= 0 || l.Quantity != decimal.Truncate(l.Quantity) || l.Quantity > 500)
                error = "Assets are counted one by one — a whole number up to 500.";
            if (error is not null)
            {
                result.Add(new GoodsReceiptLinePreviewDto(index, l.Kind, error, [], null, null, null, l.Rate is null, error));
                continue;
            }
            var count = (int)l.Quantity;
            var numbers = await ops.PeekCodeNumbersAsync(prefix!, count, planned, ct);
            planned[prefix!] = numbers[^1];
            var codes = numbers.Select(n => AssetRules.AssetCode(prefix!, n)).ToList();
            assets += count;
            if (l.Rate is null) withoutCost += count;
            result.Add(new GoodsReceiptLinePreviewDto(index, l.Kind,
                count == 1
                    ? $"Will create 1 asset: {codes[0]}, with its own QR label"
                    : $"Will create {count} assets: {AssetRules.CodeRange(codes)}, each with its own QR label",
                codes, null, null, "Nos", l.Rate is null, null));
        }
        return new GoodsReceiptPreviewDto(result, total, assets, withoutCost,
            withoutCost > 0 ? MissingCostWarning(withoutCost, assets) : null);
    }

    // ── Purchase orders ─────────────────────────────────────────────────────

    /// <summary>Purchase orders still waiting for goods, with what each line has left, so
    /// "Against purchase order" can fill the receipt lines in one click.</summary>
    public async Task<List<OpenPurchaseOrderDto>> GetOpenPurchaseOrdersAsync(int? supplierId, CancellationToken ct = default)
    {
        var orders = await db.PurchaseOrders.AsNoTracking().Include(p => p.Lines)
            .Where(p => p.Status == PurchaseOrderStatus.Sent || p.Status == PurchaseOrderStatus.PartiallyReceived)
            .Where(p => supplierId == null || p.SupplierId == supplierId)
            .OrderByDescending(p => p.PoDate).Take(200)
            .Select(p => new
            {
                p.PurchaseOrderId, p.PoNumber, p.SupplierId, p.PoDate,
                SupplierName = db.Suppliers.IgnoreQueryFilters().Where(s => s.SupplierId == p.SupplierId)
                    .Select(s => s.SupplierName).FirstOrDefault(),
                p.Lines
            })
            .ToListAsync(ct);
        var itemIds = orders.SelectMany(o => o.Lines).Where(l => l.InventoryItemId != null)
            .Select(l => l.InventoryItemId!.Value).Distinct().ToList();
        var items = await db.InventoryItems.AsNoTracking().Where(i => itemIds.Contains(i.InventoryItemId))
            .ToDictionaryAsync(i => i.InventoryItemId, ct);
        return orders.Select(o => new OpenPurchaseOrderDto(o.PurchaseOrderId, o.PoNumber, o.SupplierId,
                o.SupplierName ?? "", o.PoDate,
                o.Lines.OrderBy(l => l.LineNumber).Where(l => l.Quantity > l.ReceivedQuantity).Select(l =>
                {
                    var item = l.InventoryItemId is { } id && items.TryGetValue(id, out var it) ? it : null;
                    return new PurchaseOrderReceiptLineDto(l.PurchaseOrderLineId, l.InventoryItemId, l.ItemName,
                        item?.ItemCode, item is null ? null : (byte)item.Kind, l.Unit ?? item?.Unit ?? "",
                        l.Quantity, l.ReceivedQuantity, l.Quantity - l.ReceivedQuantity, l.Rate);
                }).ToList()))
            .Where(o => o.Lines.Count > 0).ToList();
    }

    // ── Export ──────────────────────────────────────────────────────────────

    public async Task<InventoryExportResult?> ExportReceiptsAsync(GoodsReceiptListRequest request, CancellationToken ct = default)
    {
        var rows = await ShapeReceipts(FilterReceipts(request)
                .OrderByDescending(r => r.ReceiptDate).ThenByDescending(r => r.GoodsReceiptId).Take(MaxExportRows))
            .ToListAsync(ct);
        if (rows.Count == 0) return null;
        var content = ExcelWorkbookHelper.Write("Incoming Goods", new List<ExcelColumn<GoodsReceiptRowDto>>
        {
            new("Receipt", r => r.ReceiptNumber),
            new("Date", r => r.ReceiptDate, ExcelCellKind.Date),
            new("Status", r => r.IsReversed ? "Reversed" : r.IsReversal ? "Reversal" : ((GoodsReceiptStatus)r.Status).ToString()),
            new("Supplier", r => r.IsOpeningBalance ? "Opening balance" : r.SupplierName),
            new("Invoice number", r => r.InvoiceNumber),
            new("Purchase order", r => r.PoNumber),
            new("Lines", r => r.LineCount),
            new("Assets created", r => r.AssetCount),
            new("Value", r => r.TotalAmount, ExcelCellKind.Money),
            new("Reversal", r => r.ReversalNumber)
        }, rows);
        return new InventoryExportResult(content, ExcelWorkbookHelper.ContentType,
            ExcelWorkbookHelper.FileName("incoming_goods", clock.UtcNow));
    }
}
