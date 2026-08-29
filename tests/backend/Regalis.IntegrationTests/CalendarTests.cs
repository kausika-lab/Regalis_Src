using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Regalis.Application.Bookings;
using Regalis.IntegrationTests.Infrastructure;
using Regalis.Shared;
using Regalis.Shared.Api;

namespace Regalis.IntegrationTests;

[Collection(ApiCollection.Name)]
public class CalendarTests(RegalisApiFactory factory)
{
    [Fact]
    public async Task Calendar_OneDataSource_AdminSeesDetail_CustomerSeesAnonymized()
    {
        var admin = factory.CreateClient();
        admin.UseToken((await admin.LoginAsAdminAsync()).AccessToken);
        var workspaceId = await admin.CreateWorkspaceAsync();

        var customer = factory.CreateClient();
        var (_, _, session) = await customer.RegisterCustomerAsync(factory);
        customer.UseToken(session.AccessToken);

        var start = BookingTestHelpers.NextMondayUtc(9);
        var hold = await customer.PostAsJsonAsync("/api/bookings",
            new CreateBookingRequest(workspaceId, start, 60, null));
        var booking = (await hold.Content.ReadFromJsonAsync<ApiResponse<BookingDto>>())!.Data!;

        var date = DateOnly.FromDateTime(IstClock.ToIst(start));
        var query = $"/api/calendar?view=day&date={date:yyyy-MM-dd}&workspaceId={workspaceId}";

        // Both portals read the same grid (Invariant 1)…
        var adminGrid = (await admin.GetFromJsonAsync<ApiResponse<CalendarResponse>>(query))!.Data!;
        var adminItem = adminGrid.Workspaces.Single().Items.Single(i => i.Kind == "booking");
        adminItem.Title.Should().Contain(booking.BookingNumber); // …with names for admins

        var customerGrid = (await customer.GetFromJsonAsync<ApiResponse<CalendarResponse>>(query))!.Data!;
        var ownItem = customerGrid.Workspaces.Single().Items.Single(i => i.Kind == "booking");
        ownItem.IsOwn.Should().BeTrue();
        ownItem.Title.Should().Contain(booking.BookingNumber); // own booking is identified

        // A second customer sees the slot as reserved, nothing more.
        var other = factory.CreateClient();
        var (_, _, otherSession) = await other.RegisterCustomerAsync(factory);
        other.UseToken(otherSession.AccessToken);
        var otherGrid = (await other.GetFromJsonAsync<ApiResponse<CalendarResponse>>(query))!.Data!;
        var strangerItem = otherGrid.Workspaces.Single().Items.Single(i => i.Kind == "booking");
        strangerItem.Title.Should().Be("Reserved");
        strangerItem.IsOwn.Should().BeFalse();
    }

    [Fact]
    public async Task Block_RemovesAvailability_AndUnblockRestoresIt()
    {
        var admin = factory.CreateClient();
        admin.UseToken((await admin.LoginAsAdminAsync()).AccessToken);
        var workspaceId = await admin.CreateWorkspaceAsync();
        var start = BookingTestHelpers.NextMondayUtc(10);

        var blockResponse = await admin.PostAsJsonAsync("/api/calendar/block",
            new CreateBlockRequest(workspaceId, start, start.AddHours(2), 1, "AC repair"));
        blockResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var block = (await blockResponse.Content.ReadFromJsonAsync<ApiResponse<BlockDto>>())!.Data!;

        var check = await admin.PostAsJsonAsync("/api/availability/check",
            new AvailabilityCheckRequest(workspaceId, start, start.AddHours(1)));
        var blocked = (await check.Content.ReadFromJsonAsync<ApiResponse<AvailabilityCheckResponse>>())!.Data!;
        blocked.IsAvailable.Should().BeFalse();
        blocked.ConflictReason.Should().Be("Blocked");

        (await admin.PostAsync($"/api/calendar/unblock/{block.WorkspaceBlockId}", null))
            .EnsureSuccessStatusCode();
        var checkAfter = await admin.PostAsJsonAsync("/api/availability/check",
            new AvailabilityCheckRequest(workspaceId, start, start.AddHours(1)));
        (await checkAfter.Content.ReadFromJsonAsync<ApiResponse<AvailabilityCheckResponse>>())!
            .Data!.IsAvailable.Should().BeTrue();
    }

    [Fact]
    public async Task Block_OverAConfirmedBooking_IsRefused()
    {
        var admin = factory.CreateClient();
        admin.UseToken((await admin.LoginAsAdminAsync()).AccessToken);
        var workspaceId = await admin.CreateWorkspaceAsync();

        var customer = factory.CreateClient();
        var (_, _, session) = await customer.RegisterCustomerAsync(factory);
        var start = BookingTestHelpers.NextMondayUtc(12);
        await admin.PostAsJsonAsync("/api/bookings/admin",
            new AdminCreateBookingRequest(session.User.UserId, workspaceId, start, 60, null,
                PaymentMethods.Cash));

        var blockResponse = await admin.PostAsJsonAsync("/api/calendar/block",
            new CreateBlockRequest(workspaceId, start, start.AddHours(1), 1, null));
        blockResponse.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Customer_CannotBlock()
    {
        var customer = factory.CreateClient();
        var (_, _, session) = await customer.RegisterCustomerAsync(factory);
        customer.UseToken(session.AccessToken);

        var response = await customer.PostAsJsonAsync("/api/calendar/block",
            new CreateBlockRequest(1, DateTime.UtcNow.AddDays(1), DateTime.UtcNow.AddDays(1).AddHours(1), 1, null));
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
