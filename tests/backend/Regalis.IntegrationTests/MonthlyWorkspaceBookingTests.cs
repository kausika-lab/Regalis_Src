using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Regalis.Application.Access;
using Regalis.Application.Bookings;
using Regalis.Application.Finance;
using Regalis.Application.Masters;
using Regalis.Domain.Enums;
using Regalis.Infrastructure.Persistence;
using Regalis.IntegrationTests.Infrastructure;
using Regalis.Shared;
using Regalis.Shared.Api;
using Regalis.Shared.Constants;

namespace Regalis.IntegrationTests;

/// <summary>
/// Desks and cabins booked by the month: the booking holds one workspace for the whole
/// 1/3/6/12-month term, is priced at the workspace's PricePerMonth x months, blocks every
/// hourly booking inside the term, shows as unavailable (not hidden) on the pick list, and
/// survives a daily check-out.
/// </summary>
[Collection(ApiCollection.Name)]
public class MonthlyWorkspaceBookingTests(RegalisApiFactory factory)
{
    private async Task<HttpClient> AdminAsync()
    {
        var admin = factory.CreateClient();
        admin.UseToken((await admin.LoginAsAdminAsync()).AccessToken);
        return admin;
    }

    private static async Task<(int WorkspaceId, int TypeId)> CreateAsync(HttpClient admin, string typeCode,
        decimal? pricePerMonth, string? name = null)
    {
        var types = (await admin.GetFromJsonAsync<ApiResponse<List<WorkspaceTypeDto>>>(
            "/api/workspace-types"))!.Data!;
        var typeId = types.Single(t => t.TypeCode == typeCode).WorkspaceTypeId;
        var code = $"M-{Guid.NewGuid().ToString("N")[..8].ToUpperInvariant()}";
        var response = await admin.PostAsJsonAsync("/api/workspaces", new SaveWorkspaceRequest(
            LocationId: 1, WorkspaceTypeId: typeId, WorkspaceCode: code, WorkspaceName: name ?? $"Monthly {code}",
            FloorNumber: "1", Capacity: 1, PricePerHour: 200m, PricePerDay: null, PricePerMonth: pricePerMonth,
            Equipment: null, Description: null, IsBookable: true, AmenityIds: []));
        response.EnsureSuccessStatusCode();
        return ((await response.Content.ReadFromJsonAsync<ApiResponse<WorkspaceDto>>())!.Data!.WorkspaceId, typeId);
    }

    private async Task<(HttpClient Client, long UserId)> CustomerWithWalletAsync(decimal credit)
    {
        var customer = factory.CreateClient();
        var (_, _, session) = await customer.RegisterCustomerAsync(factory);
        customer.UseToken(session.AccessToken);
        using var scope = factory.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<IWalletService>()
            .CreditAsync(session.User.UserId, credit, WalletCategory.AdminAdjustment, "Test credit");
        await scope.ServiceProvider.GetRequiredService<RegalisDbContext>().SaveChangesAsync();
        return (customer, session.User.UserId);
    }

    private static DateOnly NextMonday() =>
        DateOnly.FromDateTime(IstClock.ToIst(BookingTestHelpers.NextMondayUtc(9)));

    private static async Task<ConfirmBookingResponse> BookMonthlyAsync(HttpClient customer, int workspaceId,
        DateOnly start, int months)
    {
        var hold = await customer.PostAsJsonAsync("/api/bookings/monthly",
            new MonthlyBookingRequest(workspaceId, start, months));
        hold.StatusCode.Should().Be(HttpStatusCode.Created, await hold.Content.ReadAsStringAsync());
        var booking = (await hold.Content.ReadFromJsonAsync<ApiResponse<BookingDto>>())!.Data!;
        var confirm = await customer.PostAsJsonAsync($"/api/bookings/{booking.BookingId}/confirm",
            new ConfirmBookingRequest(PaymentMethods.Wallet, null, null, null));
        confirm.EnsureSuccessStatusCode();
        return (await confirm.Content.ReadFromJsonAsync<ApiResponse<ConfirmBookingResponse>>())!.Data!;
    }

    private static async Task<List<MonthlyWorkspaceDto>> MonthlyListAsync(HttpClient client, int typeId,
        DateOnly start, int months) =>
        (await client.GetFromJsonAsync<ApiResponse<List<MonthlyWorkspaceDto>>>(
            $"/api/availability/monthly?locationId=1&workspaceTypeId={typeId}&startDate={start:yyyy-MM-dd}&months={months}"))!.Data!;

    [Fact]
    public async Task MonthlyDesk_HoldsTheWholeTerm_PricedByTheMonth_AndShowsAsTaken()
    {
        var admin = await AdminAsync();
        var (desk, deskType) = await CreateAsync(admin, "DESK", 9000m);
        var (customer, _) = await CustomerWithWalletAsync(50_000m);
        var start = NextMonday();

        var quote = (await (await customer.PostAsJsonAsync("/api/bookings/monthly/quote",
                new MonthlyBookingRequest(desk, start, 3)))
            .Content.ReadFromJsonAsync<ApiResponse<MonthlyQuoteResponse>>())!.Data!;
        quote.BaseAmount.Should().Be(27_000m, "9,000 a month for 3 months");
        quote.TotalAmount.Should().Be(31_860m, "plus 9% CGST and 9% SGST");
        quote.EndDate.Should().Be(start.AddMonths(3).AddDays(-1));

        var before = await MonthlyListAsync(customer, deskType, start, 3);
        before.Single(w => w.WorkspaceId == desk).IsAvailable.Should().BeTrue();

        var confirmed = await BookMonthlyAsync(customer, desk, start, 3);
        confirmed.Booking.BillingMonths.Should().Be((byte)3);
        confirmed.Booking.TotalAmount.Should().Be(31_860m);
        IstClock.IstDateOf(confirmed.Booking.EndUtc).Should().Be(quote.EndDate);

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<RegalisDbContext>();
            var line = await db.InvoiceLines.SingleAsync(l => l.InvoiceId == confirmed.InvoiceId);
            line.Quantity.Should().Be(3m);
            line.UnitPrice.Should().Be(9000m);
            line.Description.Should().Contain("Monthly workspace booking").And.Contain("3 months");
        }

        // Still listed, but taken - greyed out on the pick list, not removed.
        var after = await MonthlyListAsync(customer, deskType, start, 3);
        var taken = after.Single(w => w.WorkspaceId == desk);
        taken.IsAvailable.Should().BeFalse();
        taken.UnavailableReason.Should().Be("Booked");
        // An overlapping later term is taken too.
        (await MonthlyListAsync(customer, deskType, start.AddDays(20), 1))
            .Single(w => w.WorkspaceId == desk).IsAvailable.Should().BeFalse();

        // No hourly booking can land inside the term.
        var (other, _) = await CustomerWithWalletAsync(5000m);
        var middle = IstClock.ToUtc(start.AddDays(14).ToDateTime(new TimeOnly(10, 0)));
        while (IstClock.ToIst(middle).DayOfWeek == DayOfWeek.Sunday) middle = middle.AddDays(1);
        (await other.PostAsJsonAsync("/api/bookings", new CreateBookingRequest(desk, middle, 60, null)))
            .StatusCode.Should().Be(HttpStatusCode.Conflict);

        // The hourly pick list hides it by default and greys it out on request.
        var hourly = $"/api/availability/workspaces?locationId=1&workspaceTypeId={deskType}" +
                     $"&startUtc={middle:O}&durationMinutes=60";
        (await other.GetFromJsonAsync<ApiResponse<List<AvailableWorkspaceDto>>>(hourly))!.Data!
            .Should().NotContain(w => w.WorkspaceId == desk);
        (await other.GetFromJsonAsync<ApiResponse<List<AvailableWorkspaceDto>>>(hourly + "&includeBusy=true"))!.Data!
            .Single(w => w.WorkspaceId == desk).IsAvailable.Should().BeFalse();

        // A monthly term is not rescheduled or extended like an hourly slot.
        (await customer.PutAsJsonAsync($"/api/bookings/{confirmed.Booking.BookingId}",
                new RescheduleBookingRequest(middle)))
            .StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task MonthlyBooking_Rules()
    {
        var admin = await AdminAsync();
        var (noPrice, deskType) = await CreateAsync(admin, "DESK", null);
        var (room, roomType) = await CreateAsync(admin, "MEETING_ROOM", 5000m);
        var (desk, _) = await CreateAsync(admin, "DESK", 9000m);
        var (customer, _) = await CustomerWithWalletAsync(1000m);
        var start = NextMonday();

        async Task<string> RefusedAsync(MonthlyBookingRequest request, HttpStatusCode expected)
        {
            var response = await customer.PostAsJsonAsync("/api/bookings/monthly", request);
            response.StatusCode.Should().Be(expected);
            return await response.Content.ReadAsStringAsync();
        }

        (await RefusedAsync(new(noPrice, start, 1), HttpStatusCode.UnprocessableEntity))
            .Should().Contain("no monthly price");
        (await RefusedAsync(new(room, start, 1), HttpStatusCode.UnprocessableEntity))
            .Should().Contain("Only desks and cabins");
        (await RefusedAsync(new(desk, start, 2), HttpStatusCode.BadRequest))
            .Should().Contain("1, 3, 6 or 12");
        (await RefusedAsync(new(desk, start.AddDays(-14), 1), HttpStatusCode.UnprocessableEntity))
            .Should().Contain("past");

        (await MonthlyListAsync(customer, deskType, start, 1))
            .Single(w => w.WorkspaceId == noPrice).UnavailableReason.Should().Be("NoMonthlyPrice");
        (await customer.GetAsync(
                $"/api/availability/monthly?locationId=1&workspaceTypeId={roomType}&startDate={start:yyyy-MM-dd}&months=1"))
            .StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task HotDesk_IsAlwaysBooked_ByTheMonth()
    {
        var admin = await AdminAsync();
        var (hot, deskType) = await CreateAsync(admin, "DESK", 9000m, $"Hot Desk {Guid.NewGuid().ToString("N")[..6]}");
        var (customer, _) = await CustomerWithWalletAsync(50_000m);
        var start = NextMonday();

        // Listed, greyed out as booked although nobody holds it.
        var listed = (await MonthlyListAsync(customer, deskType, start, 1)).Single(w => w.WorkspaceId == hot);
        listed.IsAvailable.Should().BeFalse();
        listed.UnavailableReason.Should().Be("Booked");

        (await customer.PostAsJsonAsync("/api/bookings/monthly/quote", new MonthlyBookingRequest(hot, start, 1)))
            .StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await customer.PostAsJsonAsync("/api/bookings/monthly", new MonthlyBookingRequest(hot, start, 1)))
            .StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task DailyCheckOut_DoesNotEndAMonthlyTerm()
    {
        var admin = await AdminAsync();
        var (desk, deskType) = await CreateAsync(admin, "DESK", 9000m);
        var (customer, _) = await CustomerWithWalletAsync(20_000m);
        var start = NextMonday();
        var confirmed = await BookMonthlyAsync(customer, desk, start, 1);

        // Move the term so it is running now.
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<RegalisDbContext>();
            var booking = await db.Bookings.SingleAsync(b => b.BookingId == confirmed.Booking.BookingId);
            var shift = booking.StartUtc - DateTime.UtcNow.AddHours(-1);
            booking.StartUtc -= shift;
            booking.EndUtc -= shift;
            await db.SaveChangesAsync();
        }

        (await customer.PostAsJsonAsync("/api/attendance/check-in", new SelfCheckRequest(null)))
            .EnsureSuccessStatusCode();
        (await customer.PostAsync("/api/attendance/check-out", null)).EnsureSuccessStatusCode();

        var after = (await customer.GetFromJsonAsync<ApiResponse<BookingDto>>(
            $"/api/bookings/{confirmed.Booking.BookingId}"))!.Data!;
        after.Status.Should().Be((byte)BookingStatus.CheckedIn, "the desk stays the member's for the term");
    }
}
