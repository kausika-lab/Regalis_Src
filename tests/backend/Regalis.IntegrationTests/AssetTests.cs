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
/// Assets (Regalis_WorkSpace_Assets_Feature_Specification): every physical unit is its
/// own record with its own never-reused code; stock is only ever the sum of movements;
/// posted documents are reversed, not edited; Outgoing Goods never takes stock below
/// zero; the team's sheet comes in as one opening-balance receipt.
/// </summary>
[Collection(ApiCollection.Name)]
public class AssetTests(RegalisApiFactory factory)
{
    private static string Tag() => Guid.NewGuid().ToString("N")[..5].ToUpperInvariant();
    private static DateOnly Today() => DateOnly.FromDateTime(DateTime.UtcNow.AddHours(5.5));
    private static string Mobile() => $"9{Random.Shared.NextInt64(100_000_000, 999_999_999)}";

    private async Task<HttpClient> AdminAsync()
    {
        var admin = factory.CreateClient();
        admin.UseToken((await admin.LoginAsAdminAsync()).AccessToken);
        return admin;
    }

    private static async Task<T> DataAsync<T>(HttpResponseMessage response, HttpStatusCode expected = HttpStatusCode.OK)
    {
        var body = await response.Content.ReadAsStringAsync();
        response.StatusCode.Should().Be(expected, body);
        return (await response.Content.ReadFromJsonAsync<ApiResponse<T>>())!.Data!;
    }

    private static async Task<AssetFormOptionsDto> OptionsAsync(HttpClient admin) =>
        (await admin.GetFromJsonAsync<ApiResponse<AssetFormOptionsDto>>("/api/admin/items/options"))!.Data!;

    private static int Category(AssetFormOptionsDto o, string name) =>
        o.Categories.Single(c => c.CategoryName == name).AssetCategoryId;

    private static int Room(AssetFormOptionsDto o, string name) =>
        o.Locations.Single(l => l.LocationName == name).AssetLocationId;

    private static CreateAssetItemRequest AssetForm(AssetFormOptionsDto o, string prefix, int quantity,
        decimal? cost = 45000m, string room = "MD Cabin", string name = "Air Conditioner 2.0 TR") =>
        new(2, name, Category(o, "Electrical / Appliance"), "2.0 TR inverter split AC", "Blue Star", cost,
            null, "Ind Air Con Engineers", null, prefix, quantity, null, Room(o, room), 1,
            Today().AddDays(-42), null, null, null, null, null);

    private static async Task<AssetItemDetailDto> ConsumableAsync(HttpClient admin, AssetFormOptionsDto o,
        string code, decimal reorder = 3m, decimal cost = 45m)
    {
        var created = await DataAsync<CreateAssetItemResult>(await admin.PostAsJsonAsync("/api/admin/items",
            new CreateAssetItemRequest(1, $"Coffee {code}", Category(o, "Pantry"), null, null, cost, null, null, null,
                null, null, null, null, null, null, null, code, "kg", reorder, Room(o, "Pantry Area"))),
            HttpStatusCode.Created);
        return await DataAsync<AssetItemDetailDto>(await admin.GetAsync($"/api/admin/items/{created.InventoryItemIds[0]}"));
    }

    private static Task<GoodsReceiptDto> ReceiveConsumableAsync(HttpClient admin, AssetFormOptionsDto o, int itemId,
        decimal qty, decimal rate = 45m, bool opening = true) =>
        PostReceiptAsync(admin, new SaveGoodsReceiptRequest(Today(), opening, null, null, null, Room(o, "Pantry Area"),
            null, [new SaveGoodsReceiptLineRequest(1, itemId, qty, rate)], Post: true));

    private static async Task<GoodsReceiptDto> PostReceiptAsync(HttpClient admin, SaveGoodsReceiptRequest request) =>
        await DataAsync<GoodsReceiptDto>(await admin.PostAsJsonAsync("/api/admin/goods-receipts", request),
            HttpStatusCode.Created);

    private static async Task<GoodsIssueResult> IssueAsync(HttpClient admin, CreateGoodsIssueRequest request) =>
        await DataAsync<GoodsIssueResult>(await admin.PostAsJsonAsync("/api/admin/goods-issues", request),
            HttpStatusCode.Created);

    private static async Task<decimal?> OnHandAsync(HttpClient admin, int itemId) =>
        (await DataAsync<AssetItemDetailDto>(await admin.GetAsync($"/api/admin/items/{itemId}"))).OnHand;

    // ── Items: one record per unit ──────────────────────────────────────────

    [Fact]
    public async Task AssetForm_CreatesOneRecordPerUnit_WithItsOwnCode_ThroughAPostedReceipt()
    {
        var admin = await AdminAsync();
        var o = await OptionsAsync(admin);
        var prefix = $"R-2W-E-T{Tag()}";

        var created = await DataAsync<CreateAssetItemResult>(
            await admin.PostAsJsonAsync("/api/admin/items", AssetForm(o, prefix, 4)), HttpStatusCode.Created);

        created.Codes.Should().Equal($"{prefix}-001", $"{prefix}-002", $"{prefix}-003", $"{prefix}-004");
        created.InventoryItemIds.Should().HaveCount(4, "4 means FOUR records, not one record of four");
        created.TotalCost.Should().Be(180000m);
        created.ReceiptNumber.Should().MatchRegex(@"^IN-\d{4}-\d{4}$");

        // Born from Incoming Goods: the receipt is posted and each unit has a movement.
        var receipt = await DataAsync<GoodsReceiptDto>(await admin.GetAsync($"/api/admin/goods-receipts/{created.GoodsReceiptId}"));
        receipt.Status.Should().Be((byte)GoodsReceiptStatus.Posted);
        receipt.Lines.Single().FirstCode.Should().Be($"{prefix}-001");
        receipt.Lines.Single().LastCode.Should().Be($"{prefix}-004");
        var ledger = await DataAsync<PagedResult<StockLedgerRowDto>>(
            await admin.GetAsync($"/api/admin/stock/ledger?search={prefix}&pageSize=50"));
        ledger.TotalCount.Should().Be(4);

        var item = await DataAsync<AssetItemDetailDto>(await admin.GetAsync($"/api/admin/items/{created.InventoryItemIds[1]}"));
        item.ItemCode.Should().Be($"{prefix}-002");
        item.Status.Should().Be((byte)AssetStatus.InUse, "MD Cabin is a room, not the store");
        item.SiblingCount.Should().Be(4);
        item.QrText.Should().Be(
            $"REGALIS WS ASSET\nCode: {prefix}-002\nItem: Air Conditioner 2.0 TR\nBrand: Blue Star\nWhere: MD Cabin\n" +
            $"Bought: {Today().AddDays(-42):yyyy-MM-dd}\nProperty of Regalis Workspace\nIf found, contact admin@regalis.in");
        item.Value.BookValue.Should().BeLessThan(45000m).And.BeGreaterThan(44000m, "6 weeks of a 7-year life");
        item.LabelPrintedUtc.Should().BeNull("no sticker has been printed yet");

        // A second batch carries on the numbering — never twice.
        var more = await DataAsync<CreateAssetItemResult>(
            await admin.PostAsJsonAsync("/api/admin/items", AssetForm(o, prefix, 2)), HttpStatusCode.Created);
        more.Codes.Should().Equal($"{prefix}-005", $"{prefix}-006");
    }

    [Fact]
    public async Task ItemsList_SummaryCountsAllRecords_AndPrintingLabelsClearsNoQr()
    {
        var admin = await AdminAsync();
        var o = await OptionsAsync(admin);
        var prefix = $"R-2W-E-L{Tag()}";
        var noCost = await admin.PostAsJsonAsync("/api/admin/items", AssetForm(o, prefix, 3, cost: null));
        noCost.StatusCode.Should().Be(HttpStatusCode.BadRequest, "the asset form requires the purchase cost");

        var ok = await DataAsync<CreateAssetItemResult>(
            await admin.PostAsJsonAsync("/api/admin/items", AssetForm(o, prefix, 3)), HttpStatusCode.Created);
        var list = await DataAsync<AssetItemListResult>(
            await admin.GetAsync($"/api/admin/items?search={prefix}&noLabel=true"));
        list.Page.TotalCount.Should().Be(3);
        list.Page.PageSize.Should().Be(50, "the server sets the page size");
        var before = list.Summary.NoLabelCount;

        var sheet = await DataAsync<AssetLabelSheetDto>(await admin.GetAsync($"/api/admin/items/labels?search={prefix}"));
        sheet.Labels.Should().HaveCount(3);
        sheet.Labels.Should().OnlyContain(l => l.QrText.StartsWith("REGALIS WS ASSET\nCode: " + prefix));
        (await admin.PostAsJsonAsync("/api/admin/items/labels/printed",
            new MarkLabelsPrintedRequest(ok.InventoryItemIds))).EnsureSuccessStatusCode();

        var after = await DataAsync<AssetItemListResult>(await admin.GetAsync($"/api/admin/items?search={prefix}&noLabel=true"));
        after.Page.TotalCount.Should().Be(0);
        after.Summary.NoLabelCount.Should().Be(before - 3);
    }

    [Fact]
    public async Task Consumable_HasNoOpeningQuantity_StockOnlyComesFromMovements()
    {
        var admin = await AdminAsync();
        var o = await OptionsAsync(admin);
        var coffee = await ConsumableAsync(admin, o, $"CF-{Tag()}");
        coffee.OnHand.Should().Be(0m, "a new consumable starts with nothing");
        coffee.Kind.Should().Be((byte)ItemKind.Consumable);

        var receipt = await ReceiveConsumableAsync(admin, o, coffee.InventoryItemId, 5m);
        receipt.IsOpeningBalance.Should().BeTrue();
        (await OnHandAsync(admin, coffee.InventoryItemId)).Should().Be(5m);

        // There is no field anywhere that takes a stock figure: the edit form ignores it.
        var edit = await admin.PutAsJsonAsync($"/api/admin/items/{coffee.InventoryItemId}", new
        {
            itemName = coffee.ItemName, categoryId = coffee.CategoryId, unit = "kg", reorderLevel = 3m,
            unitCost = 45m, keptInLocationId = coffee.KeptInLocationId, onHand = 999m, openingQuantity = 999m
        });
        edit.EnsureSuccessStatusCode();
        (await OnHandAsync(admin, coffee.InventoryItemId)).Should().Be(5m);

        // The older item route refuses an opening quantity too.
        var legacy = await admin.PostAsJsonAsync("/api/inventory/items", new SaveInventoryItemRequest(
            $"LG-{Tag()}", "Legacy", "Pantry", "Nos", 5m, 0m, 1m, 1, null));
        legacy.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    // ── Outgoing Goods ──────────────────────────────────────────────────────

    [Fact]
    public async Task UsedUp_NeverTakesStockBelowZero_AndWarnsAtTheReorderLevel()
    {
        var admin = await AdminAsync();
        var o = await OptionsAsync(admin);
        var coffee = await ConsumableAsync(admin, o, $"CF-{Tag()}", reorder: 3m);
        await ReceiveConsumableAsync(admin, o, coffee.InventoryItemId, 11m);

        var request = new CreateGoodsIssueRequest(1, null, Room(o, "Pantry Area"), null,
            [new GoodsIssueLineRequest(coffee.InventoryItemId, 4m)]);
        var preview = await DataAsync<GoodsIssuePreviewDto>(await admin.PostAsJsonAsync("/api/admin/goods-issues/preview", request));
        preview.Lines.Single().Message.Should().Be("11 kg in stock — after this, 7 kg. Still above the reorder level of 3.");

        var issued = await IssueAsync(admin, request);
        issued.Issue.IssueNumber.Should().MatchRegex(@"^OUT-\d{4}-\d{4}$");
        (await OnHandAsync(admin, coffee.InventoryItemId)).Should().Be(7m);

        var tooMuch = await admin.PostAsJsonAsync("/api/admin/goods-issues", request with
        {
            Lines = [new GoodsIssueLineRequest(coffee.InventoryItemId, 8m)]
        });
        tooMuch.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await tooMuch.Content.ReadAsStringAsync()).Should().Contain("Only 7 kg on hand");
        (await OnHandAsync(admin, coffee.InventoryItemId)).Should().Be(7m);

        // Crossing the reorder level flags it and notifies the admins.
        await IssueAsync(admin, request with { Lines = [new GoodsIssueLineRequest(coffee.InventoryItemId, 5m)] });
        var detail = await DataAsync<AssetItemDetailDto>(await admin.GetAsync($"/api/admin/items/{coffee.InventoryItemId}"));
        detail.OnHand.Should().Be(2m);
        detail.BelowReorder.Should().BeTrue();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RegalisDbContext>();
        (await db.AdminNotifications.AnyAsync(n => n.EventCode == "stock.below_reorder"
                                                   && n.ReferenceId == coffee.InventoryItemId)).Should().BeTrue();
    }

    [Fact]
    public async Task Assets_MoveRepairScrap_AndAnAssetInRepairCannotMove()
    {
        var admin = await AdminAsync();
        var o = await OptionsAsync(admin);
        var created = await DataAsync<CreateAssetItemResult>(
            await admin.PostAsJsonAsync("/api/admin/items", AssetForm(o, $"R-2W-E-M{Tag()}", 2, room: "Store")),
            HttpStatusCode.Created);
        var (first, second) = (created.InventoryItemIds[0], created.InventoryItemIds[1]);
        (await DataAsync<AssetItemDetailDto>(await admin.GetAsync($"/api/admin/items/{first}")))
            .Status.Should().Be((byte)AssetStatus.InStore);

        // Moved: room and status change, written to history.
        var moved = await IssueAsync(admin, new CreateGoodsIssueRequest(2, null, null, Room(o, "Conference Room"),
            [new GoodsIssueLineRequest(first, 1m)]));
        var item = await DataAsync<AssetItemDetailDto>(await admin.GetAsync($"/api/admin/items/{first}"));
        item.LocationName.Should().Be("Conference Room");
        item.Status.Should().Be((byte)AssetStatus.InUse);
        item.RecentEvents.First().Summary.Should().Be("Moved");

        // Sent for repair: it cannot be moved until it is back.
        await IssueAsync(admin, new CreateGoodsIssueRequest(3, null, null, null,
            [new GoodsIssueLineRequest(first, 1m)], RepairVendor: "Blue Star Service", ExpectedCost: 2500m));
        var blocked = await admin.PostAsJsonAsync("/api/admin/goods-issues",
            new CreateGoodsIssueRequest(2, null, null, Room(o, "MD Cabin"), [new GoodsIssueLineRequest(first, 1m)]));
        blocked.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await blocked.Content.ReadAsStringAsync()).Should().Contain("away for repair. Close the repair first.");
        var repair = (await DataAsync<AssetItemDetailDto>(await admin.GetAsync($"/api/admin/items/{first}"))).OpenRepair!;
        var back = await DataAsync<AssetItemDetailDto>(await admin.PostAsJsonAsync(
            $"/api/admin/items/{first}/repairs/{repair.AssetRepairId}/close", new CloseRepairRequest(null, 1800m, "Gas refilled", null)));
        back.Status.Should().Be((byte)AssetStatus.InUse);
        back.Value.RepairSpend.Should().Be(1800m);

        // Scrapped: out of the register, worth 0, the record stays.
        await IssueAsync(admin, new CreateGoodsIssueRequest(5, null, null, null,
            [new GoodsIssueLineRequest(second, 1m)], ReasonText: "Beyond repair", AmountRecovered: 0m));
        var scrapped = await DataAsync<AssetItemDetailDto>(await admin.GetAsync($"/api/admin/items/{second}"));
        scrapped.Status.Should().Be((byte)AssetStatus.Scrapped);
        scrapped.Value.BookValue.Should().Be(0m);
        scrapped.IsActive.Should().BeTrue("an asset is never deleted");

        // A move is reversed by the opposite movement, never by deleting it — and only
        // while the asset is still where that move left it.
        var reverseLate = await admin.PostAsJsonAsync($"/api/admin/goods-issues/{moved.Issue.GoodsIssueId}/reverse",
            new ReverseRequest("Wrong room"));
        reverseLate.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity, "it has been to repair since");
        var issues = await DataAsync<PagedResult<GoodsIssueRowDto>>(await admin.GetAsync($"/api/admin/goods-issues?inventoryItemId={first}"));
        issues.TotalCount.Should().Be(2, "nothing on Outgoing Goods is ever deleted");
    }

    [Fact]
    public async Task ReversingAnIssue_PutsTheAssetBackExactly()
    {
        var admin = await AdminAsync();
        var o = await OptionsAsync(admin);
        var id = (await DataAsync<CreateAssetItemResult>(
            await admin.PostAsJsonAsync("/api/admin/items", AssetForm(o, $"R-2W-E-R{Tag()}", 1)), HttpStatusCode.Created))
            .InventoryItemIds[0];
        var lost = await IssueAsync(admin, new CreateGoodsIssueRequest(6, null, null, null,
            [new GoodsIssueLineRequest(id, 1m)], ReportedBy: "Security", Note: "Missing after the event"));
        (await DataAsync<AssetItemDetailDto>(await admin.GetAsync($"/api/admin/items/{id}")))
            .Status.Should().Be((byte)AssetStatus.Lost);

        var reversal = await DataAsync<GoodsIssueDto>(await admin.PostAsJsonAsync(
            $"/api/admin/goods-issues/{lost.Issue.GoodsIssueId}/reverse", new ReverseRequest("Found in the store room")));
        reversal.Reason.Should().Be((byte)GoodsIssueReason.Reversal);
        reversal.ReversesNumber.Should().Be(lost.Issue.IssueNumber);
        var item = await DataAsync<AssetItemDetailDto>(await admin.GetAsync($"/api/admin/items/{id}"));
        item.Status.Should().Be((byte)AssetStatus.InUse);
        item.LocationName.Should().Be("MD Cabin");

        (await admin.PostAsJsonAsync($"/api/admin/goods-issues/{lost.Issue.GoodsIssueId}/reverse",
            new ReverseRequest("Again"))).StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    // ── Incoming Goods ──────────────────────────────────────────────────────

    [Fact]
    public async Task DraftReceipt_ChangesNothing_PostingCreates_ReversingTakesBackOut()
    {
        var admin = await AdminAsync();
        var o = await OptionsAsync(admin);
        var prefix = $"R-2W-E-G{Tag()}";
        var coffee = await ConsumableAsync(admin, o, $"CF-{Tag()}");
        await ReceiveConsumableAsync(admin, o, coffee.InventoryItemId, 1m);

        var request = new SaveGoodsReceiptRequest(Today(), true, null, "BS/4471", null, Room(o, "Store"), null,
        [
            new SaveGoodsReceiptLineRequest(2, null, 8m, 2243m, null, "Grid Ceiling 2x2 mm",
                Category(o, "Office Interior"), prefix, LocationId: Room(o, "Private Cabin 1")),
            new SaveGoodsReceiptLineRequest(1, coffee.InventoryItemId, 10m, 45m)
        ]);

        // What each line will do, worked out before posting.
        var preview = await DataAsync<GoodsReceiptPreviewDto>(await admin.PostAsJsonAsync("/api/admin/goods-receipts/preview", request));
        preview.Lines[0].Message.Should().Be($"Will create 8 assets: {prefix}-001 to -008, each with its own QR label");
        preview.Lines[1].Message.Should().Be("Stock goes from 1 kg to 11 kg. No labels.");
        preview.Total.Should().Be(18394m);

        var draft = await PostReceiptAsync(admin, request);
        draft.Status.Should().Be((byte)GoodsReceiptStatus.Draft);
        (await OnHandAsync(admin, coffee.InventoryItemId)).Should().Be(1m, "a draft changes nothing");
        (await DataAsync<AssetItemListResult>(await admin.GetAsync($"/api/admin/items?search={prefix}")))
            .Page.TotalCount.Should().Be(0, "no asset exists until the receipt is posted");

        var posted = await DataAsync<GoodsReceiptDto>(await admin.PostAsJsonAsync(
            $"/api/admin/goods-receipts/{draft.GoodsReceiptId}/post", new PostGoodsReceiptRequest()));
        posted.Status.Should().Be((byte)GoodsReceiptStatus.Posted);
        posted.Lines[0].FirstCode.Should().Be($"{prefix}-001");
        posted.Lines[0].LastCode.Should().Be($"{prefix}-008");
        (await OnHandAsync(admin, coffee.InventoryItemId)).Should().Be(11m);

        // A posted receipt is never edited.
        (await admin.PutAsJsonAsync($"/api/admin/goods-receipts/{draft.GoodsReceiptId}", request))
            .StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);

        var reversal = await DataAsync<GoodsReceiptDto>(await admin.PostAsJsonAsync(
            $"/api/admin/goods-receipts/{draft.GoodsReceiptId}/reverse", new ReverseRequest("Delivered to the wrong branch")));
        reversal.ReversesNumber.Should().Be(posted.ReceiptNumber);
        reversal.TotalAmount.Should().Be(-posted.TotalAmount);
        (await OnHandAsync(admin, coffee.InventoryItemId)).Should().Be(1m);
        var assets = await DataAsync<AssetItemListResult>(await admin.GetAsync($"/api/admin/items?search={prefix}&includeInactive=true"));
        assets.Page.Items.Should().HaveCount(8).And.OnlyContain(a => !a.IsActive);

        // The numbers are retired with them: the next grid ceiling is -009.
        var next = await DataAsync<NextCodesDto>(await admin.GetAsync($"/api/admin/items/next-codes?prefix={prefix}&count=1"));
        next.Codes.Single().Should().Be($"{prefix}-009");
    }

    [Fact]
    public async Task ReceiptWithAssetsWithoutCost_NeedsConfirmation()
    {
        var admin = await AdminAsync();
        var o = await OptionsAsync(admin);
        var request = new SaveGoodsReceiptRequest(Today(), true, null, null, null, Room(o, "Store"), null,
        [
            new SaveGoodsReceiptLineRequest(2, null, 3m, null, null, "Switch Board",
                Category(o, "Electrical / Appliance"), $"R-2W-E-S{Tag()}")
        ], Post: true);
        var refused = await admin.PostAsJsonAsync("/api/admin/goods-receipts", request);
        refused.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await refused.Content.ReadAsStringAsync()).Should()
            .Contain("3 of these 3 assets have no purchase cost").And.Contain("COST_MISSING");

        var posted = await PostReceiptAsync(admin, request with { ConfirmMissingCost = true });
        posted.Status.Should().Be((byte)GoodsReceiptStatus.Posted);
        var list = await DataAsync<AssetItemListResult>(await admin.GetAsync(
            $"/api/admin/items?search={posted.Lines[0].CodePrefix}&costMissing=true"));
        list.Page.Items.Should().HaveCount(3).And.OnlyContain(i => i.Value == null, "no cost: a dash, never a zero");
    }

    [Fact]
    public async Task TwoAdminsPostingTheSamePrefixAtOnce_NeverGetTheSameNumber()
    {
        var o = await OptionsAsync(await AdminAsync());
        var prefix = $"R-2W-E-C{Tag()}";
        var posts = Enumerable.Range(0, 4).Select(async _ =>
        {
            var admin = await AdminAsync();
            for (var attempt = 0; ; attempt++)
            {
                var response = await admin.PostAsJsonAsync("/api/admin/items", AssetForm(o, prefix, 3));
                // A deadlock victim under Serializable is retried, as the screen would.
                if (response.StatusCode == HttpStatusCode.Created || attempt == 3)
                {
                    return await DataAsync<CreateAssetItemResult>(response, HttpStatusCode.Created);
                }
            }
        });
        var results = await Task.WhenAll(posts);
        var codes = results.SelectMany(r => r.Codes).ToList();
        codes.Should().HaveCount(12).And.OnlyHaveUniqueItems();
        codes.Order().Should().Equal(Enumerable.Range(1, 12).Select(n => $"{prefix}-{n:000}"));
    }

    // ── Current Stock and counts ────────────────────────────────────────────

    [Fact]
    public async Task StockCount_CorrectsTheFigureWithAnAdjustmentMovement()
    {
        var admin = await AdminAsync();
        var o = await OptionsAsync(admin);
        var room = await DataAsync<AssetLocationDto>(await admin.PostAsJsonAsync("/api/admin/asset-locations",
            new SaveAssetLocationRequest($"Count Shelf {Tag()}", null, false, null)));
        var created = await DataAsync<CreateAssetItemResult>(await admin.PostAsJsonAsync("/api/admin/items",
            new CreateAssetItemRequest(1, "Sugar", Category(o, "Pantry"), null, null, 100m, null, null, null,
                null, null, null, null, null, null, null, $"SG-{Tag()}", "kg", 2m, room.AssetLocationId)),
            HttpStatusCode.Created);
        var sugar = created.InventoryItemIds[0];
        await PostReceiptAsync(admin, new SaveGoodsReceiptRequest(Today(), true, null, null, null, room.AssetLocationId,
            null, [new SaveGoodsReceiptLineRequest(1, sugar, 7m, 100m)], Post: true));

        // Only one count at a time across the module: clear any left open by another test.
        foreach (var open in (await DataAsync<List<StockCountRowDto>>(await admin.GetAsync("/api/admin/stock-counts")))
                 .Where(c => c.Status is 1 or 2))
        {
            await admin.PostAsync($"/api/admin/stock-counts/{open.StockCountId}/cancel", null);
        }

        var count = await DataAsync<StockCountDto>(await admin.PostAsJsonAsync("/api/admin/stock-counts",
            new StartStockCountRequest(null, room.AssetLocationId, "Front desk", null)));
        count.Lines.Should().ContainSingle().Which.RecordedQuantity.Should().BeNull("the counter does not see the figure");

        var submitted = await DataAsync<StockCountDto>(await admin.PostAsJsonAsync(
            $"/api/admin/stock-counts/{count.StockCountId}/submit", new SubmitStockCountRequest([new(sugar, 5m)])));
        var line = submitted.Lines.Single();
        (line.RecordedQuantity, line.CountedQuantity, line.Difference).Should().Be((7m, 5m, -2m));
        (await OnHandAsync(admin, sugar)).Should().Be(7m, "submitting writes nothing");

        await DataAsync<StockCountDto>(await admin.PostAsync($"/api/admin/stock-counts/{count.StockCountId}/confirm", null));
        (await OnHandAsync(admin, sugar)).Should().Be(5m);
        var ledger = await DataAsync<PagedResult<StockLedgerRowDto>>(await admin.GetAsync($"/api/admin/stock/ledger?inventoryItemId={sugar}"));
        ledger.Items.First().Should().Match<StockLedgerRowDto>(r =>
            r.What == "Stock count adjustment" && r.Change == -2m && r.Balance == 5m);
        ledger.Items.Last().Balance.Should().Be(7m, "the running balance is worked out row by row");

        var sheet = await DataAsync<StockSheetDto>(await admin.GetAsync($"/api/admin/stock?locationId={room.AssetLocationId}"));
        var row = sheet.Rows.Single(r => r.InventoryItemId == sugar);
        (row.Opening + row.In - row.Out).Should().Be(row.OnHand);
        row.OnHand.Should().Be(5m);
        sheet.LastCountDate.Should().Be(Today());
    }

    // ── Import (§12) ────────────────────────────────────────────────────────

    private static byte[] TeamSheet(params object?[][] rows)
    {
        string[] headers = ["Sr. No.", "Asset ID", "Item Name", "Category", "Description", "Brand / Model", "Serial No.",
            "Purchase Date", "Vendor", "Quantity", "Purchase Amount", "Location", "Assigned To", "Status", "Condition", "Remarks"];
        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("Assets");
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
                    case decimal m: cell.Value = m; break;
                    default: cell.Value = rows[r][c]!.ToString(); break;
                }
            }
        }
        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    private static MultipartFormDataContent Upload(byte[] bytes)
    {
        var file = new ByteArrayContent(bytes);
        file.Headers.ContentType = new MediaTypeHeaderValue(
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
        return new MultipartFormDataContent { { file, "file", "asset-list.xlsx" } };
    }

    [Fact]
    public async Task TeamSheet_ImportsAsOneOpeningBalanceReceipt_OneRecordPerUnit()
    {
        var admin = await AdminAsync();
        var p = $"R-2W-E-I{Tag()}";
        var bad = TeamSheet(
            [1, p + "AC", "Air Conditioner 2.0 TR", "Electrical", "Inverter split", "Blue Star", null,
                new DateTime(2206, 9, 1), "Ind Air Con", 4, 45000m, "MD Cabin", null, null, "Good", null],
            [2, p + "SB", "Switch Board", "Electrical", null, null, null, null, null, 8, null, null, null, null, null, null]);
        var preview = await DataAsync<AssetImportResultDto>(await admin.PostAsync("/api/admin/items/import/preview", Upload(bad)));
        preview.ErrorRows.Should().Be(2);
        preview.Rows[0].Errors.Should().ContainSingle(e => e.Contains("after the receipt date"));
        preview.Rows[1].Errors.Should().ContainSingle(e => e.Contains("Location is empty"));
        (await admin.PostAsync("/api/admin/items/import", Upload(bad)))
            .StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity, "nothing is saved while a row has errors");

        var good = TeamSheet(
            [1, p + "AC", "Air Conditioner 2.0 TR", "Electrical", "Inverter split", "Blue Star", null,
                new DateTime(2026, 9, 1), "Ind Air Con", 4, 45000m, "MD Cabin", null, null, "Good", null],
            [2, p + "SB ", "Switch Board", "Electrical", null, null, null, null, null, 8, null, "Open Work Station", null, null, null, null],
            [3, p + "SB", "Switch Board", "electrical", null, null, null, null, null, 4, null, $"Annex {p}", null, null, null, null]);
        var checkedSheet = await DataAsync<AssetImportResultDto>(await admin.PostAsync("/api/admin/items/import/preview", Upload(good)));
        checkedSheet.ErrorRows.Should().Be(0);
        checkedSheet.Units.Should().Be(16, "rows describe physical things: 4 + 8 + 4");
        checkedSheet.Prefixes.Should().ContainSingle(x => x.Prefix == p + "SB")
            .Which.Should().Match<AssetImportPrefixDto>(x => x.Units == 12 && x.Range == $"{p}SB-001 to -012",
                "the trailing space does not make a second prefix");
        checkedSheet.NewLocations.Should().Equal($"Annex {p}");
        checkedSheet.CostMissingRows.Should().Be(2);

        var saved = await DataAsync<AssetImportResultDto>(await admin.PostAsync("/api/admin/items/import", Upload(good)));
        saved.Saved.Should().BeTrue();
        var receipt = await DataAsync<GoodsReceiptDto>(await admin.GetAsync($"/api/admin/goods-receipts/{saved.GoodsReceiptId}"));
        receipt.Status.Should().Be((byte)GoodsReceiptStatus.Imported);
        receipt.IsOpeningBalance.Should().BeTrue();
        receipt.Lines.Should().HaveCount(3);
        var items = await DataAsync<AssetItemListResult>(await admin.GetAsync($"/api/admin/items?search={p}"));
        items.Page.TotalCount.Should().Be(16);
        items.Page.Items.Should().OnlyContain(i => i.Status == (byte)AssetStatus.InUse);
    }

    // ── Permissions ─────────────────────────────────────────────────────────

    [Fact]
    public async Task Permissions_AreCheckedOnTheServer_AndCustomersAreKeptOut()
    {
        var admin = await AdminAsync();
        var o = await OptionsAsync(admin);
        var roles = (await admin.GetFromJsonAsync<ApiResponse<List<RoleDto>>>("/api/roles"))!.Data!;
        var email = $"fin-{Guid.NewGuid():N}@test.regalis.in";
        const string password = "R0le!TestPass1";
        (await admin.PostAsJsonAsync("/api/users", new CreateUserRequest("Finance Clerk", email, Mobile(), password, 2,
            [roles.Single(r => r.RoleCode == RoleCodes.Finance).RoleId], null, 1))).EnsureSuccessStatusCode();
        var finance = factory.CreateClient();
        finance.UseToken((await finance.LoginAsync(email, password, "admin")).AccessToken);

        (await finance.GetAsync("/api/admin/items")).StatusCode.Should().Be(HttpStatusCode.OK, "finance may view");
        (await finance.PostAsJsonAsync("/api/admin/goods-receipts", new SaveGoodsReceiptRequest(Today(), true, null,
                null, null, Room(o, "Store"), null, [new SaveGoodsReceiptLineRequest(1, 1, 1m, 1m)])))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden, "receiving needs INVENTORY.RECEIVE");
        (await finance.PostAsJsonAsync("/api/admin/goods-issues/1/reverse", new ReverseRequest("x")))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var customer = factory.CreateClient();
        var (_, _, session) = await customer.RegisterCustomerAsync(factory);
        customer.UseToken(session.AccessToken);
        (await customer.GetAsync("/api/admin/items")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // ── Purchase orders ─────────────────────────────────────────────────────

    [Fact]
    public async Task OldInventoryRoutes_LeaveAssetsAlone()
    {
        var admin = await AdminAsync();
        var o = await OptionsAsync(admin);
        var prefix = $"R-2W-E-O{Tag()}";
        var id = (await DataAsync<CreateAssetItemResult>(
            await admin.PostAsJsonAsync("/api/admin/items", AssetForm(o, prefix, 1)), HttpStatusCode.Created))
            .InventoryItemIds[0];

        var picker = (await admin.GetFromJsonAsync<ApiResponse<PagedResult<InventoryItemDto>>>(
            $"/api/inventory/items?search={prefix}"))!.Data!;
        picker.TotalCount.Should().Be(0, "asset units stay off the quotation and PO pickers");
        (await admin.PostAsJsonAsync("/api/inventory/transactions",
                new CreateInventoryTransactionRequest(id, 2, 1m, null, "x", null, null, null, null, null)))
            .StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await admin.DeleteAsync($"/api/inventory/items/{id}")).StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }
}
