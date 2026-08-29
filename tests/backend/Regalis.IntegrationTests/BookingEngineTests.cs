using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Regalis.Application.Bookings;
using Regalis.Domain.Entities.Booking;
using Regalis.Domain.Enums;
using Regalis.Infrastructure.Persistence;
using Regalis.IntegrationTests.Infrastructure;
using Regalis.Shared.Api;
using Regalis.Shared.Constants;

namespace Regalis.IntegrationTests;

[Collection(ApiCollection.Name)]
public class BookingEngineTests(RegalisApiFactory factory)
{
    private async Task<(HttpClient Admin, HttpClient Customer, int WorkspaceId)> SetUpAsync(
        decimal pricePerHour = 500m)
    {
        var admin = factory.CreateClient();
        admin.UseToken((await admin.LoginAsAdminAsync()).AccessToken);
        var workspaceId = await admin.CreateWorkspaceAsync(pricePerHour);

        var customer = factory.CreateClient();
        var (_, _, session) = await customer.RegisterCustomerAsync(factory);
        customer.UseToken(session.AccessToken);
        return (admin, customer, workspaceId);
    }

    [Fact]
    public async Task Quote_ComputesTheGstBreakdown()
    {
        var (_, customer, workspaceId) = await SetUpAsync(pricePerHour: 500m);
        var start = BookingTestHelpers.NextMondayUtc(10);

        var response = await customer.PostAsJsonAsync("/api/bookings/quote",
            new QuoteRequest(workspaceId, start, 120));
        response.EnsureSuccessStatusCode();
        var quote = (await response.Content.ReadFromJsonAsync<ApiResponse<QuoteResponse>>())!.Data!;

        quote.BaseAmount.Should().Be(1000m);   // 500/hr × 2h
        quote.CgstPercent.Should().Be(9m);
        quote.CgstAmount.Should().Be(90m);
        quote.SgstAmount.Should().Be(90m);
        quote.TotalAmount.Should().Be(1180m);
    }

    [Fact]
    public async Task Hold_HappyPath_CreatesAPendingBookingWithGapFreeNumber()
    {
        var (_, customer, workspaceId) = await SetUpAsync();
        var start = BookingTestHelpers.NextMondayUtc(9);

        var response = await customer.PostAsJsonAsync("/api/bookings",
            new CreateBookingRequest(workspaceId, start, 60, "window seat please"));
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var booking = (await response.Content.ReadFromJsonAsync<ApiResponse<BookingDto>>())!.Data!;

        booking.BookingNumber.Should().MatchRegex(@"^REG-\d{6}$");
        booking.Status.Should().Be((byte)BookingStatus.Pending);
        booking.PaymentStatus.Should().Be((byte)BookingPaymentStatus.Pending);
        booking.HoldExpiresUtc.Should().NotBeNull();
        booking.TotalAmount.Should().Be(590m); // 500 + 9% + 9%
    }

    [Fact]
    public async Task Hold_SecondOverlappingRequest_Returns409SlotUnavailable()
    {
        var (_, customer, workspaceId) = await SetUpAsync();
        var start = BookingTestHelpers.NextMondayUtc(11);

        (await customer.PostAsJsonAsync("/api/bookings",
            new CreateBookingRequest(workspaceId, start, 120, null)))
            .StatusCode.Should().Be(HttpStatusCode.Created);

        // Overlaps the middle of the first booking.
        var conflict = await customer.PostAsJsonAsync("/api/bookings",
            new CreateBookingRequest(workspaceId, start.AddMinutes(60), 120, null));
        conflict.StatusCode.Should().Be(HttpStatusCode.Conflict);
        var envelope = await conflict.Content.ReadFromJsonAsync<ApiResponse>();
        envelope!.Errors.Should().ContainSingle(e => e.Code == ErrorCodes.SlotUnavailable);
    }

    [Fact]
    public async Task Hold_AdjacentBookings_BothSucceed_HalfOpenIntervals()
    {
        var (_, customer, workspaceId) = await SetUpAsync();
        var start = BookingTestHelpers.NextMondayUtc(14);

        // [14:00, 15:00) and [15:00, 16:00) do not conflict (ARCHITECTURE §6).
        (await customer.PostAsJsonAsync("/api/bookings",
            new CreateBookingRequest(workspaceId, start, 60, null)))
            .StatusCode.Should().Be(HttpStatusCode.Created);
        (await customer.PostAsJsonAsync("/api/bookings",
            new CreateBookingRequest(workspaceId, start.AddMinutes(60), 60, null)))
            .StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Theory]
    [InlineData(10, 15, 60, "misaligned start")]       // 10:15 IST is not on the 30-min grid
    [InlineData(10, 0, 45, "duration not a multiple")] // 45 min is not a multiple of 30
    [InlineData(10, 0, 510, "duration above max")]     // > 480 min
    [InlineData(6, 0, 60, "before opening hours")]     // location opens 08:00 IST
    [InlineData(20, 30, 60, "runs past closing")]      // 20:30 + 60min > 21:00 IST
    public async Task Hold_RuleViolations_Return422(int hourIst, int minuteIst, int duration, string because)
    {
        var (_, customer, workspaceId) = await SetUpAsync();
        var start = BookingTestHelpers.NextMondayUtc(hourIst, minuteIst);

        var response = await customer.PostAsJsonAsync("/api/bookings",
            new CreateBookingRequest(workspaceId, start, duration, null));
        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity, because);
    }

    [Fact]
    public async Task Hold_OnABlockedWorkspace_Returns409()
    {
        var (_, customer, workspaceId) = await SetUpAsync();
        var start = BookingTestHelpers.NextMondayUtc(16);

        // Housekeeping/maintenance blocks occupy the same calendar (Invariant 12).
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<RegalisDbContext>();
            db.WorkspaceBlocks.Add(new WorkspaceBlock
            {
                WorkspaceId = workspaceId,
                LocationId = 1,
                StartUtc = start,
                EndUtc = start.AddHours(2),
                BlockReason = BlockReason.Maintenance,
                BlockedByUserId = 1
            });
            await db.SaveChangesAsync();
        }

        var response = await customer.PostAsJsonAsync("/api/bookings",
            new CreateBookingRequest(workspaceId, start.AddMinutes(30), 60, null));
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Booking_ConcurrentRequestsForSameSlot_OnlyOneSucceeds()
    {
        // The integration test named by ARCHITECTURE §6: 20 parallel confirmations
        // of one slot must produce exactly one occupancy row.
        var (_, customer, workspaceId) = await SetUpAsync();
        var start = BookingTestHelpers.NextMondayUtc(12);
        var request = new CreateBookingRequest(workspaceId, start, 60, null);

        var attempts = await Task.WhenAll(Enumerable.Range(0, 20).Select(async _ =>
        {
            var response = await customer.PostAsJsonAsync("/api/bookings", request);
            return response.StatusCode;
        }));

        attempts.Count(s => s == HttpStatusCode.Created).Should().Be(1);
        attempts.Count(s => s == HttpStatusCode.Conflict).Should().Be(19);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RegalisDbContext>();
        var occupancyRows = await db.Bookings.CountAsync(b =>
            b.WorkspaceId == workspaceId && b.StartUtc == start &&
            Domain.Entities.Booking.Booking.OccupancyStatuses.Contains(b.Status));
        occupancyRows.Should().Be(1, "the engine must never double-book a slot");
    }

    [Fact]
    public async Task Availability_ReflectsHolds_AndScopesBookingReads()
    {
        var (_, customer, workspaceId) = await SetUpAsync();
        var start = BookingTestHelpers.NextMondayUtc(17);

        // Free before the hold…
        var checkBefore = await customer.PostAsJsonAsync("/api/availability/check",
            new AvailabilityCheckRequest(workspaceId, start, start.AddHours(1)));
        (await checkBefore.Content.ReadFromJsonAsync<ApiResponse<AvailabilityCheckResponse>>())!
            .Data!.IsAvailable.Should().BeTrue();

        var hold = await customer.PostAsJsonAsync("/api/bookings",
            new CreateBookingRequest(workspaceId, start, 60, null));
        var booking = (await hold.Content.ReadFromJsonAsync<ApiResponse<BookingDto>>())!.Data!;

        // …occupied after it: a Pending hold reserves the slot.
        var checkAfter = await customer.PostAsJsonAsync("/api/availability/check",
            new AvailabilityCheckRequest(workspaceId, start, start.AddHours(1)));
        var result = (await checkAfter.Content.ReadFromJsonAsync<ApiResponse<AvailabilityCheckResponse>>())!.Data!;
        result.IsAvailable.Should().BeFalse();
        result.ConflictReason.Should().Be("Booked");

        // Row scoping: another customer cannot read the booking by id.
        var other = factory.CreateClient();
        var (_, _, otherSession) = await other.RegisterCustomerAsync(factory);
        other.UseToken(otherSession.AccessToken);
        (await other.GetAsync($"/api/bookings/{booking.BookingId}"))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
