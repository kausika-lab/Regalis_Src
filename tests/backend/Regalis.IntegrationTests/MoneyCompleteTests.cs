using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Regalis.Application.Bookings;
using Regalis.Application.Finance;
using Regalis.Application.Operations;
using Regalis.Domain.Enums;
using Regalis.Infrastructure.Persistence;
using Regalis.IntegrationTests.Infrastructure;
using Regalis.Shared.Api;
using Regalis.Shared.Constants;

namespace Regalis.IntegrationTests;

[Collection(ApiCollection.Name)]
public class MoneyCompleteTests(RegalisApiFactory factory)
{
    private async Task<(HttpClient Customer, long CustomerId, int WorkspaceId)> SetUpAsync()
    {
        var admin = factory.CreateClient();
        admin.UseToken((await admin.LoginAsAdminAsync()).AccessToken);
        var workspaceId = await admin.CreateWorkspaceAsync(pricePerHour: 500m);

        var customer = factory.CreateClient();
        var (_, _, session) = await customer.RegisterCustomerAsync(factory);
        customer.UseToken(session.AccessToken);
        return (customer, session.User.UserId, workspaceId);
    }

    private async Task CreditWalletAsync(long customerId, decimal amount)
    {
        using var scope = factory.Services.CreateScope();
        var wallets = scope.ServiceProvider.GetRequiredService<IWalletService>();
        var db = scope.ServiceProvider.GetRequiredService<RegalisDbContext>();
        await wallets.CreditAsync(customerId, amount, WalletCategory.AdminAdjustment, "Test credit");
        await db.SaveChangesAsync();
    }

    private async Task<ConfirmBookingResponse> BookAndConfirmAsync(HttpClient customer,
        int workspaceId, DateTime start, int minutes = 60)
    {
        var hold = await customer.PostAsJsonAsync("/api/bookings",
            new CreateBookingRequest(workspaceId, start, minutes, null));
        hold.StatusCode.Should().Be(HttpStatusCode.Created);
        var booking = (await hold.Content.ReadFromJsonAsync<ApiResponse<BookingDto>>())!.Data!;
        var confirm = await customer.PostAsJsonAsync($"/api/bookings/{booking.BookingId}/confirm",
            new ConfirmBookingRequest(PaymentMethods.Wallet, null, null, null));
        confirm.EnsureSuccessStatusCode();
        return (await confirm.Content.ReadFromJsonAsync<ApiResponse<ConfirmBookingResponse>>())!.Data!;
    }

    // ── Cancellation policy ─────────────────────────────────────────────────

    [Fact]
    public async Task Cancel_BeforeCutoff_RefundsFullyToWallet_AndRevokesTheToken()
    {
        var (customer, customerId, workspaceId) = await SetUpAsync();
        await CreditWalletAsync(customerId, 5000m);
        // Next Monday is > 24h away — before the cutoff.
        var confirmed = await BookAndConfirmAsync(customer, workspaceId,
            BookingTestHelpers.NextMondayUtc(9));
        var balanceAfterBooking = 5000m - confirmed.Booking.TotalAmount;

        var cancel = await customer.PostAsJsonAsync(
            $"/api/bookings/{confirmed.Booking.BookingId}/cancel",
            new CancelBookingRequest("plans changed"));
        cancel.EnsureSuccessStatusCode();
        var result = (await cancel.Content.ReadFromJsonAsync<ApiResponse<CancelBookingResponse>>())!.Data!;

        result.RefundPercent.Should().Be(100m);
        result.RefundAmount.Should().Be(confirmed.Booking.TotalAmount);
        result.Booking.Status.Should().Be((byte)BookingStatus.Cancelled);
        result.Booking.PaymentStatus.Should().Be((byte)BookingPaymentStatus.Refunded);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RegalisDbContext>();
        (await db.Wallets.SingleAsync(w => w.CustomerId == customerId)).Balance
            .Should().Be(balanceAfterBooking + result.RefundAmount);
        // The booking row still exists (Invariant 3) and the QR token is dead.
        (await db.Bookings.AnyAsync(b => b.BookingId == confirmed.Booking.BookingId)).Should().BeTrue();
        (await db.AccessTokens.SingleAsync(t => t.BookingId == confirmed.Booking.BookingId))
            .Status.Should().Be(AccessTokenStatus.Revoked);
    }

    [Fact]
    public async Task Cancel_AfterCutoff_RefundsNothing()
    {
        var (customer, customerId, workspaceId) = await SetUpAsync();
        await CreditWalletAsync(customerId, 5000m);
        var confirmed = await BookAndConfirmAsync(customer, workspaceId,
            BookingTestHelpers.NextMondayUtc(10));

        // Move the booking start to within the 24h cutoff window.
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<RegalisDbContext>();
            var soonUtc = DateTime.UtcNow.AddHours(2);
            await db.Bookings.Where(b => b.BookingId == confirmed.Booking.BookingId)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(b => b.StartUtc, soonUtc)
                    .SetProperty(b => b.EndUtc, soonUtc.AddHours(1)));
        }

        var cancel = await customer.PostAsJsonAsync(
            $"/api/bookings/{confirmed.Booking.BookingId}/cancel", new CancelBookingRequest(null));
        var result = (await cancel.Content.ReadFromJsonAsync<ApiResponse<CancelBookingResponse>>())!.Data!;

        result.RefundPercent.Should().Be(0m);
        result.RefundAmount.Should().Be(0m);
        result.Booking.Status.Should().Be((byte)BookingStatus.Cancelled);
        result.Booking.PaymentStatus.Should().Be((byte)BookingPaymentStatus.Paid);
    }

    // ── Reschedule / extend ─────────────────────────────────────────────────

    [Fact]
    public async Task Reschedule_MovesTheBooking_AndConflictsAreRefused()
    {
        var (customer, customerId, workspaceId) = await SetUpAsync();
        await CreditWalletAsync(customerId, 5000m);
        var first = await BookAndConfirmAsync(customer, workspaceId, BookingTestHelpers.NextMondayUtc(11));
        var second = await BookAndConfirmAsync(customer, workspaceId, BookingTestHelpers.NextMondayUtc(14));

        // Moving the first onto the second must 409.
        var conflict = await customer.PutAsJsonAsync($"/api/bookings/{first.Booking.BookingId}",
            new RescheduleBookingRequest(BookingTestHelpers.NextMondayUtc(14)));
        conflict.StatusCode.Should().Be(HttpStatusCode.Conflict);

        // Moving to a free slot succeeds and the QR window follows.
        var newStart = BookingTestHelpers.NextMondayUtc(16);
        var move = await customer.PutAsJsonAsync($"/api/bookings/{first.Booking.BookingId}",
            new RescheduleBookingRequest(newStart));
        move.EnsureSuccessStatusCode();
        var moved = (await move.Content.ReadFromJsonAsync<ApiResponse<BookingDto>>())!.Data!;
        moved.StartUtc.Should().Be(newStart);
        moved.TotalAmount.Should().Be(first.Booking.TotalAmount); // same duration, same price

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RegalisDbContext>();
        (await db.AccessTokens.SingleAsync(t => t.BookingId == first.Booking.BookingId))
            .ValidFromUtc.Should().Be(newStart);
        _ = second;
    }

    [Fact]
    public async Task Extend_DebitsWallet_AndIssuesASupplementaryInvoice()
    {
        var (customer, customerId, workspaceId) = await SetUpAsync();
        await CreditWalletAsync(customerId, 5000m);
        var confirmed = await BookAndConfirmAsync(customer, workspaceId,
            BookingTestHelpers.NextMondayUtc(9), minutes: 60);

        var extend = await customer.PostAsJsonAsync(
            $"/api/bookings/{confirmed.Booking.BookingId}/extend",
            new ExtendBookingRequest(60, PaymentMethods.Wallet));
        extend.EnsureSuccessStatusCode();
        var result = (await extend.Content.ReadFromJsonAsync<ApiResponse<ExtendBookingResponse>>())!.Data!;

        result.AdditionalAmount.Should().Be(590m); // 500 + 18% GST
        result.Booking.DurationMinutes.Should().Be(120);
        result.Booking.TotalAmount.Should().Be(1180m);
        result.InvoiceNumber.Should().MatchRegex(@"^REG/INV/\d{4}-\d{2}/\d{5}$");

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RegalisDbContext>();
        (await db.Invoices.CountAsync(i => i.BookingId == confirmed.Booking.BookingId)).Should().Be(2);
    }

    // ── Wallet recharge + admin adjust ──────────────────────────────────────

    [Fact]
    public async Task WalletRecharge_SandboxFlow_CreditsTheLedger()
    {
        var (customer, _, _) = await SetUpAsync();

        var order = (await (await customer.PostAsJsonAsync("/api/wallet/recharge",
                new RechargeOrderRequest(1500m)))
            .Content.ReadFromJsonAsync<ApiResponse<PaymentOrderResponse>>())!.Data!;
        var paid = (await (await customer.PostAsJsonAsync("/api/payments/sandbox-complete",
                new SandboxCompleteRequest(order.GatewayOrderId)))
            .Content.ReadFromJsonAsync<ApiResponse<SandboxCompleteResponse>>())!.Data!;
        var verify = await customer.PostAsJsonAsync("/api/wallet/recharge/verify",
            new RechargeVerifyRequest(order.GatewayOrderId, paid.GatewayPaymentId, paid.GatewaySignature));
        verify.EnsureSuccessStatusCode();
        var result = (await verify.Content.ReadFromJsonAsync<ApiResponse<RechargeResponse>>())!.Data!;

        result.Wallet.Balance.Should().Be(1500m);
        result.TransactionNumber.Should().MatchRegex(@"^WTX-\d{6}$");

        // Replaying the same order must fail (already settled).
        var replay = await customer.PostAsJsonAsync("/api/wallet/recharge/verify",
            new RechargeVerifyRequest(order.GatewayOrderId, paid.GatewayPaymentId, paid.GatewaySignature));
        replay.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task WalletRecharge_OutsideLimits_IsRefused()
    {
        var (customer, _, _) = await SetUpAsync();
        (await customer.PostAsJsonAsync("/api/wallet/recharge", new RechargeOrderRequest(50m)))
            .StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await customer.PostAsJsonAsync("/api/wallet/recharge", new RechargeOrderRequest(90000m)))
            .StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task AdminWalletAdjust_RequiresReason_AndMovesTheBalance()
    {
        var (customer, customerId, _) = await SetUpAsync();
        _ = customer;
        var admin = factory.CreateClient();
        admin.UseToken((await admin.LoginAsAdminAsync()).AccessToken);

        var credit = await admin.PostAsJsonAsync($"/api/wallet/{customerId}/adjust",
            new WalletAdjustRequest("credit", 250m, "Goodwill for outage"));
        credit.EnsureSuccessStatusCode();
        var wallet = (await credit.Content.ReadFromJsonAsync<ApiResponse<WalletDto>>())!.Data!;
        wallet.Balance.Should().Be(250m);

        // No reason → validation failure.
        (await admin.PostAsJsonAsync($"/api/wallet/{customerId}/adjust",
                new WalletAdjustRequest("debit", 50m, "")))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // ── Notifications ───────────────────────────────────────────────────────

    [Fact]
    public async Task Notifications_ConfirmCreatesOne_ReadFlowWorks()
    {
        var (customer, customerId, workspaceId) = await SetUpAsync();
        await CreditWalletAsync(customerId, 5000m);
        await BookAndConfirmAsync(customer, workspaceId, BookingTestHelpers.NextMondayUtc(12));

        var unread = (await customer.GetFromJsonAsync<ApiResponse<int>>(
            "/api/notifications/my/unread-count"))!.Data;
        unread.Should().BeGreaterThan(0);

        var list = (await customer.GetFromJsonAsync<ApiResponse<PagedResult<NotificationDto>>>(
            "/api/notifications/my"))!.Data!;
        list.Items.Should().Contain(n => n.Title == "Booking confirmed");

        (await customer.PostAsync("/api/notifications/read-all", null)).EnsureSuccessStatusCode();
        (await customer.GetFromJsonAsync<ApiResponse<int>>("/api/notifications/my/unread-count"))!
            .Data.Should().Be(0);
    }

    // ── Passes ──────────────────────────────────────────────────────────────

    private async Task<PassPlanDto> CreateDayPassPlanAsync(HttpClient admin, decimal price = 800m)
    {
        var code = $"DP-{Guid.NewGuid().ToString("N")[..6].ToUpperInvariant()}";
        var response = await admin.PostAsJsonAsync("/api/pass-plans", new SavePassPlanRequest(
            code, $"Day Pass {code}", 1, null, price, 1, null,
            "Full-day access to any desk", null, 1, true));
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        return (await response.Content.ReadFromJsonAsync<ApiResponse<PassPlanDto>>())!.Data!;
    }

    [Fact]
    public async Task MembershipPurchase_IsNumberedMem_OffTheSamePassCounter()
    {
        var admin = factory.CreateClient();
        admin.UseToken((await admin.LoginAsAdminAsync()).AccessToken);
        var dayPlan = await CreateDayPassPlanAsync(admin, 100m);
        // The Membership screen sells the MEM-… plans.
        var memCode = $"MEM-T{Guid.NewGuid().ToString("N")[..5].ToUpperInvariant()}-1M";
        var created = await admin.PostAsJsonAsync("/api/pass-plans", new SavePassPlanRequest(
            memCode, "Test membership - Monthly", 2, null, 100m, 30, null,
            "Monthly membership", null, 1, true));
        created.StatusCode.Should().Be(HttpStatusCode.Created);
        var memPlan = (await created.Content.ReadFromJsonAsync<ApiResponse<PassPlanDto>>())!.Data!;

        var (customer, customerId, _) = await SetUpAsync();
        await CreditWalletAsync(customerId, 1000m);
        var tomorrow = DateOnly.FromDateTime(Shared.IstClock.ToIst(DateTime.UtcNow)).AddDays(1);
        async Task<string> BuyAsync(int planId, DateOnly from)
        {
            var r = await customer.PostAsJsonAsync("/api/passes/purchase",
                new PurchasePassRequest(planId, from, PaymentMethods.Wallet, null, null, null));
            r.EnsureSuccessStatusCode();
            return (await r.Content.ReadFromJsonAsync<ApiResponse<PurchasePassResponse>>())!.Data!.Pass.PassNumber;
        }

        var membership = await BuyAsync(memPlan.PassPlanId, tomorrow);
        var dayPass = await BuyAsync(dayPlan.PassPlanId, tomorrow.AddDays(40));

        membership.Should().MatchRegex(@"^MEM-\d{6}$");
        dayPass.Should().MatchRegex(@"^PASS-\d{6}$");
        // One counter behind both series: the numbers follow on, never collide.
        int.Parse(dayPass[5..]).Should().BeGreaterThan(int.Parse(membership[4..]));

        var mine = (await customer.GetFromJsonAsync<ApiResponse<PagedResult<CustomerPassDto>>>(
            "/api/passes/my"))!.Data!;
        mine.Items.Select(p => p.PassNumber).Should().Contain([membership, dayPass]);
    }

    [Fact]
    public async Task PassPurchase_WithWallet_ProducesPassInvoiceAndQr()
    {
        var admin = factory.CreateClient();
        admin.UseToken((await admin.LoginAsAdminAsync()).AccessToken);
        var plan = await CreateDayPassPlanAsync(admin, 800m);

        var (customer, customerId, _) = await SetUpAsync();
        await CreditWalletAsync(customerId, 2000m);

        var tomorrow = DateOnly.FromDateTime(Shared.IstClock.ToIst(DateTime.UtcNow)).AddDays(1);
        var purchase = await customer.PostAsJsonAsync("/api/passes/purchase",
            new PurchasePassRequest(plan.PassPlanId, tomorrow, PaymentMethods.Wallet, null, null, null));
        purchase.EnsureSuccessStatusCode();
        var result = (await purchase.Content.ReadFromJsonAsync<ApiResponse<PurchasePassResponse>>())!.Data!;

        result.Pass.PassNumber.Should().MatchRegex(@"^PASS-\d{6}$");
        result.Pass.TotalAmount.Should().Be(944m); // 800 + 18% GST
        result.Pass.ValidFrom.Should().Be(tomorrow);
        result.Pass.ValidTo.Should().Be(tomorrow); // 1-day validity
        result.QrPayload.Should().StartWith("RGL1.");
        result.InvoiceNumber.Should().MatchRegex(@"^REG/INV/");

        var myPasses = (await customer.GetFromJsonAsync<ApiResponse<PagedResult<CustomerPassDto>>>(
            "/api/passes/my"))!.Data!;
        myPasses.Items.Should().ContainSingle(p => p.PassNumber == result.Pass.PassNumber);
    }

    [Fact]
    public async Task PassPurchase_SandboxGatewayFlow_Works()
    {
        var admin = factory.CreateClient();
        admin.UseToken((await admin.LoginAsAdminAsync()).AccessToken);
        var plan = await CreateDayPassPlanAsync(admin);

        var (customer, _, _) = await SetUpAsync();
        var order = (await (await customer.PostAsJsonAsync("/api/passes/order",
                new PassOrderRequest(plan.PassPlanId)))
            .Content.ReadFromJsonAsync<ApiResponse<PaymentOrderResponse>>())!.Data!;
        var paid = (await (await customer.PostAsJsonAsync("/api/payments/sandbox-complete",
                new SandboxCompleteRequest(order.GatewayOrderId)))
            .Content.ReadFromJsonAsync<ApiResponse<SandboxCompleteResponse>>())!.Data!;

        var tomorrow = DateOnly.FromDateTime(Shared.IstClock.ToIst(DateTime.UtcNow)).AddDays(1);
        var purchase = await customer.PostAsJsonAsync("/api/passes/purchase",
            new PurchasePassRequest(plan.PassPlanId, tomorrow, PaymentMethods.Gateway,
                order.GatewayOrderId, paid.GatewayPaymentId, paid.GatewaySignature));
        purchase.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task PassCancel_BeforeStart_RefundsToWallet()
    {
        var admin = factory.CreateClient();
        admin.UseToken((await admin.LoginAsAdminAsync()).AccessToken);
        var plan = await CreateDayPassPlanAsync(admin, 600m);

        var (customer, customerId, _) = await SetUpAsync();
        await CreditWalletAsync(customerId, 1000m);
        var nextWeek = DateOnly.FromDateTime(Shared.IstClock.ToIst(DateTime.UtcNow)).AddDays(7);
        var purchase = (await (await customer.PostAsJsonAsync("/api/passes/purchase",
                new PurchasePassRequest(plan.PassPlanId, nextWeek, PaymentMethods.Wallet, null, null, null)))
            .Content.ReadFromJsonAsync<ApiResponse<PurchasePassResponse>>())!.Data!;

        // The seeded matrix gives customers no PASS.EDIT — cancellation is a
        // front-desk action.
        (await customer.PostAsync($"/api/passes/{purchase.Pass.CustomerPassId}/cancel", null))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var cancel = await admin.PostAsync($"/api/passes/{purchase.Pass.CustomerPassId}/cancel", null);
        cancel.EnsureSuccessStatusCode();

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RegalisDbContext>();
        (await db.Wallets.SingleAsync(w => w.CustomerId == customerId)).Balance
            .Should().Be(1000m - purchase.Pass.TotalAmount + purchase.Pass.TotalAmount);
        (await db.AccessTokens.SingleAsync(t => t.CustomerPassId == purchase.Pass.CustomerPassId))
            .Status.Should().Be(AccessTokenStatus.Revoked);
    }
}
