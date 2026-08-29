using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Regalis.Application.Access;
using Regalis.Application.Bookings;
using Regalis.Application.Finance;
using Regalis.Domain.Enums;
using Regalis.Infrastructure.Persistence;
using Regalis.IntegrationTests.Infrastructure;
using Regalis.Shared.Api;
using Regalis.Shared.Constants;

namespace Regalis.IntegrationTests;

[Collection(ApiCollection.Name)]
public class AccessControlTests(RegalisApiFactory factory)
{
    private async Task<(HttpClient Admin, HttpClient Customer, long CustomerId,
        ConfirmBookingResponse Confirmed)> BookNowAsync()
    {
        var admin = factory.CreateClient();
        admin.UseToken((await admin.LoginAsAdminAsync()).AccessToken);
        var workspaceId = await admin.CreateWorkspaceAsync();

        var customer = factory.CreateClient();
        var (_, _, session) = await customer.RegisterCustomerAsync(factory);
        customer.UseToken(session.AccessToken);

        // Admin books for the customer (cash) at next Monday 09:00 IST, then the
        // test moves the window over "now" so the token is live.
        var start = BookingTestHelpers.NextMondayUtc(9);
        var response = await admin.PostAsJsonAsync("/api/bookings/admin",
            new AdminCreateBookingRequest(session.User.UserId, workspaceId, start, 120, null,
                PaymentMethods.Cash));
        response.EnsureSuccessStatusCode();
        var confirmed = (await response.Content
            .ReadFromJsonAsync<ApiResponse<ConfirmBookingResponse>>())!.Data!;

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RegalisDbContext>();
        var nowUtc = DateTime.UtcNow;
        await db.Bookings.Where(b => b.BookingId == confirmed.Booking.BookingId)
            .ExecuteUpdateAsync(s => s
                .SetProperty(b => b.StartUtc, nowUtc.AddMinutes(-10))
                .SetProperty(b => b.EndUtc, nowUtc.AddHours(2)));
        await db.AccessTokens.Where(t => t.BookingId == confirmed.Booking.BookingId)
            .ExecuteUpdateAsync(s => s
                .SetProperty(t => t.ValidFromUtc, nowUtc.AddMinutes(-10))
                .SetProperty(t => t.ValidToUtc, nowUtc.AddHours(2)));

        return (admin, customer, session.User.UserId, confirmed);
    }

    [Fact]
    public async Task Validate_GrantIn_ChecksInBookingAndWritesAttendance()
    {
        var (admin, _, customerId, confirmed) = await BookNowAsync();

        var response = await admin.PostAsJsonAsync("/api/access/validate",
            new ValidateAccessRequest(confirmed.QrPayload, null, 1, "GATE-1", 1));
        response.EnsureSuccessStatusCode();
        var decision = (await response.Content.ReadFromJsonAsync<ApiResponse<AccessDecisionDto>>())!.Data!;

        decision.IsGranted.Should().BeTrue();
        decision.Reference.Should().Be(confirmed.Booking.BookingNumber);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RegalisDbContext>();
        (await db.Bookings.SingleAsync(b => b.BookingId == confirmed.Booking.BookingId))
            .Status.Should().Be(BookingStatus.CheckedIn);
        var attendance = await db.Attendances.SingleAsync(a => a.UserId == customerId);
        attendance.Status.Should().Be(AttendanceStatus.CheckedIn);
        attendance.BookingId.Should().Be(confirmed.Booking.BookingId);
        (await db.AccessLogs.CountAsync(l => l.UserId == customerId &&
            l.Result == AccessResult.Granted)).Should().Be(1);
    }

    [Fact]
    public async Task Validate_OutAfterIn_CompletesBookingAndClosesAttendance()
    {
        var (admin, _, customerId, confirmed) = await BookNowAsync();
        await admin.PostAsJsonAsync("/api/access/validate",
            new ValidateAccessRequest(confirmed.QrPayload, null, 1, "GATE-1", 1));

        var exit = await admin.PostAsJsonAsync("/api/access/validate",
            new ValidateAccessRequest(confirmed.QrPayload, null, 2, "GATE-1", 1));
        var decision = (await exit.Content.ReadFromJsonAsync<ApiResponse<AccessDecisionDto>>())!.Data!;
        decision.IsGranted.Should().BeTrue();

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RegalisDbContext>();
        (await db.Bookings.SingleAsync(b => b.BookingId == confirmed.Booking.BookingId))
            .Status.Should().Be(BookingStatus.Completed);
        var attendance = await db.Attendances.SingleAsync(a => a.UserId == customerId);
        attendance.Status.Should().Be(AttendanceStatus.CheckedOut);
        attendance.TotalMinutes.Should().NotBeNull();
    }

    [Fact]
    public async Task Validate_DenyPaths_TamperedRevokedExitWithoutEntry()
    {
        var (admin, _, _, confirmed) = await BookNowAsync();

        // Tampered payload → TokenNotFound, logged.
        var tampered = await admin.PostAsJsonAsync("/api/access/validate",
            new ValidateAccessRequest($"RGL1.{confirmed.AccessTokenValue}.forged-signature",
                null, 1, "GATE-1", 1));
        var tamperedDecision = (await tampered.Content
            .ReadFromJsonAsync<ApiResponse<AccessDecisionDto>>())!.Data!;
        tamperedDecision.IsGranted.Should().BeFalse();
        tamperedDecision.DenyReason.Should().Be(DenyReasons.TokenNotFound);

        // Exit without an entry → AlreadyCheckedOut.
        var exit = await admin.PostAsJsonAsync("/api/access/validate",
            new ValidateAccessRequest(confirmed.QrPayload, null, 2, "GATE-1", 1));
        (await exit.Content.ReadFromJsonAsync<ApiResponse<AccessDecisionDto>>())!
            .Data!.DenyReason.Should().Be(DenyReasons.AlreadyCheckedOut);

        // Cancelling the booking revokes the token → TokenRevoked.
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<RegalisDbContext>();
            // Move the start back to the future so cancellation rules allow it.
            await db.Bookings.Where(b => b.BookingId == confirmed.Booking.BookingId)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(b => b.StartUtc, DateTime.UtcNow.AddDays(3))
                    .SetProperty(b => b.EndUtc, DateTime.UtcNow.AddDays(3).AddHours(2)));
        }
        await admin.PostAsJsonAsync($"/api/bookings/{confirmed.Booking.BookingId}/cancel",
            new CancelBookingRequest("test"));
        var revoked = await admin.PostAsJsonAsync("/api/access/validate",
            new ValidateAccessRequest(confirmed.QrPayload, null, 1, "GATE-1", 1));
        (await revoked.Content.ReadFromJsonAsync<ApiResponse<AccessDecisionDto>>())!
            .Data!.DenyReason.Should().Be(DenyReasons.TokenRevoked);
    }

    [Fact]
    public async Task Validate_ExpiredWindow_IsDenied()
    {
        var (admin, _, _, confirmed) = await BookNowAsync();
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<RegalisDbContext>();
            await db.AccessTokens.Where(t => t.BookingId == confirmed.Booking.BookingId)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(t => t.ValidFromUtc, DateTime.UtcNow.AddHours(-4))
                    .SetProperty(t => t.ValidToUtc, DateTime.UtcNow.AddHours(-2)));
        }
        var response = await admin.PostAsJsonAsync("/api/access/validate",
            new ValidateAccessRequest(confirmed.QrPayload, null, 1, "GATE-1", 1));
        (await response.Content.ReadFromJsonAsync<ApiResponse<AccessDecisionDto>>())!
            .Data!.DenyReason.Should().Be(DenyReasons.OutsideValidWindow);
    }

    [Fact]
    public async Task Validate_DeviceOffline_IsDeviceErrorNeverAGrant()
    {
        var (admin, _, customerId, confirmed) = await BookNowAsync();

        // The mock adapter's documented simulation: OFFLINE* device ids are down.
        var response = await admin.PostAsJsonAsync("/api/access/validate",
            new ValidateAccessRequest(confirmed.QrPayload, null, 1, "OFFLINE-GATE", 1));
        var decision = (await response.Content.ReadFromJsonAsync<ApiResponse<AccessDecisionDto>>())!.Data!;

        decision.IsGranted.Should().BeFalse();
        decision.Result.Should().Be((byte)AccessResult.DeviceError);
        decision.DenyReason.Should().Be(DenyReasons.DeviceOffline);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RegalisDbContext>();
        // Invariant 9: nothing was granted — no attendance, booking unchanged.
        (await db.Attendances.AnyAsync(a => a.UserId == customerId)).Should().BeFalse();
        (await db.Bookings.SingleAsync(b => b.BookingId == confirmed.Booking.BookingId))
            .Status.Should().Be(BookingStatus.Confirmed);
    }

    [Fact]
    public async Task ManualOverride_RequiresThePermission_AndIsExceptionLogged()
    {
        var (admin, customer, customerId, confirmed) = await BookNowAsync();

        // A customer (no ACCESS.OVERRIDE) is refused.
        var forbidden = await customer.PostAsJsonAsync("/api/access/manual-override",
            new ManualOverrideRequest(customerId, 1, null, 1, null, "let me in"));
        forbidden.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        // The admin override grants, logs the exception and audits it.
        var response = await admin.PostAsJsonAsync("/api/access/manual-override",
            new ManualOverrideRequest(customerId, 1, null, 1, confirmed.Booking.BookingId,
                "Device down at the gate — verified identity by ID card"));
        response.EnsureSuccessStatusCode();
        var decision = (await response.Content.ReadFromJsonAsync<ApiResponse<AccessDecisionDto>>())!.Data!;
        decision.IsGranted.Should().BeTrue();

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RegalisDbContext>();
        var log = await db.AccessLogs.SingleAsync(l => l.AccessLogId == decision.AccessLogId);
        log.IsManualOverride.Should().BeTrue();
        log.OverrideReason.Should().Contain("Device down");
        (await db.AuditLogs.AnyAsync(a => a.Action == AuditActions.AccessOverride &&
            a.EntityId == customerId.ToString())).Should().BeTrue();
        (await db.Attendances.AnyAsync(a => a.UserId == customerId)).Should().BeTrue();
        (await db.Bookings.SingleAsync(b => b.BookingId == confirmed.Booking.BookingId))
            .Status.Should().Be(BookingStatus.CheckedIn);
    }

    [Fact]
    public async Task SelfCheck_WithLiveBooking_InAndOut()
    {
        var (_, customer, _, confirmed) = await BookNowAsync();
        _ = confirmed;

        var checkIn = await customer.PostAsJsonAsync("/api/attendance/check-in",
            new SelfCheckRequest(null));
        checkIn.EnsureSuccessStatusCode();
        var open = (await customer.GetFromJsonAsync<ApiResponse<AttendanceDto>>(
            "/api/attendance/my/open"))!.Data;
        open.Should().NotBeNull();
        open!.CheckInMethod.Should().Be((byte)AccessMethod.App);

        var checkOut = await customer.PostAsync("/api/attendance/check-out", null);
        checkOut.EnsureSuccessStatusCode();
        (await customer.GetFromJsonAsync<ApiResponse<AttendanceDto>>("/api/attendance/my/open"))!
            .Data.Should().BeNull();
    }

    [Fact]
    public async Task SelfCheckIn_WithoutBasis_IsRefused()
    {
        var customer = factory.CreateClient();
        var (_, _, session) = await customer.RegisterCustomerAsync(factory);
        customer.UseToken(session.AccessToken);

        var response = await customer.PostAsJsonAsync("/api/attendance/check-in",
            new SelfCheckRequest(null));
        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task Correction_KeepsOriginals_AndPayrollExportsCsv()
    {
        var (admin, customer, customerId, _) = await BookNowAsync();
        await customer.PostAsJsonAsync("/api/attendance/check-in", new SelfCheckRequest(null));
        await customer.PostAsync("/api/attendance/check-out", null);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RegalisDbContext>();
        var attendance = await db.Attendances.SingleAsync(a => a.UserId == customerId);
        var originalCheckIn = attendance.CheckInUtc;

        var correctedIn = originalCheckIn.AddMinutes(-30);
        var correctedOut = originalCheckIn.AddHours(3);
        var correct = await admin.PutAsJsonAsync($"/api/attendance/{attendance.AttendanceId}",
            new CorrectAttendanceRequest(correctedIn, correctedOut, "Forgot to badge in"));
        correct.EnsureSuccessStatusCode();
        var corrected = (await correct.Content.ReadFromJsonAsync<ApiResponse<AttendanceDto>>())!.Data!;

        corrected.IsCorrected.Should().BeTrue();
        corrected.OriginalCheckInUtc.Should().BeCloseTo(originalCheckIn, TimeSpan.FromSeconds(1));
        corrected.TotalMinutes.Should().Be(210);

        // Payroll export includes the corrected row, flagged.
        var today = corrected.AttendanceDate.ToString("yyyy-MM-dd");
        var export = await admin.GetAsync($"/api/payroll/export?from={today}&to={today}");
        export.EnsureSuccessStatusCode();
        export.Content.Headers.ContentType!.MediaType.Should().Be("text/csv");
        var csv = await export.Content.ReadAsStringAsync();
        csv.Should().Contain("CheckInIST").And.Contain(",Yes");
    }
}
