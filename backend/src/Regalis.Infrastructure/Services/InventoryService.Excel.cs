using System.Data;
using System.Globalization;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Regalis.Application.Common.Exceptions;
using Regalis.Application.Operations;
using Regalis.Domain.Entities.Identity;
using Regalis.Domain.Entities.Operations;
using Regalis.Domain.Enums;
using Regalis.Infrastructure.Excel;
using Regalis.Shared;
using Regalis.Shared.Constants;

namespace Regalis.Infrastructure.Services;

/// <summary>
/// Excel export and import for the Assets screens (Items, Incoming Goods, Outgoing Goods,
/// Current Stock). Reading and writing the files is <see cref="ExcelWorkbookHelper"/>'s job;
/// this part decides what goes in them and what a valid row is.
///
/// Stock is never imported: an Items import leaves stock alone, and a goods import writes
/// ordinary ledger rows through the same builder the screen's Save uses, so Current Stock
/// moves exactly as if each row had been keyed in.
/// </summary>
public partial class InventoryService
{
    private const string StatusNew = "New";
    private const string StatusUpdate = "Update";
    private const string StatusSkip = "Skip";
    private const string StatusError = "Error";

    private static readonly decimal[] AllowedTaxRates = [0m, 5m, 12m, 18m, 28m];

    // ── Export ──────────────────────────────────────────────────────────────

    /// <summary>The item master as an Excel workbook, with the same filters as the Items
    /// screen (search, category, location, below-reorder) and every matching row —
    /// not just the page on screen. Status uses the screen's thresholds. The columns
    /// match the import template, so an export can be edited and imported back.</summary>
    public async Task<InventoryExportResult?> ExportItemsExcelAsync(InventoryItemListRequest request,
        CancellationToken ct = default)
    {
        var items = await FilterItems(request).OrderBy(i => i.ItemName).ToListAsync(ct);
        var movements = await MovementsByItemAsync(items.Select(i => i.InventoryItemId).ToList(), ct);
        var rows = items.Select(i => ToDto(i, CurrentStock(i, movements)))
            .Where(d => request.BelowReorder != true || d.NeedsReorder)
            .ToList();
        if (rows.Count == 0) return null;
        var locations = await LocationNamesAsync(ct);

        static bool Service(InventoryItemDto d) => d.ItemType == (byte)InventoryItemType.Service;
        ExcelColumn<InventoryItemDto>[] columns =
        [
            new("Item Code", d => d.ItemCode),
            new("Item Name", d => d.ItemName),
            new("Item Type", d => Service(d) ? "Service" : "Goods"),
            new("Category", d => d.Category),
            new("Location", d => locations.GetValueOrDefault(d.LocationId)),
            new("Unit", d => d.Unit),
            new("Unit Cost", d => d.UnitCost, ExcelCellKind.Money),
            new("Current Stock", d => Service(d) ? null : d.CurrentStock, ExcelCellKind.Number),
            new("Reorder Level", d => Service(d) ? null : d.ReorderLevel, ExcelCellKind.Number),
            new("Status", d => Service(d) ? "Service" : StockStatus(d.CurrentStock, d.ReorderLevel)),
            new("Stock Value", d => Service(d) ? null : d.UnitCost * d.CurrentStock, ExcelCellKind.Money),
            new("HSN/SAC Code", d => d.HsnSacCode),
            new("Selling Rate", d => d.SellingRate, ExcelCellKind.Money),
            new("Tax", d => d.IsNonTaxable ? "Non-Taxable" : $"{d.TaxPercent:0.##}%"),
            new("Description", d => d.Description)
        ];
        return Workbook("items", "Inventory Items", columns, rows);
    }

    /// <summary>Incoming or Outgoing Goods with the screen's filters (search, supplier or
    /// item, date range), newest first like the screen, every matching row.</summary>
    public async Task<InventoryExportResult?> ExportGoodsExcelAsync(InventoryTransactionListRequest request,
        byte transactionType, CancellationToken ct = default)
    {
        request.TransactionType = transactionType;
        var rows = await Shape(FilterTransactions(request).OrderByDescending(t => t.InventoryTransactionId))
            .ToListAsync(ct);
        if (rows.Count == 0) return null;

        if (transactionType != (byte)InventoryTransactionType.In)
        {
            ExcelColumn<InventoryTransactionDto>[] outgoing =
            [
                new("Transaction No", t => t.TransactionNumber),
                new("Date", t => t.TransactionDate, ExcelCellKind.Date),
                new("Item Code", t => t.ItemCode),
                new("Item Name", t => t.ItemName),
                new("Unit", t => t.Unit),
                new("Quantity", t => Math.Abs(t.Quantity), ExcelCellKind.Number),
                new("Issued To", t => t.SourceOrDestination),
                new("Reference", t => t.ReferenceNumber),
                new("Remarks", t => t.Notes),
                new("Issued By", t => t.EnteredByName),
                new("Recorded On", t => IstClock.ToIst(t.CreatedDate), ExcelCellKind.DateTime)
            ];
            return Workbook("outgoing_goods", "Outgoing Goods", outgoing, rows);
        }

        // The screen shows each item's balance now, read from the same derivation.
        var itemIds = rows.Select(r => r.InventoryItemId).Distinct().ToList();
        var items = await db.InventoryItems.AsNoTracking()
            .Where(i => itemIds.Contains(i.InventoryItemId)).ToListAsync(ct);
        var movements = await MovementsByItemAsync(itemIds, ct);
        var stock = items.ToDictionary(i => i.InventoryItemId,
            i => (Current: CurrentStock(i, movements), i.ReorderLevel));

        ExcelColumn<InventoryTransactionDto>[] incoming =
        [
            new("Transaction No", t => t.TransactionNumber),
            new("Date", t => t.TransactionDate, ExcelCellKind.Date),
            new("Item Code", t => t.ItemCode),
            new("Item Name", t => t.ItemName),
            new("Unit", t => t.Unit),
            new("Quantity", t => Math.Abs(t.Quantity), ExcelCellKind.Number),
            new("Supplier", t => t.SupplierName ?? t.SourceOrDestination),
            new("PO Number", t => t.PoNumber),
            new("Supplier Invoice No", t => t.PurchaseInvoiceNumber),
            new("Reference", t => t.ReferenceNumber),
            new("Rate", t => t.UnitCost, ExcelCellKind.Money),
            new("Total", t => t.UnitCost * Math.Abs(t.Quantity), ExcelCellKind.Money),
            new("Current Stock", t => stock.TryGetValue(t.InventoryItemId, out var s) ? s.Current : null,
                ExcelCellKind.Number),
            new("Status", t => stock.TryGetValue(t.InventoryItemId, out var s)
                ? StockStatus(s.Current, s.ReorderLevel) : null),
            new("Received By", t => t.EnteredByName),
            new("Remarks", t => t.Notes),
            new("Recorded On", t => IstClock.ToIst(t.CreatedDate), ExcelCellKind.DateTime)
        ];
        return Workbook("incoming_goods", "Incoming Goods", incoming, rows);
    }

    /// <summary>The Current Stock screen: the ledger derivation per item, plus unit cost,
    /// location and stock value from the item master.</summary>
    public async Task<InventoryExportResult?> ExportStockExcelAsync(int? locationId, bool? belowReorder,
        CancellationToken ct = default)
    {
        var rows = await GetStockAsync(locationId, belowReorder, ct);
        if (rows.Count == 0) return null;
        var itemIds = rows.Select(r => r.InventoryItemId).ToList();
        var items = await db.InventoryItems.AsNoTracking()
            .Where(i => itemIds.Contains(i.InventoryItemId))
            .Select(i => new { i.InventoryItemId, i.UnitCost, i.LocationId })
            .ToDictionaryAsync(i => i.InventoryItemId, ct);
        var locations = await LocationNamesAsync(ct);

        decimal? CostOf(InventoryStockDto r) => items.GetValueOrDefault(r.InventoryItemId)?.UnitCost;
        ExcelColumn<InventoryStockDto>[] columns =
        [
            new("Item Code", r => r.ItemCode),
            new("Item Name", r => r.ItemName),
            new("Category", r => r.Category),
            new("Location", r => items.TryGetValue(r.InventoryItemId, out var i)
                ? locations.GetValueOrDefault(i.LocationId) : null),
            new("Unit", r => r.Unit),
            new("Opening Quantity", r => r.OpeningQuantity, ExcelCellKind.Number),
            new("Total In", r => r.TotalIn, ExcelCellKind.Number),
            new("Total Out", r => r.TotalOut, ExcelCellKind.Number),
            new("Damage", r => r.TotalDamage, ExcelCellKind.Number),
            new("Adjustments", r => r.TotalAdjustment, ExcelCellKind.Number),
            new("Current Stock", r => r.CurrentStock, ExcelCellKind.Number),
            new("Reorder Level", r => r.ReorderLevel, ExcelCellKind.Number),
            new("Status", r => StockStatus(r.CurrentStock, r.ReorderLevel)),
            new("Unit Cost", r => CostOf(r), ExcelCellKind.Money),
            new("Stock Value", r => CostOf(r) * r.CurrentStock, ExcelCellKind.Money)
        ];
        return Workbook("current_stock", "Current Stock", columns, rows);
    }

    private InventoryExportResult Workbook<T>(string screen, string sheetName,
        IReadOnlyList<ExcelColumn<T>> columns, IReadOnlyList<T> rows) =>
        new(ExcelWorkbookHelper.Write(sheetName, columns, rows), ExcelWorkbookHelper.ContentType,
            ExcelWorkbookHelper.FileName(screen, clock.UtcNow));

    private async Task<Dictionary<int, string>> LocationNamesAsync(CancellationToken ct) =>
        await db.Locations.AsNoTracking().ToDictionaryAsync(l => l.LocationId, l => l.LocationName, ct);

    /// <summary>The Items screen's badge: Critical at half the reorder level or less,
    /// Low Stock at the reorder level or less, otherwise OK.</summary>
    private static string StockStatus(decimal stock, decimal reorderLevel) =>
        reorderLevel > 0 && stock <= reorderLevel * 0.5m ? "Critical"
        : reorderLevel > 0 && stock <= reorderLevel ? "Low Stock"
        : "OK";

    // ── Import: entry points ────────────────────────────────────────────────

    public async Task<InventoryExportResult> ImportTemplateAsync(string kind, CancellationToken ct = default)
    {
        var spec = await ImportSpecAsync(kind, ct);
        return new InventoryExportResult(
            ExcelWorkbookHelper.Template(spec.SheetName, spec.Columns, spec.Notes),
            ExcelWorkbookHelper.ContentType, $"{spec.Screen}_import_template.xlsx");
    }

    public async Task<InventoryImportResultDto> ImportAsync(string kind, InventoryImportUpload upload,
        string? existingMode, bool save, CancellationToken ct = default)
    {
        var spec = await ImportSpecAsync(kind, ct);
        var mode = ModeFor(kind, existingMode);
        var sheet = ExcelWorkbookHelper.Read(upload.Content, upload.FileName, upload.Length, spec.Columns);

        if (!save)
        {
            var preview = await CheckAsync(kind, sheet, mode, tracked: false, ct);
            return Result(kind, spec, sheet, preview, saved: false, created: 0, updated: 0);
        }

        // Checked again inside the transaction: the preview is advice, this is the rule.
        // Serializable, like a single ledger entry, so a concurrent issue cannot slip
        // between the stock check and the insert.
        var strategy = db.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            db.ChangeTracker.Clear();
            await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
            var checks = await CheckAsync(kind, sheet, mode, tracked: true, ct);
            var valid = checks.Where(c => c.Ok && c.Status != StatusSkip).ToList();
            if (valid.Count == 0)
            {
                throw new BusinessRuleException(
                    "There are no valid rows to import. Fix the rows marked in red and upload the file again.");
            }

            int created = 0, updated = 0;
            if (kind == InventoryImportKinds.Items)
            {
                foreach (var check in valid)
                {
                    if (check.Existing is { } existing)
                    {
                        Apply(existing, check.Item!);
                        updated++;
                    }
                    else
                    {
                        var item = new InventoryItem { ItemCode = check.Item!.ItemCode };
                        Apply(item, check.Item);
                        db.InventoryItems.Add(item);
                        created++;
                    }
                }
            }
            else
            {
                // Numbered in date order, so the ledger reads the way the stock check ran.
                foreach (var check in valid.OrderBy(c => c.Date).ThenBy(c => c.Row.RowNumber))
                {
                    db.InventoryTransactions.Add(await NewEntryAsync(check.MovementItem!, check.Movement!, ct));
                    created++;
                }
            }

            db.AuditLogs.Add(ImportAuditRow(spec, upload.FileName, checks, created, updated));
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            return Result(kind, spec, sheet, checks, saved: true, created, updated);
        });
    }

    public async Task<InventoryExportResult> ImportErrorReportAsync(string kind, InventoryImportUpload upload,
        string? existingMode, CancellationToken ct = default)
    {
        var spec = await ImportSpecAsync(kind, ct);
        var mode = ModeFor(kind, existingMode);
        var sheet = ExcelWorkbookHelper.Read(upload.Content, upload.FileName, upload.Length, spec.Columns);
        var checks = await CheckAsync(kind, sheet, mode, tracked: false, ct);
        var failed = checks.Where(c => !c.Ok).Select(c => (c.Row, string.Join(" ", c.Errors))).ToList();
        if (failed.Count == 0)
        {
            throw new BusinessRuleException("Every row in this file is valid, so there is no error report.");
        }
        return new InventoryExportResult(
            ExcelWorkbookHelper.ErrorReport(spec.SheetName, spec.Columns, failed),
            ExcelWorkbookHelper.ContentType,
            ExcelWorkbookHelper.FileName($"{spec.Screen}_import_errors", clock.UtcNow));
    }

    /// <summary>Items need a choice for codes that exist; updating needs Edit on top of
    /// the Create the route already demands.</summary>
    private string? ModeFor(string kind, string? existingMode)
    {
        if (kind != InventoryImportKinds.Items) return null;
        var mode = string.Equals(existingMode, InventoryImportModes.Update, StringComparison.OrdinalIgnoreCase)
            ? InventoryImportModes.Update
            : InventoryImportModes.Skip;
        if (mode == InventoryImportModes.Update && !currentUser.HasPermission(PermissionCodes.Inventory.Edit))
        {
            throw new ForbiddenException("Updating existing items needs the Inventory Edit permission.");
        }
        return mode;
    }

    private async Task<List<RowCheck>> CheckAsync(string kind, ImportSheet sheet, string? mode, bool tracked,
        CancellationToken ct) => kind switch
    {
        InventoryImportKinds.Items => await CheckItemsAsync(sheet, mode!, tracked, ct),
        InventoryImportKinds.Incoming => await CheckGoodsAsync(sheet, incoming: true, ct),
        _ => await CheckGoodsAsync(sheet, incoming: false, ct)
    };

    // ── Import: templates ───────────────────────────────────────────────────

    private sealed record ImportSpec(string Screen, string SheetName, string EntityName,
        List<ImportColumn> Columns, List<string> Notes);

    private async Task<ImportSpec> ImportSpecAsync(string kind, CancellationToken ct)
    {
        const string limits = "Accepted file: .xlsx (Excel 2007 or later), up to 5 MB and 5,000 rows.";
        const string unknownColumns =
            "Columns this import does not know (for example Status or Recorded On from an export) are ignored.";
        var today = IstClock.IstDateOf(clock.UtcNow);

        if (kind == InventoryImportKinds.Items)
        {
            var locations = await db.Locations.AsNoTracking().OrderBy(l => l.LocationName)
                .Select(l => l.LocationName).ToListAsync(ct);
            return new ImportSpec("items", "Items", nameof(InventoryItem),
            [
                new("Item Code", true,
                    "Unique code, up to 40 characters. An existing item is matched by this code, ignoring " +
                    "upper/lower case and spaces at either end. A code may appear only once in the file.", "C-12"),
                new("Item Name", true, "Up to 150 characters.", "Coffee Beans"),
                new("Item Type", false,
                    "Goods or Service. Blank means Goods for a new item and no change for an existing one. " +
                    "A service holds no stock.", "Goods"),
                new("Category", false,
                    "Free text, up to 60 characters. A category already in use is matched ignoring case; " +
                    "any other value starts a new category.", "Pantry"),
                new("Location", true,
                    "Name or code of an existing location" +
                    (locations.Count > 0 ? ": " + string.Join(", ", locations) : "") + ".",
                    locations.FirstOrDefault() ?? "Main"),
                new("Unit", true, "Unit of measure, up to 20 characters, e.g. Nos, Kg, Ltr, Box.", "Kg"),
                new("Unit Cost", false, "Purchase cost in ₹. A number, 0 or more, at most 2 decimals.", 450m,
                    ExcelCellKind.Money),
                new("Reorder Level", false,
                    "The item shows Low Stock at or below this quantity. A number, 0 or more.", 5m,
                    ExcelCellKind.Number),
                new("HSN/SAC Code", false,
                    "Digits only, up to 20. Format the column as Text in Excel to keep leading zeros.", "090121"),
                new("Selling Rate", false, "Rate quoted on a quotation, in ₹. A number, 0 or more.", 600m,
                    ExcelCellKind.Money),
                new("Tax", false, "Non-Taxable, 0%, 5%, 12%, 18% or 28%.", "5%"),
                new("Description", false, "Up to 500 characters. Printed under the item name on a quotation.",
                    "Arabica, 1 kg pack")
            ],
            [
                "One item per row, starting on row 2. Overwrite or delete the example row.",
                "Stock is NOT imported here. A new item starts with 0 in stock; bring stock in with an " +
                "Incoming Goods import.",
                "When a code already exists you choose on the import screen to update that item or skip it. " +
                "On update, a blank cell keeps the item's current value.",
                unknownColumns,
                limits
            ]);
        }

        if (kind is not (InventoryImportKinds.Incoming or InventoryImportKinds.Outgoing))
        {
            throw new NotFoundException("Import", kind);
        }

        var exampleCode = await db.InventoryItems.AsNoTracking()
            .Where(i => i.ItemType == InventoryItemType.Goods)
            .OrderBy(i => i.ItemCode).Select(i => i.ItemCode).FirstOrDefaultAsync(ct) ?? "C-12";
        var common = new List<string>
        {
            "One movement per row, starting on row 2. Overwrite or delete the example row.",
            "Item Code must be an existing goods item. Services hold no stock and cannot be moved.",
            "Date is the day of the movement: an Excel date, or text as yyyy-mm-dd, dd-mm-yyyy or " +
            "dd/mm/yyyy. It cannot be in the future.",
            "Each row is recorded on the transaction ledger exactly like an entry made on the screen, " +
            "with you as the person who recorded it. Current Stock updates straight away.",
            "A saved row cannot be edited or deleted by importing again. Correct it on the screen, " +
            "which posts a compensating adjustment.",
            unknownColumns,
            limits
        };

        if (kind == InventoryImportKinds.Incoming)
        {
            var supplier = await db.Suppliers.AsNoTracking().OrderBy(s => s.SupplierName)
                .Select(s => s.SupplierName).FirstOrDefaultAsync(ct);
            return new ImportSpec("incoming_goods", "Incoming Goods", nameof(InventoryTransaction),
            [
                new("Item Code", true, "Code of an existing goods item.", exampleCode),
                new("Quantity", true, "Quantity received. Greater than 0, at most 3 decimals.", 10m,
                    ExcelCellKind.Number),
                new("Date", true, "Date received. Not in the future.", today, ExcelCellKind.Date),
                new("Supplier", false,
                    "Name or code of a registered supplier. Leave blank if the goods did not come from one.",
                    supplier),
                new("Reference", false, "Up to 80 characters, e.g. a delivery note number.", "DN-1042"),
                new("Supplier Invoice No", false, "The supplier's own bill number. Up to 80 characters.",
                    "INV-5531"),
                new("Rate", false, "Unit cost in ₹. A number, 0 or more, at most 2 decimals.", 450m,
                    ExcelCellKind.Money),
                new("Remarks", false, "Up to 500 characters.", "Monthly restock")
            ], common);
        }

        common.Insert(2,
            "Stock may not go below zero. For each item, rows are applied to today's stock in date order " +
            "(then file order); a row that would take stock below zero is rejected.");
        return new ImportSpec("outgoing_goods", "Outgoing Goods", nameof(InventoryTransaction),
        [
            new("Item Code", true, "Code of an existing goods item.", exampleCode),
            new("Quantity", true, "Quantity issued. Greater than 0, at most 3 decimals.", 2m,
                ExcelCellKind.Number),
            new("Date", true, "Date issued. Not in the future.", today, ExcelCellKind.Date),
            new("Issued To", false, "Person, department or purpose. Up to 150 characters.", "Pantry - 3rd floor"),
            new("Reference", false, "Up to 80 characters.", "REQ-208"),
            new("Remarks", false, "Up to 500 characters.", "Weekly issue")
        ], common);
    }

    // ── Import: row checks ──────────────────────────────────────────────────

    private sealed class RowCheck(ImportSheetRow row)
    {
        public ImportSheetRow Row { get; } = row;
        public List<string> Errors { get; } = [];
        public List<string> Notes { get; } = [];
        public string Status { get; set; } = StatusNew;
        public bool Ok => Errors.Count == 0;

        // Items
        public SaveInventoryItemRequest? Item { get; set; }
        public InventoryItem? Existing { get; set; }

        // Goods
        public CreateInventoryTransactionRequest? Movement { get; set; }
        public InventoryItem? MovementItem { get; set; }
        public DateOnly Date { get; set; }
    }

    private static string CodeKey(string code) => code.Trim().ToUpperInvariant();

    private async Task<List<RowCheck>> CheckItemsAsync(ImportSheet sheet, string mode, bool tracked,
        CancellationToken ct)
    {
        var all = tracked
            ? await db.InventoryItems.ToListAsync(ct)
            : await db.InventoryItems.AsNoTracking().ToListAsync(ct);
        var existingByCode = all.GroupBy(i => CodeKey(i.ItemCode)).ToDictionary(g => g.Key, g => g.First());
        // The unique index covers soft-deleted rows too, so their codes cannot be reused.
        var deletedCodes = (await db.InventoryItems.IgnoreQueryFilters().AsNoTracking()
                .Where(i => i.IsDeleted).Select(i => i.ItemCode).ToListAsync(ct))
            .Select(CodeKey).ToHashSet();
        var withLedger = (await db.InventoryTransactions.AsNoTracking()
            .Select(t => t.InventoryItemId).Distinct().ToListAsync(ct)).ToHashSet();
        var locations = await db.Locations.AsNoTracking()
            .Select(l => new { l.LocationId, l.LocationName, l.LocationCode }).ToListAsync(ct);
        var categories = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var category in all.Select(i => i.Category?.Trim()).Where(c => !string.IsNullOrEmpty(c)))
        {
            categories.TryAdd(category!, category!);
        }
        var newCategories = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var validator = new SaveInventoryItemRequestValidator();

        var checks = sheet.Rows.Select(r => new RowCheck(r)).ToList();
        foreach (var group in checks.Where(c => c.Row.Text("Item Code") is not null)
                     .GroupBy(c => CodeKey(c.Row.Text("Item Code")!)).Where(g => g.Count() > 1))
        {
            var rows = string.Join(", ", group.Select(c => c.Row.RowNumber));
            foreach (var check in group)
            {
                check.Errors.Add($"Item code {group.Key} appears more than once in this file (rows {rows}).");
            }
        }

        foreach (var check in checks)
        {
            var row = check.Row;
            var errors = check.Errors;

            var code = row.Text("Item Code") is { } rawCode ? CodeKey(rawCode) : null;
            if (code is null) errors.Add("Item Code is required.");
            else if (code.Length > 40) errors.Add("Item Code is longer than 40 characters.");

            var name = RequiredText(row, "Item Name", 150, errors);
            var unit = RequiredText(row, "Unit", 20, errors);

            byte? itemType = null;
            if (row.Text("Item Type") is { } typeText)
            {
                if (typeText.Equals("Goods", StringComparison.OrdinalIgnoreCase)) itemType = (byte)InventoryItemType.Goods;
                else if (typeText.Equals("Service", StringComparison.OrdinalIgnoreCase)) itemType = (byte)InventoryItemType.Service;
                else errors.Add($"Item Type \"{typeText}\" is not valid. Use Goods or Service.");
            }

            string? category = null;
            if (row.Text("Category") is { } categoryText)
            {
                if (categoryText.Length > 60) errors.Add("Category is longer than 60 characters.");
                else if (categories.TryGetValue(categoryText, out var known)) category = known;
                else
                {
                    newCategories.TryAdd(categoryText, categoryText);
                    category = newCategories[categoryText];
                    check.Notes.Add($"New category \"{category}\".");
                }
            }

            var locationId = 0;
            if (row.Text("Location") is not { } locationText)
            {
                errors.Add("Location is required.");
            }
            else
            {
                var location = locations.FirstOrDefault(l =>
                    string.Equals(l.LocationName.Trim(), locationText, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(l.LocationCode.Trim(), locationText, StringComparison.OrdinalIgnoreCase));
                if (location is null)
                {
                    errors.Add($"Unknown location \"{locationText}\". Use one of: " +
                               string.Join(", ", locations.Select(l => l.LocationName)) + ".");
                }
                else
                {
                    locationId = location.LocationId;
                }
            }

            var unitCost = OptionalNumber(row, "Unit Cost", 2, errors);
            var reorderLevel = OptionalNumber(row, "Reorder Level", 3, errors);
            var sellingRate = OptionalNumber(row, "Selling Rate", 2, errors);

            var hsn = row.Text("HSN/SAC Code");
            if (hsn is not null && (hsn.Length > 20 || !hsn.All(char.IsAsciiDigit)))
            {
                errors.Add("HSN/SAC Code must be digits only, up to 20.");
            }

            bool? nonTaxable = null;
            decimal? taxPercent = null;
            if (row.Raw("Tax") is { } rawTax)
            {
                if (TryTax(rawTax, out var nt, out var rate))
                {
                    nonTaxable = nt;
                    taxPercent = rate;
                }
                else
                {
                    errors.Add($"Tax \"{ExcelWorkbookHelper.AsText(rawTax)}\" is not valid. " +
                               "Use Non-Taxable, 0%, 5%, 12%, 18% or 28%.");
                }
            }

            var description = row.Text("Description");
            if (description is { Length: > 500 }) errors.Add("Description is longer than 500 characters.");

            if (code is not null && existingByCode.TryGetValue(code, out var existing) && existing.Kind == ItemKind.Asset)
            {
                errors.Add($"{existing.ItemCode} is an asset: edit it on Assets → Items.");
            }
            else if (code is not null && existingByCode.TryGetValue(code, out existing))
            {
                check.Existing = existing;
                if (mode == InventoryImportModes.Skip)
                {
                    check.Status = StatusSkip;
                    check.Notes.Add("Already exists — skipped.");
                }
                else
                {
                    check.Status = StatusUpdate;
                    if (itemType == (byte)InventoryItemType.Service && existing.ItemType != InventoryItemType.Service
                        && withLedger.Contains(existing.InventoryItemId))
                    {
                        errors.Add("This item has stock movements, so it cannot become a service.");
                    }
                }
            }
            else if (code is not null && deletedCodes.Contains(code))
            {
                errors.Add($"Code {code} belongs to a deleted item and cannot be reused.");
            }

            if (!check.Ok || check.Status == StatusSkip) continue;

            // On update a blank cell keeps what the item has; stock (opening quantity) is
            // never taken from the file.
            var current = check.Existing;
            var request = new SaveInventoryItemRequest(
                code!, name!, category ?? current?.Category, unit!,
                OpeningQuantity: current?.OpeningQuantity ?? 0m,
                ReorderLevel: reorderLevel ?? current?.ReorderLevel ?? 0m,
                UnitCost: unitCost ?? current?.UnitCost,
                LocationId: locationId,
                Description: description ?? current?.Description,
                ItemType: itemType,
                HsnSacCode: hsn,
                SellingRate: sellingRate,
                TaxPercent: taxPercent,
                IsNonTaxable: nonTaxable);
            errors.AddRange(validator.Validate(request).Errors.Select(e => e.ErrorMessage).Distinct());
            check.Item = request;
        }
        return checks;
    }

    private async Task<List<RowCheck>> CheckGoodsAsync(ImportSheet sheet, bool incoming, CancellationToken ct)
    {
        var itemsByCode = (await db.InventoryItems.AsNoTracking().ToListAsync(ct))
            .GroupBy(i => CodeKey(i.ItemCode)).ToDictionary(g => g.Key, g => g.First());
        var suppliers = incoming
            ? await db.Suppliers.AsNoTracking()
                .Select(s => new { s.SupplierId, s.SupplierName, s.SupplierCode }).ToListAsync(ct)
            : [];
        var today = IstClock.IstDateOf(clock.UtcNow);
        var type = incoming ? InventoryTransactionType.In : InventoryTransactionType.Out;
        var validator = new CreateInventoryTransactionRequestValidator();

        var checks = sheet.Rows.Select(r => new RowCheck(r)).ToList();
        foreach (var check in checks)
        {
            var row = check.Row;
            var errors = check.Errors;

            InventoryItem? item = null;
            if (row.Text("Item Code") is not { } code)
            {
                errors.Add("Item Code is required.");
            }
            else if (!itemsByCode.TryGetValue(CodeKey(code), out item))
            {
                errors.Add($"Unknown item code \"{code}\".");
            }
            else if (item.ItemType == InventoryItemType.Service)
            {
                errors.Add($"{item.ItemCode} is a service, so it has no stock to move.");
                item = null;
            }
            else if (item.Kind == ItemKind.Asset)
            {
                errors.Add($"{item.ItemCode} is an asset: it moves through Assets → Incoming / Outgoing Goods.");
                item = null;
            }

            decimal quantity = 0m;
            var rawQuantity = row.Raw("Quantity");
            if (rawQuantity is null) errors.Add("Quantity is required.");
            else if (!ExcelWorkbookHelper.TryDecimal(rawQuantity, out quantity)) errors.Add("Quantity must be a number.");
            else if (quantity <= 0) errors.Add("Quantity must be greater than 0.");
            else if (ExcelWorkbookHelper.Scale(quantity) > 3) errors.Add("Quantity can have at most 3 decimals.");

            var date = default(DateOnly);
            var rawDate = row.Raw("Date");
            if (rawDate is null) errors.Add("Date is required.");
            else if (!ExcelWorkbookHelper.TryDate(rawDate, out date))
                errors.Add("Date is not a valid date. Use an Excel date or yyyy-mm-dd, dd-mm-yyyy or dd/mm/yyyy.");
            else if (date > today) errors.Add($"Date {date:dd-MMM-yyyy} is in the future.");
            else if (date.Year < 2000) errors.Add($"Date {date:dd-MMM-yyyy} is before 2000 — check the year.");

            int? supplierId = null;
            string? issuedTo = null, invoice = null;
            decimal? rate = null;
            if (incoming)
            {
                if (row.Text("Supplier") is { } supplierText)
                {
                    var supplier = suppliers.FirstOrDefault(s =>
                        string.Equals(s.SupplierName.Trim(), supplierText, StringComparison.OrdinalIgnoreCase)
                        || string.Equals(s.SupplierCode.Trim(), supplierText, StringComparison.OrdinalIgnoreCase));
                    if (supplier is null) errors.Add($"Unknown supplier \"{supplierText}\".");
                    else supplierId = supplier.SupplierId;
                }
                invoice = OptionalText(row, "Supplier Invoice No", 80, errors);
                rate = OptionalNumber(row, "Rate", 2, errors);
            }
            else
            {
                issuedTo = OptionalText(row, "Issued To", 150, errors);
            }
            var reference = OptionalText(row, "Reference", 80, errors);
            var remarks = OptionalText(row, "Remarks", 500, errors);

            if (!check.Ok) continue;
            var request = new CreateInventoryTransactionRequest(
                item!.InventoryItemId, (byte)type, quantity, date, issuedTo, reference, rate,
                DamageDescription: null, DamageCause: null, Notes: remarks,
                SupplierId: supplierId, PurchaseOrderId: null, PurchaseInvoiceNumber: invoice,
                AttachmentPath: null);
            errors.AddRange(validator.Validate(request).Errors.Select(e => e.ErrorMessage).Distinct());
            check.Movement = request;
            check.MovementItem = item;
            check.Date = date;
        }

        if (!incoming)
        {
            // Stock may not go below zero: start from each item's stock now and apply the
            // file's valid rows in date order, then file order. A refused row does not move
            // the running balance, because it will not be saved.
            var byItem = checks.Where(c => c.Ok).GroupBy(c => c.MovementItem!.InventoryItemId).ToList();
            var movements = await MovementsByItemAsync(byItem.Select(g => g.Key).ToList(), ct);
            foreach (var group in byItem)
            {
                var item = group.First().MovementItem!;
                var balance = CurrentStock(item, movements);
                foreach (var check in group.OrderBy(c => c.Date).ThenBy(c => c.Row.RowNumber))
                {
                    var quantity = check.Movement!.Quantity;
                    if (quantity > balance)
                    {
                        check.Errors.Add(
                            $"Only {balance:0.###} {item.Unit} of {item.ItemCode} is in stock at this point " +
                            $"(current stock less earlier rows in this file), so {quantity:0.###} cannot be issued.");
                    }
                    else
                    {
                        balance -= quantity;
                    }
                }
            }
        }
        return checks;
    }

    private static string? RequiredText(ImportSheetRow row, string header, int maxLength, List<string> errors)
    {
        var value = row.Text(header);
        if (value is null) errors.Add($"{header} is required.");
        else if (value.Length > maxLength) errors.Add($"{header} is longer than {maxLength} characters.");
        return value;
    }

    private static string? OptionalText(ImportSheetRow row, string header, int maxLength, List<string> errors)
    {
        var value = row.Text(header);
        if (value is { } v && v.Length > maxLength) errors.Add($"{header} is longer than {maxLength} characters.");
        return value;
    }

    private static decimal? OptionalNumber(ImportSheetRow row, string header, int decimals, List<string> errors)
    {
        var raw = row.Raw(header);
        if (raw is null) return null;
        if (!ExcelWorkbookHelper.TryDecimal(raw, out var value))
        {
            errors.Add($"{header} must be a number.");
            return null;
        }
        if (value < 0)
        {
            errors.Add($"{header} cannot be negative.");
            return null;
        }
        if (ExcelWorkbookHelper.Scale(value) > decimals)
        {
            errors.Add($"{header} can have at most {decimals} decimals.");
            return null;
        }
        return value;
    }

    /// <summary>Non-Taxable, or one of the GST rates, typed as 18, 18% or a cell
    /// formatted as a percentage (0.18).</summary>
    private static bool TryTax(object raw, out bool nonTaxable, out decimal percent)
    {
        nonTaxable = false;
        percent = 0m;
        var text = ExcelWorkbookHelper.AsText(raw) ?? "";
        var compact = new string(text.Where(char.IsLetterOrDigit).ToArray()).ToLowerInvariant();
        if (compact is "nontaxable" or "nt" or "exempt")
        {
            nonTaxable = true;
            return true;
        }

        decimal value;
        if (raw is double d)
        {
            value = (decimal)d;
            if (value is > 0m and < 1m) value *= 100m;
        }
        else if (!decimal.TryParse(text.Replace("%", "").Trim(), NumberStyles.Number,
                     CultureInfo.InvariantCulture, out value))
        {
            return false;
        }
        value = Math.Round(value, 2);
        if (!AllowedTaxRates.Contains(value)) return false;
        percent = value;
        return true;
    }

    // ── Import: result and audit ────────────────────────────────────────────

    private static InventoryImportResultDto Result(string kind, ImportSpec spec, ImportSheet sheet,
        List<RowCheck> checks, bool saved, int created, int updated)
    {
        var rows = checks.Select(c => new InventoryImportRowDto(
            c.Row.RowNumber,
            c.Ok ? c.Status : StatusError,
            c.Errors,
            c.Notes,
            spec.Columns.ToDictionary(col => col.Header, col => ExcelWorkbookHelper.AsText(c.Row.Raw(col.Header)))))
            .ToList();
        return new InventoryImportResultDto(
            kind,
            spec.Columns.Select(c => c.Header).ToList(),
            sheet.IgnoredColumns,
            TotalRows: checks.Count,
            ValidRows: checks.Count(c => c.Ok && c.Status != StatusSkip),
            ErrorRows: checks.Count(c => !c.Ok),
            SkippedRows: checks.Count(c => c.Ok && c.Status == StatusSkip),
            ExistingRows: checks.Count(c => c.Existing is not null),
            Saved: saved,
            CreatedCount: created,
            UpdatedCount: updated,
            Rows: rows);
    }

    /// <summary>One IMPORT row on the audit trail for the whole file. The rows it created
    /// or updated carry their own CREATE/UPDATE entries from the interceptor.</summary>
    private AuditLog ImportAuditRow(ImportSpec spec, string fileName, List<RowCheck> checks, int created,
        int updated) => new()
    {
        UserId = currentUser.IsAuthenticated ? currentUser.UserId : null,
        UserName = currentUser.FullName,
        Action = AuditActions.Import,
        ModuleName = "Operations",
        EntityName = spec.EntityName,
        NewValue = JsonSerializer.Serialize(new
        {
            Screen = spec.Screen,
            File = fileName,
            Rows = checks.Count,
            Created = created,
            Updated = updated,
            Skipped = checks.Count(c => c.Ok && c.Status == StatusSkip),
            Rejected = checks.Count(c => !c.Ok)
        }),
        IpAddress = currentUser.IpAddress,
        UserAgent = currentUser.UserAgent is { Length: > 300 } ua ? ua[..300] : currentUser.UserAgent,
        CorrelationId = currentUser.CorrelationId,
        TimestampUtc = clock.UtcNow,
        Result = AuditResults.Success
    };
}
