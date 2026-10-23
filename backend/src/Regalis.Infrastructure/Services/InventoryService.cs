using System.Data;
using System.Globalization;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Regalis.Application.Abstractions;
using Regalis.Application.Common.Exceptions;
using Regalis.Application.Operations;
using Regalis.Domain.Entities.Operations;
using Regalis.Domain.Enums;
using Regalis.Infrastructure.Persistence;
using Regalis.Shared;
using Regalis.Shared.Api;

namespace Regalis.Infrastructure.Services;

/// <summary>
/// Inventory (M19). Current stock is never a stored column — it is derived from the
/// append-only transaction ledger (Invariant 11). There is no update or delete on a
/// transaction; a mistake is corrected with a compensating Adjustment row.
/// </summary>
public partial class InventoryService(
    RegalisDbContext db,
    INumberSequenceService numberSequence,
    ICurrentUser currentUser,
    IDateTimeProvider clock) : IInventoryService
{
    private IQueryable<InventoryItem> FilterItems(InventoryItemListRequest request)
    {
        var query = db.InventoryItems.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var term = request.Search.Trim();
            // Name, code (the SKU) or HSN/SAC — the quotation's item picker searches all three.
            query = query.Where(i => i.ItemCode.Contains(term) || i.ItemName.Contains(term)
                                     || (i.HsnSacCode != null && i.HsnSacCode.Contains(term)));
        }
        if (!string.IsNullOrWhiteSpace(request.Category))
        {
            query = query.Where(i => i.Category == request.Category);
        }
        if (request.LocationId is not null)
        {
            query = query.Where(i => i.LocationId == request.LocationId);
        }
        if (request.ItemType is not null)
        {
            var type = (InventoryItemType)request.ItemType.Value;
            query = query.Where(i => i.ItemType == type);
        }
        // Asset units (one row per physical thing, Assets module) stay off the pickers this
        // list feeds — quotations, purchase orders — unless asked for by kind.
        query = request.Kind is { } kind
            ? query.Where(i => i.Kind == (ItemKind)kind)
            : query.Where(i => i.Kind != ItemKind.Asset);
        return query;
    }

    public async Task<PagedResult<InventoryItemDto>> GetItemsAsync(InventoryItemListRequest request,
        CancellationToken ct = default)
    {
        var query = FilterItems(request);

        // BelowReorder filters on derived stock, so it is applied after aggregation.
        if (request.BelowReorder == true)
        {
            var all = await query.OrderBy(i => i.ItemName).ToListAsync(ct);
            var stockByItem = await MovementsByItemAsync(all.Select(i => i.InventoryItemId).ToList(), ct);
            var flagged = all
                .Select(i => ToDto(i, CurrentStock(i, stockByItem)))
                .Where(d => d.NeedsReorder)
                .ToList();
            var pageItems = flagged
                .Skip((request.Page - 1) * request.PageSize)
                .Take(request.PageSize)
                .ToList();
            return PagedResult<InventoryItemDto>.Create(pageItems, request.Page, request.PageSize, flagged.Count);
        }

        var totalCount = await query.CountAsync(ct);
        var items = await query
            .OrderBy(i => i.ItemName)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync(ct);
        var movements = await MovementsByItemAsync(items.Select(i => i.InventoryItemId).ToList(), ct);
        var dtos = items.Select(i => ToDto(i, CurrentStock(i, movements))).ToList();
        return PagedResult<InventoryItemDto>.Create(dtos, request.Page, request.PageSize, totalCount);
    }

    public async Task<InventoryItemDto> GetItemAsync(int itemId, CancellationToken ct = default)
    {
        var item = await db.InventoryItems.AsNoTracking()
            .FirstOrDefaultAsync(i => i.InventoryItemId == itemId, ct)
            ?? throw new NotFoundException("InventoryItem", itemId);
        var movements = await MovementsByItemAsync([itemId], ct);
        return ToDto(item, CurrentStock(item, movements));
    }

    public async Task<InventoryItemDto> CreateItemAsync(SaveInventoryItemRequest request,
        CancellationToken ct = default)
    {
        var code = request.ItemCode.Trim().ToUpperInvariant();
        if (request.OpeningQuantity != 0)
        {
            throw new BusinessRuleException(NoOpeningQuantity);
        }
        if (await db.InventoryItems.AnyAsync(i => i.ItemCode == code, ct))
        {
            throw new ConflictException($"Item code {code} is already in use.");
        }
        if (!await db.Locations.AnyAsync(l => l.LocationId == request.LocationId, ct))
        {
            throw new NotFoundException("Location", request.LocationId);
        }
        var item = new InventoryItem { ItemCode = code };
        Apply(item, request);
        db.InventoryItems.Add(item);
        await db.SaveChangesAsync(ct);
        return ToDto(item, item.OpeningQuantity);
    }

    public async Task<InventoryItemDto> UpdateItemAsync(int itemId, SaveInventoryItemRequest request,
        CancellationToken ct = default)
    {
        var item = await db.InventoryItems
            .FirstOrDefaultAsync(i => i.InventoryItemId == itemId, ct)
            ?? throw new NotFoundException("InventoryItem", itemId);
        EnsureNotAsset(item);
        // The code and the opening quantity are frozen: stock only ever changes through a
        // movement (Assets spec §5.2) — history must keep meaning what it meant.
        var hasLedger = await db.InventoryTransactions.AnyAsync(t => t.InventoryItemId == itemId, ct);
        if (item.OpeningQuantity != request.OpeningQuantity)
        {
            throw new BusinessRuleException(NoOpeningQuantity);
        }
        if (hasLedger && request.ItemType == (byte)InventoryItemType.Service
            && item.ItemType != InventoryItemType.Service)
        {
            throw new BusinessRuleException(
                "This item has stock movements, so it cannot become a service.");
        }
        Apply(item, request);
        await db.SaveChangesAsync(ct);
        return await GetItemAsync(itemId, ct);
    }

    public async Task DeleteItemAsync(int itemId, CancellationToken ct = default)
    {
        var item = await db.InventoryItems
            .FirstOrDefaultAsync(i => i.InventoryItemId == itemId, ct)
            ?? throw new NotFoundException("InventoryItem", itemId);
        EnsureNotAsset(item);
        if (await db.InventoryTransactions.AnyAsync(t => t.InventoryItemId == itemId, ct))
        {
            throw new BusinessRuleException(
                "This item has ledger entries and cannot be deleted — deactivate it instead.");
        }
        db.InventoryItems.Remove(item); // interceptor converts to soft delete
        await db.SaveChangesAsync(ct);
    }

    public async Task<List<InventoryStockDto>> GetStockAsync(int? locationId, bool? belowReorder,
        CancellationToken ct = default)
    {
        // Services hold no stock, so they have no place on the stock view.
        var itemsQuery = db.InventoryItems.AsNoTracking()
            .Where(i => i.ItemType != InventoryItemType.Service && i.Kind != ItemKind.Asset);
        if (locationId is not null)
        {
            itemsQuery = itemsQuery.Where(i => i.LocationId == locationId);
        }
        var items = await itemsQuery.OrderBy(i => i.ItemName).ToListAsync(ct);
        var sums = await db.InventoryTransactions.AsNoTracking()
            .Where(t => items.Select(i => i.InventoryItemId).Contains(t.InventoryItemId))
            .GroupBy(t => new { t.InventoryItemId, t.TransactionType })
            .Select(g => new { g.Key.InventoryItemId, g.Key.TransactionType, Total = g.Sum(t => t.Quantity) })
            .ToListAsync(ct);

        decimal SumOf(int itemId, InventoryTransactionType type) =>
            sums.Where(s => s.InventoryItemId == itemId && s.TransactionType == type)
                .Sum(s => s.Total);

        var rows = items.Select(i =>
        {
            var totalIn = SumOf(i.InventoryItemId, InventoryTransactionType.In)
                          + SumOf(i.InventoryItemId, InventoryTransactionType.OpeningBalance);
            var totalOut = SumOf(i.InventoryItemId, InventoryTransactionType.Out);
            var totalDamage = SumOf(i.InventoryItemId, InventoryTransactionType.Damage);
            var totalAdjustment = SumOf(i.InventoryItemId, InventoryTransactionType.Adjustment);
            var current = i.OpeningQuantity + totalIn + totalAdjustment - totalOut - totalDamage;
            return new InventoryStockDto(
                i.InventoryItemId, i.ItemCode, i.ItemName, i.Category, i.Unit, i.OpeningQuantity,
                totalIn, totalOut, totalDamage, totalAdjustment, current, i.ReorderLevel,
                current <= i.ReorderLevel);
        });
        if (belowReorder == true)
        {
            rows = rows.Where(r => r.NeedsReorder);
        }
        return rows.ToList();
    }

    public async Task<InventoryTransactionDto> CreateTransactionAsync(
        CreateInventoryTransactionRequest request, CancellationToken ct = default)
    {
        var type = (InventoryTransactionType)request.TransactionType;

        var strategy = db.Database.CreateExecutionStrategy();
        var transactionId = await strategy.ExecuteAsync(async () =>
        {
            // Serializable so two concurrent issues cannot both pass the stock check.
            await using var dbTransaction =
                await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);

            var item = await db.InventoryItems
                .FirstOrDefaultAsync(i => i.InventoryItemId == request.InventoryItemId, ct)
                ?? throw new NotFoundException("InventoryItem", request.InventoryItemId);
            if (item.ItemType == InventoryItemType.Service)
            {
                throw new BusinessRuleException(
                    $"{item.ItemName} is a service, so it has no stock to move.");
            }
            EnsureNotAsset(item);

            var movements = await MovementsByItemAsync([item.InventoryItemId], ct);
            var current = CurrentStock(item, movements);
            var delta = type switch
            {
                InventoryTransactionType.Out or InventoryTransactionType.Damage => -request.Quantity,
                _ => request.Quantity
            };
            if (current + delta < 0)
            {
                throw new BusinessRuleException(
                    $"Only {current:0.###} {item.Unit} in stock — the ledger cannot go negative.");
            }

            var entry = await NewEntryAsync(item, request, ct);
            db.InventoryTransactions.Add(entry);
            await db.SaveChangesAsync(ct);
            await dbTransaction.CommitAsync(ct);
            return entry.InventoryTransactionId;
        });

        return await Shape(db.InventoryTransactions.AsNoTracking()
                .Where(t => t.InventoryTransactionId == transactionId))
            .FirstAsync(ct);
    }

    /// <summary>A ledger row as the screen records it — numbered from the shared sequence,
    /// entered by the caller. Excel imports build their rows here too.</summary>
    private async Task<InventoryTransaction> NewEntryAsync(InventoryItem item,
        CreateInventoryTransactionRequest request, CancellationToken ct) => new()
    {
        InventoryItemId = item.InventoryItemId,
        TransactionNumber = await numberSequence.NextAsync(SequenceKeys.InventoryTransaction, ct),
        TransactionType = (InventoryTransactionType)request.TransactionType,
        Quantity = request.Quantity,
        TransactionDate = request.TransactionDate ?? IstClock.IstDateOf(clock.UtcNow),
        SourceOrDestination = request.SourceOrDestination?.Trim(),
        ReferenceNumber = request.ReferenceNumber?.Trim(),
        SupplierId = request.SupplierId,
        PurchaseOrderId = request.PurchaseOrderId,
        PurchaseInvoiceNumber = request.PurchaseInvoiceNumber?.Trim(),
        AttachmentPath = request.AttachmentPath?.Trim(),
        UnitCost = request.UnitCost,
        DamageDescription = request.DamageDescription?.Trim(),
        DamageCause = request.DamageCause?.Trim(),
        LocationId = item.LocationId,
        EnteredByUserId = currentUser.UserId,
        Notes = request.Notes?.Trim()
    };

    public async Task<PagedResult<InventoryTransactionDto>> GetTransactionsAsync(
        InventoryTransactionListRequest request, CancellationToken ct = default)
    {
        var query = FilterTransactions(request);
        var totalCount = await query.CountAsync(ct);
        var items = await Shape(query
                .OrderByDescending(t => t.InventoryTransactionId)
                .Skip((request.Page - 1) * request.PageSize)
                .Take(request.PageSize))
            .ToListAsync(ct);
        return PagedResult<InventoryTransactionDto>.Create(items, request.Page, request.PageSize, totalCount);
    }

    public async Task<InventoryExportResult> ExportAsync(InventoryTransactionListRequest request,
        CancellationToken ct = default)
    {
        var rows = await Shape(FilterTransactions(request)
                .OrderBy(t => t.TransactionDate).ThenBy(t => t.InventoryTransactionId))
            .ToListAsync(ct);

        var csv = new StringBuilder();
        csv.AppendLine("TransactionNumber,Date,ItemCode,ItemName,Type,Quantity,Unit," +
                       "SourceOrDestination,Reference,UnitCost,DamageDescription,EnteredBy,Notes");
        foreach (var r in rows)
        {
            csv.Append(r.TransactionNumber).Append(',')
                .Append(r.TransactionDate.ToString("yyyy-MM-dd")).Append(',')
                .Append(Escape(r.ItemCode)).Append(',')
                .Append(Escape(r.ItemName)).Append(',')
                .Append(TypeName(r.TransactionType)).Append(',')
                .Append(r.Quantity.ToString("0.###", CultureInfo.InvariantCulture)).Append(',')
                .Append(Escape(r.Unit)).Append(',')
                .Append(Escape(r.SourceOrDestination ?? "")).Append(',')
                .Append(Escape(r.ReferenceNumber ?? "")).Append(',')
                .Append(r.UnitCost?.ToString("0.##", CultureInfo.InvariantCulture) ?? "").Append(',')
                .Append(Escape(r.DamageDescription ?? "")).Append(',')
                .Append(Escape(r.EnteredByName)).Append(',')
                .Append(Escape(r.Notes ?? ""))
                .AppendLine();
        }
        var content = Encoding.UTF8.GetPreamble()
            .Concat(Encoding.UTF8.GetBytes(csv.ToString())).ToArray();
        var todayIst = IstClock.IstDateOf(clock.UtcNow);
        return new InventoryExportResult(content, "text/csv", $"inventory_{todayIst:yyyyMMdd}.csv");
    }

    private const string NoOpeningQuantity =
        "There is no opening quantity: an item's stock only comes from movements. " +
        "Record the first stock on Assets → Incoming Goods as an opening balance.";

    /// <summary>Assets are one record per physical unit and only move through the Assets
    /// screens (receipts, movements), never through these older item routes.</summary>
    private static void EnsureNotAsset(InventoryItem item)
    {
        if (item.Kind == ItemKind.Asset)
        {
            throw new BusinessRuleException(
                $"{item.ItemCode} is an asset. It is edited on Assets → Items and moves through Incoming / Outgoing Goods.");
        }
    }

    // ── Derivation ──────────────────────────────────────────────────────────

    private sealed record Movement(int InventoryItemId, InventoryTransactionType TransactionType, decimal Total);

    private async Task<List<Movement>> MovementsByItemAsync(List<int> itemIds, CancellationToken ct) =>
        await db.InventoryTransactions.AsNoTracking()
            .Where(t => itemIds.Contains(t.InventoryItemId))
            .GroupBy(t => new { t.InventoryItemId, t.TransactionType })
            .Select(g => new Movement(g.Key.InventoryItemId, g.Key.TransactionType, g.Sum(t => t.Quantity)))
            .ToListAsync(ct);

    private static decimal CurrentStock(InventoryItem item, List<Movement> movements)
    {
        decimal SumOf(InventoryTransactionType type) => movements
            .Where(m => m.InventoryItemId == item.InventoryItemId && m.TransactionType == type)
            .Sum(m => m.Total);
        return item.OpeningQuantity
               + SumOf(InventoryTransactionType.In)
               + SumOf(InventoryTransactionType.OpeningBalance)
               + SumOf(InventoryTransactionType.Adjustment)
               - SumOf(InventoryTransactionType.Out)
               - SumOf(InventoryTransactionType.Damage);
    }

    // ── Shaping ─────────────────────────────────────────────────────────────

    private IQueryable<InventoryTransaction> FilterTransactions(InventoryTransactionListRequest request)
    {
        var query = db.InventoryTransactions.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            // The goods screens' search box: item, supplier, PO#, reference, purpose, remarks.
            var term = request.Search.Trim();
            query = query.Where(t => t.TransactionNumber.Contains(term)
                                     || t.InventoryItem.ItemCode.Contains(term)
                                     || t.InventoryItem.ItemName.Contains(term)
                                     || (t.ReferenceNumber != null && t.ReferenceNumber.Contains(term))
                                     || (t.SourceOrDestination != null && t.SourceOrDestination.Contains(term))
                                     || (t.PurchaseInvoiceNumber != null && t.PurchaseInvoiceNumber.Contains(term))
                                     || (t.Notes != null && t.Notes.Contains(term))
                                     || db.Suppliers.Any(s => s.SupplierId == t.SupplierId
                                                              && s.SupplierName.Contains(term))
                                     || db.PurchaseOrders.Any(p => p.PurchaseOrderId == t.PurchaseOrderId
                                                                   && p.PoNumber.Contains(term)));
        }
        if (request.InventoryItemId is not null)
        {
            query = query.Where(t => t.InventoryItemId == request.InventoryItemId);
        }
        if (request.TransactionType is not null)
        {
            query = query.Where(t => (byte)t.TransactionType == request.TransactionType);
        }
        if (request.From is not null)
        {
            query = query.Where(t => t.TransactionDate >= request.From);
        }
        if (request.To is not null)
        {
            query = query.Where(t => t.TransactionDate <= request.To);
        }
        if (request.SupplierId is not null)
        {
            query = query.Where(t => t.SupplierId == request.SupplierId);
        }
        if (request.PurchaseOrderId is not null)
        {
            query = query.Where(t => t.PurchaseOrderId == request.PurchaseOrderId);
        }
        if (request.LocationId is not null)
        {
            query = query.Where(t => t.LocationId == request.LocationId);
        }
        return query;
    }

    private IQueryable<InventoryTransactionDto> Shape(IQueryable<InventoryTransaction> query) =>
        query
            .Join(db.InventoryItems, t => t.InventoryItemId, i => i.InventoryItemId,
                (t, i) => new { t, i })
            .Join(db.Users, x => x.t.EnteredByUserId, u => u.UserId,
                (x, u) => new InventoryTransactionDto(
                    x.t.InventoryTransactionId, x.t.TransactionNumber, x.t.InventoryItemId,
                    x.i.ItemCode, x.i.ItemName, x.i.Unit, (byte)x.t.TransactionType, x.t.Quantity,
                    x.t.TransactionDate, x.t.SourceOrDestination, x.t.ReferenceNumber,
                    x.t.SupplierId,
                    db.Suppliers.Where(s => s.SupplierId == x.t.SupplierId)
                        .Select(s => s.SupplierName).FirstOrDefault(),
                    x.t.PurchaseOrderId,
                    db.PurchaseOrders.Where(p => p.PurchaseOrderId == x.t.PurchaseOrderId)
                        .Select(p => p.PoNumber).FirstOrDefault(),
                    x.t.PurchaseInvoiceNumber, x.t.AttachmentPath, x.t.UnitCost,
                    x.t.DamageDescription, x.t.DamageCause, x.t.LocationId, x.t.EnteredByUserId,
                    u.FullName, x.t.Notes, x.t.CreatedDate));

    private static void Apply(InventoryItem item, SaveInventoryItemRequest request)
    {
        item.ItemName = request.ItemName.Trim();
        item.Category = request.Category?.Trim();
        item.Unit = request.Unit.Trim();
        item.OpeningQuantity = request.OpeningQuantity;
        item.ReorderLevel = request.ReorderLevel;
        item.UnitCost = request.UnitCost;
        item.LocationId = request.LocationId;
        item.Description = request.Description?.Trim();

        // Sales details: null means "not sent", so the stored value stands.
        if (request.ItemType is { } type) item.ItemType = (InventoryItemType)type;
        if (request.HsnSacCode is not null)
        {
            item.HsnSacCode = string.IsNullOrWhiteSpace(request.HsnSacCode) ? null : request.HsnSacCode.Trim();
        }
        if (request.SellingRate is { } rate) item.SellingRate = rate;
        if (request.IsNonTaxable is { } nonTaxable) item.IsNonTaxable = nonTaxable;
        if (request.TaxPercent is { } tax) item.TaxPercent = tax;
        if (item.IsNonTaxable) item.TaxPercent = 0m;
        if (item.ItemType == InventoryItemType.Service)
        {
            item.OpeningQuantity = 0m;
            item.ReorderLevel = 0m;
        }
    }

    private static InventoryItemDto ToDto(InventoryItem item, decimal currentStock) => new(
        item.InventoryItemId, item.ItemCode, item.ItemName, item.Category, item.Unit,
        item.OpeningQuantity, item.ReorderLevel, item.UnitCost, item.LocationId, item.Description,
        currentStock, currentStock <= item.ReorderLevel, item.IsActive,
        (byte)item.ItemType, item.HsnSacCode, item.SellingRate, item.TaxPercent, item.IsNonTaxable);

    private static string TypeName(byte type) => type switch
    {
        1 => "In",
        2 => "Out",
        3 => "Damage",
        4 => "OpeningBalance",
        5 => "Adjustment",
        _ => type.ToString()
    };

    private static string Escape(string value) =>
        value.Contains(',') || value.Contains('"')
            ? $"\"{value.Replace("\"", "\"\"")}\""
            : value;
}
