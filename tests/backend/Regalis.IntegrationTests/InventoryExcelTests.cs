using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using ClosedXML.Excel;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Regalis.Application.Administration;
using Regalis.Application.Finance;
using Regalis.Application.Operations;
using Regalis.Domain.Enums;
using Regalis.Infrastructure.Persistence;
using Regalis.IntegrationTests.Infrastructure;
using Regalis.Shared.Api;
using Regalis.Shared.Constants;

namespace Regalis.IntegrationTests;

/// <summary>
/// Excel export and import on the Assets screens (Items, Incoming Goods, Outgoing Goods,
/// Current Stock): exports honour the screen filters and return every row; imports check
/// each row on the server, save the valid ones in one transaction as ordinary ledger rows,
/// and never touch stock directly.
/// </summary>
[Collection(ApiCollection.Name)]
public class InventoryExcelTests(RegalisApiFactory factory)
{
    private const string XlsxType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
    private const string Location = "REG-HQ";

    private async Task<HttpClient> AdminAsync()
    {
        var admin = factory.CreateClient();
        admin.UseToken((await admin.LoginAsAdminAsync()).AccessToken);
        return admin;
    }

    private static string Tag() => Guid.NewGuid().ToString("N")[..6].ToUpperInvariant();

    private static string Today() => DateTime.UtcNow.AddHours(5.5).ToString("yyyy-MM-dd");

    private static string DaysAgo(int days) => DateTime.UtcNow.AddHours(5.5).AddDays(-days).ToString("yyyy-MM-dd");

    private static async Task<InventoryItemDto> CreateItemAsync(HttpClient admin, string code,
        string category = "Pantry", decimal opening = 0m, decimal reorder = 0m, byte type = 1)
    {
        var response = await admin.PostAsJsonAsync("/api/inventory/items", new SaveInventoryItemRequest(
            code, $"Item {code}", category, "Nos", opening, reorder, 25m, 1, null, ItemType: type));
        response.StatusCode.Should().Be(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<ApiResponse<InventoryItemDto>>())!.Data!;
    }

    private static async Task MoveAsync(HttpClient admin, int itemId, byte type, decimal qty, string date,
        string? reference = null)
    {
        var response = await admin.PostAsJsonAsync("/api/inventory/transactions",
            new CreateInventoryTransactionRequest(itemId, type, qty, DateOnly.Parse(date), "Desk", reference,
                null, null, null, null));
        response.StatusCode.Should().Be(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
    }

    private static async Task<decimal> StockOfAsync(HttpClient admin, int itemId) =>
        (await admin.GetFromJsonAsync<ApiResponse<InventoryItemDto>>($"/api/inventory/items/{itemId}"))!
        .Data!.CurrentStock;

    /// <summary>A workbook with a header row and data rows, as a user would build it.</summary>
    private static byte[] Workbook(string[] headers, params object?[][] rows)
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("Data");
        for (var c = 0; c < headers.Length; c++) sheet.Cell(1, c + 1).Value = headers[c];
        for (var r = 0; r < rows.Length; r++)
        {
            for (var c = 0; c < rows[r].Length; c++)
            {
                var cell = sheet.Cell(r + 2, c + 1);
                switch (rows[r][c])
                {
                    case null: break;
                    case DateTime d: cell.Value = d; break;
                    case int i: cell.Value = i; break;
                    case double n: cell.Value = n; break;
                    case decimal m: cell.Value = m; break;
                    default: cell.Value = rows[r][c]!.ToString(); break;
                }
            }
        }
        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    private static MultipartFormDataContent Form(byte[] bytes, string fileName = "import.xlsx", string? existing = null)
    {
        var form = new MultipartFormDataContent();
        var file = new ByteArrayContent(bytes);
        file.Headers.ContentType = new MediaTypeHeaderValue(XlsxType);
        form.Add(file, "file", fileName);
        if (existing is not null) form.Add(new StringContent(existing), "existing");
        return form;
    }

    private static async Task<InventoryImportResultDto> ImportAsync(HttpClient admin, string kind, byte[] file,
        bool save, string? existing = null)
    {
        var response = await admin.PostAsync($"/api/inventory/{kind}/import{(save ? "" : "/preview")}",
            Form(file, existing: existing));
        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<ApiResponse<InventoryImportResultDto>>())!.Data!;
    }

    private static async Task<XLWorkbook> DownloadAsync(HttpResponseMessage response)
    {
        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        response.Content.Headers.ContentType!.MediaType.Should().Be(XlsxType);
        return new XLWorkbook(new MemoryStream(await response.Content.ReadAsByteArrayAsync()));
    }

    private static List<string> Headers(IXLWorksheet sheet) =>
        sheet.Row(1).CellsUsed().Select(c => c.GetString()).ToList();

    private static int DataRows(IXLWorksheet sheet) => sheet.LastRowUsed()!.RowNumber() - 1;

    private static string[] ItemHeaders =>
    [
        "Item Code", "Item Name", "Item Type", "Category", "Location", "Unit", "Unit Cost", "Reorder Level",
        "HSN/SAC Code", "Selling Rate", "Tax", "Description"
    ];

    // ── Export ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task ItemsExport_UsesTheFilters_ReturnsEveryRow_AndKeepsTypes()
    {
        var admin = await AdminAsync();
        var category = $"XL{Tag()}";
        for (var i = 0; i < 23; i++)
        {
            await CreateItemAsync(admin, $"{category}-{i:00}", category, opening: i, reorder: 5m);
        }

        // More rows than one page (20), all exported.
        var response = await admin.GetAsync($"/api/inventory/items/export?category={category}");
        response.Content.Headers.ContentDisposition!.FileName!.Trim('"')
            .Should().MatchRegex(@"^items_\d{4}-\d{2}-\d{2}_\d{4}\.xlsx$");
        using var all = await DownloadAsync(response);
        var sheet = all.Worksheet(1);
        DataRows(sheet).Should().Be(23);
        Headers(sheet).Should().StartWith(["Item Code", "Item Name", "Item Type", "Category", "Location"]);
        Headers(sheet).Should().Contain(["Unit Cost", "Current Stock", "Status", "Stock Value", "Tax"]);
        var costColumn = Headers(sheet).IndexOf("Unit Cost") + 1;
        sheet.Cell(2, costColumn).DataType.Should().Be(XLDataType.Number, "currency stays a number");
        sheet.Cell(2, costColumn).GetDouble().Should().Be(25);

        // The below-reorder checkbox: stock 0..5 against reorder level 5 → 6 items.
        using var low = await DownloadAsync(
            await admin.GetAsync($"/api/inventory/items/export?category={category}&belowReorder=true"));
        DataRows(low.Worksheet(1)).Should().Be(6);

        // Search narrows to one.
        using var one = await DownloadAsync(
            await admin.GetAsync($"/api/inventory/items/export?category={category}&search={category}-07"));
        DataRows(one.Worksheet(1)).Should().Be(1);

        // Nothing matches → 204, which the screen turns into "no records to export".
        (await admin.GetAsync($"/api/inventory/items/export?search=NOPE{Tag()}"))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task GoodsExport_HonoursSearchAndDateRange_WithRealDates()
    {
        var admin = await AdminAsync();
        var item = await CreateItemAsync(admin, $"GX-{Tag()}");
        var reference = $"REF{Tag()}";
        for (var i = 0; i < 22; i++)
        {
            await MoveAsync(admin, item.InventoryItemId, 1, 2m, DaysAgo(i), reference);
        }
        await MoveAsync(admin, item.InventoryItemId, 2, 3m, DaysAgo(1), reference);

        // The list's search now works too (it used to be ignored).
        var listed = (await admin.GetFromJsonAsync<ApiResponse<PagedResult<InventoryTransactionDto>>>(
            $"/api/inventory/transactions?transactionType=1&search={reference}"))!.Data!;
        listed.TotalCount.Should().Be(22);

        var response = await admin.GetAsync($"/api/inventory/incoming/export?search={reference}");
        response.Content.Headers.ContentDisposition!.FileName!.Trim('"')
            .Should().MatchRegex(@"^incoming_goods_\d{4}-\d{2}-\d{2}_\d{4}\.xlsx$");
        using var incoming = await DownloadAsync(response);
        var sheet = incoming.Worksheet(1);
        DataRows(sheet).Should().Be(22, "all matching rows, not just the first page of 20");
        var dateColumn = Headers(sheet).IndexOf("Date") + 1;
        sheet.Cell(2, dateColumn).DataType.Should().Be(XLDataType.DateTime, "dates are real Excel dates");

        using var ranged = await DownloadAsync(await admin.GetAsync(
            $"/api/inventory/incoming/export?search={reference}&from={DaysAgo(4)}&to={DaysAgo(2)}"));
        DataRows(ranged.Worksheet(1)).Should().Be(3);

        using var outgoing = await DownloadAsync(
            await admin.GetAsync($"/api/inventory/outgoing/export?search={reference}"));
        DataRows(outgoing.Worksheet(1)).Should().Be(1);
        Headers(outgoing.Worksheet(1)).Should().Contain(["Issued To", "Issued By"]);

        (await admin.GetAsync($"/api/inventory/outgoing/export?search={reference}&from={Today()}"))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task StockExport_MatchesTheStockScreen()
    {
        var admin = await AdminAsync();
        var item = await CreateItemAsync(admin, $"SX-{Tag()}", opening: 10m, reorder: 20m);
        await MoveAsync(admin, item.InventoryItemId, 2, 4m, Today());

        var response = await admin.GetAsync("/api/inventory/stock/export?belowReorder=true");
        response.Content.Headers.ContentDisposition!.FileName!.Trim('"')
            .Should().MatchRegex(@"^current_stock_\d{4}-\d{2}-\d{2}_\d{4}\.xlsx$");
        using var book = await DownloadAsync(response);
        var sheet = book.Worksheet(1);
        var headers = Headers(sheet);
        var row = sheet.RowsUsed().Single(r => r.Cell(1).GetString() == item.ItemCode);
        row.Cell(headers.IndexOf("Current Stock") + 1).GetDouble().Should().Be(6);
        row.Cell(headers.IndexOf("Status") + 1).GetString().Should().Be("Critical");
        row.Cell(headers.IndexOf("Stock Value") + 1).GetDouble().Should().Be(150);
    }

    // ── Items import ────────────────────────────────────────────────────────

    [Fact]
    public async Task Templates_HaveHeadersAnExampleAndInstructions()
    {
        var admin = await AdminAsync();
        foreach (var kind in new[] { "items", "incoming", "outgoing" })
        {
            using var book = await DownloadAsync(await admin.GetAsync($"/api/inventory/{kind}/import/template"));
            book.Worksheets.Select(w => w.Name).Should().Contain("Instructions");
            DataRows(book.Worksheet(1)).Should().Be(1, "one example row");
            Headers(book.Worksheet(1)).Should().Contain("Item Code");
        }
        (await admin.GetAsync("/api/inventory/stock/import/template")).StatusCode
            .Should().Be(HttpStatusCode.NotFound, "Current Stock is export only");
    }

    [Fact]
    public async Task ItemsImport_ValidatesEachRow_AndSavesOnlyTheValidOnes()
    {
        var admin = await AdminAsync();
        var t = Tag();
        var existing = await CreateItemAsync(admin, $"IM{t}-OLD", category: "Pantry");
        var file = Workbook(ItemHeaders,
            [$"im{t}-a ", "Beans", "Goods", "pantry", Location, "Kg", 450, 5, "090121", 600, "5%", "Arabica"],
            [$"IM{t}-B", "Cleaning", "Service", $"New{t}", "Regalis WorkSpace", "Nos", null, null, null, 1500, "Non-Taxable", null],
            [$"IM{t}-DUP", "One", null, null, Location, "Nos"],
            [$"im{t}-dup", "Two", null, null, Location, "Nos"],
            [$"IM{t}-BAD", "Bad", "Gadget", null, "Mars", "Nos", -1, null, "12AB", null, "7%"],
            [null, "No code", null, null, Location, "Nos"],
            [$"IM{t}-OLD", "Renamed", null, null, Location, "Nos", 99]);

        var preview = await ImportAsync(admin, "items", file, save: false, existing: "skip");
        preview.TotalRows.Should().Be(7);
        preview.ValidRows.Should().Be(2);
        preview.SkippedRows.Should().Be(1);
        preview.ErrorRows.Should().Be(4);
        preview.ExistingRows.Should().Be(1);
        var rows = preview.Rows.ToDictionary(r => r.RowNumber);
        rows[2].Status.Should().Be("New");
        rows[3].Notes.Should().Contain(n => n.Contains("New category"));
        rows[4].Errors.Should().Contain(e => e.Contains("more than once"));
        rows[5].Errors.Should().Contain(e => e.Contains("more than once"));
        rows[6].Errors.Should().HaveCountGreaterThanOrEqualTo(4)
            .And.Contain(e => e.Contains("Item Type"))
            .And.Contain(e => e.Contains("Unknown location"))
            .And.Contain(e => e.Contains("Tax"));
        rows[7].Errors.Should().Contain("Item Code is required.");
        rows[8].Status.Should().Be("Skip");

        // Nothing was saved by the preview.
        (await admin.GetFromJsonAsync<ApiResponse<PagedResult<InventoryItemDto>>>(
            $"/api/inventory/items?search=IM{t}-A"))!.Data!.TotalCount.Should().Be(0);

        var saved = await ImportAsync(admin, "items", file, save: true, existing: "skip");
        saved.Saved.Should().BeTrue();
        saved.CreatedCount.Should().Be(2);
        saved.UpdatedCount.Should().Be(0);

        var created = (await admin.GetFromJsonAsync<ApiResponse<PagedResult<InventoryItemDto>>>(
            $"/api/inventory/items?search=IM{t}-A"))!.Data!.Items.Single();
        created.ItemCode.Should().Be($"IM{t}-A", "codes are trimmed and upper-cased like the form does");
        created.Category.Should().Be("Pantry", "an existing category keeps its spelling");
        created.CurrentStock.Should().Be(0m, "stock is never imported");
        created.UnitCost.Should().Be(450m);
        created.TaxPercent.Should().Be(5m);
        created.IsNonTaxable.Should().BeFalse();
        created.HsnSacCode.Should().Be("090121");

        var untouched = (await admin.GetFromJsonAsync<ApiResponse<InventoryItemDto>>(
            $"/api/inventory/items/{existing.InventoryItemId}"))!.Data!;
        untouched.ItemName.Should().Be(existing.ItemName, "skip leaves existing items alone");

        // The failed rows come back as a workbook with an Error column.
        using var report = await DownloadAsync(await admin.PostAsync("/api/inventory/items/import/errors",
            Form(file, existing: "skip")));
        var errorSheet = report.Worksheet(1);
        Headers(errorSheet).Last().Should().Be("Error");
        DataRows(errorSheet).Should().Be(4);
    }

    [Fact]
    public async Task ItemsImport_UpdateExisting_KeepsBlanks_AndNeverTouchesStock()
    {
        var admin = await AdminAsync();
        var item = await CreateItemAsync(admin, $"UP-{Tag()}", category: "Stationery", opening: 7m, reorder: 3m);
        await MoveAsync(admin, item.InventoryItemId, 1, 5m, Today());

        // Exported file round-trips: extra columns like Current Stock are ignored.
        using var export = await DownloadAsync(await admin.GetAsync($"/api/inventory/items/export?search={item.ItemCode}"));
        var sheet = export.Worksheet(1);
        var headers = Headers(sheet);
        sheet.Cell(2, headers.IndexOf("Item Name") + 1).Value = "Renamed by import";
        sheet.Cell(2, headers.IndexOf("Current Stock") + 1).Value = 999;
        sheet.Cell(2, headers.IndexOf("Category") + 1).Value = "";
        using var stream = new MemoryStream();
        export.SaveAs(stream);

        var preview = await ImportAsync(admin, "items", stream.ToArray(), save: false, existing: "update");
        preview.IgnoredColumns.Should().Contain(["Current Stock", "Status", "Stock Value"]);
        preview.Rows.Single().Status.Should().Be("Update");

        var saved = await ImportAsync(admin, "items", stream.ToArray(), save: true, existing: "update");
        saved.UpdatedCount.Should().Be(1);
        var after = (await admin.GetFromJsonAsync<ApiResponse<InventoryItemDto>>(
            $"/api/inventory/items/{item.InventoryItemId}"))!.Data!;
        after.ItemName.Should().Be("Renamed by import");
        after.Category.Should().Be("Stationery", "a blank cell keeps the current value");
        after.CurrentStock.Should().Be(12m, "stock only moves through the ledger");

        // A goods item with movements cannot become a service.
        var toService = Workbook(ItemHeaders, [item.ItemCode, "X", "Service", null, Location, "Nos"]);
        (await ImportAsync(admin, "items", toService, save: false, existing: "update"))
            .Rows.Single().Errors.Should().Contain(e => e.Contains("cannot become a service"));
    }

    [Fact]
    public async Task Import_RefusesBadFiles_WithAClearMessage()
    {
        var admin = await AdminAsync();

        async Task<string> RefusedAsync(byte[] bytes, string fileName = "import.xlsx")
        {
            var response = await admin.PostAsync("/api/inventory/items/import/preview", Form(bytes, fileName));
            response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
            return await response.Content.ReadAsStringAsync();
        }

        (await RefusedAsync(Workbook(["Code", "Name"], ["A", "B"])))
            .Should().Contain("missing").And.Contain("Item Code");
        (await RefusedAsync(Workbook(ItemHeaders))).Should().Contain("no data rows");
        (await RefusedAsync("just some text"u8.ToArray(), "notes.txt")).Should().Contain(".xlsx");
        (await RefusedAsync("PK not really a zip"u8.ToArray())).Should().Contain("could not be read");
        (await RefusedAsync(Workbook(ItemHeaders, ["A", "B", null, null, Location, "Nos"]), "old.xls"))
            .Should().Contain("save it as .xlsx");

        var tooMany = Enumerable.Range(0, 5_001)
            .Select(i => new object?[] { $"BULK{i}", "Bulk", null, null, Location, "Nos" }).ToArray();
        (await RefusedAsync(Workbook(ItemHeaders, tooMany))).Should().Contain("5,000");

        var huge = new byte[6 * 1024 * 1024];
        Random.Shared.NextBytes(huge);
        (await RefusedAsync(huge)).Should().Contain("The limit is 5 MB");
    }

    // ── Goods import ────────────────────────────────────────────────────────

    [Fact]
    public async Task IncomingImport_WritesLedgerRows_AndCurrentStockFollows()
    {
        var admin = await AdminAsync();
        var t = Tag();
        var goods = await CreateItemAsync(admin, $"IN{t}", opening: 2m);
        var service = await CreateItemAsync(admin, $"SV{t}", type: 2);
        var supplierCode = $"S{t}";
        (await admin.PostAsJsonAsync("/api/suppliers", new SaveSupplierRequest(
            supplierCode, $"Supplier {t}", null, null, null, null, null, null, null, null, null, null, null)))
            .EnsureSuccessStatusCode();

        string[] headers = ["Item Code", "Quantity", "Date", "Supplier", "Reference", "Supplier Invoice No", "Rate", "Remarks"];
        var tomorrow = DateTime.UtcNow.AddHours(5.5).Date.AddDays(1);
        var file = Workbook(headers,
            [$"in{t}", 10, DateTime.UtcNow.AddHours(5.5).Date.AddDays(-2), $"supplier {t}", "DN-1", "B-1", 45.5, "first"],
            [$"IN{t}", 2.5, DaysAgo(1), supplierCode, null, null, null, null],
            [$"NOPE{t}", 1, Today()],
            [$"SV{t}", 1, Today()],
            [$"IN{t}", 0, Today()],
            [$"IN{t}", 1, tomorrow],
            [$"IN{t}", 1, Today(), "Nobody Ltd"],
            [$"IN{t}", 1, "31/02/2026"]);

        var preview = await ImportAsync(admin, "incoming", file, save: false);
        preview.ValidRows.Should().Be(2);
        preview.ErrorRows.Should().Be(6);
        var errors = preview.Rows.Where(r => r.Status == "Error").SelectMany(r => r.Errors).ToList();
        errors.Should().Contain(e => e.Contains("Unknown item code"));
        errors.Should().Contain(e => e.Contains("is a service"));
        errors.Should().Contain("Quantity must be greater than 0.");
        errors.Should().Contain(e => e.Contains("in the future"));
        errors.Should().Contain(e => e.Contains("Unknown supplier"));
        errors.Should().Contain(e => e.Contains("not a valid date"));

        var saved = await ImportAsync(admin, "incoming", file, save: true);
        saved.CreatedCount.Should().Be(2);
        (await StockOfAsync(admin, goods.InventoryItemId)).Should().Be(14.5m, "2 opening + 10 + 2.5");
        (await StockOfAsync(admin, service.InventoryItemId)).Should().Be(0m);

        var ledger = (await admin.GetFromJsonAsync<ApiResponse<PagedResult<InventoryTransactionDto>>>(
            $"/api/inventory/transactions?inventoryItemId={goods.InventoryItemId}"))!.Data!.Items;
        ledger.Should().HaveCount(2);
        ledger.Should().OnlyContain(r => r.TransactionType == 1 && r.TransactionNumber != ""
                                         && r.EnteredByName != "");
        ledger.Should().Contain(r => r.SupplierName == $"Supplier {t}" && r.UnitCost == 45.5m
                                     && r.PurchaseInvoiceNumber == "B-1" && r.Notes == "first");

        // One IMPORT row on the audit trail for the file.
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RegalisDbContext>();
        (await db.AuditLogs.AnyAsync(a => a.Action == AuditActions.Import && a.NewValue!.Contains("incoming_goods")))
            .Should().BeTrue();
    }

    [Fact]
    public async Task OutgoingImport_NeverTakesStockBelowZero_InDateOrder()
    {
        var admin = await AdminAsync();
        var t = Tag();
        var item = await CreateItemAsync(admin, $"OUT{t}", opening: 10m);
        string[] headers = ["Item Code", "Quantity", "Date", "Issued To", "Reference", "Remarks"];
        // File order is not date order: the check runs 4 (3 days ago), 5 (2 days ago), 3 (today).
        var file = Workbook(headers,
            [$"OUT{t}", 3, Today(), "Pantry"],
            [$"OUT{t}", 4, DaysAgo(3), "Pantry"],
            [$"OUT{t}", 5, DaysAgo(2), "Pantry"],
            [$"OUT{t}", 1, DaysAgo(1), "Pantry"]);

        var preview = await ImportAsync(admin, "outgoing", file, save: false);
        preview.ValidRows.Should().Be(3);
        var refused = preview.Rows.Single(r => r.Status == "Error");
        refused.RowNumber.Should().Be(2, "10 − 4 − 5 − 1 leaves 0 when today's 3 is reached");
        refused.Errors.Single().Should().Contain("Only 0");

        var saved = await ImportAsync(admin, "outgoing", file, save: true);
        saved.CreatedCount.Should().Be(3);
        (await StockOfAsync(admin, item.InventoryItemId)).Should().Be(0m);

        var stock = (await admin.GetFromJsonAsync<ApiResponse<List<InventoryStockDto>>>(
            "/api/inventory/stock"))!.Data!.Single(s => s.InventoryItemId == item.InventoryItemId);
        stock.TotalOut.Should().Be(10m);
        stock.CurrentStock.Should().Be(0m);

        // Importing the same file again: every row now overdraws.
        var again = await ImportAsync(admin, "outgoing", file, save: false);
        again.ValidRows.Should().Be(0);
        (await admin.PostAsync("/api/inventory/outgoing/import", Form(file)))
            .StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    // ── Permissions ─────────────────────────────────────────────────────────

    [Fact]
    public async Task ViewOnlyStaff_CannotImport_OrExport()
    {
        var admin = await AdminAsync();
        var roles = (await admin.GetFromJsonAsync<ApiResponse<List<RoleDto>>>("/api/roles"))!.Data!;
        var email = $"hk-{Guid.NewGuid():N}@test.regalis.in";
        (await admin.PostAsJsonAsync("/api/users", new CreateUserRequest("Housekeeping Probe", email,
            $"7{Random.Shared.NextInt64(100_000_000, 999_999_999)}", "H0usekeep!Pass", 3,
            [roles.Single(r => r.RoleCode == RoleCodes.Housekeeping).RoleId], null, 1))).EnsureSuccessStatusCode();
        var staff = factory.CreateClient();
        staff.UseToken((await staff.LoginAsync(email, "H0usekeep!Pass", "admin")).AccessToken);

        (await staff.GetAsync("/api/inventory/stock")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await staff.GetAsync("/api/inventory/items/import/template")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await staff.PostAsync("/api/inventory/incoming/import",
                Form(Workbook(["Item Code", "Quantity", "Date"], ["X", 1, Today()]))))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await staff.GetAsync("/api/inventory/stock/export")).StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var customer = factory.CreateClient();
        customer.UseToken((await customer.RegisterCustomerAsync(factory)).Session.AccessToken);
        (await customer.GetAsync("/api/inventory/items/import/template")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
