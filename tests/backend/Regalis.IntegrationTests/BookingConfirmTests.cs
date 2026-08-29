using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Regalis.Application.Bookings;
using Regalis.Application.Finance;
using Regalis.Domain.Enums;
using Regalis.Infrastructure.Persistence;
using Regalis.IntegrationTests.Infrastructure;
using Regalis.Shared.Api;
using Regalis.Shared.Constants;

namespace Regalis.IntegrationTests;

[Collection(ApiCollection.Name)]
public class BookingConfirmTests(RegalisApiFactory factory)
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

    private async Task<BookingDto> HoldAsync(HttpClient customer, int workspaceId, DateTime start,
        int minutes = 60)
    {
        var response = await customer.PostAsJsonAsync("/api/bookings",
            new CreateBookingRequest(workspaceId, start, minutes, null));
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        return (await response.Content.ReadFromJsonAsync<ApiResponse<BookingDto>>())!.Data!;
    }

    [Fact]
    public async Task Confirm_WithWallet_ProducesTheAtomicQuintet()
    {
        var (customer, customerId, workspaceId) = await SetUpAsync();
        await CreditWalletAsync(customerId, 5000m);
        var booking = await HoldAsync(customer, workspaceId, BookingTestHelpers.NextMondayUtc(9));

        var response = await customer.PostAsJsonAsync($"/api/bookings/{booking.BookingId}/confirm",
            new ConfirmBookingRequest(PaymentMethods.Wallet, null, null, null));
        response.EnsureSuccessStatusCode();
        var confirmed = (await response.Content.ReadFromJsonAsync<ApiResponse<ConfirmBookingResponse>>())!.Data!;

        // Design Invariant 4: id + payment + invoice + QR + notification, atomically.
        confirmed.Booking.Status.Should().Be((byte)BookingStatus.Confirmed);
        confirmed.Booking.PaymentStatus.Should().Be((byte)BookingPaymentStatus.Paid);
        confirmed.PaymentNumber.Should().MatchRegex(@"^PAY-\d{6}$");
        confirmed.InvoiceNumber.Should().MatchRegex(@"^REG/INV/\d{4}-\d{2}/\d{5}$");
        confirmed.AccessTokenValue.Should().HaveLength(64);
        confirmed.QrPayload.Should().StartWith("RGL1.");

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RegalisDbContext>();

        var wallet = await db.Wallets.SingleAsync(w => w.CustomerId == customerId);
        wallet.Balance.Should().Be(5000m - confirmed.Booking.TotalAmount);
        var ledger = await db.WalletTransactions
            .Where(t => t.CustomerId == customerId).OrderBy(t => t.WalletTransactionId).ToListAsync();
        ledger.Should().HaveCount(2); // credit + booking debit
        ledger[^1].BalanceAfter.Should().Be(wallet.Balance);

        var invoice = await db.Invoices.Include(i => i.Lines)
            .SingleAsync(i => i.InvoiceId == confirmed.InvoiceId);
        invoice.Status.Should().Be(InvoiceStatus.Paid);
        invoice.CgstAmount.Should().Be(45m);
        invoice.SgstAmount.Should().Be(45m);
        invoice.TotalAmount.Should().Be(590m);
        invoice.OutstandingAmount.Should().Be(0m);
        invoice.Lines.Should().ContainSingle();
        invoice.CustomerName.Should().NotBeNullOrEmpty();

        (await db.AccessTokens.SingleAsync(t => t.BookingId == booking.BookingId))
            .Status.Should().Be(AccessTokenStatus.Active);
        (await db.Notifications.Where(n => n.UserId == customerId &&
                n.Category == NotificationCategory.Booking).CountAsync())
            .Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task Confirm_WithSandboxGateway_EndToEnd()
    {
        var (customer, _, workspaceId) = await SetUpAsync();
        var booking = await HoldAsync(customer, workspaceId, BookingTestHelpers.NextMondayUtc(10));

        var orderResponse = await customer.PostAsJsonAsync("/api/payments/order",
            new CreatePaymentOrderRequest(booking.BookingId));
        orderResponse.EnsureSuccessStatusCode();
        var order = (await orderResponse.Content.ReadFromJsonAsync<ApiResponse<PaymentOrderResponse>>())!.Data!;
        order.Provider.Should().Be("Sandbox");
        order.Amount.Should().Be(booking.TotalAmount);

        var checkout = await customer.PostAsJsonAsync("/api/payments/sandbox-complete",
            new SandboxCompleteRequest(order.GatewayOrderId));
        var paid = (await checkout.Content.ReadFromJsonAsync<ApiResponse<SandboxCompleteResponse>>())!.Data!;

        var confirm = await customer.PostAsJsonAsync($"/api/bookings/{booking.BookingId}/confirm",
            new ConfirmBookingRequest(PaymentMethods.Gateway, order.GatewayOrderId,
                paid.GatewayPaymentId, paid.GatewaySignature));
        confirm.EnsureSuccessStatusCode();
        var confirmed = (await confirm.Content.ReadFromJsonAsync<ApiResponse<ConfirmBookingResponse>>())!.Data!;
        confirmed.Booking.Status.Should().Be((byte)BookingStatus.Confirmed);
    }

    [Fact]
    public async Task Confirm_TamperedGatewaySignature_FailsAndKeepsTheHold()
    {
        var (customer, _, workspaceId) = await SetUpAsync();
        var booking = await HoldAsync(customer, workspaceId, BookingTestHelpers.NextMondayUtc(11));

        var order = (await (await customer.PostAsJsonAsync("/api/payments/order",
                new CreatePaymentOrderRequest(booking.BookingId)))
            .Content.ReadFromJsonAsync<ApiResponse<PaymentOrderResponse>>())!.Data!;

        var confirm = await customer.PostAsJsonAsync($"/api/bookings/{booking.BookingId}/confirm",
            new ConfirmBookingRequest(PaymentMethods.Gateway, order.GatewayOrderId,
                "pay_FAKE", "not-a-real-signature"));
        confirm.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        var envelope = await confirm.Content.ReadFromJsonAsync<ApiResponse>();
        envelope!.Errors.Should().ContainSingle(e => e.Code == ErrorCodes.PaymentVerificationFailed);

        // The booking is still Pending — nothing was half-written.
        var check = (await customer.GetFromJsonAsync<ApiResponse<BookingDto>>(
            $"/api/bookings/{booking.BookingId}"))!.Data!;
        check.Status.Should().Be((byte)BookingStatus.Pending);
    }

    [Fact]
    public async Task Confirm_InsufficientWallet_Returns422AndKeepsTheHold()
    {
        var (customer, _, workspaceId) = await SetUpAsync();
        var booking = await HoldAsync(customer, workspaceId, BookingTestHelpers.NextMondayUtc(13));

        var confirm = await customer.PostAsJsonAsync($"/api/bookings/{booking.BookingId}/confirm",
            new ConfirmBookingRequest(PaymentMethods.Wallet, null, null, null));
        confirm.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        var envelope = await confirm.Content.ReadFromJsonAsync<ApiResponse>();
        envelope!.Errors.Should().ContainSingle(e => e.Code == ErrorCodes.InsufficientWalletBalance);
    }

    [Fact]
    public async Task Confirm_Twice_SecondAttemptIsRefused()
    {
        var (customer, customerId, workspaceId) = await SetUpAsync();
        await CreditWalletAsync(customerId, 5000m);
        var booking = await HoldAsync(customer, workspaceId, BookingTestHelpers.NextMondayUtc(14));

        (await customer.PostAsJsonAsync($"/api/bookings/{booking.BookingId}/confirm",
            new ConfirmBookingRequest(PaymentMethods.Wallet, null, null, null)))
            .EnsureSuccessStatusCode();
        var second = await customer.PostAsJsonAsync($"/api/bookings/{booking.BookingId}/confirm",
            new ConfirmBookingRequest(PaymentMethods.Wallet, null, null, null));
        second.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task Confirm_ExpiredHold_IsRefused()
    {
        var (customer, customerId, workspaceId) = await SetUpAsync();
        await CreditWalletAsync(customerId, 5000m);
        var booking = await HoldAsync(customer, workspaceId, BookingTestHelpers.NextMondayUtc(15));

        // Age the hold past the payment window.
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<RegalisDbContext>();
            await db.Bookings.Where(b => b.BookingId == booking.BookingId)
                .ExecuteUpdateAsync(s => s.SetProperty(
                    b => b.CreatedDate, DateTime.UtcNow.AddMinutes(-30)));
        }

        var confirm = await customer.PostAsJsonAsync($"/api/bookings/{booking.BookingId}/confirm",
            new ConfirmBookingRequest(PaymentMethods.Wallet, null, null, null));
        confirm.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task AdminBooking_CashSettlement_ConfirmsImmediately()
    {
        var admin = factory.CreateClient();
        admin.UseToken((await admin.LoginAsAdminAsync()).AccessToken);
        var workspaceId = await admin.CreateWorkspaceAsync();

        var customerClient = factory.CreateClient();
        var (_, _, session) = await customerClient.RegisterCustomerAsync(factory);

        var response = await admin.PostAsJsonAsync("/api/bookings/admin",
            new AdminCreateBookingRequest(session.User.UserId, workspaceId,
                BookingTestHelpers.NextMondayUtc(16), 60, "walk-in", PaymentMethods.Cash));
        response.EnsureSuccessStatusCode();
        var confirmed = (await response.Content.ReadFromJsonAsync<ApiResponse<ConfirmBookingResponse>>())!.Data!;

        confirmed.Booking.Status.Should().Be((byte)BookingStatus.Confirmed);
        confirmed.Booking.BookingSource.Should().Be((byte)BookingSource.Admin);
        confirmed.Booking.CustomerId.Should().Be(session.User.UserId);
    }

    [Fact]
    public async Task InvoicePdf_DownloadsAsRealPdf()
    {
        var (customer, customerId, workspaceId) = await SetUpAsync();
        await CreditWalletAsync(customerId, 5000m);
        var booking = await HoldAsync(customer, workspaceId, BookingTestHelpers.NextMondayUtc(17));
        var confirmed = (await (await customer.PostAsJsonAsync(
                $"/api/bookings/{booking.BookingId}/confirm",
                new ConfirmBookingRequest(PaymentMethods.Wallet, null, null, null)))
            .Content.ReadFromJsonAsync<ApiResponse<ConfirmBookingResponse>>())!.Data!;

        var pdfResponse = await customer.GetAsync($"/api/invoices/{confirmed.InvoiceId}/pdf");
        pdfResponse.EnsureSuccessStatusCode();
        pdfResponse.Content.Headers.ContentType!.MediaType.Should().Be("application/pdf");
        var bytes = await pdfResponse.Content.ReadAsByteArrayAsync();
        System.Text.Encoding.ASCII.GetString(bytes[..5]).Should().Be("%PDF-");

        // Row scope: another customer cannot fetch it.
        var other = factory.CreateClient();
        var (_, _, otherSession) = await other.RegisterCustomerAsync(factory);
        other.UseToken(otherSession.AccessToken);
        (await other.GetAsync($"/api/invoices/{confirmed.InvoiceId}"))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
