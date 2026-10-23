using System.Data;
using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Regalis.Application.Abstractions;
using Regalis.Application.Common.Exceptions;
using Regalis.Application.Operations;
using Regalis.Domain.Entities.Operations;
using Regalis.Domain.Enums;
using Regalis.Infrastructure.Persistence;
using Regalis.Shared;
using Regalis.Shared.Api;
using Regalis.Shared.Constants;

namespace Regalis.Infrastructure.Services;

/// <summary>
/// Assets (Regalis_WorkSpace_Assets_Feature_Specification, Oct 2026): Items, Incoming
/// Goods, Outgoing Goods, Current Stock and QR labels. The rules it never breaks:
/// on hand is always summed from the ledger and never typed; stock never goes below
/// zero (checked inside the writing transaction); posted receipts and issues are
/// reversed, never edited; nothing is deleted; codes are never reused; kind never
/// changes; every figure is counted on the server across all rows.
/// </summary>
public partial class AssetService(
    RegalisDbContext db,
    AssetOperations ops,
    ISettingService settings,
    ICurrentUser currentUser,
    IDateTimeProvider clock,
    IOptions<StorageOptions> storageOptions) : IAssetService
{
    /// <summary>"Fifty rows a page, with the page size set by the server" (§4.5).</summary>
    private const int ItemsPageSize = 50;
    private const int MaxExportRows = 5_000;
    private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

    private DateOnly Today => IstClock.IstDateOf(clock.UtcNow);

    /// <summary>Serializable, like the ledger's own writes: the stock check and the write
    /// it guards happen in one transaction, so two admins cannot both pass it.</summary>
    private async Task<T> InTransactionAsync<T>(Func<Task<T>> work, CancellationToken ct)
    {
        var strategy = db.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            db.ChangeTracker.Clear();
            await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
            var result = await work();
            await transaction.CommitAsync(ct);
            return result;
        });
    }

    private async Task<Dictionary<int, AssetLocation>> RoomsAsync(CancellationToken ct) =>
        await db.AssetLocations.IgnoreQueryFilters().AsNoTracking().ToDictionaryAsync(r => r.AssetLocationId, ct);

    private async Task<(string Owner, string Email, int AlertDays)> LabelSettingsAsync(CancellationToken ct) =>
        (await settings.GetStringAsync(AppSettingKeys.Assets.LabelOwnerName, "Regalis Workspace", ct),
         await settings.GetStringAsync(AppSettingKeys.Assets.LabelContactEmail, "admin@regalis.in", ct),
         await settings.GetIntAsync(AppSettingKeys.Assets.WarrantyAlertDays, 60, ct));

    private static bool Retired(AssetDetail? d) => d is not null && AssetRules.IsRetired(d.Status);

    private static string Qty(decimal value) => value.ToString("0.###", Invariant);

    private static string Money(decimal value) =>
        "Rs. " + value.ToString("#,##,##0.##", CultureInfo.GetCultureInfo("en-IN"));

    // ── Consumable stock ────────────────────────────────────────────────────

    private sealed record ConsumableStock(int ItemId, string Name, decimal OnHand, decimal ReorderLevel)
    {
        public bool Below => OnHand <= ReorderLevel;
    }

    /// <summary>On hand for every active consumable — the Below reorder card and filter
    /// work on derived stock, so they are worked out across all of them.</summary>
    private async Task<List<ConsumableStock>> ConsumableStockAsync(CancellationToken ct)
    {
        var items = await db.InventoryItems.AsNoTracking()
            .Where(i => i.IsActive && i.Kind == ItemKind.Consumable && i.ItemType == InventoryItemType.Goods)
            .Select(i => new { i.InventoryItemId, i.ItemName, i.ReorderLevel })
            .ToListAsync(ct);
        var onHand = await ops.OnHandAsync(items.Select(i => i.InventoryItemId).ToList(), ct);
        return items.Select(i => new ConsumableStock(i.InventoryItemId, i.ItemName,
            onHand.GetValueOrDefault(i.InventoryItemId), i.ReorderLevel)).ToList();
    }

    // ── Items list ──────────────────────────────────────────────────────────

    private async Task<IQueryable<InventoryItem>> FilterAsync(AssetItemListRequest request, CancellationToken ct)
    {
        var query = db.InventoryItems.AsNoTracking();
        if (request.IncludeInactive != true) query = query.Where(i => i.IsActive);

        query = request.Kind switch
        {
            1 => query.Where(i => i.Kind == ItemKind.Consumable && i.ItemType == InventoryItemType.Goods),
            2 => query.Where(i => i.Kind == ItemKind.Asset),
            3 => query.Where(i => i.ItemType == InventoryItemType.Service),
            // "All kinds" is everything Regalis owns; services are only sold.
            _ => query.Where(i => i.ItemType == InventoryItemType.Goods)
        };

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var term = request.Search.Trim();
            query = query.Where(i => i.ItemCode.Contains(term) || i.ItemName.Contains(term)
                                     || (i.BrandModel != null && i.BrandModel.Contains(term))
                                     || (i.AssetDetail != null && i.AssetDetail.SerialNumber != null
                                         && i.AssetDetail.SerialNumber.Contains(term)));
        }
        if (request.CategoryId is { } categoryId) query = query.Where(i => i.AssetCategoryId == categoryId);
        if (request.LocationId is { } roomId)
        {
            query = query.Where(i => (i.AssetDetail != null && i.AssetDetail.AssetLocationId == roomId)
                                     || (i.Kind == ItemKind.Consumable && i.KeptInLocationId == roomId));
        }
        if (request.Status is { } status)
        {
            var s = (AssetStatus)status;
            query = query.Where(i => i.AssetDetail != null && i.AssetDetail.Status == s);
        }
        if (request.NoLabel == true)
        {
            query = query.Where(i => i.AssetDetail != null && i.AssetDetail.LabelPrintedUtc == null
                                     && i.AssetDetail.Status != AssetStatus.Scrapped
                                     && i.AssetDetail.Status != AssetStatus.Lost
                                     && i.AssetDetail.Status != AssetStatus.ReturnedToSupplier);
        }
        if (request.CostMissing == true)
        {
            query = query.Where(i => i.UnitCost == null && i.ItemType == InventoryItemType.Goods);
        }
        if (request.BelowReorder == true)
        {
            var below = (await ConsumableStockAsync(ct)).Where(s => s.Below).Select(s => s.ItemId).ToList();
            query = query.Where(i => below.Contains(i.InventoryItemId));
        }
        if (request.ReceiptId is { } receiptId)
        {
            query = query.Where(i => i.AssetDetail != null && db.GoodsReceiptLines.Any(l =>
                l.GoodsReceiptId == receiptId && l.GoodsReceiptLineId == i.AssetDetail.GoodsReceiptLineId));
        }
        if (!string.IsNullOrWhiteSpace(request.Ids))
        {
            var ids = request.Ids.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(s => int.TryParse(s, NumberStyles.Integer, Invariant, out var id) ? id : 0)
                .Where(id => id > 0).Distinct().Take(MaxExportRows).ToList();
            query = query.Where(i => ids.Contains(i.InventoryItemId));
        }
        return query;
    }

    private static IQueryable<InventoryItem> Sort(IQueryable<InventoryItem> query, AssetItemListRequest request)
    {
        var desc = string.Equals(request.SortDir, "desc", StringComparison.OrdinalIgnoreCase);
        return (request.SortBy ?? "").ToLowerInvariant() switch
        {
            "code" => desc ? query.OrderByDescending(i => i.ItemCode) : query.OrderBy(i => i.ItemCode),
            "name" => desc
                ? query.OrderByDescending(i => i.ItemName).ThenByDescending(i => i.ItemCode)
                : query.OrderBy(i => i.ItemName).ThenBy(i => i.ItemCode),
            "category" => desc
                ? query.OrderByDescending(i => i.Category).ThenBy(i => i.ItemCode)
                : query.OrderBy(i => i.Category).ThenBy(i => i.ItemCode),
            "cost" => desc
                ? query.OrderByDescending(i => i.UnitCost).ThenBy(i => i.ItemCode)
                : query.OrderBy(i => i.UnitCost).ThenBy(i => i.ItemCode),
            // Assets first, siblings together.
            _ => query.OrderByDescending(i => i.Kind).ThenBy(i => i.ItemName).ThenBy(i => i.ItemCode)
        };
    }

    public async Task<AssetItemListResult> GetItemsAsync(AssetItemListRequest request, CancellationToken ct = default)
    {
        var query = await FilterAsync(request, ct);
        var total = await query.CountAsync(ct);
        var page = Math.Max(1, request.Page);
        var items = await Sort(query, request)
            .Include(i => i.AssetDetail).Include(i => i.AssetCategory)
            .Skip((page - 1) * ItemsPageSize).Take(ItemsPageSize)
            .ToListAsync(ct);
        var rows = await RowsAsync(items, ct);
        var summary = await SummaryAsync(ct);
        return new AssetItemListResult(PagedResult<AssetItemRowDto>.Create(rows, page, ItemsPageSize, total), summary);
    }

    private async Task<List<AssetItemRowDto>> RowsAsync(List<InventoryItem> items, CancellationToken ct)
    {
        var rooms = await RoomsAsync(ct);
        var (_, _, alertDays) = await LabelSettingsAsync(ct);
        var onHand = await ops.OnHandAsync(items.Where(i => i.Kind == ItemKind.Consumable)
            .Select(i => i.InventoryItemId).ToList(), ct);
        var today = Today;
        return items.Select(i => Row(i, rooms, onHand, today, alertDays)).ToList();
    }

    private static AssetItemRowDto Row(InventoryItem i, IReadOnlyDictionary<int, AssetLocation> rooms,
        IReadOnlyDictionary<int, decimal> onHand, DateOnly today, int alertDays)
    {
        var d = i.AssetDetail;
        var isService = i.ItemType == InventoryItemType.Service;
        if (i.Kind == ItemKind.Asset && d is not null)
        {
            var value = AssetRules.BookValue(i.UnitCost, d.PurchaseDate, i.AssetCategory?.UsefulLifeYears, d.Status, today);
            return new AssetItemRowDto(i.InventoryItemId, i.ItemCode, i.ItemName, (byte)i.Kind, false,
                i.AssetCategoryId, i.AssetCategory?.CategoryName ?? i.Category, i.BrandModel, d.SerialNumber,
                d.AssetLocationId, rooms.TryGetValue(d.AssetLocationId, out var room) ? room.LocationName : null,
                (byte)d.Status, (byte)d.Condition, null, i.Unit, 0m, false, i.UnitCost, value,
                d.LabelPrintedUtc, d.PurchaseDate, d.WarrantyUntil,
                WarrantySoon(d.WarrantyUntil, today, alertDays), i.IsActive);
        }
        decimal? stock = isService ? null : onHand.GetValueOrDefault(i.InventoryItemId);
        return new AssetItemRowDto(i.InventoryItemId, i.ItemCode, i.ItemName, (byte)i.Kind, isService,
            i.AssetCategoryId, i.AssetCategory?.CategoryName ?? i.Category, i.BrandModel, null,
            i.KeptInLocationId,
            i.KeptInLocationId is { } kept && rooms.TryGetValue(kept, out var keptIn) ? keptIn.LocationName : null,
            null, null, stock, i.Unit, i.ReorderLevel, stock is { } s && s <= i.ReorderLevel,
            i.UnitCost, stock is { } q && i.UnitCost is { } cost ? Math.Round(q * cost, 2) : null,
            null, null, null, false, i.IsActive);
    }

    private static bool WarrantySoon(DateOnly? until, DateOnly today, int alertDays) =>
        until is { } u && u >= today && u.DayNumber - today.DayNumber <= alertDays;

    /// <summary>The five cards, counted across every active record (§4.2).</summary>
    private async Task<AssetSummaryDto> SummaryAsync(CancellationToken ct)
    {
        var inRegister = db.AssetDetails.AsNoTracking()
            .Where(d => d.InventoryItem.IsActive
                        && d.Status != AssetStatus.Scrapped && d.Status != AssetStatus.Lost
                        && d.Status != AssetStatus.ReturnedToSupplier);
        var assetCount = await inRegister.CountAsync(ct);
        var assetValue = await inRegister.SumAsync(d => d.InventoryItem.UnitCost ?? 0m, ct);
        var noLabel = await inRegister.CountAsync(d => d.LabelPrintedUtc == null, ct);
        var costMissing = await inRegister.CountAsync(d => d.InventoryItem.UnitCost == null, ct);
        var inRepair = await inRegister.Where(d => d.Status == AssetStatus.InRepair)
            .GroupBy(d => d.InventoryItem.ItemName)
            .Select(g => new { Name = g.Key, Count = g.Count() })
            .OrderByDescending(g => g.Count).ToListAsync(ct);
        var consumables = await ConsumableStockAsync(ct);
        var below = consumables.Where(c => c.Below).OrderBy(c => c.Name).ToList();
        return new AssetSummaryDto(
            assetCount, assetValue, consumables.Count,
            below.Count, below.Select(c => c.Name).Take(5).ToList(),
            inRepair.Sum(r => r.Count),
            inRepair.Count == 0 ? null : string.Join(", ", inRepair.Take(3).Select(r => $"{r.Count} {r.Name}")),
            noLabel, costMissing);
    }

    // ── One item ────────────────────────────────────────────────────────────

    public async Task<AssetItemDetailDto> GetItemAsync(int itemId, CancellationToken ct = default)
    {
        var item = await db.InventoryItems.AsNoTracking()
            .Include(i => i.AssetDetail).Include(i => i.AssetCategory)
            .FirstOrDefaultAsync(i => i.InventoryItemId == itemId, ct)
            ?? throw new NotFoundException("InventoryItem", itemId);
        return await DetailAsync(item, ct);
    }

    private async Task<AssetItemDetailDto> DetailAsync(InventoryItem item, CancellationToken ct)
    {
        var rooms = await RoomsAsync(ct);
        var (owner, email, alertDays) = await LabelSettingsAsync(ct);
        var today = Today;
        var d = item.AssetDetail;
        var supplierName = item.SupplierId is null ? null
            : await db.Suppliers.IgnoreQueryFilters().Where(s => s.SupplierId == item.SupplierId)
                .Select(s => s.SupplierName).FirstOrDefaultAsync(ct);
        var events = await EventsQuery(item.InventoryItemId).Take(5).ToListAsync(ct);
        var documents = await db.ItemDocuments.CountAsync(x => x.InventoryItemId == item.InventoryItemId, ct);
        var repairs = await db.AssetRepairs.AsNoTracking()
            .Where(r => r.InventoryItemId == item.InventoryItemId).ToListAsync(ct);
        var repairSpend = repairs.Sum(r => r.Cost ?? 0m);
        var life = item.AssetCategory?.UsefulLifeYears;

        if (item.Kind == ItemKind.Asset && d is not null)
        {
            var roomName = rooms.TryGetValue(d.AssetLocationId, out var room) ? room.LocationName : null;
            var siblings = await db.AssetDetails.CountAsync(x => x.CodePrefix == d.CodePrefix
                && x.InventoryItem.IsActive && x.Status != AssetStatus.Scrapped && x.Status != AssetStatus.Lost
                && x.Status != AssetStatus.ReturnedToSupplier, ct);
            var bookValue = AssetRules.BookValue(item.UnitCost, d.PurchaseDate, life, d.Status, today);
            decimal? age = d.PurchaseDate is { } bought ? Math.Round(AssetRules.AgeYears(bought, today), 2) : null;
            decimal? used = age is { } a && life is > 0 ? Math.Min(100m, Math.Round(a / life.Value * 100m, 1)) : null;
            var openRepair = repairs.Where(r => r.IsOpen).OrderByDescending(r => r.AssetRepairId).FirstOrDefault();
            var repairDto = openRepair is null ? null : await RepairDtoAsync(openRepair, ct);
            return new AssetItemDetailDto(
                item.InventoryItemId, item.ItemCode, item.ItemName, (byte)item.Kind, false, item.IsActive,
                item.AssetCategoryId, item.AssetCategory?.CategoryName ?? item.Category, item.Description,
                item.BrandModel, item.UnitCost, item.SupplierId, supplierName, item.VendorName, item.Remarks,
                d.CodePrefix, d.SerialNumber, d.AssetLocationId, roomName, (byte)d.Status, (byte)d.Condition,
                d.PurchaseDate, d.WarrantyUntil, WarrantySoon(d.WarrantyUntil, today, alertDays),
                d.LabelPrintedUtc, d.RetiredReason, d.RetiredValue, d.RetiredUtc,
                siblings, siblings > 1 ? $"one of {siblings} under {d.CodePrefix}" : null,
                AssetRules.QrText(item.ItemCode, item.ItemName, item.BrandModel, roomName, d.PurchaseDate, owner, email),
                repairDto,
                item.Unit, 0m, null, false, null, null,
                item.HsnSacCode, item.SellingRate, item.TaxPercent, item.IsNonTaxable,
                new AssetValueDto(item.UnitCost, bookValue,
                    life is > 0 ? "Straight line, 5% floor" : "Not depreciated", life, age, used, repairSpend, null),
                events, documents);
        }

        var isService = item.ItemType == InventoryItemType.Service;
        decimal? onHand = isService ? null : (await ops.OnHandAsync([item.InventoryItemId], ct))[item.InventoryItemId];
        var keptName = item.KeptInLocationId is { } kept && rooms.TryGetValue(kept, out var keptRoom)
            ? keptRoom.LocationName : null;
        decimal? stockValue = onHand is { } q && item.UnitCost is { } c ? Math.Round(q * c, 2) : null;
        return new AssetItemDetailDto(
            item.InventoryItemId, item.ItemCode, item.ItemName, (byte)item.Kind, isService, item.IsActive,
            item.AssetCategoryId, item.AssetCategory?.CategoryName ?? item.Category, item.Description,
            item.BrandModel, item.UnitCost, item.SupplierId, supplierName, item.VendorName, item.Remarks,
            null, null, null, null, null, null, null, null, false, null, null, null, null,
            0, null, null, null,
            item.Unit, item.ReorderLevel, onHand, onHand is { } s && s <= item.ReorderLevel,
            item.KeptInLocationId, keptName,
            item.HsnSacCode, item.SellingRate, item.TaxPercent, item.IsNonTaxable,
            new AssetValueDto(item.UnitCost, null, "Quantity x unit cost", null, null, null, 0m, stockValue),
            events, documents);
    }

    private IQueryable<ItemEventDto> EventsQuery(int itemId) =>
        db.ItemEvents.AsNoTracking()
            .Where(e => e.InventoryItemId == itemId)
            .OrderByDescending(e => e.EventDate).ThenByDescending(e => e.ItemEventId)
            .Select(e => new ItemEventDto(e.ItemEventId, e.EventType, e.EventDate, e.Summary, e.FromText,
                e.ToText, e.DocumentNumber,
                db.Users.IgnoreQueryFilters().Where(u => u.UserId == e.ActorUserId).Select(u => u.FullName).FirstOrDefault(),
                e.CreatedUtc));

    public async Task<List<ItemEventDto>> GetEventsAsync(int itemId, CancellationToken ct = default)
    {
        await EnsureItemAsync(itemId, ct);
        return await EventsQuery(itemId).Take(500).ToListAsync(ct);
    }

    private async Task EnsureItemAsync(int itemId, CancellationToken ct)
    {
        if (!await db.InventoryItems.AnyAsync(i => i.InventoryItemId == itemId, ct))
        {
            throw new NotFoundException("InventoryItem", itemId);
        }
    }

    public async Task<AssetItemDetailDto?> FindByCodeAsync(string codeOrQrText, CancellationToken ct = default)
    {
        var code = AssetRules.CodeFromScan(codeOrQrText);
        if (code is null) return null;
        var item = await db.InventoryItems.AsNoTracking()
            .Include(i => i.AssetDetail).Include(i => i.AssetCategory)
            .FirstOrDefaultAsync(i => i.ItemCode == code, ct);
        return item is null ? null : await DetailAsync(item, ct);
    }

    // ── Creating ────────────────────────────────────────────────────────────

    public async Task<CreateAssetItemResult> CreateItemAsync(CreateAssetItemRequest request, CancellationToken ct = default)
    {
        if (request.Kind == (byte)ItemKind.Asset)
        {
            return await CreateAssetsAsync(request, ct);
        }

        var code = (request.ItemCode ?? "").Trim().ToUpperInvariant();
        var errors = new List<ApiError>();
        if (code.Length == 0) errors.Add(Field("Code is required.", nameof(request.ItemCode)));
        if (!request.IsService)
        {
            if (request.CategoryId is null) errors.Add(Field("Choose a category.", nameof(request.CategoryId)));
            if (string.IsNullOrWhiteSpace(request.Unit)) errors.Add(Field("Unit is required.", nameof(request.Unit)));
            if (request.ReorderLevel is null) errors.Add(Field("Reorder level is required.", nameof(request.ReorderLevel)));
            if (request.UnitCost is null) errors.Add(Field("Unit cost is required.", nameof(request.UnitCost)));
            if (request.KeptInLocationId is null) errors.Add(Field("Choose where it is kept.", nameof(request.KeptInLocationId)));
        }
        if (errors.Count > 0) throw new AppValidationException(errors);
        if (await db.InventoryItems.IgnoreQueryFilters().AnyAsync(i => i.ItemCode == code, ct))
        {
            throw new ConflictException("This code already exists.", ErrorCodes.BusinessRuleViolation);
        }
        var category = await CategoryOrNullAsync(request.CategoryId, ct);
        await EnsureRoomAsync(request.KeptInLocationId, ct);
        await EnsureSupplierAsync(request.SupplierId, ct);

        var item = new InventoryItem
        {
            ItemCode = code,
            ItemName = request.ItemName.Trim(),
            Kind = ItemKind.Consumable,
            ItemType = request.IsService ? InventoryItemType.Service : InventoryItemType.Goods,
            AssetCategoryId = category?.AssetCategoryId,
            Category = category?.CategoryName,
            Unit = request.IsService ? (string.IsNullOrWhiteSpace(request.Unit) ? "Nos" : request.Unit.Trim()) : request.Unit!.Trim(),
            ReorderLevel = request.IsService ? 0m : request.ReorderLevel ?? 0m,
            UnitCost = request.UnitCost,
            KeptInLocationId = request.IsService ? null : request.KeptInLocationId,
            Description = Trim(request.Description),
            BrandModel = Trim(request.BrandModel),
            SupplierId = request.SupplierId,
            VendorName = Trim(request.VendorName),
            Remarks = Trim(request.Remarks),
            LocationId = await ops.DefaultBranchIdAsync(ct),
            HsnSacCode = string.IsNullOrWhiteSpace(request.HsnSacCode) ? null : request.HsnSacCode.Trim(),
            SellingRate = request.SellingRate ?? 0m,
            IsNonTaxable = request.IsNonTaxable ?? true
        };
        item.TaxPercent = item.IsNonTaxable ? 0m : request.TaxPercent ?? 0m;
        db.InventoryItems.Add(item);
        await db.SaveChangesAsync(ct);
        ops.AddEvent(item.InventoryItemId, "CREATED", Today,
            request.IsService ? "Service created" : "Item created — first stock comes in on Incoming Goods");
        await db.SaveChangesAsync(ct);
        return new CreateAssetItemResult([item.InventoryItemId], [item.ItemCode], null, null, null);
    }

    /// <summary>The asset form (§5.1). "How many are you adding?" = 4 creates FOUR records,
    /// -001 to -004, through a posted receipt — so even these assets are born from Incoming
    /// Goods, with a movement behind each.</summary>
    private async Task<CreateAssetItemResult> CreateAssetsAsync(CreateAssetItemRequest request, CancellationToken ct)
    {
        ops.RequirePermission(PermissionCodes.Inventory.Receive,
            "Adding assets posts a receipt, which needs the Incoming Goods permission.");
        var errors = new List<ApiError>();
        var prefix = AssetRules.NormalisePrefix(request.CodePrefix);
        if (request.CategoryId is null) errors.Add(Field("Choose a category.", nameof(request.CategoryId)));
        if (prefix is null) errors.Add(Field("Code prefix is required.", nameof(request.CodePrefix)));
        else if (!AssetRules.IsValidPrefix(prefix))
            errors.Add(Field("Use letters, digits and hyphens only, up to 30 characters.", nameof(request.CodePrefix)));
        if (request.LocationId is null) errors.Add(Field("Choose the room it is in.", nameof(request.LocationId)));
        if (request.PurchaseDate is null) errors.Add(Field("Purchase date is required.", nameof(request.PurchaseDate)));
        if (request.UnitCost is null) errors.Add(Field("Purchase cost each is required.", nameof(request.UnitCost)));
        if (request.Quantity is not > 0) errors.Add(Field("How many are you adding?", nameof(request.Quantity)));
        if (request.Quantity > 1 && !string.IsNullOrWhiteSpace(request.SerialNumber))
            errors.Add(Field("Serial numbers are per unit: add them from each item page.", nameof(request.SerialNumber)));
        if (errors.Count > 0) throw new AppValidationException(errors);

        var receipt = new SaveGoodsReceiptRequest(
            request.PurchaseDate!.Value, request.SupplierId is null, request.SupplierId, null, null,
            request.LocationId, "Added from the New item form",
            [
                new SaveGoodsReceiptLineRequest((byte)ItemKind.Asset, null, request.Quantity!.Value, request.UnitCost,
                    null, request.ItemName, request.CategoryId, prefix, Trim(request.BrandModel),
                    Trim(request.Description), Trim(request.SerialNumber), request.LocationId,
                    request.Condition ?? (byte)AssetCondition.Good, request.PurchaseDate, request.WarrantyUntil,
                    Trim(request.VendorName), Trim(request.Remarks))
            ],
            Post: true, ConfirmMissingCost: true);
        var posted = await SaveReceiptAsync(null, receipt, GoodsReceiptSource.ItemForm, ct);
        var line = posted.Lines[0];
        var ids = await db.AssetDetails.AsNoTracking()
            .Where(d => d.GoodsReceiptLineId == line.GoodsReceiptLineId)
            .OrderBy(d => d.CodeNumber).Select(d => d.InventoryItemId).ToListAsync(ct);
        var codes = await db.InventoryItems.AsNoTracking().Where(i => ids.Contains(i.InventoryItemId))
            .OrderBy(i => i.ItemCode).Select(i => i.ItemCode).ToListAsync(ct);
        return new CreateAssetItemResult(ids, codes, posted.ReceiptNumber, posted.GoodsReceiptId, posted.TotalAmount);
    }

    private static ApiError Field(string message, string field) =>
        new(ErrorCodes.ValidationError, message, char.ToLowerInvariant(field[0]) + field[1..]);

    private static string? Trim(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private async Task<AssetCategory?> CategoryOrNullAsync(int? categoryId, CancellationToken ct)
    {
        if (categoryId is null) return null;
        return await db.AssetCategories.FirstOrDefaultAsync(c => c.AssetCategoryId == categoryId, ct)
               ?? throw new NotFoundException("AssetCategory", categoryId);
    }

    private async Task EnsureRoomAsync(int? roomId, CancellationToken ct)
    {
        if (roomId is not null && !await db.AssetLocations.AnyAsync(r => r.AssetLocationId == roomId, ct))
        {
            throw new NotFoundException("AssetLocation", roomId);
        }
    }

    private async Task EnsureSupplierAsync(int? supplierId, CancellationToken ct)
    {
        if (supplierId is not null && !await db.Suppliers.AnyAsync(s => s.SupplierId == supplierId, ct))
        {
            throw new NotFoundException("Supplier", supplierId);
        }
    }

    // ── Editing ─────────────────────────────────────────────────────────────

    public async Task<AssetItemSaveResult> UpdateItemAsync(int itemId, UpdateAssetItemRequest request,
        CancellationToken ct = default)
    {
        var item = await db.InventoryItems.Include(i => i.AssetDetail).Include(i => i.AssetCategory)
            .FirstOrDefaultAsync(i => i.InventoryItemId == itemId, ct)
            ?? throw new NotFoundException("InventoryItem", itemId);
        var category = await CategoryOrNullAsync(request.CategoryId, ct);
        await EnsureSupplierAsync(request.SupplierId, ct);
        var warnings = new List<string>();
        var changed = new List<string>();
        void Track(string name, object? before, object? after)
        {
            if (!Equals(before, after)) changed.Add(name);
        }

        Track("name", item.ItemName, request.ItemName.Trim());
        item.ItemName = request.ItemName.Trim();
        Track("category", item.AssetCategoryId, category?.AssetCategoryId);
        if (category is not null || item.ItemType != InventoryItemType.Service)
        {
            if (category is null && item.ItemType == InventoryItemType.Goods)
            {
                throw new AppValidationException([Field("Choose a category.", nameof(request.CategoryId))]);
            }
            item.AssetCategoryId = category?.AssetCategoryId;
            item.Category = category?.CategoryName;
        }
        Track("description", item.Description, Trim(request.Description));
        item.Description = Trim(request.Description);
        Track("brand", item.BrandModel, Trim(request.BrandModel));
        item.BrandModel = Trim(request.BrandModel);
        Track("cost", item.UnitCost, request.UnitCost);
        item.UnitCost = request.UnitCost;
        item.SupplierId = request.SupplierId;
        Track("vendor", item.VendorName, Trim(request.VendorName));
        item.VendorName = Trim(request.VendorName);
        item.Remarks = Trim(request.Remarks);

        if (item.Kind == ItemKind.Asset && item.AssetDetail is { } d)
        {
            var serial = Trim(request.SerialNumber);
            if (serial is not null && !string.Equals(serial, d.SerialNumber, StringComparison.OrdinalIgnoreCase))
            {
                var check = await CheckSerialAsync(serial, itemId, ct);
                // A duplicate serial warns rather than blocks: cheap items sometimes repeat.
                if (check.Duplicate) warnings.Add($"This serial number is already on {check.ExistingCode}.");
            }
            Track("serial number", d.SerialNumber, serial);
            d.SerialNumber = serial;
            if (request.Condition is { } condition)
            {
                Track("condition", d.Condition, (AssetCondition)condition);
                d.Condition = (AssetCondition)condition;
            }
            if (request.PurchaseDate is { } bought && bought > Today)
            {
                throw new AppValidationException([Field("Purchase date cannot be in the future.", nameof(request.PurchaseDate))]);
            }
            Track("purchase date", d.PurchaseDate, request.PurchaseDate);
            d.PurchaseDate = request.PurchaseDate;
            Track("warranty", d.WarrantyUntil, request.WarrantyUntil);
            d.WarrantyUntil = request.WarrantyUntil;
        }
        else
        {
            if (item.ItemType == InventoryItemType.Goods)
            {
                if (string.IsNullOrWhiteSpace(request.Unit))
                    throw new AppValidationException([Field("Unit is required.", nameof(request.Unit))]);
                await EnsureRoomAsync(request.KeptInLocationId, ct);
                Track("unit", item.Unit, request.Unit.Trim());
                item.Unit = request.Unit.Trim();
                Track("reorder level", item.ReorderLevel, request.ReorderLevel ?? 0m);
                item.ReorderLevel = request.ReorderLevel ?? 0m;
                Track("kept in", item.KeptInLocationId, request.KeptInLocationId);
                item.KeptInLocationId = request.KeptInLocationId;
            }
            if (request.HsnSacCode is not null)
                item.HsnSacCode = string.IsNullOrWhiteSpace(request.HsnSacCode) ? null : request.HsnSacCode.Trim();
            if (request.SellingRate is { } rate) item.SellingRate = rate;
            if (request.IsNonTaxable is { } nonTaxable) item.IsNonTaxable = nonTaxable;
            if (request.TaxPercent is { } tax) item.TaxPercent = tax;
            if (item.IsNonTaxable) item.TaxPercent = 0m;
        }

        if (changed.Count > 0)
        {
            ops.AddEvent(itemId, "EDITED", Today, "Details edited: " + string.Join(", ", changed));
        }
        await db.SaveChangesAsync(ct);
        return new AssetItemSaveResult(await GetItemAsync(itemId, ct), warnings);
    }

    /// <summary>
    /// Items are made inactive, never deleted. An asset leaves the register through a
    /// movement (scrapped, lost, returned) — so only a retired asset can be made inactive.
    /// A consumable must have nothing on hand, so stock never vanishes without a row.
    /// </summary>
    public async Task<AssetItemDetailDto> SetActiveAsync(int itemId, bool active, string? reason,
        CancellationToken ct = default)
    {
        var item = await db.InventoryItems.Include(i => i.AssetDetail)
            .FirstOrDefaultAsync(i => i.InventoryItemId == itemId, ct)
            ?? throw new NotFoundException("InventoryItem", itemId);
        if (item.IsActive == active) return await GetItemAsync(itemId, ct);
        if (!active)
        {
            if (item.Kind == ItemKind.Asset && !Retired(item.AssetDetail))
            {
                throw new BusinessRuleException(
                    "An asset leaves the register through Outgoing Goods (scrapped, lost or returned), " +
                    "or by reversing the receipt that created it by mistake.");
            }
            if (item.Kind == ItemKind.Consumable && item.ItemType == InventoryItemType.Goods)
            {
                var onHand = (await ops.OnHandAsync([itemId], ct))[itemId];
                if (onHand != 0)
                {
                    throw new BusinessRuleException(
                        $"{Qty(onHand)} {item.Unit} is still on hand. Issue it or record a stock count first.");
                }
            }
        }
        item.IsActive = active;
        ops.AddEvent(itemId, active ? "ACTIVATED" : "DEACTIVATED", Today,
            active ? "Made active again" : "Made inactive" + (string.IsNullOrWhiteSpace(reason) ? "" : ": " + reason.Trim()));
        await db.SaveChangesAsync(ct);
        return await GetItemAsync(itemId, ct);
    }

    public async Task<NextCodesDto> NextCodesAsync(string prefix, int count, CancellationToken ct = default)
    {
        var clean = AssetRules.NormalisePrefix(prefix);
        if (clean is null || !AssetRules.IsValidPrefix(clean))
        {
            throw new AppValidationException([Field("Use letters, digits and hyphens only.", "prefix")]);
        }
        count = Math.Clamp(count, 1, 500);
        var numbers = await ops.PeekCodeNumbersAsync(clean, count, null, ct);
        var codes = numbers.Select(n => AssetRules.AssetCode(clean, n)).ToList();
        return new NextCodesDto(clean, count, codes, AssetRules.CodeRange(codes));
    }

    public async Task<SerialCheckDto> CheckSerialAsync(string serial, int? excludeItemId, CancellationToken ct = default)
    {
        var s = serial.Trim();
        var hit = await db.AssetDetails.AsNoTracking()
            .Where(d => d.SerialNumber == s && d.InventoryItemId != (excludeItemId ?? 0))
            .Select(d => new { d.InventoryItemId, d.InventoryItem.ItemCode }).FirstOrDefaultAsync(ct);
        return new SerialCheckDto(hit is not null, hit?.ItemCode, hit?.InventoryItemId);
    }

    // ── Repairs ─────────────────────────────────────────────────────────────

    private async Task<AssetRepairDto> RepairDtoAsync(AssetRepair r, CancellationToken ct)
    {
        var number = await db.GoodsIssues.Where(g => g.GoodsIssueId == r.GoodsIssueId)
            .Select(g => g.IssueNumber).FirstOrDefaultAsync(ct);
        return new AssetRepairDto(r.AssetRepairId, r.GoodsIssueId, number, r.SentOn, r.RepairVendor,
            r.Description, r.ExpectedCost, r.Cost, r.IsOpen, r.ClosedOn, r.ClosedNote);
    }

    public async Task<List<AssetRepairDto>> GetRepairsAsync(int itemId, CancellationToken ct = default)
    {
        await EnsureItemAsync(itemId, ct);
        return await db.AssetRepairs.AsNoTracking()
            .Where(r => r.InventoryItemId == itemId)
            .OrderByDescending(r => r.SentOn).ThenByDescending(r => r.AssetRepairId)
            .Select(r => new AssetRepairDto(r.AssetRepairId, r.GoodsIssueId,
                db.GoodsIssues.Where(g => g.GoodsIssueId == r.GoodsIssueId).Select(g => g.IssueNumber).FirstOrDefault(),
                r.SentOn, r.RepairVendor, r.Description, r.ExpectedCost, r.Cost, r.IsOpen, r.ClosedOn, r.ClosedNote))
            .ToListAsync(ct);
    }

    /// <summary>"A repair that is finished is closed from the item page, which writes a
    /// matching movement back in" (§7.2): the asset goes back to In use / In store.</summary>
    public async Task<AssetItemDetailDto> CloseRepairAsync(int itemId, long repairId, CloseRepairRequest request,
        CancellationToken ct = default)
    {
        var closedOn = request.ClosedOn ?? Today;
        await InTransactionAsync(async () =>
        {
            var repair = await db.AssetRepairs.FirstOrDefaultAsync(
                r => r.AssetRepairId == repairId && r.InventoryItemId == itemId, ct)
                ?? throw new NotFoundException("AssetRepair", repairId);
            if (!repair.IsOpen) throw new BusinessRuleException("This repair is already closed.");
            if (closedOn > Today) throw new BusinessRuleException("The return date cannot be in the future.");
            if (closedOn < repair.SentOn) throw new BusinessRuleException("It cannot come back before it was sent.");
            var detail = await db.AssetDetails.FirstAsync(d => d.InventoryItemId == itemId, ct);
            var rooms = await RoomsAsync(ct);
            repair.IsOpen = false;
            repair.ClosedOn = closedOn;
            repair.Cost = request.Cost;
            repair.ClosedNote = Trim(request.Note);
            if (detail.Status == AssetStatus.InRepair) detail.Status = repair.ReturnStatus;
            if (request.Condition is { } condition) detail.Condition = (AssetCondition)condition;
            ops.AddEvent(itemId, "REPAIR_CLOSED", closedOn,
                "Back from repair" + (request.Cost is { } cost ? $" ({Money(cost)})" : ""),
                repair.RepairVendor, rooms.TryGetValue(detail.AssetLocationId, out var room) ? room.LocationName : null);
            await db.SaveChangesAsync(ct);
            return true;
        }, ct);
        return await GetItemAsync(itemId, ct);
    }

    // ── Labels ──────────────────────────────────────────────────────────────

    /// <summary>The label sheet (§9): one label per asset still in the register. Consumables
    /// are skipped — there is no single thing to stick a label on — and counted.</summary>
    public async Task<AssetLabelSheetDto> GetLabelsAsync(AssetItemListRequest request, CancellationToken ct = default)
    {
        var query = await FilterAsync(request, ct);
        var items = await Sort(query, request).Include(i => i.AssetDetail)
            .Take(MaxExportRows).ToListAsync(ct);
        var rooms = await RoomsAsync(ct);
        var (owner, email, _) = await LabelSettingsAsync(ct);
        var labels = new List<AssetLabelDto>();
        int skippedConsumables = 0, skippedRetired = 0;
        foreach (var item in items)
        {
            if (item.Kind != ItemKind.Asset || item.AssetDetail is null) { skippedConsumables++; continue; }
            if (Retired(item.AssetDetail) || !item.IsActive) { skippedRetired++; continue; }
            var d = item.AssetDetail;
            var room = rooms.TryGetValue(d.AssetLocationId, out var r) ? r.LocationName : null;
            labels.Add(new AssetLabelDto(item.InventoryItemId, item.ItemCode, item.ItemName, item.BrandModel,
                d.SerialNumber, room,
                AssetRules.QrText(item.ItemCode, item.ItemName, item.BrandModel, room, d.PurchaseDate, owner, email),
                d.LabelPrintedUtc));
        }
        return new AssetLabelSheetDto(labels, skippedConsumables, skippedRetired);
    }

    /// <summary>Records when each label was printed — what empties the "No QR printed"
    /// card. Reprinting is always allowed; the code never changes.</summary>
    public async Task<int> MarkLabelsPrintedAsync(MarkLabelsPrintedRequest request, CancellationToken ct = default)
    {
        var ids = request.InventoryItemIds.Distinct().Take(MaxExportRows).ToList();
        var details = await db.AssetDetails.Where(d => ids.Contains(d.InventoryItemId)).ToListAsync(ct);
        var now = clock.UtcNow;
        foreach (var d in details)
        {
            ops.AddEvent(d.InventoryItemId, "LABEL_PRINTED", Today,
                d.LabelPrintedUtc is null ? "QR label printed" : "QR label reprinted");
            d.LabelPrintedUtc = now;
        }
        await db.SaveChangesAsync(ct);
        return details.Count;
    }

    // ── Documents ───────────────────────────────────────────────────────────

    private const long MaxDocumentBytes = 10 * 1024 * 1024;
    private const int MaxDocuments = 20;

    private static readonly Dictionary<string, string> AllowedDocumentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        [".pdf"] = "application/pdf",
        [".jpg"] = "image/jpeg",
        [".jpeg"] = "image/jpeg",
        [".png"] = "image/png"
    };

    public async Task<List<ItemDocumentDto>> GetDocumentsAsync(int itemId, CancellationToken ct = default)
    {
        await EnsureItemAsync(itemId, ct);
        return await db.ItemDocuments.AsNoTracking()
            .Where(x => x.InventoryItemId == itemId)
            .OrderByDescending(x => x.UploadedUtc)
            .Select(x => new ItemDocumentDto(x.ItemDocumentId, (byte)x.DocumentType, x.FileName, x.ContentType,
                x.FileSizeBytes,
                db.Users.IgnoreQueryFilters().Where(u => u.UserId == x.UploadedByUserId).Select(u => u.FullName).FirstOrDefault(),
                x.UploadedUtc))
            .ToListAsync(ct);
    }

    public async Task<ItemDocumentDto> AddDocumentAsync(int itemId, byte documentType, AttachmentUpload upload,
        CancellationToken ct = default)
    {
        await EnsureItemAsync(itemId, ct);
        if (documentType is < 1 or > 4) throw new BusinessRuleException("Choose what the document is.");
        if (await db.ItemDocuments.CountAsync(x => x.InventoryItemId == itemId, ct) >= MaxDocuments)
        {
            throw new BusinessRuleException($"An item can carry at most {MaxDocuments} documents.");
        }
        if (upload.Length is <= 0 or > MaxDocumentBytes)
        {
            throw new BusinessRuleException("Each file must be between 1 byte and 10 MB.");
        }
        var extension = Path.GetExtension(upload.FileName);
        if (string.IsNullOrEmpty(extension) || !AllowedDocumentTypes.TryGetValue(extension, out var contentType))
        {
            throw new BusinessRuleException("Allowed file types: .pdf .jpg .jpeg .png");
        }
        using var buffer = new MemoryStream();
        await upload.Content.CopyToAsync(buffer, ct);
        var bytes = buffer.ToArray();
        if (bytes.LongLength > MaxDocumentBytes || !FileTypeSniffer.Matches(extension, bytes))
        {
            throw new BusinessRuleException("The file content does not match its extension.");
        }
        var stored = $"{Guid.NewGuid():N}{extension.ToLowerInvariant()}";
        var directory = Path.Combine(storageOptions.Value.UploadRoot, "items", itemId.ToString(Invariant));
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, stored);
        await File.WriteAllBytesAsync(path, bytes, ct);
        var doc = new ItemDocument
        {
            InventoryItemId = itemId,
            DocumentType = (ItemDocumentType)documentType,
            FileName = Path.GetFileName(upload.FileName),
            StoredFileName = stored,
            FilePath = path,
            ContentType = contentType,
            FileSizeBytes = bytes.LongLength,
            UploadedByUserId = currentUser.UserId,
            UploadedUtc = clock.UtcNow
        };
        db.ItemDocuments.Add(doc);
        ops.AddEvent(itemId, "DOCUMENT_ADDED", Today, "Document added: " + doc.FileName);
        await db.SaveChangesAsync(ct);
        return new ItemDocumentDto(doc.ItemDocumentId, documentType, doc.FileName, doc.ContentType,
            doc.FileSizeBytes, currentUser.FullName, doc.UploadedUtc);
    }

    public async Task<TicketAttachmentContent> GetDocumentAsync(int itemId, long documentId, CancellationToken ct = default)
    {
        var doc = await db.ItemDocuments.AsNoTracking()
            .FirstOrDefaultAsync(x => x.ItemDocumentId == documentId && x.InventoryItemId == itemId, ct)
            ?? throw new NotFoundException("ItemDocument", documentId);
        if (!File.Exists(doc.FilePath)) throw new NotFoundException("ItemDocument", documentId);
        return new TicketAttachmentContent(await File.ReadAllBytesAsync(doc.FilePath, ct), doc.ContentType, doc.FileName);
    }

    // ── Form options, categories and rooms ──────────────────────────────────

    public async Task<AssetFormOptionsDto> GetFormOptionsAsync(CancellationToken ct = default)
    {
        var (owner, email, alertDays) = await LabelSettingsAsync(ct);
        var units = (await settings.GetStringAsync(AppSettingKeys.Assets.Units, "kg,litre,piece,box,bottle,ream,packet,Nos", ct))
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
        var suppliers = await db.Suppliers.AsNoTracking().Where(s => s.IsActive)
            .OrderBy(s => s.SupplierName).Select(s => new IdNameDto(s.SupplierId, s.SupplierName)).ToListAsync(ct);
        return new AssetFormOptionsDto(
            (await GetCategoriesAsync(ct)).Where(c => c.IsActive).ToList(),
            (await GetLocationsAsync(ct)).Where(l => l.IsActive).ToList(),
            suppliers, units, email, owner, alertDays);
    }

    public async Task<List<AssetCategoryDto>> GetCategoriesAsync(CancellationToken ct = default) =>
        await db.AssetCategories.AsNoTracking()
            .OrderBy(c => c.DisplayOrder).ThenBy(c => c.CategoryName)
            .Select(c => new AssetCategoryDto(c.AssetCategoryId, c.CategoryName, (byte)c.DefaultKind, c.CodePrefix,
                c.UsefulLifeYears, c.DisplayOrder, c.IsActive,
                db.InventoryItems.Count(i => i.AssetCategoryId == c.AssetCategoryId && i.IsActive)))
            .ToListAsync(ct);

    public async Task<AssetCategoryDto> SaveCategoryAsync(int? categoryId, SaveAssetCategoryRequest request,
        CancellationToken ct = default)
    {
        var name = request.CategoryName.Trim();
        var prefix = AssetRules.NormalisePrefix(request.CodePrefix);
        if (prefix is null || !AssetRules.IsValidPrefix(prefix))
        {
            throw new AppValidationException([Field("Use letters, digits and hyphens only.", nameof(request.CodePrefix))]);
        }
        if (await db.AssetCategories.AnyAsync(c => c.CategoryName == name && c.AssetCategoryId != (categoryId ?? 0), ct))
        {
            throw new ConflictException($"A category called {name} already exists.", ErrorCodes.BusinessRuleViolation);
        }
        AssetCategory category;
        if (categoryId is null)
        {
            category = new AssetCategory();
            db.AssetCategories.Add(category);
            category.DisplayOrder = request.DisplayOrder
                ?? (await db.AssetCategories.MaxAsync(c => (int?)c.DisplayOrder, ct) ?? 0) + 1;
        }
        else
        {
            category = await db.AssetCategories.FirstOrDefaultAsync(c => c.AssetCategoryId == categoryId, ct)
                       ?? throw new NotFoundException("AssetCategory", categoryId);
            if (request.DisplayOrder is { } order) category.DisplayOrder = order;
        }
        var renamed = category.CategoryName != name;
        category.CategoryName = name;
        category.DefaultKind = (ItemKind)request.DefaultKind;
        category.CodePrefix = prefix;
        category.UsefulLifeYears = request.UsefulLifeYears;
        category.IsActive = request.IsActive;
        await db.SaveChangesAsync(ct);
        if (renamed && categoryId is not null)
        {
            // The old free-text column the quotation and report screens still read.
            await db.InventoryItems.Where(i => i.AssetCategoryId == category.AssetCategoryId)
                .ExecuteUpdateAsync(s => s.SetProperty(i => i.Category, name), ct);
        }
        return (await GetCategoriesAsync(ct)).First(c => c.AssetCategoryId == category.AssetCategoryId);
    }

    public async Task<List<AssetLocationDto>> GetLocationsAsync(CancellationToken ct = default) =>
        await db.AssetLocations.AsNoTracking()
            .OrderBy(l => l.DisplayOrder).ThenBy(l => l.LocationName)
            .Select(l => new AssetLocationDto(l.AssetLocationId, l.LocationName, l.WorkspaceId,
                db.Workspaces.Where(w => w.WorkspaceId == l.WorkspaceId).Select(w => w.WorkspaceName).FirstOrDefault(),
                l.IsStore, l.DisplayOrder, l.IsActive,
                db.AssetDetails.Count(d => d.AssetLocationId == l.AssetLocationId && d.InventoryItem.IsActive
                    && d.Status != AssetStatus.Scrapped && d.Status != AssetStatus.Lost
                    && d.Status != AssetStatus.ReturnedToSupplier)))
            .ToListAsync(ct);

    public async Task<AssetLocationDto> SaveLocationAsync(int? locationId, SaveAssetLocationRequest request,
        CancellationToken ct = default)
    {
        var name = request.LocationName.Trim();
        if (await db.AssetLocations.AnyAsync(l => l.LocationName == name && l.AssetLocationId != (locationId ?? 0), ct))
        {
            throw new ConflictException($"A room called {name} already exists.", ErrorCodes.BusinessRuleViolation);
        }
        if (request.WorkspaceId is { } wid && !await db.Workspaces.AnyAsync(w => w.WorkspaceId == wid, ct))
        {
            throw new NotFoundException("Workspace", wid);
        }
        AssetLocation room;
        if (locationId is null)
        {
            room = new AssetLocation();
            db.AssetLocations.Add(room);
            room.DisplayOrder = request.DisplayOrder
                ?? (await db.AssetLocations.MaxAsync(l => (int?)l.DisplayOrder, ct) ?? 0) + 1;
        }
        else
        {
            room = await db.AssetLocations.FirstOrDefaultAsync(l => l.AssetLocationId == locationId, ct)
                   ?? throw new NotFoundException("AssetLocation", locationId);
            if (request.DisplayOrder is { } order) room.DisplayOrder = order;
            if (room.IsActive && !request.IsActive)
            {
                var here = await db.AssetDetails.CountAsync(d => d.AssetLocationId == room.AssetLocationId
                    && d.InventoryItem.IsActive && d.Status != AssetStatus.Scrapped && d.Status != AssetStatus.Lost
                    && d.Status != AssetStatus.ReturnedToSupplier, ct);
                if (here > 0)
                {
                    throw new BusinessRuleException($"{here} asset(s) are still in {room.LocationName}. Move them out first.");
                }
            }
        }
        room.LocationName = name;
        room.WorkspaceId = request.WorkspaceId;
        room.IsStore = request.IsStore;
        room.IsActive = request.IsActive;
        await db.SaveChangesAsync(ct);
        return (await GetLocationsAsync(ct)).First(l => l.AssetLocationId == room.AssetLocationId);
    }
}
