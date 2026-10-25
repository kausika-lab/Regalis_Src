using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ClosedXML.Excel;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Regalis.Application.Bookings;
using Regalis.Application.Reporting.Analytics;
using Regalis.Domain.Entities.Reporting;
using Regalis.Infrastructure.Persistence;
using Regalis.IntegrationTests.Infrastructure;
using Regalis.Shared;
using Regalis.Shared.Api;

namespace Regalis.IntegrationTests;

/// <summary>
/// Reports &amp; Analysis (spec 2026-10): totals cover every matching row and not the
/// page, the export equals the screen, revenue is money received net of refunds,
/// the rollup is re-runnable, and the routes are staff-only. Each test filters by a
/// freshly registered customer so other tests' rows cannot move its numbers.
/// </summary>
[Collection(ApiCollection.Name)]
public class ReportsAnalysisTests(RegalisApiFactory factory)
{
    private static Dictionary<string, JsonElement> Filters(params (string Key, object Value)[] values) =>
        values.ToDictionary(v => v.Key, v => JsonSerializer.SerializeToElement(v.Value));

    private async Task<(HttpClient Admin, long CustomerId, List<BookingDto> Bookings, DateOnly From, DateOnly To)>
        BookFiftyFiveAsync()
    {
        var admin = factory.CreateClient();
        admin.UseToken((await admin.LoginAsAdminAsync()).AccessToken);
        var workspaceId = await admin.CreateWorkspaceAsync(pricePerHour: 333m);
        var customer = factory.CreateClient();
        var (_, _, session) = await customer.RegisterCustomerAsync(factory);
        var customerId = session.User.UserId;

        // 55 one-hour bookings, Monday–Friday, 09:00–19:00 IST: more than one page.
        var monday = BookingTestHelpers.NextMondayUtc(9);
        for (var day = 0; day < 5; day++)
        {
            for (var hour = 0; hour < 11; hour++)
            {
                (await admin.PostAsJsonAsync("/api/bookings/admin", new AdminCreateBookingRequest(customerId,
                        workspaceId, monday.AddDays(day).AddHours(hour), 60, null, PaymentMethods.Cash)))
                    .EnsureSuccessStatusCode();
            }
        }
        var list = (await admin.GetFromJsonAsync<ApiResponse<PagedResult<BookingDto>>>(
            $"/api/bookings?customerId={customerId}&pageSize=100"))!.Data!;
        var from = IstClock.IstDateOf(monday);
        return (admin, customerId, list.Items.ToList(), from, from.AddDays(4));
    }

    [Fact]
    public async Task BookingReport_TotalsCoverEveryRow_NotThePage()
    {
        var (admin, customerId, bookings, from, to) = await BookFiftyFiveAsync();
        bookings.Should().HaveCount(55);
        var filters = Filters(("from", from.ToString("yyyy-MM-dd")), ("to", to.ToString("yyyy-MM-dd")),
            ("customer", new[] { customerId.ToString() }));

        var response = await admin.PostAsJsonAsync("/api/admin/reports/booking/run",
            new ReportRunRequest { Filters = filters, Page = 1 });
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var page1 = (await response.Content.ReadFromJsonAsync<ApiResponse<ReportRunResult>>())!.Data!;

        page1.TotalRows.Should().Be(55);
        page1.Rows.Should().HaveCount(50);
        var expected = bookings.Sum(b => b.TotalAmount);
        ((JsonElement)page1.Totals["amount"]!).GetDecimal().Should().Be(expected);
        ((JsonElement)page1.Totals["hours"]!).GetDecimal().Should().Be(55m);
        page1.Summary.Single(s => s.Key == "bookings").Value.Should().Be(55);
        page1.Summary.Single(s => s.Key == "value").Value.Should().Be(expected);
        page1.Sentence.Should().Contain("55 bookings");
        page1.Chips.Should().Contain(c => c.Key == "customer");

        var page2 = (await (await admin.PostAsJsonAsync("/api/admin/reports/booking/run",
                new ReportRunRequest { Filters = filters, Page = 2 }))
            .Content.ReadFromJsonAsync<ApiResponse<ReportRunResult>>())!.Data!;
        page2.Rows.Should().HaveCount(5);
        page2.Totals["amount"].ToString().Should().Be(page1.Totals["amount"]!.ToString());

        // Searching narrows the rows, never the summary.
        var number = bookings[0].BookingNumber;
        var searched = (await (await admin.PostAsJsonAsync("/api/admin/reports/booking/run",
                new ReportRunRequest { Filters = filters, Search = number }))
            .Content.ReadFromJsonAsync<ApiResponse<ReportRunResult>>())!.Data!;
        searched.TotalRows.Should().Be(1);
        searched.Summary.Single(s => s.Key == "bookings").Value.Should().Be(55);

        // The Excel file has every row, and its total row equals the screen to the rupee.
        var export = await admin.PostAsJsonAsync("/api/admin/reports/booking/export",
            new ReportExportRequest { Filters = filters, Format = "xlsx" });
        export.StatusCode.Should().Be(HttpStatusCode.OK);
        export.Content.Headers.ContentDisposition!.FileName!.Trim('"')
            .Should().Be($"booking-report_{from:yyyy-MM-dd}_{to:yyyy-MM-dd}.xlsx");
        using var workbook = new XLWorkbook(await export.Content.ReadAsStreamAsync());
        var sheet = workbook.Worksheet(1);
        var last = sheet.LastRowUsed()!.RowNumber();
        sheet.Cell(last, 1).GetString().Should().Be("Total of all 55 bookings");
        last.Should().Be(6 + 55 + 1);
        var amountColumn = sheet.Row(6).CellsUsed().First(c => c.GetString() == "Amount").Address.ColumnNumber;
        sheet.Cell(last, amountColumn).GetValue<decimal>().Should().Be(expected);
        sheet.Cell(7, amountColumn).DataType.Should().Be(XLDataType.Number);

        // Every export is written to the log.
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RegalisDbContext>();
        (await db.ReportExports.CountAsync(e => e.ReportKey == "booking" && e.RowCount == 55)).Should().BeGreaterThan(0);

        var pdf = await admin.PostAsJsonAsync("/api/admin/reports/booking/export",
            new ReportExportRequest { Filters = filters, Format = "pdf" });
        pdf.StatusCode.Should().Be(HttpStatusCode.OK);
        pdf.Content.Headers.ContentType!.MediaType.Should().Be("application/pdf");
    }

    [Fact]
    public async Task Revenue_IsMoneyReceived_NetOfRefunds()
    {
        var admin = factory.CreateClient();
        admin.UseToken((await admin.LoginAsAdminAsync()).AccessToken);
        var workspaceId = await admin.CreateWorkspaceAsync(pricePerHour: 400m);
        var customer = factory.CreateClient();
        var (_, _, session) = await customer.RegisterCustomerAsync(factory);
        var customerId = session.User.UserId;
        var start = BookingTestHelpers.NextMondayUtc(10);
        foreach (var hours in new[] { 0, 2 })
        {
            (await admin.PostAsJsonAsync("/api/bookings/admin", new AdminCreateBookingRequest(customerId,
                workspaceId, start.AddHours(hours), 60, null, PaymentMethods.Cash))).EnsureSuccessStatusCode();
        }
        var list = (await admin.GetFromJsonAsync<ApiResponse<PagedResult<BookingDto>>>(
            $"/api/bookings?customerId={customerId}"))!.Data!.Items;
        // Cancelled a week ahead: refunded in full, to the wallet, today.
        (await admin.PostAsJsonAsync($"/api/bookings/{list[0].BookingId}/cancel", new CancelBookingRequest("Test")))
            .EnsureSuccessStatusCode();

        var today = IstClock.IstDateOf(DateTime.UtcNow).ToString("yyyy-MM-dd");
        var result = (await (await admin.PostAsJsonAsync("/api/admin/reports/top-customers/run",
                new ReportRunRequest { Filters = Filters(("from", today), ("to", today)), Search = "Test Customer" }))
            .Content.ReadFromJsonAsync<ApiResponse<ReportRunResult>>())!.Data!;
        var row = result.Rows.Single(r => r["_id"]!.ToString() == customerId.ToString());
        // Paid for two, refunded one: revenue is the one that was kept.
        ((JsonElement)row["revenue"]!).GetDecimal().Should().Be(list[1].TotalAmount);

        // The refund report carries the refund, on the day it was paid back.
        var refunds = (await (await admin.PostAsJsonAsync("/api/admin/reports/refunds/run",
                new ReportRunRequest
                {
                    Filters = Filters(("from", today), ("to", today), ("customer", new[] { customerId.ToString() }))
                }))
            .Content.ReadFromJsonAsync<ApiResponse<ReportRunResult>>())!.Data!;
        refunds.TotalRows.Should().Be(1);
        ((JsonElement)refunds.Totals["amount"]!).GetDecimal().Should().Be(list[0].TotalAmount);
    }

    [Fact]
    public async Task Overview_OneResponse_AndAgreesWithTheReports()
    {
        var admin = factory.CreateClient();
        admin.UseToken((await admin.LoginAsAdminAsync()).AccessToken);
        var today = IstClock.IstDateOf(DateTime.UtcNow);

        var response = await admin.GetAsync($"/api/admin/reports/overview?preset=custom&from={today:yyyy-MM-dd}&to={today:yyyy-MM-dd}");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var overview = (await response.Content.ReadFromJsonAsync<ApiResponse<OverviewDto>>())!.Data!;
        overview.Cards.Select(c => c.Key).Should().Equal("revenue", "bookings", "customers", "occupancy");
        overview.Cards.All(c => c.Sparkline.Count == 1).Should().BeTrue();
        overview.Revenue.Should().NotBeNull();
        overview.MoneyOwed.Should().NotBeNull();
        overview.MoneyOwed!.Buckets.Should().HaveCount(4);
        overview.Heatmap.Kind.Should().Be("heatmap");

        // The overview's revenue card and the revenue report count the same money.
        var report = (await (await admin.PostAsJsonAsync("/api/admin/reports/revenue-by-month/run",
                new ReportRunRequest { Filters = Filters(("from", today.ToString("yyyy-MM-dd")), ("to", today.ToString("yyyy-MM-dd"))) }))
            .Content.ReadFromJsonAsync<ApiResponse<ReportRunResult>>())!.Data!;
        overview.Cards.Single(c => c.Key == "revenue").Value
            .Should().Be(((JsonElement)report.Totals["revenue"]!).GetDecimal());

        // The admin's choice is remembered on the server.
        var again = (await admin.GetFromJsonAsync<ApiResponse<OverviewDto>>("/api/admin/reports/overview"))!.Data!;
        again.Preset.Should().Be("custom");
        again.From.Should().Be(today);
        (await admin.GetAsync("/api/admin/reports/overview?preset=30d")).EnsureSuccessStatusCode();

        var pdf = await admin.GetAsync("/api/admin/reports/overview/pdf?preset=30d");
        pdf.StatusCode.Should().Be(HttpStatusCode.OK);
        pdf.Content.Headers.ContentType!.MediaType.Should().Be("application/pdf");
    }

    [Fact]
    public async Task DailyMetrics_RebuildIsRepeatable_AndTheOverviewReadsIt()
    {
        var admin = factory.CreateClient();
        admin.UseToken((await admin.LoginAsAdminAsync()).AccessToken);
        var yesterday = IstClock.IstDateOf(DateTime.UtcNow).AddDays(-1);
        var from = yesterday.AddDays(-6);
        var url = $"/api/admin/reports/metrics/rebuild?from={from:yyyy-MM-dd}&to={yesterday:yyyy-MM-dd}";
        (await admin.PostAsync(url, null)).EnsureSuccessStatusCode();
        int Count()
        {
            using var scope = factory.Services.CreateScope();
            return scope.ServiceProvider.GetRequiredService<RegalisDbContext>().DailyMetrics
                .Count(m => m.MetricDate >= from && m.MetricDate <= yesterday);
        }
        var first = Count();
        (await admin.PostAsync(url, null)).EnsureSuccessStatusCode();
        Count().Should().Be(first);
        using (var scope = factory.Services.CreateScope())
        {
            scope.ServiceProvider.GetRequiredService<RegalisDbContext>().DailyMetrics
                .Count(m => m.Category == DailyMetric.DayMarker && m.MetricDate >= from && m.MetricDate <= yesterday)
                .Should().Be(7);
        }

        var overview = (await admin.GetFromJsonAsync<ApiResponse<OverviewDto>>(
            $"/api/admin/reports/overview?preset=custom&from={from:yyyy-MM-dd}&to={yesterday:yyyy-MM-dd}"))!.Data!;
        overview.CalculatedLive.Should().BeFalse();
    }

    [Fact]
    public async Task Library_Favourites_AndSchedules()
    {
        var admin = factory.CreateClient();
        admin.UseToken((await admin.LoginAsAdminAsync()).AccessToken);

        var catalogue = (await admin.GetFromJsonAsync<ApiResponse<ReportCatalogueDto>>(
            "/api/admin/reports/definitions"))!.Data!;
        catalogue.Reports.Should().HaveCount(24);
        catalogue.Groups.Select(g => g.Key).Should().Equal("bookings", "revenue", "customers", "operations");
        catalogue.Reports.Single(r => r.Key == "gst-summary").Keywords.Should().Contain("gst");

        (await admin.PostAsync("/api/admin/reports/favourites/gst-summary", null)).EnsureSuccessStatusCode();
        catalogue = (await admin.GetFromJsonAsync<ApiResponse<ReportCatalogueDto>>("/api/admin/reports/definitions"))!.Data!;
        catalogue.PinnedAreMostUsed.Should().BeFalse();
        catalogue.Pinned.Should().Contain("gst-summary");
        (await admin.DeleteAsync("/api/admin/reports/favourites/gst-summary")).EnsureSuccessStatusCode();

        var bad = await admin.PostAsJsonAsync("/api/admin/reports/schedules", new ReportScheduleRequest
        {
            ReportKey = "money-owed", Frequency = "weekly", DayOfWeek = 1, Time = "09:15",
            Recipients = ["finance@test.regalis.in"]
        });
        bad.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var created = await admin.PostAsJsonAsync("/api/admin/reports/schedules", new ReportScheduleRequest
        {
            ReportKey = "money-owed", Frequency = "monthly", DayOfMonth = 1, Time = "07:00", Format = "pdf",
            Recipients = ["finance@test.regalis.in", "owner@test.regalis.in"], OnlyIfData = true
        });
        created.StatusCode.Should().Be(HttpStatusCode.OK);
        var schedule = (await created.Content.ReadFromJsonAsync<ApiResponse<ReportScheduleDto>>())!.Data!;
        schedule.HowOften.Should().Be("Monthly on the 1st, 07:00");
        schedule.Recipients.Should().HaveCount(2);
        IstClock.ToIst(schedule.NextRunUtc!.Value).Day.Should().Be(1);

        (await admin.DeleteAsync($"/api/admin/reports/schedules/{schedule.ScheduleId}")).EnsureSuccessStatusCode();
        var listed = (await admin.GetFromJsonAsync<ApiResponse<List<ReportScheduleDto>>>("/api/admin/reports/schedules"))!.Data!;
        listed.Single(s => s.ScheduleId == schedule.ScheduleId).IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task Reports_AreStaffOnly_AndCheckTheirFilters()
    {
        var customer = factory.CreateClient();
        var (_, _, session) = await customer.RegisterCustomerAsync(factory);
        customer.UseToken(session.AccessToken);
        (await customer.GetAsync("/api/admin/reports/overview")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await customer.PostAsJsonAsync("/api/admin/reports/booking/run", new ReportRunRequest()))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var admin = factory.CreateClient();
        admin.UseToken((await admin.LoginAsAdminAsync()).AccessToken);
        var invalid = await admin.PostAsJsonAsync("/api/admin/reports/booking/run",
            new ReportRunRequest { Filters = Filters(("from", "2026-10-10"), ("to", "2026-10-01")) });
        invalid.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await admin.PostAsJsonAsync("/api/admin/reports/nonsense/run", new ReportRunRequest()))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task EveryReport_Runs_OnItsDefaults()
    {
        var admin = factory.CreateClient();
        admin.UseToken((await admin.LoginAsAdminAsync()).AccessToken);
        var catalogue = (await admin.GetFromJsonAsync<ApiResponse<ReportCatalogueDto>>(
            "/api/admin/reports/definitions"))!.Data!;
        foreach (var report in catalogue.Reports)
        {
            var response = await admin.PostAsJsonAsync($"/api/admin/reports/{report.Key}/run",
                new ReportRunRequest { Opened = true });
            response.StatusCode.Should().Be(HttpStatusCode.OK, $"{report.Key} should run on its defaults");
            var result = (await response.Content.ReadFromJsonAsync<ApiResponse<ReportRunResult>>())!.Data!;
            result.Sentence.Should().NotBeNullOrWhiteSpace();
            result.Columns.Should().NotBeEmpty();
        }
    }
}
