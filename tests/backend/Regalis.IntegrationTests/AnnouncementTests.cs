using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Regalis.Application.Operations;
using Regalis.Domain.Enums;
using Regalis.Infrastructure.Persistence;
using Regalis.Infrastructure.Services;
using Regalis.IntegrationTests.Infrastructure;
using Regalis.Shared.Api;

namespace Regalis.IntegrationTests;

/// <summary>
/// Announcements (Notifications → Announcements sent): drafts, scheduling, sending to an
/// audience, the read count, and channels that cannot deliver saying so rather than
/// failing silently. A sent announcement is never edited or unsent.
/// </summary>
[Collection(ApiCollection.Name)]
public class AnnouncementTests(RegalisApiFactory factory)
{
    private async Task<HttpClient> AdminAsync()
    {
        var admin = factory.CreateClient();
        admin.UseToken((await admin.LoginAsAdminAsync()).AccessToken);
        return admin;
    }

    private async Task<(HttpClient Client, long CustomerId)> CustomerAsync()
    {
        var customer = factory.CreateClient();
        var (_, _, session) = await customer.RegisterCustomerAsync(factory);
        customer.UseToken(session.AccessToken);
        return (customer, session.User.UserId);
    }

    private static SaveAnnouncementRequest Request(long customerId, string action, bool whatsApp = false,
        bool email = false, DateTime? when = null) =>
        new($"Diwali holiday {Guid.NewGuid():N}"[..30], "The space will be closed on Saturday 1 November.",
            (byte)AnnouncementAudience.OneCustomer, customerId, email, whatsApp, when, action);

    private static async Task<AnnouncementDto> DataAsync(HttpResponseMessage response)
    {
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<ApiResponse<AnnouncementDto>>())!.Data!;
    }

    [Fact]
    public async Task Draft_CanBeEdited_ThenSent_AndASentOneIsFrozen()
    {
        var admin = await AdminAsync();
        var (customer, customerId) = await CustomerAsync();

        var draft = await DataAsync(await admin.PostAsJsonAsync("/api/admin/announcements", Request(customerId, "draft")));
        draft.Status.Should().Be((byte)AnnouncementStatus.Draft);
        draft.RecipientCount.Should().Be(0);

        var sent = await DataAsync(await admin.PutAsJsonAsync($"/api/admin/announcements/{draft.AnnouncementId}",
            Request(customerId, "send") with { Title = "Office closed 1 Nov" }));
        sent.Status.Should().Be((byte)AnnouncementStatus.Sent);
        sent.RecipientCount.Should().Be(1);
        sent.SentUtc.Should().NotBeNull();

        // The customer has it in-app.
        var mine = (await customer.GetFromJsonAsync<ApiResponse<PagedResult<NotificationDto>>>(
            "/api/notifications/my?pageSize=50"))!.Data!;
        var copy = mine.Items.Should().ContainSingle(n => n.Title == "Office closed 1 Nov").Subject;

        // Reading it moves the read count.
        (await customer.PatchAsync($"/api/notifications/{copy.NotificationId}/read", null)).EnsureSuccessStatusCode();
        (await admin.GetFromJsonAsync<ApiResponse<AnnouncementDto>>($"/api/admin/announcements/{sent.AnnouncementId}"))!
            .Data!.ReadCount.Should().Be(1);

        // Frozen: no edit, no delete.
        (await admin.PutAsJsonAsync($"/api/admin/announcements/{sent.AnnouncementId}", Request(customerId, "draft")))
            .IsSuccessStatusCode.Should().BeFalse();
        (await admin.DeleteAsync($"/api/admin/announcements/{sent.AnnouncementId}"))
            .IsSuccessStatusCode.Should().BeFalse();
    }

    [Fact]
    public async Task ChannelsThatCannotDeliver_AreRecordedAsNotSent()
    {
        var admin = await AdminAsync();
        var (_, customerId) = await CustomerAsync();
        var channels = (await admin.GetFromJsonAsync<ApiResponse<AnnouncementChannelsDto>>(
            "/api/admin/announcements/channels"))!.Data!;
        channels.WhatsAppConfigured.Should().BeFalse("no WhatsApp provider is configured under test");

        var sent = await DataAsync(await admin.PostAsJsonAsync("/api/admin/announcements",
            Request(customerId, "send", whatsApp: true, email: !channels.EmailConfigured)));
        sent.WhatsAppManualShare.Should().BeTrue("the list must say WhatsApp did not go out, straight away");
        sent.ChannelsPending.Should().BeTrue();

        using var scope = factory.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<AnnouncementService>().DeliverChannelsAsync(default);
        var after = (await admin.GetFromJsonAsync<ApiResponse<AnnouncementDto>>(
            $"/api/admin/announcements/{sent.AnnouncementId}"))!.Data!;
        after.ChannelsPending.Should().BeFalse();
        after.WhatsAppSentCount.Should().Be(0);
        after.DeliveryNote.Should().Contain("WhatsApp NOT sent").And.Contain("manual share");
        if (!channels.EmailConfigured)
        {
            after.DeliveryNote.Should().Contain("Email NOT sent");
            after.EmailSentCount.Should().Be(0);
        }
    }

    [Fact]
    public async Task Scheduling_NeedsAFutureTime_AndPublishesWhenDue()
    {
        var admin = await AdminAsync();
        var (_, customerId) = await CustomerAsync();

        (await admin.PostAsJsonAsync("/api/admin/announcements",
                Request(customerId, "schedule", when: DateTime.UtcNow.AddMinutes(-5))))
            .IsSuccessStatusCode.Should().BeFalse();

        var scheduled = await DataAsync(await admin.PostAsJsonAsync("/api/admin/announcements",
            Request(customerId, "schedule", when: DateTime.UtcNow.AddHours(2))));
        scheduled.Status.Should().Be((byte)AnnouncementStatus.Scheduled);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RegalisDbContext>();
        var row = await db.Announcements.SingleAsync(a => a.AnnouncementId == scheduled.AnnouncementId);
        row.ScheduledForUtc = DateTime.UtcNow.AddMinutes(-1);
        await db.SaveChangesAsync();
        await scope.ServiceProvider.GetRequiredService<AnnouncementService>().PublishDueAsync(default);

        var published = (await admin.GetFromJsonAsync<ApiResponse<AnnouncementDto>>(
            $"/api/admin/announcements/{scheduled.AnnouncementId}"))!.Data!;
        published.Status.Should().Be((byte)AnnouncementStatus.Sent);
        published.RecipientCount.Should().Be(1);
    }

    [Fact]
    public async Task CompaniesAndIndividuals_SplitTheCustomerBase()
    {
        var admin = await AdminAsync();
        await CustomerAsync();
        async Task<int> Count(AnnouncementAudience a) => (await admin.GetFromJsonAsync<ApiResponse<int>>(
            $"/api/admin/announcements/audience-count?audience={(byte)a}"))!.Data;

        var all = await Count(AnnouncementAudience.AllCustomers);
        var companies = await Count(AnnouncementAudience.CompaniesOnly);
        var individuals = await Count(AnnouncementAudience.IndividualMembers);
        all.Should().BeGreaterThan(0);
        (companies + individuals).Should().Be(all);
    }

    [Fact]
    public async Task Customers_CannotReachTheAnnouncementsApi()
    {
        var (customer, customerId) = await CustomerAsync();
        (await customer.GetAsync("/api/admin/announcements")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await customer.PostAsJsonAsync("/api/admin/announcements", Request(customerId, "send")))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
