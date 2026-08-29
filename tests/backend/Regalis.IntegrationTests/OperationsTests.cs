using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Regalis.Application.Access;
using Regalis.Application.Administration;
using Regalis.Application.Bookings;
using Regalis.Application.Finance;
using Regalis.Application.Masters;
using Regalis.Application.Operations;
using Regalis.Domain.Enums;
using Regalis.Infrastructure.Persistence;
using Regalis.IntegrationTests.Infrastructure;
using Regalis.Shared.Api;
using Regalis.Shared.Constants;

namespace Regalis.IntegrationTests;

/// <summary>
/// Day 8 gate (M6): housekeeping schedules write real calendar blocks so customer
/// availability drops the slot; staff tokens are time-boxed to the schedule; the
/// inventory ledger is append-only with derived stock; settings and banners behave.
/// </summary>
[Collection(ApiCollection.Name)]
public class OperationsTests(RegalisApiFactory factory)
{
    private async Task<HttpClient> AdminClientAsync()
    {
        var admin = factory.CreateClient();
        admin.UseToken((await admin.LoginAsAdminAsync()).AccessToken);
        return admin;
    }

    /// <summary>Creates a housekeeping staff user via the admin API and returns its id.</summary>
    private static async Task<long> CreateStaffUserAsync(HttpClient admin)
    {
        var roles = (await admin.GetFromJsonAsync<ApiResponse<List<RoleDto>>>("/api/roles"))!.Data!;
        var housekeepingRoleId = roles.Single(r => r.RoleCode == RoleCodes.Housekeeping).RoleId;

        var email = $"staff-{Guid.NewGuid():N}@test.regalis.in";
        var mobile = $"8{Random.Shared.NextInt64(100_000_000, 999_999_999)}";
        var response = await admin.PostAsJsonAsync("/api/users", new CreateUserRequest(
            "Test Housekeeper", email, mobile, "H0usekeep!Pass", UserType: 3,
            RoleIds: [housekeepingRoleId], CompanyName: null, DefaultLocationId: 1));
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<ApiResponse<UserDetailDto>>())!.Data!.UserId;
    }

    private static SaveHousekeepingScheduleRequest ScheduleRequest(long staffUserId, int workspaceId,
        DateTime startUtc, bool blocks = true) => new(
        StaffUserId: staffUserId, LocationId: 1, WorkspaceId: workspaceId, AreaName: null,
        StartUtc: startUtc, EndUtc: startUtc.AddHours(1), TaskType: 3,
        TaskDescription: "Deep clean", Notes: null, BlocksWorkspace: blocks);

    // ── Housekeeping: the calendar-block invariant (Day 8 QA gate) ──────────

    [Fact]
    public async Task HousekeepingSchedule_RemovesSlotFromAvailability_AndCancelRestoresIt()
    {
        var admin = await AdminClientAsync();
        var staffId = await CreateStaffUserAsync(admin);
        var workspaceId = await admin.CreateWorkspaceAsync();

        var customer = factory.CreateClient();
        var (_, _, session) = await customer.RegisterCustomerAsync(factory);
        customer.UseToken(session.AccessToken);

        var start = BookingTestHelpers.NextMondayUtc(10);

        // Free before the schedule…
        var before = await customer.PostAsJsonAsync("/api/availability/check",
            new AvailabilityCheckRequest(workspaceId, start, start.AddHours(1)));
        (await before.Content.ReadFromJsonAsync<ApiResponse<AvailabilityCheckResponse>>())!
            .Data!.IsAvailable.Should().BeTrue();

        var created = await admin.PostAsJsonAsync("/api/housekeeping/schedules",
            ScheduleRequest(staffId, workspaceId, start));
        created.StatusCode.Should().Be(HttpStatusCode.Created);
        var schedule = (await created.Content.ReadFromJsonAsync<ApiResponse<HousekeepingScheduleDto>>())!.Data!;
        schedule.ScheduleNumber.Should().StartWith("HKS-");
        schedule.WorkspaceBlockId.Should().NotBeNull("a blocking schedule writes the block in the same transaction");

        // …gone while the schedule holds the block (Invariant 12, one calendar path)…
        var during = await customer.PostAsJsonAsync("/api/availability/check",
            new AvailabilityCheckRequest(workspaceId, start, start.AddHours(1)));
        var duringResult = (await during.Content.ReadFromJsonAsync<ApiResponse<AvailabilityCheckResponse>>())!.Data!;
        duringResult.IsAvailable.Should().BeFalse("the housekeeping block must remove the slot");

        // …and back the moment the schedule is cancelled.
        var cancel = await admin.PostAsJsonAsync(
            $"/api/housekeeping/schedules/{schedule.HousekeepingScheduleId}/cancel", new { });
        cancel.EnsureSuccessStatusCode();
        var after = await customer.PostAsJsonAsync("/api/availability/check",
            new AvailabilityCheckRequest(workspaceId, start, start.AddHours(1)));
        (await after.Content.ReadFromJsonAsync<ApiResponse<AvailabilityCheckResponse>>())!
            .Data!.IsAvailable.Should().BeTrue("cancelling releases the block");
    }

    [Fact]
    public async Task HousekeepingSchedule_CannotSwallowAConfirmedBooking()
    {
        var admin = await AdminClientAsync();
        var staffId = await CreateStaffUserAsync(admin);
        var workspaceId = await admin.CreateWorkspaceAsync();

        var customer = factory.CreateClient();
        var (_, _, session) = await customer.RegisterCustomerAsync(factory);
        customer.UseToken(session.AccessToken);

        var start = BookingTestHelpers.NextMondayUtc(11);

        // Hold and confirm through the wallet — a real paid booking in the window.
        using (var scope = factory.Services.CreateScope())
        {
            var wallets = scope.ServiceProvider.GetRequiredService<IWalletService>();
            var db = scope.ServiceProvider.GetRequiredService<RegalisDbContext>();
            await wallets.CreditAsync(session.User.UserId, 5000m,
                WalletCategory.AdminAdjustment, "Test credit");
            await db.SaveChangesAsync();
        }
        var hold = await customer.PostAsJsonAsync("/api/bookings",
            new CreateBookingRequest(workspaceId, start, 60, null));
        var booking = (await hold.Content.ReadFromJsonAsync<ApiResponse<BookingDto>>())!.Data!;
        var confirm = await customer.PostAsJsonAsync($"/api/bookings/{booking.BookingId}/confirm",
            new ConfirmBookingRequest(PaymentMethods.Wallet, null, null, null));
        confirm.EnsureSuccessStatusCode();

        var response = await admin.PostAsJsonAsync("/api/housekeeping/schedules",
            ScheduleRequest(staffId, workspaceId, start));
        response.StatusCode.Should().Be(HttpStatusCode.Conflict,
            "a block must never silently swallow a paid booking");
    }

    [Fact]
    public async Task StaffAccessToken_IsTimeBoxedToTheScheduleWindow()
    {
        var admin = await AdminClientAsync();
        var staffId = await CreateStaffUserAsync(admin);
        var workspaceId = await admin.CreateWorkspaceAsync();
        var start = BookingTestHelpers.NextMondayUtc(14);

        var created = await admin.PostAsJsonAsync("/api/housekeeping/schedules",
            ScheduleRequest(staffId, workspaceId, start));
        var schedule = (await created.Content.ReadFromJsonAsync<ApiResponse<HousekeepingScheduleDto>>())!.Data!;

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RegalisDbContext>();
        var token = db.AccessTokens.Single(t =>
            t.HousekeepingScheduleId == schedule.HousekeepingScheduleId &&
            t.Status == AccessTokenStatus.Active);
        token.UserId.Should().Be(staffId);
        token.ValidFromUtc.Should().Be(schedule.StartUtc);
        token.ValidToUtc.Should().Be(schedule.EndUtc);
        token.TokenPurpose.Should().Be(AccessTokenPurpose.Housekeeping);

        // The gate denies the token outside its window (the schedule is next week).
        var validate = await admin.PostAsJsonAsync("/api/access/validate",
            new ValidateAccessRequest(token.QrPayload, workspaceId,
                Direction: 1, DeviceId: "TEST-GATE", AccessMethod: 1));
        var decision = (await validate.Content
            .ReadFromJsonAsync<ApiResponse<AccessDecisionDto>>())!.Data!;
        decision.IsGranted.Should().BeFalse();
        decision.DenyReason.Should().Be("OutsideValidWindow");

        // Cancelling the schedule revokes the token — no standing staff passes.
        await admin.PostAsJsonAsync(
            $"/api/housekeeping/schedules/{schedule.HousekeepingScheduleId}/cancel", new { });
        db.ChangeTracker.Clear();
        db.AccessTokens.Single(t => t.AccessTokenId == token.AccessTokenId)
            .Status.Should().Be(AccessTokenStatus.Revoked);
    }

    // ── Inventory: derived stock over an append-only ledger ─────────────────

    private static SaveInventoryItemRequest ItemRequest(string code, decimal opening = 10m,
        decimal reorder = 5m) => new(
        ItemCode: code, ItemName: $"Item {code}", Category: "Pantry", Unit: "Nos",
        OpeningQuantity: opening, ReorderLevel: reorder, UnitCost: 25m, LocationId: 1,
        Description: null);

    [Fact]
    public async Task InventoryStock_IsDerivedFromTheLedger_AndFlagsReorder()
    {
        var admin = await AdminClientAsync();
        var code = $"INV-{Guid.NewGuid().ToString("N")[..6].ToUpperInvariant()}";
        var created = await admin.PostAsJsonAsync("/api/inventory/items", ItemRequest(code));
        created.StatusCode.Should().Be(HttpStatusCode.Created);
        var item = (await created.Content.ReadFromJsonAsync<ApiResponse<InventoryItemDto>>())!.Data!;
        item.CurrentStock.Should().Be(10m);

        async Task PostTxnAsync(byte type, decimal qty, string? damage = null)
        {
            var r = await admin.PostAsJsonAsync("/api/inventory/transactions",
                new CreateInventoryTransactionRequest(item.InventoryItemId, type, qty,
                    null, "Test", null, null, damage, null, null));
            r.StatusCode.Should().Be(HttpStatusCode.Created);
        }

        await PostTxnAsync(1, 20m);            // In: 30
        await PostTxnAsync(2, 12m);            // Out: 18
        await PostTxnAsync(3, 3m, "Broken");   // Damage: 15
        await PostTxnAsync(2, 11m);            // Out: 4 → at/below reorder level 5

        var fetched = (await admin.GetFromJsonAsync<ApiResponse<InventoryItemDto>>(
            $"/api/inventory/items/{item.InventoryItemId}"))!.Data!;
        fetched.CurrentStock.Should().Be(4m, "stock = opening 10 + in 20 − out 23 − damage 3");
        fetched.NeedsReorder.Should().BeTrue("4 is at or under the reorder level of 5");

        var stock = (await admin.GetFromJsonAsync<ApiResponse<List<InventoryStockDto>>>(
            "/api/inventory/stock?belowReorder=true"))!.Data!;
        stock.Should().Contain(s => s.InventoryItemId == item.InventoryItemId);
    }

    [Fact]
    public async Task InventoryLedger_NeverGoesNegative_AndAdjustmentCompensates()
    {
        var admin = await AdminClientAsync();
        var code = $"INV-{Guid.NewGuid().ToString("N")[..6].ToUpperInvariant()}";
        var created = await admin.PostAsJsonAsync("/api/inventory/items",
            ItemRequest(code, opening: 5m, reorder: 1m));
        var item = (await created.Content.ReadFromJsonAsync<ApiResponse<InventoryItemDto>>())!.Data!;

        // Issuing more than the stock is refused.
        var overdraw = await admin.PostAsJsonAsync("/api/inventory/transactions",
            new CreateInventoryTransactionRequest(item.InventoryItemId, 2, 6m,
                null, "Test", null, null, null, null, null));
        overdraw.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);

        // A mistake is corrected with a compensating Adjustment row, never an edit.
        var adjust = await admin.PostAsJsonAsync("/api/inventory/transactions",
            new CreateInventoryTransactionRequest(item.InventoryItemId, 5, -2m,
                null, null, null, null, null, null, "Stocktake correction"));
        adjust.StatusCode.Should().Be(HttpStatusCode.Created);

        var fetched = (await admin.GetFromJsonAsync<ApiResponse<InventoryItemDto>>(
            $"/api/inventory/items/{item.InventoryItemId}"))!.Data!;
        fetched.CurrentStock.Should().Be(3m);

        // Append-only: there is no update or delete route on the ledger.
        var txns = (await admin.GetFromJsonAsync<ApiResponse<PagedResult<InventoryTransactionDto>>>(
            $"/api/inventory/transactions?inventoryItemId={item.InventoryItemId}"))!.Data!;
        txns.TotalCount.Should().Be(1);
        var txnId = txns.Items[0].InventoryTransactionId;
        (await admin.PutAsJsonAsync($"/api/inventory/transactions/{txnId}", new { }))
            .StatusCode.Should().BeOneOf(HttpStatusCode.NotFound, HttpStatusCode.MethodNotAllowed);
        (await admin.DeleteAsync($"/api/inventory/transactions/{txnId}"))
            .StatusCode.Should().BeOneOf(HttpStatusCode.NotFound, HttpStatusCode.MethodNotAllowed);
    }

    [Fact]
    public async Task InventoryExport_ReturnsCsv()
    {
        var admin = await AdminClientAsync();
        var response = await admin.GetAsync("/api/inventory/export");
        response.EnsureSuccessStatusCode();
        response.Content.Headers.ContentType!.MediaType.Should().Be("text/csv");
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("TransactionNumber,Date,ItemCode");
    }

    // ── Services, banners, settings, contact ────────────────────────────────

    [Fact]
    public async Task ServiceCatalogue_CustomerSeesAvailableItemsOnly()
    {
        var admin = await AdminClientAsync();
        var suffix = Guid.NewGuid().ToString("N")[..6].ToUpperInvariant();
        foreach (var (code, available) in new[] { ($"CF-{suffix}", true), ($"CX-{suffix}", false) })
        {
            var r = await admin.PostAsJsonAsync("/api/services", new SaveServiceItemRequest(
                code, $"Coffee {code}", "Coffee", 120m, "Cup", null, null, available, 1, null));
            r.StatusCode.Should().Be(HttpStatusCode.Created);
        }

        var customer = factory.CreateClient();
        var (_, _, session) = await customer.RegisterCustomerAsync(factory);
        customer.UseToken(session.AccessToken);

        var list = (await customer.GetFromJsonAsync<ApiResponse<PagedResult<ServiceItemDto>>>(
            "/api/services?pageSize=100&category=Coffee"))!.Data!;
        list.Items.Should().Contain(s => s.ItemCode == $"CF-{suffix}");
        list.Items.Should().NotContain(s => s.ItemCode == $"CX-{suffix}",
            "customers see available items only");

        // Writes are admin-side: SERVICE.CREATE is not a customer permission.
        var forbidden = await customer.PostAsJsonAsync("/api/services", new SaveServiceItemRequest(
            $"CZ-{suffix}", "Nope", "Coffee", 1m, null, null, null, true, 1, null));
        forbidden.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Banners_ActiveList_FiltersWindowAndPortal()
    {
        var admin = await AdminClientAsync();
        var now = DateTime.UtcNow;
        var suffix = Guid.NewGuid().ToString("N")[..6];

        async Task<int> CreateBannerAsync(string title, DateTime from, DateTime to, byte portal)
        {
            var r = await admin.PostAsJsonAsync("/api/banners", new SaveBannerRequest(
                title, null, "/img/banner.png", null, null, 1, from, to, null, portal));
            r.StatusCode.Should().Be(HttpStatusCode.Created);
            return (await r.Content.ReadFromJsonAsync<ApiResponse<BannerDto>>())!.Data!.BannerId;
        }

        await CreateBannerAsync($"live-{suffix}", now.AddHours(-1), now.AddDays(7), portal: 1);
        await CreateBannerAsync($"expired-{suffix}", now.AddDays(-14), now.AddDays(-7), portal: 1);
        await CreateBannerAsync($"adminonly-{suffix}", now.AddHours(-1), now.AddDays(7), portal: 2);

        var customer = factory.CreateClient();
        var (_, _, session) = await customer.RegisterCustomerAsync(factory);
        customer.UseToken(session.AccessToken);

        var active = (await customer.GetFromJsonAsync<ApiResponse<List<BannerDto>>>(
            "/api/banners/active"))!.Data!;
        active.Should().Contain(b => b.Title == $"live-{suffix}");
        active.Should().NotContain(b => b.Title == $"expired-{suffix}");
        active.Should().NotContain(b => b.Title == $"adminonly-{suffix}",
            "customers never see admin-portal banners");
    }

    [Fact]
    public async Task Settings_PublicIsAnonymous_AdminUpdateApplies()
    {
        // Contact Us works signed out.
        var anonymous = factory.CreateClient();
        var pub = await anonymous.GetAsync("/api/settings/public");
        pub.EnsureSuccessStatusCode();
        var data = (await pub.Content.ReadFromJsonAsync<ApiResponse<PublicSettingsDto>>())!.Data!;
        data.CompanyName.Should().NotBeNullOrWhiteSpace();

        // The full list needs SETTING.VIEW.
        (await anonymous.GetAsync("/api/settings")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        var admin = await AdminClientAsync();
        var update = await admin.PutAsJsonAsync("/api/settings", new UpdateAppSettingsRequest(
            [new AppSettingValue("Support.Hours", "Mon-Sat 08:00-20:00 IST")]));
        update.EnsureSuccessStatusCode();

        var refreshed = (await anonymous.GetFromJsonAsync<ApiResponse<PublicSettingsDto>>(
            "/api/settings/public"))!.Data!;
        refreshed.SupportHours.Should().Be("Mon-Sat 08:00-20:00 IST",
            "the settings cache is evicted on save");

        // Data-type guard: an int setting rejects a non-numeric value.
        var bad = await admin.PutAsJsonAsync("/api/settings", new UpdateAppSettingsRequest(
            [new AppSettingValue("Invoice.DueDays", "soon")]));
        bad.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }
}
