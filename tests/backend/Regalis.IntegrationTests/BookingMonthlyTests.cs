using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Regalis.Application.Bookings;
using Regalis.IntegrationTests.Infrastructure;
using Regalis.Shared;
using Regalis.Shared.Api;

namespace Regalis.IntegrationTests;

/// <summary>
/// The Bookings screen's monthly chart: counts match the admin list under the same
/// filters, revenue counts only bookings that were kept, and customers cannot read it.
/// Each test filters by a freshly registered customer so other tests' bookings in the
/// shared database cannot move the numbers.
/// </summary>
[Collection(ApiCollection.Name)]
public class BookingMonthlyTests(RegalisApiFactory factory)
{
    [Fact]
    public async Task Monthly_CountsBookingsAndOnlyKeptRevenue()
    {
        var admin = factory.CreateClient();
        admin.UseToken((await admin.LoginAsAdminAsync()).AccessToken);
        var workspaceId = await admin.CreateWorkspaceAsync();
        var customer = factory.CreateClient();
        var (_, _, session) = await customer.RegisterCustomerAsync(factory);
        var customerId = session.User.UserId;

        var start = BookingTestHelpers.NextMondayUtc(10);
        foreach (var hours in new[] { 0, 2 })
        {
            (await admin.PostAsJsonAsync("/api/bookings/admin",
                    new AdminCreateBookingRequest(customerId, workspaceId, start.AddHours(hours), 60,
                        null, PaymentMethods.Cash)))
                .IsSuccessStatusCode.Should().BeTrue();
        }

        var list = (await admin.GetFromJsonAsync<ApiResponse<PagedResult<BookingDto>>>(
            $"/api/bookings?customerId={customerId}"))!.Data!;
        list.Items.Should().HaveCount(2);
        var istDate = DateOnly.FromDateTime(start.AddMinutes(330));
        var url = $"/api/bookings/monthly?customerId={customerId}&year={istDate.Year}";

        var monthly = (await admin.GetFromJsonAsync<ApiResponse<BookingMonthlyDto>>(url))!.Data!;
        monthly.Months.Select(m => m.Month).Should().Equal(Enumerable.Range(1, 12));
        monthly.TotalCount.Should().Be(2);
        var month = monthly.Months[istDate.Month - 1];
        month.Count.Should().Be(2);
        month.Revenue.Should().Be(list.Items.Sum(b => b.TotalAmount));
        monthly.TotalRevenue.Should().Be(month.Revenue);

        // A cancelled booking still counts as a booking but brings in no revenue.
        var cancelled = list.Items[0];
        (await admin.PostAsJsonAsync($"/api/bookings/{cancelled.BookingId}/cancel",
            new CancelBookingRequest("Test"))).IsSuccessStatusCode.Should().BeTrue();
        var after = (await admin.GetFromJsonAsync<ApiResponse<BookingMonthlyDto>>(url))!.Data!;
        after.TotalCount.Should().Be(2);
        after.TotalRevenue.Should().Be(list.Items[1].TotalAmount);

        // The status filter applies, like the list's.
        var onlyCancelled = (await admin.GetFromJsonAsync<ApiResponse<BookingMonthlyDto>>(
            $"{url}&status=5"))!.Data!;
        onlyCancelled.TotalCount.Should().Be(1);
        onlyCancelled.TotalRevenue.Should().Be(0m);
    }

    [Fact]
    public async Task Monthly_IsRefusedToCustomers()
    {
        var customer = factory.CreateClient();
        var (_, _, session) = await customer.RegisterCustomerAsync(factory);
        customer.UseToken(session.AccessToken);
        (await customer.GetAsync("/api/bookings/monthly")).StatusCode
            .Should().Be(HttpStatusCode.Forbidden);
    }
}
