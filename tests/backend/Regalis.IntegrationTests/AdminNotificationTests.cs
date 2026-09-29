using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Regalis.Application.Finance;
using Regalis.Application.Auth;
using Regalis.Application.Operations;
using Regalis.Domain.Enums;
using Regalis.Infrastructure.BackgroundJobs;
using Regalis.Infrastructure.Persistence;
using Regalis.IntegrationTests.Infrastructure;
using Regalis.Shared.Api;

namespace Regalis.IntegrationTests;

/// <summary>
/// The admin Notifications page: events raised by the backend when they happen, read
/// state per admin, "Needs action" that clears when the record is resolved, and the
/// per-admin channel settings (with WhatsApp's manual-share fallback made visible).
/// </summary>
[Collection(ApiCollection.Name)]
public class AdminNotificationTests(RegalisApiFactory factory)
{
    private async Task<HttpClient> AdminAsync()
    {
        var admin = factory.CreateClient();
        admin.UseToken((await admin.LoginAsAdminAsync()).AccessToken);
        return admin;
    }

    private async Task<(HttpClient Client, long CustomerId, string Name)> CustomerAsync()
    {
        var customer = factory.CreateClient();
        var (_, _, session) = await customer.RegisterCustomerAsync(factory);
        customer.UseToken(session.AccessToken);
        return (customer, session.User.UserId, session.User.FullName);
    }

    private static async Task<AdminNotificationFeedDto> FeedAsync(HttpClient admin, string query) =>
        (await admin.GetFromJsonAsync<ApiResponse<AdminNotificationFeedDto>>(
            $"/api/admin/notifications?pageSize=100&{query}"))!.Data!;

    [Fact]
    public async Task NewCustomer_IsRaised_AndReadStateIsPerAdmin()
    {
        var admin = await AdminAsync();
        var (_, customerId, name) = await CustomerAsync();

        var feed = await FeedAsync(admin, $"customerId={customerId}&type=1");
        var registered = feed.Page.Items.Should().ContainSingle(n => n.EventCode == "customer.registered").Subject;
        registered.CustomerName.Should().Be(name);
        registered.ActionRoute.Should().Be($"/admin/customers/{customerId}");
        registered.IsRead.Should().BeFalse();

        (await admin.PostAsync($"/api/admin/notifications/{registered.AdminNotificationId}/read", null))
            .EnsureSuccessStatusCode();
        (await FeedAsync(admin, $"customerId={customerId}&type=1")).Page.Items
            .Single(n => n.AdminNotificationId == registered.AdminNotificationId).IsRead.Should().BeTrue();

        // One row per event; the read is this admin's alone, not a flag on the event.
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RegalisDbContext>();
        (await db.AdminNotifications.CountAsync(n => n.CustomerId == customerId
            && n.EventCode == "customer.registered")).Should().Be(1);
        (await db.AdminNotificationReads.Where(r => r.AdminNotificationId == registered.AdminNotificationId)
            .Select(r => r.UserId).ToListAsync()).Should().ContainSingle();
    }

    [Fact]
    public async Task CustomerEditingTheirOwnDetails_IsRaisedWithOldAndNewValues()
    {
        var admin = await AdminAsync();
        var (customer, customerId, name) = await CustomerAsync();

        (await customer.PutAsJsonAsync("/api/profile",
            new UpdateProfileRequest(name, "9876500011", "Nimbus Labs", "12 MG Road"))).EnsureSuccessStatusCode();

        var change = (await FeedAsync(admin, $"customerId={customerId}&type=6")).Page.Items
            .Should().ContainSingle(n => n.EventCode == "customer.details_changed").Subject;
        change.Title.Should().Be("Customer updated their account details");
        change.Body.Should().Contain("mobile number").And.Contain("to 9876500011").And.Contain("to Nimbus Labs");
        change.ActionLabel.Should().Be("Review change");
    }

    [Fact]
    public async Task WalletAdjustmentAndLowBalance_AreRaised()
    {
        var admin = await AdminAsync();
        var (_, customerId, _) = await CustomerAsync();
        using (var scope = factory.Services.CreateScope())
        {
            var wallets = scope.ServiceProvider.GetRequiredService<IWalletService>();
            var db = scope.ServiceProvider.GetRequiredService<RegalisDbContext>();
            await wallets.CreditAsync(customerId, 150m, WalletCategory.AdminAdjustment, "Goodwill");
            await db.SaveChangesAsync();
            // 150 → 40 crosses the ₹100 limit: one low-balance alert, not one per debit.
            await wallets.DebitAsync(customerId, 110m, WalletCategory.Booking, "Booking");
            await db.SaveChangesAsync();
            await wallets.DebitAsync(customerId, 10m, WalletCategory.Booking, "Booking");
            await db.SaveChangesAsync();
        }

        var wallet = (await FeedAsync(admin, $"customerId={customerId}&type=4")).Page.Items;
        wallet.Should().ContainSingle(n => n.EventCode == "wallet.adjustment")
            .Which.Body.Should().Contain("+₹150.00").And.Contain("Goodwill");
        wallet.Should().ContainSingle(n => n.EventCode == "wallet.low_balance")
            .Which.Body.Should().Contain("₹40.00");
    }

    [Fact]
    public async Task NewTicket_NeedsAction_UntilTheTicketIsClosed_NotWhenRead()
    {
        var admin = await AdminAsync();
        var (customer, customerId, _) = await CustomerAsync();
        var categories = (await customer.GetFromJsonAsync<ApiResponse<List<TicketCategoryDto>>>(
            "/api/ticket-categories"))!.Data!;
        var category = categories.First(c => c.IssueTypes.Count > 0);
        var ticket = (await (await customer.PostAsJsonAsync("/api/tickets", new CreateTicketRequest(
                category.TicketCategoryId, category.IssueTypes[0].TicketIssueTypeId,
                "Air conditioning not working", "Cabin 06 is too warm.", 3)))
            .Content.ReadFromJsonAsync<ApiResponse<TicketDetailDto>>())!.Data!;

        var raised = (await FeedAsync(admin, $"customerId={customerId}&type=5")).Page.Items
            .Should().ContainSingle(n => n.EventCode == "support.ticket_created").Subject;
        raised.NeedsAction.Should().BeTrue();
        raised.ActionLabel.Should().Be("Assign ticket");
        var before = (await FeedAsync(admin, "")).Summary.NeedsAction;

        // Reading it does not resolve it.
        await admin.PostAsync($"/api/admin/notifications/{raised.AdminNotificationId}/read", null);
        (await FeedAsync(admin, $"customerId={customerId}&needsAction=true")).Page.Items
            .Should().Contain(n => n.AdminNotificationId == raised.AdminNotificationId);

        // Closing the ticket does.
        (await admin.PostAsync($"/api/tickets/{ticket.TicketId}/close", null)).EnsureSuccessStatusCode();
        (await FeedAsync(admin, $"customerId={customerId}&needsAction=true")).Page.Items
            .Should().NotContain(n => n.AdminNotificationId == raised.AdminNotificationId);
        (await FeedAsync(admin, "")).Summary.NeedsAction.Should().Be(before - 1);
    }

    [Fact]
    public async Task Settings_DefaultToEmailForCustomerPaymentAndSupport_AndWhatsAppFallsBackVisibly()
    {
        var admin = await AdminAsync();
        var settings = (await admin.GetFromJsonAsync<ApiResponse<List<AdminNotificationSettingDto>>>(
            "/api/admin/notification-settings"))!.Data!;
        settings.Should().HaveCount(8).And.OnlyContain(s => s.InApp);
        settings.Where(s => s.Email).Select(s => s.Type).Should().BeEquivalentTo(new byte[] { 1, 3, 5 });
        settings.Should().OnlyContain(s => !s.WhatsApp);

        try
        {
            // WhatsApp on for new customers. No provider is configured under test.
            (await admin.PutAsJsonAsync("/api/admin/notification-settings", new SaveAdminNotificationSettingsRequest(
                [new AdminNotificationSettingItem(1, false, true)]))).EnsureSuccessStatusCode();
            var (_, customerId, _) = await CustomerAsync();

            using var scope = factory.Services.CreateScope();
            await scope.ServiceProvider.GetRequiredService<AdminNotificationJobs>().DeliverPendingAsync(default);
            var db = scope.ServiceProvider.GetRequiredService<RegalisDbContext>();
            var row = await db.AdminNotifications.AsNoTracking()
                .SingleAsync(n => n.CustomerId == customerId && n.EventCode == "customer.registered");
            row.DeliveredUtc.Should().NotBeNull();
            row.DeliveryNote.Should().Contain("WhatsApp NOT sent").And.Contain("manual share");
        }
        finally
        {
            await admin.PutAsJsonAsync("/api/admin/notification-settings", new SaveAdminNotificationSettingsRequest(
                [new AdminNotificationSettingItem(1, true, false)]));
        }
    }

    [Fact]
    public async Task OverdueInvoice_IsRaisedOnce_AndClearsWhenPaid()
    {
        var admin = await AdminAsync();
        var (_, customerId, _) = await CustomerAsync();
        long invoiceId;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<RegalisDbContext>();
            var invoice = new Domain.Entities.Finance.Invoice
            {
                InvoiceNumber = $"TEST/INV/{Guid.NewGuid().ToString("N")[..8]}",
                CustomerId = customerId,
                LocationId = 1,
                InvoiceDate = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-20),
                DueDate = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-5),
                SubTotal = 1000m, TaxableAmount = 1000m, TotalAmount = 1000m,
                Status = InvoiceStatus.Sent,
                CustomerName = "Test", CustomerEmail = "t@example.com", CompanyName = "Regalis WorkSpace"
            };
            db.Invoices.Add(invoice);
            await db.SaveChangesAsync();
            invoiceId = invoice.InvoiceId;

            var jobs = scope.ServiceProvider.GetRequiredService<AdminNotificationJobs>();
            await jobs.RaiseOverdueInvoicesAsync(default);
            await jobs.RaiseOverdueInvoicesAsync(default);
        }

        var overdue = (await FeedAsync(admin, $"customerId={customerId}&type=3")).Page.Items
            .Where(n => n.EventCode == "invoice.overdue").ToList();
        overdue.Should().ContainSingle("the scan raises an invoice once, however often it runs");
        overdue[0].NeedsAction.Should().BeTrue();

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<RegalisDbContext>();
            var invoice = await db.Invoices.SingleAsync(i => i.InvoiceId == invoiceId);
            invoice.Status = InvoiceStatus.Paid;
            await db.SaveChangesAsync();
        }
        (await FeedAsync(admin, $"customerId={customerId}&type=3")).Page.Items
            .Single(n => n.EventCode == "invoice.overdue").NeedsAction.Should().BeFalse();
    }

    [Fact]
    public async Task MarkAllRead_ClearsTheUnreadCount_ForThisAdmin()
    {
        var admin = await AdminAsync();
        await CustomerAsync();
        (await admin.GetFromJsonAsync<ApiResponse<int>>("/api/notifications/my/unread-count"))!
            .Data.Should().BeGreaterThan(0, "the bell counts the admin feed for admin users");

        (await admin.PostAsync("/api/admin/notifications/read-all", null)).EnsureSuccessStatusCode();

        (await admin.GetFromJsonAsync<ApiResponse<int>>("/api/admin/notifications/unread-count"))!
            .Data.Should().Be(0);
        (await admin.GetFromJsonAsync<ApiResponse<int>>("/api/notifications/my/unread-count"))!
            .Data.Should().Be(0);
    }

    [Fact]
    public async Task Customers_CannotReadTheAdminFeed()
    {
        var (customer, _, _) = await CustomerAsync();
        (await customer.GetAsync("/api/admin/notifications")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await customer.GetAsync("/api/admin/notification-settings")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
