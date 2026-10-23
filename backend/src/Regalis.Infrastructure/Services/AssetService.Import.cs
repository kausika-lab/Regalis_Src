using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Regalis.Application.Common.Exceptions;
using Regalis.Application.Operations;
using Regalis.Domain.Entities.Identity;
using Regalis.Domain.Entities.Operations;
using Regalis.Domain.Enums;
using Regalis.Infrastructure.Excel;
using Regalis.Shared.Constants;

namespace Regalis.Infrastructure.Services;

/// <summary>
/// Bringing in the list Regalis already has (§12): the team's own sheet, in its own
/// columns, becomes ONE opening-balance receipt. A row with Quantity 8 becomes eight
/// asset records, each with its own code under the row's Asset ID prefix. Nothing is
/// written until every row is clean; the preview shows the codes first.
/// </summary>
public partial class AssetService
{
    private const string ImportSheetName = "Asset list";

    // The team's sheet, column for column (§12.1). Sr. No., Assigned To and Status are
    // read and ignored: the code replaces the serial, nobody is assigned yet, and
    // everything starts In use because it is all installed.
    private static readonly List<ImportColumn> ImportColumns =
    [
        new("Sr. No.", false, "Ignored — the code replaces it.", 1),
        new("Asset ID", true, "The code prefix as Regalis writes it, e.g. R-2W-E-EAC. The import adds -001, -002 … per unit.", "R-2W-E-EAC"),
        new("Item Name", true, "What it is called.", "Air Conditioner 2.0 TR"),
        new("Category", true, "One of the categories under Items → Categories & rooms, e.g. Electrical / Appliance, Furniture, Office Interior.", "Electrical / Appliance"),
        new("Description", false, "Free text.", "Blue Star make 2.0 TR inverter, with copper piping"),
        new("Brand / Model", false, "Kept apart from the name.", "Blue Star"),
        new("Serial No.", false, "Only when Quantity is 1. Fill the rest in later from each item page.", null),
        new("Purchase Date", false, "dd-mm-yyyy or a real Excel date. Cannot be after the receipt date.", new DateOnly(2026, 9, 1), ExcelCellKind.Date),
        new("Vendor", false, "Who it was bought from. Linked to a supplier when the name matches one.", "Ind Air Con Engineers Pvt Ltd"),
        new("Quantity", true, "How many records to create — 4 makes four assets, -001 to -004.", 4),
        new("Purchase Amount", false, "Cost of ONE unit in rupees, before GST. Blank shows as \"cost missing\" until filled in.", 45000m, ExcelCellKind.Money),
        new("Location", true, "The room it is in, e.g. MD Cabin. A room not in the list is added.", "MD Cabin"),
        new("Assigned To", false, "Ignored for now.", null),
        new("Status", false, "Ignored — everything starts In use.", null),
        new("Condition", false, "Good, Fair, Needs attention or Damaged. Blank means Good.", "Good"),
        new("Remarks", false, "Anything else.", null)
    ];

    private sealed class ImportRowCheck(int rowNumber)
    {
        public int RowNumber { get; } = rowNumber;
        public List<string> Errors { get; } = [];
        public List<string> Notes { get; } = [];
        public string? Name { get; set; }
        public string? Prefix { get; set; }
        public int Quantity { get; set; }
        public AssetCategory? Category { get; set; }
        public string? CategoryText { get; set; }
        public string? LocationName { get; set; }
        public int? LocationId { get; set; }
        public decimal? UnitCost { get; set; }
        public DateOnly? PurchaseDate { get; set; }
        public AssetCondition Condition { get; set; } = AssetCondition.Good;
        public string? Serial { get; set; }
        public string? Brand { get; set; }
        public string? Description { get; set; }
        public string? Vendor { get; set; }
        public string? Remarks { get; set; }
        public List<string> Codes { get; set; } = [];
        public bool Ok => Errors.Count == 0;
    }

    public Task<InventoryExportResult> ImportTemplateAsync(CancellationToken ct = default) =>
        Task.FromResult(new InventoryExportResult(
            ExcelWorkbookHelper.Template(ImportSheetName, ImportColumns,
            [
                "One row per kind of thing, in the same columns as the team's own asset list.",
                "Quantity is how many physical units the row describes: each becomes its own asset with its own code and QR label.",
                "Asset ID is the prefix only (R-2W-E-ESB). Several rows may share one prefix — their units are numbered on, never twice.",
                "Fix dates in the future (01-09-2206 is a typo for 2026) and blank locations before uploading; those rows are refused.",
                "The whole sheet comes in as one Incoming Goods receipt marked opening balance. Nothing is saved until every row is clean."
            ]),
            ExcelWorkbookHelper.ContentType, "asset_list_template.xlsx"));

    public async Task<AssetImportResultDto> ImportAsync(InventoryImportUpload upload, DateOnly? receiptDate, bool save,
        CancellationToken ct = default)
    {
        ops.RequirePermission(PermissionCodes.Inventory.Receive,
            "The import posts an opening-balance receipt, which needs the Incoming Goods permission.");
        var date = receiptDate ?? Today;
        if (date > Today) throw new BusinessRuleException("The receipt date cannot be in the future.");
        var sheet = ExcelWorkbookHelper.Read(upload.Content, upload.FileName, upload.Length, ImportColumns);
        var (checks, newRooms) = await CheckImportAsync(sheet, date, ct);

        if (!save || checks.Any(c => !c.Ok))
        {
            if (save)
            {
                throw new BusinessRuleException(
                    $"{checks.Count(c => !c.Ok)} row(s) still have errors. Fix them in the sheet and upload it again — nothing was saved.");
            }
            return ImportResult(date, sheet, checks, newRooms, false, null, null);
        }

        var (receiptId, number) = await InTransactionAsync(async () =>
        {
            // Rooms the sheet names that the register does not have yet.
            var roomIds = await db.AssetLocations.ToDictionaryAsync(r => RoomKey(r.LocationName), r => r.AssetLocationId, ct);
            var order = (await db.AssetLocations.MaxAsync(r => (int?)r.DisplayOrder, ct) ?? 0);
            foreach (var name in newRooms)
            {
                var room = new AssetLocation { LocationName = name, DisplayOrder = ++order };
                db.AssetLocations.Add(room);
                await db.SaveChangesAsync(ct);
                roomIds[RoomKey(name)] = room.AssetLocationId;
            }
            var suppliers = await db.Suppliers.AsNoTracking().Select(s => new { s.SupplierId, s.SupplierName }).ToListAsync(ct);

            var request = new SaveGoodsReceiptRequest(date, true, null, null, null, null,
                $"Imported from {Path.GetFileName(upload.FileName)}",
                checks.Select(c => new SaveGoodsReceiptLineRequest((byte)ItemKind.Asset, null, c.Quantity, c.UnitCost,
                    null, c.Name, c.Category!.AssetCategoryId, c.Prefix, c.Brand, c.Description, c.Serial,
                    roomIds[RoomKey(c.LocationName!)], (byte)c.Condition, c.PurchaseDate, null, c.Vendor, c.Remarks)).ToList(),
                Post: true, ConfirmMissingCost: true);
            var id = await SaveReceiptCoreAsync(null, request, GoodsReceiptSource.Import, ct);
            var receipt = await db.GoodsReceipts.AsNoTracking().FirstAsync(r => r.GoodsReceiptId == id, ct);

            // Link the new assets to a supplier when the sheet's vendor names one exactly.
            foreach (var group in checks.Where(c => c.Vendor is not null).GroupBy(c => c.Vendor!))
            {
                var supplier = suppliers.FirstOrDefault(s => string.Equals(s.SupplierName.Trim(), group.Key,
                    StringComparison.OrdinalIgnoreCase));
                if (supplier is null) continue;
                await db.InventoryItems
                    .Where(i => i.AssetDetail != null && i.VendorName == group.Key && i.SupplierId == null
                                && db.GoodsReceiptLines.Any(l => l.GoodsReceiptId == id
                                                                 && l.GoodsReceiptLineId == i.AssetDetail.GoodsReceiptLineId))
                    .ExecuteUpdateAsync(s => s.SetProperty(i => i.SupplierId, supplier.SupplierId), ct);
            }

            db.AuditLogs.Add(new AuditLog
            {
                UserId = currentUser.IsAuthenticated ? currentUser.UserId : null,
                UserName = currentUser.FullName,
                Action = AuditActions.Import,
                ModuleName = "Operations",
                EntityName = nameof(GoodsReceipt),
                EntityId = id.ToString(CultureInfo.InvariantCulture),
                NewValue = JsonSerializer.Serialize(new
                {
                    Screen = "Assets import",
                    File = upload.FileName,
                    Rows = checks.Count,
                    Assets = checks.Sum(c => c.Quantity),
                    receipt.ReceiptNumber,
                    NewRooms = newRooms
                }),
                IpAddress = currentUser.IpAddress,
                UserAgent = currentUser.UserAgent is { Length: > 300 } ua ? ua[..300] : currentUser.UserAgent,
                CorrelationId = currentUser.CorrelationId,
                TimestampUtc = clock.UtcNow,
                Result = AuditResults.Success
            });
            await db.SaveChangesAsync(ct);
            return (id, receipt.ReceiptNumber);
        }, ct);

        // The codes the save actually issued.
        var lines = await db.GoodsReceiptLines.AsNoTracking().Where(l => l.GoodsReceiptId == receiptId)
            .OrderBy(l => l.LineNumber).ToListAsync(ct);
        for (var i = 0; i < checks.Count && i < lines.Count; i++)
        {
            checks[i].Codes = lines[i].FirstCode is null ? [] : [lines[i].FirstCode!, lines[i].LastCode!];
        }
        return ImportResult(date, sheet, checks, newRooms, true, number, receiptId);
    }

    private static string RoomKey(string name) => Regex.Replace(name.Trim(), @"\s+", " ").ToLowerInvariant();

    private async Task<(List<ImportRowCheck> Checks, List<string> NewRooms)> CheckImportAsync(ImportSheet sheet,
        DateOnly receiptDate, CancellationToken ct)
    {
        var categories = await db.AssetCategories.AsNoTracking().Where(c => c.IsActive).ToListAsync(ct);
        var rooms = await db.AssetLocations.AsNoTracking().ToListAsync(ct);
        var roomByKey = rooms.GroupBy(r => RoomKey(r.LocationName)).ToDictionary(g => g.Key, g => g.First());
        var newRooms = new Dictionary<string, string>();
        var serials = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var planned = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var checks = new List<ImportRowCheck>();

        foreach (var row in sheet.Rows)
        {
            var check = new ImportRowCheck(row.RowNumber);
            checks.Add(check);

            var rawPrefix = row.Text("Asset ID");
            check.Prefix = AssetRules.NormalisePrefix(rawPrefix);
            if (check.Prefix is null) check.Errors.Add("Asset ID is empty.");
            else if (!AssetRules.IsValidPrefix(check.Prefix))
                check.Errors.Add($"Asset ID \"{rawPrefix}\" has characters a code cannot hold.");
            else if (rawPrefix != check.Prefix)
                check.Notes.Add($"Asset ID read as {check.Prefix}.");
            if (check.Prefix is not null && Regex.IsMatch(check.Prefix, @"-\d{3}$"))
                check.Notes.Add("Asset ID already ends in a number; the import adds its own running number after it.");

            check.Name = row.Text("Item Name");
            if (check.Name is null) check.Errors.Add("Item Name is empty.");
            else if (check.Name.Length > 150) check.Errors.Add("Item Name is longer than 150 characters.");

            check.CategoryText = row.Text("Category");
            check.Category = MatchCategory(check.CategoryText, categories);
            if (check.CategoryText is null) check.Errors.Add("Category is empty.");
            else if (check.Category is null)
                check.Errors.Add($"Category \"{check.CategoryText}\" is not in the list: " +
                                 string.Join(", ", categories.Select(c => c.CategoryName)) + ".");
            else if (!string.Equals(check.Category.CategoryName, check.CategoryText, StringComparison.OrdinalIgnoreCase))
                check.Notes.Add($"Category \"{check.CategoryText}\" mapped to {check.Category.CategoryName}.");

            if (!ExcelWorkbookHelper.TryDecimal(row.Raw("Quantity"), out var qty) || qty < 1 || qty != decimal.Truncate(qty))
                check.Errors.Add("Quantity must be a whole number, 1 or more.");
            else if (qty > 500) check.Errors.Add("Quantity is more than 500 — split the row.");
            else check.Quantity = (int)qty;

            var cost = row.Raw("Purchase Amount");
            if (cost is not null)
            {
                if (!ExcelWorkbookHelper.TryDecimal(cost, out var unitCost) || unitCost < 0)
                    check.Errors.Add("Purchase Amount is not an amount.");
                else check.UnitCost = Math.Round(unitCost, 2, MidpointRounding.AwayFromZero);
            }
            if (check.UnitCost is null) check.Notes.Add("No purchase cost: it will show as \"cost missing\" until filled in.");

            var bought = row.Raw("Purchase Date");
            if (bought is not null)
            {
                if (!ExcelWorkbookHelper.TryDate(bought, out var purchaseDate))
                    check.Errors.Add($"Purchase Date \"{row.Text("Purchase Date")}\" is not a date.");
                else if (purchaseDate > receiptDate)
                    check.Errors.Add($"Purchase Date {purchaseDate:dd-MM-yyyy} is after the receipt date — a typo?");
                else check.PurchaseDate = purchaseDate;
            }

            check.LocationName = row.Text("Location") is { } location ? Regex.Replace(location, @"\s+", " ") : null;
            if (check.LocationName is null) check.Errors.Add("Location is empty — every asset needs a room.");
            else if (check.LocationName.Length > 100) check.Errors.Add("Location is longer than 100 characters.");
            else if (roomByKey.TryGetValue(RoomKey(check.LocationName), out var room))
            {
                check.LocationId = room.AssetLocationId;
                check.LocationName = room.LocationName;
                if (!room.IsActive) check.Errors.Add($"Room {room.LocationName} is inactive.");
            }
            else
            {
                newRooms.TryAdd(RoomKey(check.LocationName), check.LocationName);
                check.Notes.Add($"New room {check.LocationName} will be added.");
            }

            var conditionText = row.Text("Condition");
            if (conditionText is not null)
            {
                if (AssetRules.ParseCondition(conditionText) is { } condition) check.Condition = condition;
                else check.Notes.Add($"Condition \"{conditionText}\" not recognised — set to Good.");
            }

            check.Serial = row.Text("Serial No.");
            if (check.Serial is not null && check.Quantity > 1)
            {
                check.Notes.Add("Serial No. skipped: the row has several units. Add each from its item page.");
                check.Serial = null;
            }
            if (check.Serial is not null)
            {
                if (!serials.Add(check.Serial)) check.Notes.Add("This serial number is on another row too.");
                else if (await db.AssetDetails.AnyAsync(d => d.SerialNumber == check.Serial, ct))
                    check.Notes.Add("This serial number is already on another asset.");
            }

            check.Brand = Clip(row.Text("Brand / Model"), 150);
            check.Description = Clip(row.Text("Description"), 500);
            check.Vendor = Clip(row.Text("Vendor"), 150);
            check.Remarks = Clip(row.Text("Remarks"), 500);

            if (check.Ok)
            {
                var numbers = await ops.PeekCodeNumbersAsync(check.Prefix!, check.Quantity, planned, ct);
                planned[check.Prefix!] = numbers[^1];
                check.Codes = numbers.Select(n => AssetRules.AssetCode(check.Prefix!, n)).ToList();
            }
        }
        return (checks, newRooms.Values.OrderBy(n => n).ToList());
    }

    private static string? Clip(string? value, int max) =>
        value is null ? null : value.Length <= max ? value : value[..max];

    /// <summary>The sheet's three category words, mapped to the admin's categories: an exact
    /// name first, then the obvious family ("Electrical" → Electrical / Appliance).</summary>
    private static AssetCategory? MatchCategory(string? text, List<AssetCategory> categories)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        var t = text.Trim();
        var exact = categories.FirstOrDefault(c => string.Equals(c.CategoryName, t, StringComparison.OrdinalIgnoreCase));
        if (exact is not null) return exact;
        string Squash(string s) => new string(s.Where(char.IsLetterOrDigit).ToArray()).ToLowerInvariant();
        var key = Squash(t);
        var squashed = categories.FirstOrDefault(c => Squash(c.CategoryName) == key);
        if (squashed is not null) return squashed;
        string? family = key switch
        {
            _ when key.Contains("electric") || key.Contains("appliance") => "electric",
            _ when key.Contains("furnit") => "furnit",
            _ when key.Contains("interior") || key.Contains("civil") => "interior",
            _ when key is "it" || key.Contains("computer") || key.StartsWith("itequip") => "it",
            _ => null
        };
        return family is null ? null
            : categories.FirstOrDefault(c => family == "it"
                ? Squash(c.CategoryName).StartsWith("it")
                : Squash(c.CategoryName).Contains(family));
    }

    private static AssetImportResultDto ImportResult(DateOnly date, ImportSheet sheet, List<ImportRowCheck> checks,
        List<string> newRooms, bool saved, string? receiptNumber, long? receiptId)
    {
        var valid = checks.Where(c => c.Ok).ToList();
        var prefixes = valid.GroupBy(c => c.Prefix!)
            .Select(g =>
            {
                var codes = g.SelectMany(c => c.Codes).OrderBy(c => c, StringComparer.Ordinal).ToList();
                return new AssetImportPrefixDto(g.Key, g.Count(), g.Sum(c => c.Quantity),
                    codes.Count == 0 ? "" : AssetRules.CodeRange([codes[0], codes[^1]]));
            })
            .OrderBy(p => p.Prefix, StringComparer.Ordinal).ToList();
        return new AssetImportResultDto(date, checks.Count, valid.Count, checks.Count - valid.Count,
            valid.Sum(c => c.Quantity), valid.Count(c => c.UnitCost is null),
            valid.Sum(c => (c.UnitCost ?? 0m) * c.Quantity), newRooms, sheet.IgnoredColumns, prefixes,
            checks.Select(c => new AssetImportRowDto(c.RowNumber, c.Ok ? "New" : "Error", c.Errors, c.Notes, c.Name,
                c.Prefix, c.Quantity,
                c.Codes.Count == 0 ? null : AssetRules.CodeRange(c.Codes.Count == 1 ? c.Codes : [c.Codes[0], c.Codes[^1]]),
                c.UnitCost, c.LocationName, c.Category?.CategoryName ?? c.CategoryText)).ToList(),
            saved, receiptNumber, receiptId);
    }
}
