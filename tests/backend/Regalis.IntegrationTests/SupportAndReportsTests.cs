using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Regalis.Application.Access;
using Regalis.Application.Bookings;
using Regalis.Application.Finance;
using Regalis.Application.Operations;
using Regalis.Application.Reporting;
using Regalis.Domain.Enums;
using Regalis.Infrastructure.Persistence;
using Regalis.IntegrationTests.Infrastructure;
using Regalis.Shared.Api;
using Regalis.Shared.Constants;

namespace Regalis.IntegrationTests;

/// <summary>
/// Day 9: support tickets (scoping, internal notes, attachments with content
/// sniffing), reports 1–8 with Excel/PDF export, and the full end-to-end journey —
/// register → book → pay → invoice → QR → check-in → check-out → report.
/// </summary>
[Collection(ApiCollection.Name)]
public class SupportAndReportsTests(RegalisApiFactory factory)
{
    private async Task<HttpClient> AdminClientAsync()
    {
        var admin = factory.CreateClient();
        admin.UseToken((await admin.LoginAsAdminAsync()).AccessToken);
        return admin;
    }

    private async Task<(HttpClient Client, long UserId)> CustomerClientAsync()
    {
        var customer = factory.CreateClient();
        var (_, _, session) = await customer.RegisterCustomerAsync(factory);
        customer.UseToken(session.AccessToken);
        return (customer, session.User.UserId);
    }

    // ── The Day 9 QA gate: the whole customer journey in one test ───────────

    [Fact]
    public async Task EndToEnd_RegisterBookPayQrCheckInOutReport()
    {
        var admin = await AdminClientAsync();
        var workspaceId = await admin.CreateWorkspaceAsync(pricePerHour: 500m);
        var (customer, customerId) = await CustomerClientAsync();

        // Fund the wallet, hold, confirm — the paid booking quintet.
        using (var scope = factory.Services.CreateScope())
        {
            var wallets = scope.ServiceProvider.GetRequiredService<IWalletService>();
            var db = scope.ServiceProvider.GetRequiredService<RegalisDbContext>();
            await wallets.CreditAsync(customerId, 5000m, WalletCategory.AdminAdjustment, "Test credit");
            await db.SaveChangesAsync();
        }
        var start = BookingTestHelpers.NextMondayUtc(9);
        var hold = await customer.PostAsJsonAsync("/api/bookings",
            new CreateBookingRequest(workspaceId, start, 60, null));
        var booking = (await hold.Content.ReadFromJsonAsync<ApiResponse<BookingDto>>())!.Data!;
        var confirm = await customer.PostAsJsonAsync($"/api/bookings/{booking.BookingId}/confirm",
            new ConfirmBookingRequest(PaymentMethods.Wallet, null, null, null));
        confirm.EnsureSuccessStatusCode();
        var confirmed = (await confirm.Content
            .ReadFromJsonAsync<ApiResponse<ConfirmBookingResponse>>())!.Data!;
        confirmed.InvoiceNumber.Should().NotBeNullOrEmpty();
        confirmed.QrPayload.Should().StartWith("RGL1.");

        // The invoice PDF downloads.
        var invoiceRows = (await customer.GetFromJsonAsync<ApiResponse<PagedResult<InvoiceDto>>>(
            "/api/invoices/my"))!.Data!;
        var invoiceId = invoiceRows.Items.First(i => i.InvoiceNumber == confirmed.InvoiceNumber).InvoiceId;
        var pdf = await customer.GetAsync($"/api/invoices/{invoiceId}/pdf");
        pdf.EnsureSuccessStatusCode();
        pdf.Content.Headers.ContentType!.MediaType.Should().Be("application/pdf");

        // Move the window over "now" so the QR is live, then gate in and out.
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<RegalisDbContext>();
            var nowUtc = DateTime.UtcNow;
            await db.Bookings.Where(b => b.BookingId == booking.BookingId)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(b => b.StartUtc, nowUtc.AddMinutes(-10))
                    .SetProperty(b => b.EndUtc, nowUtc.AddHours(2)));
            await db.AccessTokens.Where(t => t.BookingId == booking.BookingId)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(t => t.ValidFromUtc, nowUtc.AddMinutes(-10))
                    .SetProperty(t => t.ValidToUtc, nowUtc.AddHours(2)));
        }
        var gateIn = (await (await admin.PostAsJsonAsync("/api/access/validate",
                new ValidateAccessRequest(confirmed.QrPayload, null, 1, "GATE-1", 1)))
            .Content.ReadFromJsonAsync<ApiResponse<AccessDecisionDto>>())!.Data!;
        gateIn.IsGranted.Should().BeTrue();
        var gateOut = (await (await admin.PostAsJsonAsync("/api/access/validate",
                new ValidateAccessRequest(confirmed.QrPayload, null, 2, "GATE-1", 1)))
            .Content.ReadFromJsonAsync<ApiResponse<AccessDecisionDto>>())!.Data!;
        gateOut.IsGranted.Should().BeTrue();

        // Attendance closed, booking completed.
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<RegalisDbContext>();
            var attendance = db.Attendances.Single(a =>
                a.UserId == customerId && a.BookingId == booking.BookingId);
            attendance.Status.Should().Be(AttendanceStatus.CheckedOut);
            db.Bookings.Single(b => b.BookingId == booking.BookingId)
                .Status.Should().Be(BookingStatus.Completed);
        }

        // The booking report carries the row; the revenue report carries the payment.
        var today = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1));
        var horizon = today.AddDays(20);
        var bookingReport = (await admin.GetFromJsonAsync<ApiResponse<ReportResult>>(
            $"/api/reports/booking?from={today:yyyy-MM-dd}&to={horizon:yyyy-MM-dd}&customerId={customerId}"))!
            .Data!;
        bookingReport.Rows.Should().Contain(r =>
            r["number"]!.ToString() == confirmed.Booking.BookingNumber);
        var revenueReport = (await admin.GetFromJsonAsync<ApiResponse<ReportResult>>(
            $"/api/reports/revenue?from={today:yyyy-MM-dd}&to={horizon:yyyy-MM-dd}&customerId={customerId}"))!
            .Data!;
        revenueReport.Summary.Should().Contain(s => s.Label == "Captured payments" && s.Value != "0");
    }

    // ── Tickets ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task Tickets_RaiseTriageReplyScopeAndInternalNotes()
    {
        var admin = await AdminClientAsync();
        var (customer, _) = await CustomerClientAsync();
        var (otherCustomer, _) = await CustomerClientAsync();

        // The guided flow starts from the seeded categories.
        var categories = (await customer.GetFromJsonAsync<ApiResponse<List<TicketCategoryDto>>>(
            "/api/ticket-categories"))!.Data!;
        var category = categories.First(c => c.CategoryName == "Booking Related");
        category.IssueTypes.Should().NotBeEmpty();

        var created = await customer.PostAsJsonAsync("/api/tickets", new CreateTicketRequest(
            category.TicketCategoryId, category.IssueTypes[0].TicketIssueTypeId,
            "Invoice missing", "I cannot find my invoice for yesterday.", Priority: 3));
        created.StatusCode.Should().Be(HttpStatusCode.Created);
        var ticket = (await created.Content.ReadFromJsonAsync<ApiResponse<TicketDetailDto>>())!.Data!;
        ticket.TicketNumber.Should().StartWith("TKT-");
        ticket.Status.Should().Be((byte)TicketStatus.Open);

        // Row scoping: another customer gets 404, not 403 — no existence leak.
        (await otherCustomer.GetAsync($"/api/tickets/{ticket.TicketId}"))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);

        // The ticket reaches the admin triage grid.
        var triage = (await admin.GetFromJsonAsync<ApiResponse<PagedResult<TicketListItemDto>>>(
            $"/api/tickets?search={ticket.TicketNumber}"))!.Data!;
        triage.Items.Should().ContainSingle(t => t.TicketId == ticket.TicketId);

        // Admin assigns to themselves and answers, plus an internal note.
        var me = await admin.GetFromJsonAsync<ApiResponse<Application.Auth.MeResponse>>("/api/auth/me");
        var assign = await admin.PatchAsJsonAsync($"/api/tickets/{ticket.TicketId}/assign",
            new AssignTicketRequest(me!.Data!.User.UserId));
        assign.EnsureSuccessStatusCode();
        await admin.PostAsJsonAsync($"/api/tickets/{ticket.TicketId}/messages",
            new AddTicketMessageRequest("Checking with finance.", IsInternalNote: true));
        await admin.PostAsJsonAsync($"/api/tickets/{ticket.TicketId}/messages",
            new AddTicketMessageRequest("Your invoice is attached under My Invoices.", IsInternalNote: false));

        // The customer sees the public reply, never the internal note.
        var customerView = (await customer.GetFromJsonAsync<ApiResponse<TicketDetailDto>>(
            $"/api/tickets/{ticket.TicketId}"))!.Data!;
        customerView.Status.Should().Be((byte)TicketStatus.InProgress);
        customerView.Messages.Should().ContainSingle(m => !m.IsInternalNote);
        customerView.Messages.Should().NotContain(m => m.IsInternalNote);
        var adminView = (await admin.GetFromJsonAsync<ApiResponse<TicketDetailDto>>(
            $"/api/tickets/{ticket.TicketId}"))!.Data!;
        adminView.Messages.Should().HaveCount(2);

        // Resolve → the customer's reply reopens; close ends the conversation.
        await admin.PatchAsJsonAsync($"/api/tickets/{ticket.TicketId}/status",
            new ChangeTicketStatusRequest((byte)TicketStatus.Resolved, "Invoice re-sent."));
        var reopened = (await (await customer.PostAsJsonAsync($"/api/tickets/{ticket.TicketId}/messages",
                new AddTicketMessageRequest("Still not visible for me.", false)))
            .Content.ReadFromJsonAsync<ApiResponse<TicketDetailDto>>())!.Data!;
        reopened.Status.Should().Be((byte)TicketStatus.Reopened);
        reopened.ReopenCount.Should().Be(1);
        await admin.PostAsJsonAsync($"/api/tickets/{ticket.TicketId}/close", new { });
        (await customer.PostAsJsonAsync($"/api/tickets/{ticket.TicketId}/messages",
                new AddTicketMessageRequest("One more thing…", false)))
            .StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task TicketAttachments_AcceptRealPng_RejectSpoofedExtension()
    {
        var (customer, _) = await CustomerClientAsync();
        var categories = (await customer.GetFromJsonAsync<ApiResponse<List<TicketCategoryDto>>>(
            "/api/ticket-categories"))!.Data!;
        var category = categories[0];
        var ticket = (await (await customer.PostAsJsonAsync("/api/tickets", new CreateTicketRequest(
                category.TicketCategoryId, category.IssueTypes[0].TicketIssueTypeId,
                "Broken chair", "Photo attached.", 2)))
            .Content.ReadFromJsonAsync<ApiResponse<TicketDetailDto>>())!.Data!;

        static MultipartFormDataContent Multipart(byte[] bytes, string fileName)
        {
            var content = new MultipartFormDataContent();
            var file = new ByteArrayContent(bytes);
            file.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/png");
            content.Add(file, "file", fileName);
            return content;
        }

        // A real PNG header passes the sniff…
        var pngBytes = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }
            .Concat(new byte[64]).ToArray();
        var ok = await customer.PostAsync($"/api/tickets/{ticket.TicketId}/attachments",
            Multipart(pngBytes, "chair.png"));
        ok.EnsureSuccessStatusCode();
        var withAttachment = (await ok.Content.ReadFromJsonAsync<ApiResponse<TicketDetailDto>>())!.Data!;
        var attachment = withAttachment.Attachments.Single();
        attachment.FileName.Should().Be("chair.png");

        // …a text file renamed .png does not.
        var spoofed = System.Text.Encoding.UTF8.GetBytes("#!/bin/sh\necho pwned — definitely not a png");
        (await customer.PostAsync($"/api/tickets/{ticket.TicketId}/attachments",
                Multipart(spoofed, "notes.png")))
            .StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        // …and a disallowed extension is refused outright.
        (await customer.PostAsync($"/api/tickets/{ticket.TicketId}/attachments",
                Multipart(pngBytes, "script.exe")))
            .StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);

        // The stored file streams back through the authorized endpoint.
        var download = await customer.GetAsync(
            $"/api/tickets/{ticket.TicketId}/attachments/{attachment.TicketAttachmentId}");
        download.EnsureSuccessStatusCode();
        (await download.Content.ReadAsByteArrayAsync()).Should().BeEquivalentTo(pngBytes);
    }

    // ── Reports ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task Reports_CatalogueListsSixteen_Day9SetAvailable()
    {
        var admin = await AdminClientAsync();
        var catalogue = (await admin.GetFromJsonAsync<ApiResponse<List<ReportInfo>>>(
            "/api/reports"))!.Data!;
        catalogue.Should().HaveCount(16);
        catalogue.Count(r => r.IsAvailable).Should().Be(8);

        // A Day 10 key is catalogued but politely refused for now.
        (await admin.GetAsync("/api/reports/qr-access"))
            .StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        // An unknown key is 404.
        (await admin.GetAsync("/api/reports/nonsense"))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Reports_ExportsExcelAndPdf()
    {
        var admin = await AdminClientAsync();

        var xlsx = await admin.GetAsync("/api/reports/booking/export?format=xlsx");
        xlsx.EnsureSuccessStatusCode();
        xlsx.Content.Headers.ContentType!.MediaType.Should()
            .Be("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
        (await xlsx.Content.ReadAsByteArrayAsync()).Length.Should().BeGreaterThan(1000);

        var pdf = await admin.GetAsync("/api/reports/occupancy/export?format=pdf");
        pdf.EnsureSuccessStatusCode();
        pdf.Content.Headers.ContentType!.MediaType.Should().Be("application/pdf");
        var bytes = await pdf.Content.ReadAsByteArrayAsync();
        System.Text.Encoding.ASCII.GetString(bytes[..4]).Should().Be("%PDF");

        // Reports are an admin-portal privilege — a customer is refused.
        var (customer, _) = await CustomerClientAsync();
        (await customer.GetAsync("/api/reports/booking"))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
