using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Regalis.Application.Administration;
using Regalis.Application.Auth;
using Regalis.Application.Masters;
using Regalis.Application.Reporting;
using Regalis.IntegrationTests.Infrastructure;
using Regalis.Shared.Api;

namespace Regalis.IntegrationTests;

/// <summary>
/// Day 10 — feature complete: reports 9–16 live, dashboard aggregates, audit trail,
/// own-record profile, and the security-review fixes (hidden catalogue items and
/// off-window banners no longer leak to customers).
/// </summary>
[Collection(ApiCollection.Name)]
public class FeatureCompleteTests(RegalisApiFactory factory)
{
    private async Task<HttpClient> AdminClientAsync()
    {
        var admin = factory.CreateClient();
        admin.UseToken((await admin.LoginAsAdminAsync()).AccessToken);
        return admin;
    }

    private async Task<HttpClient> CustomerClientAsync()
    {
        var customer = factory.CreateClient();
        var (_, _, session) = await customer.RegisterCustomerAsync(factory);
        customer.UseToken(session.AccessToken);
        return customer;
    }

    [Fact]
    public async Task Reports_EveryCatalogueKeyRuns()
    {
        var admin = await AdminClientAsync();
        var catalogue = (await admin.GetFromJsonAsync<ApiResponse<List<ReportInfo>>>(
            "/api/reports"))!.Data!;
        // 16 operational reports + the 9 the Billing & Finance module adds.
        catalogue.Should().HaveCount(25);
        catalogue.Should().OnlyContain(r => r.IsAvailable, "Day 10 is the feature-complete target");
        catalogue.Select(r => r.Key).Should().Contain(new[]
        {
            "quotation", "outstanding", "expense", "purchase-order", "inventory",
            "incoming-goods", "outgoing-goods", "bank-statement", "finance-summary"
        }, "the Billing & Finance reports are part of the catalogue");

        // Every key actually runs — not just the catalogue flag.
        foreach (var report in catalogue)
        {
            var response = await admin.GetAsync($"/api/reports/{report.Key}");
            response.StatusCode.Should().Be(HttpStatusCode.OK, $"report {report.Key} must run");
            var result = (await response.Content.ReadFromJsonAsync<ApiResponse<ReportResult>>())!.Data!;
            result.Columns.Should().NotBeEmpty();
            result.Summary.Should().NotBeEmpty();
        }
    }

    [Fact]
    public async Task Dashboards_AdminCardsAndCustomerAggregates()
    {
        var admin = await AdminClientAsync();
        var adminDash = (await admin.GetFromJsonAsync<ApiResponse<AdminDashboardDto>>(
            "/api/dashboard/admin"))!.Data!;
        adminDash.Cards.Should().NotBeEmpty();
        adminDash.Cards.Should().Contain(c => c.Key == "revenue-today");
        adminDash.Cards.Should().Contain(c => c.Key == "occupancy");

        var customer = await CustomerClientAsync();
        var customerDash = (await customer.GetFromJsonAsync<ApiResponse<CustomerDashboardDto>>(
            "/api/dashboard/customer"))!.Data!;
        customerDash.WalletBalance.Should().Be(0m);
        customerDash.TodaysBooking.Should().BeNull();

        // Customers hold DASHBOARD.VIEW for their own home, but the operational
        // aggregates are admin-portal only (Day 11 hardening).
        (await customer.GetAsync("/api/dashboard/admin")).StatusCode
            .Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task AuditTrail_RecordsAdminActions_AndExports()
    {
        var admin = await AdminClientAsync();
        // A workspace create writes audit rows via the interceptor.
        await admin.CreateWorkspaceAsync();

        var logs = (await admin.GetFromJsonAsync<ApiResponse<PagedResult<AuditLogDto>>>(
            "/api/audit?pageSize=50"))!.Data!;
        logs.TotalCount.Should().BeGreaterThan(0);
        logs.Items.Should().OnlyContain(l => l.TimestampUtc != default);

        var modules = (await admin.GetFromJsonAsync<ApiResponse<List<string>>>(
            "/api/audit/modules"))!.Data!;
        modules.Should().NotBeEmpty();

        var export = await admin.GetAsync("/api/audit/export");
        export.EnsureSuccessStatusCode();
        export.Content.Headers.ContentType!.MediaType.Should().Be("text/csv");

        // AUDIT.VIEW is admin/finance only.
        var customer = await CustomerClientAsync();
        (await customer.GetAsync("/api/audit")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Profile_GetUpdatePhoto_OwnRecordOnly()
    {
        var customer = await CustomerClientAsync();
        var profile = (await customer.GetFromJsonAsync<ApiResponse<ProfileDto>>(
            "/api/profile"))!.Data!;
        profile.HasPhoto.Should().BeFalse();

        var newMobile = $"7{Random.Shared.NextInt64(100_000_000, 999_999_999)}";
        var updated = (await (await customer.PutAsJsonAsync("/api/profile",
                new UpdateProfileRequest("Renamed Customer", newMobile, "Acme Pvt Ltd", "12 MG Road")))
            .Content.ReadFromJsonAsync<ApiResponse<ProfileDto>>())!.Data!;
        updated.FullName.Should().Be("Renamed Customer");
        updated.MobileNumber.Should().Be(newMobile);
        updated.Email.Should().Be(profile.Email, "email is the login and is not edited here");

        // A mobile already registered to someone else is refused.
        var other = await CustomerClientAsync();
        (await other.PutAsJsonAsync("/api/profile",
                new UpdateProfileRequest("Other", newMobile, null, null)))
            .StatusCode.Should().Be(HttpStatusCode.Conflict);

        // Photo: a real PNG uploads and streams back; a spoofed one is refused.
        var pngBytes = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }
            .Concat(new byte[32]).ToArray();
        static MultipartFormDataContent Multipart(byte[] bytes, string fileName)
        {
            var content = new MultipartFormDataContent();
            var file = new ByteArrayContent(bytes);
            file.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/png");
            content.Add(file, "file", fileName);
            return content;
        }
        var photo = await customer.PostAsync("/api/profile/photo", Multipart(pngBytes, "me.png"));
        photo.EnsureSuccessStatusCode();
        (await photo.Content.ReadFromJsonAsync<ApiResponse<ProfileDto>>())!.Data!.HasPhoto.Should().BeTrue();
        var download = await customer.GetAsync("/api/profile/photo");
        download.EnsureSuccessStatusCode();
        (await download.Content.ReadAsByteArrayAsync()).Should().BeEquivalentTo(pngBytes);

        (await customer.PostAsync("/api/profile/photo",
                Multipart(System.Text.Encoding.UTF8.GetBytes("not a png at all, promise"), "x.png")))
            .StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task SecurityReview_HiddenCatalogueAndBannersDoNotLeakToCustomers()
    {
        var admin = await AdminClientAsync();
        var suffix = Guid.NewGuid().ToString("N")[..6].ToUpperInvariant();
        // A hidden catalogue item and an off-window banner.
        (await admin.PostAsJsonAsync("/api/services", new SaveServiceItemRequest(
            $"HD-{suffix}", "Secret latte", "Coffee", 999m, null, null, null,
            IsAvailable: false, 1, null))).EnsureSuccessStatusCode();
        (await admin.PostAsJsonAsync("/api/banners", new SaveBannerRequest(
            $"future-{suffix}", null, "/img/x.png", null, null, 1,
            DateTime.UtcNow.AddDays(7), DateTime.UtcNow.AddDays(14), null, PortalType: 1)))
            .EnsureSuccessStatusCode();

        var customer = await CustomerClientAsync();
        // includeUnavailable is honoured only for catalogue managers.
        var services = (await customer.GetFromJsonAsync<ApiResponse<PagedResult<ServiceItemDto>>>(
            "/api/services?pageSize=100&includeUnavailable=true"))!.Data!;
        services.Items.Should().NotContain(s => s.ItemCode == $"HD-{suffix}");
        var adminServices = (await admin.GetFromJsonAsync<ApiResponse<PagedResult<ServiceItemDto>>>(
            "/api/services?pageSize=100&includeUnavailable=true"))!.Data!;
        adminServices.Items.Should().Contain(s => s.ItemCode == $"HD-{suffix}");

        // The full banner grid shows a customer only live customer-portal banners.
        var banners = (await customer.GetFromJsonAsync<ApiResponse<PagedResult<BannerDto>>>(
            "/api/banners?pageSize=100"))!.Data!;
        banners.Items.Should().NotContain(b => b.Title == $"future-{suffix}");
        var adminBanners = (await admin.GetFromJsonAsync<ApiResponse<PagedResult<BannerDto>>>(
            "/api/banners?pageSize=100"))!.Data!;
        adminBanners.Items.Should().Contain(b => b.Title == $"future-{suffix}");
    }
}
