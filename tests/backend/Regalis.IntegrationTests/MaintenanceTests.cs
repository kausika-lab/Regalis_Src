using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Regalis.Application.Administration;
using Regalis.Application.Operations;
using Regalis.Domain.Enums;
using Regalis.Infrastructure.Persistence;
using Regalis.IntegrationTests.Infrastructure;
using Regalis.Shared.Api;
using Regalis.Shared.Constants;

namespace Regalis.IntegrationTests;

/// <summary>Maintenance checklist (Maintenance Feature Specification): housekeepers with
/// a login, the Pending → In progress → Done → Verified / Rework lifecycle, who may do
/// what, auto-Overdue, and the Schedules tab feeding the checklist.</summary>
[Collection(ApiCollection.Name)]
public class MaintenanceTests(RegalisApiFactory factory)
{
    private static readonly byte[] Png =
        Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNkYAAAAAYAAjCB0C8AAAAASUVORK5CYII=");

    private async Task<HttpClient> AdminAsync()
    {
        var admin = factory.CreateClient();
        admin.UseToken((await admin.LoginAsAdminAsync()).AccessToken);
        return admin;
    }

    private static string Mobile() => $"7{Random.Shared.NextInt64(100_000_000, 999_999_999)}";

    private async Task<(HousekeeperCreatedDto Created, HttpClient Client)> AddHousekeeperAsync(HttpClient admin, string? mobile = null)
    {
        var email = $"hk-{Guid.NewGuid():N}@test.regalis.in";
        var response = await admin.PostAsJsonAsync("/api/maintenance/housekeepers", new SaveHousekeeperRequest(
            "Test Housekeeper", mobile ?? Mobile(), email, 1, new DateOnly(2026, 9, 1), ["Pantry", "Lobby"]));
        response.StatusCode.Should().Be(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        var created = (await response.Content.ReadFromJsonAsync<ApiResponse<HousekeeperCreatedDto>>())!.Data!;

        var client = factory.CreateClient();
        client.UseToken((await client.LoginAsync(email, created.TemporaryPassword!, "admin")).AccessToken);
        return (created, client);
    }

    private async Task<HttpClient> ManagerAsync(HttpClient admin)
    {
        var roles = (await admin.GetFromJsonAsync<ApiResponse<List<RoleDto>>>("/api/roles"))!.Data!;
        var email = $"mgr-{Guid.NewGuid():N}@test.regalis.in";
        const string password = "R0le!TestPass1";
        (await admin.PostAsJsonAsync("/api/users", new CreateUserRequest("Ops Manager", email, Mobile(), password, 2,
            [roles.Single(r => r.RoleCode == RoleCodes.Manager).RoleId], null, 1))).EnsureSuccessStatusCode();
        var client = factory.CreateClient();
        client.UseToken((await client.LoginAsync(email, password, "admin")).AccessToken);
        return client;
    }

    private static async Task<MaintenanceWorkDto> AddWorkAsync(HttpClient client, long assignee, string title = "Clean pantry",
        DateTime? due = null)
    {
        var response = await client.PostAsJsonAsync("/api/maintenance/work", new SaveMaintenanceWorkRequest(
            title, "Pantry", null, "Cleaning", 2, assignee, due ?? DateTime.UtcNow.AddHours(3),
            "Wipe counters, restock cups.", BlockWorkspace: false, BlockFromUtc: null, NotifyHousekeeper: true));
        response.StatusCode.Should().Be(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<ApiResponse<MaintenanceWorkDto>>())!.Data!;
    }

    private static async Task<(HttpStatusCode Status, ApiResponse<MaintenanceWorkDto> Body)> PostAsync(HttpClient client,
        string url, object? body = null)
    {
        var response = body is HttpContent content ? await client.PostAsync(url, content) : await client.PostAsJsonAsync(url, body ?? new { });
        return (response.StatusCode, (await response.Content.ReadFromJsonAsync<ApiResponse<MaintenanceWorkDto>>())!);
    }

    private static MultipartFormDataContent DoneForm(string? note, bool withPhoto)
    {
        var form = new MultipartFormDataContent();
        if (note is not null) form.Add(new StringContent(note), "note");
        if (withPhoto)
        {
            var photo = new ByteArrayContent(Png);
            photo.Headers.ContentType = new MediaTypeHeaderValue("image/png");
            form.Add(photo, "photo", "pantry.png");
        }
        return form;
    }

    [Fact]
    public async Task AddingAHousekeeper_CreatesALoginThatSeesOnlyMyTasks()
    {
        var admin = await AdminAsync();
        var (created, housekeeper) = await AddHousekeeperAsync(admin);

        created.Housekeeper.HousekeeperCode.Should().StartWith("HK-");
        created.TemporaryPassword.Should().NotBeNullOrWhiteSpace();

        var me = (await housekeeper.GetFromJsonAsync<ApiResponse<MaintenanceMeDto>>("/api/maintenance/me"))!.Data!;
        me.IsHousekeeper.Should().BeTrue();
        me.CanManageWork.Should().BeFalse();
        me.CanVerify.Should().BeFalse();

        // The HOUSEKEEPING role carries HOUSEKEEPING.CREATE, but a housekeeper still cannot add work.
        var add = await housekeeper.PostAsJsonAsync("/api/maintenance/work", new SaveMaintenanceWorkRequest(
            "Self task", "Lobby", null, "Cleaning", 1, created.Housekeeper.UserId, DateTime.UtcNow.AddHours(2),
            null, false, null, false));
        add.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await housekeeper.GetAsync("/api/maintenance/housekeepers")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task DuplicatePhone_IsRefusedWithTheSpecMessage()
    {
        var admin = await AdminAsync();
        var mobile = Mobile();
        await AddHousekeeperAsync(admin, mobile);

        var again = await admin.PostAsJsonAsync("/api/maintenance/housekeepers", new SaveHousekeeperRequest(
            "Second", mobile, $"hk-{Guid.NewGuid():N}@test.regalis.in", 2, new DateOnly(2026, 9, 1), null));
        again.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await again.Content.ReadAsStringAsync()).Should().Contain("This phone number is already registered.");
    }

    [Fact]
    public async Task Lifecycle_StartDoneVerify_AndHousekeepersSeeOnlyTheirOwnWork()
    {
        var admin = await AdminAsync();
        var (hk, housekeeper) = await AddHousekeeperAsync(admin);
        var (other, otherClient) = await AddHousekeeperAsync(admin);
        var work = await AddWorkAsync(admin, hk.Housekeeper.UserId);
        var othersWork = await AddWorkAsync(admin, other.Housekeeper.UserId, "Mop lobby");

        work.WorkNumber.Should().MatchRegex("^MW-\\d{6}$");
        work.Status.Should().Be((byte)MaintenanceWorkStatus.Pending);

        // Own tasks only.
        var mine = (await housekeeper.GetFromJsonAsync<ApiResponse<MaintenanceMyTasksDto>>("/api/maintenance/my-tasks"))!.Data!;
        mine.Tasks.Select(t => t.WorkItemId).Should().Contain(work.WorkItemId).And.NotContain(othersWork.WorkItemId);
        (await housekeeper.GetAsync($"/api/maintenance/work/{othersWork.WorkItemId}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await PostAsync(otherClient, $"/api/maintenance/work/{work.WorkItemId}/start")).Status.Should().Be(HttpStatusCode.NotFound);

        // Done before start is refused; done without a note is refused.
        (await PostAsync(housekeeper, $"/api/maintenance/work/{work.WorkItemId}/done", DoneForm("x", false))).Status
            .Should().Be(HttpStatusCode.UnprocessableEntity);
        var started = await PostAsync(housekeeper, $"/api/maintenance/work/{work.WorkItemId}/start");
        started.Status.Should().Be(HttpStatusCode.OK);
        started.Body.Data!.Status.Should().Be((byte)MaintenanceWorkStatus.InProgress);
        (await PostAsync(housekeeper, $"/api/maintenance/work/{work.WorkItemId}/done", DoneForm(null, false))).Status
            .Should().Be(HttpStatusCode.BadRequest);

        var done = await PostAsync(housekeeper, $"/api/maintenance/work/{work.WorkItemId}/done", DoneForm("Counters wiped, cups restocked.", true));
        done.Status.Should().Be(HttpStatusCode.OK);
        done.Body.Data!.Status.Should().Be((byte)MaintenanceWorkStatus.Done);
        done.Body.Data.HasPhoto.Should().BeTrue();

        // A housekeeper cannot verify; the admin can.
        (await PostAsync(housekeeper, $"/api/maintenance/work/{work.WorkItemId}/verify")).Status.Should().Be(HttpStatusCode.Forbidden);
        var photo = await admin.GetAsync($"/api/maintenance/work/{work.WorkItemId}/photo");
        photo.StatusCode.Should().Be(HttpStatusCode.OK);
        (await photo.Content.ReadAsByteArrayAsync()).Should().Equal(Png);

        var verified = await PostAsync(admin, $"/api/maintenance/work/{work.WorkItemId}/verify");
        verified.Status.Should().Be(HttpStatusCode.OK);
        verified.Body.Data!.Status.Should().Be((byte)MaintenanceWorkStatus.Verified);
        verified.Body.Data.VerifiedByName.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Reject_SendsBackForRework_WithAReason_AndCountsIt()
    {
        var admin = await AdminAsync();
        var (hk, housekeeper) = await AddHousekeeperAsync(admin);
        var work = await AddWorkAsync(admin, hk.Housekeeper.UserId);
        await PostAsync(housekeeper, $"/api/maintenance/work/{work.WorkItemId}/start");
        await PostAsync(housekeeper, $"/api/maintenance/work/{work.WorkItemId}/done", DoneForm("Done.", false));

        (await PostAsync(admin, $"/api/maintenance/work/{work.WorkItemId}/reject", new MaintenanceReasonRequest(" "))).Status
            .Should().Be(HttpStatusCode.BadRequest);
        var rejected = await PostAsync(admin, $"/api/maintenance/work/{work.WorkItemId}/reject",
            new MaintenanceReasonRequest("Sink still has stains."));
        rejected.Status.Should().Be(HttpStatusCode.OK);
        rejected.Body.Data!.Status.Should().Be((byte)MaintenanceWorkStatus.Rework);
        rejected.Body.Data.ReworkCount.Should().Be(1);
        rejected.Body.Data.ReworkReason.Should().Be("Sink still has stains.");

        var rework = (await housekeeper.GetFromJsonAsync<ApiResponse<MaintenanceMyTasksDto>>("/api/maintenance/my-tasks?tab=rework"))!.Data!;
        rework.Tasks.Should().ContainSingle(t => t.WorkItemId == work.WorkItemId);

        (await PostAsync(housekeeper, $"/api/maintenance/work/{work.WorkItemId}/start")).Body.Data!.Status
            .Should().Be((byte)MaintenanceWorkStatus.InProgress);
    }

    [Fact]
    public async Task NobodyVerifiesWorkTheyCompleted()
    {
        var admin = await AdminAsync();
        var manager = await ManagerAsync(admin);
        var (hk, _) = await AddHousekeeperAsync(admin);
        var work = await AddWorkAsync(manager, hk.Housekeeper.UserId);

        // The manager steps in and finishes the job themselves…
        await PostAsync(manager, $"/api/maintenance/work/{work.WorkItemId}/start");
        await PostAsync(manager, $"/api/maintenance/work/{work.WorkItemId}/done", DoneForm("Did it myself.", false));
        var self = await PostAsync(manager, $"/api/maintenance/work/{work.WorkItemId}/verify");
        self.Status.Should().Be(HttpStatusCode.UnprocessableEntity);

        // …so someone else must verify it.
        (await PostAsync(admin, $"/api/maintenance/work/{work.WorkItemId}/verify")).Status.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task OnlyTheAdminDeletes_AndOnlyBeforeWorkStarts()
    {
        var admin = await AdminAsync();
        var manager = await ManagerAsync(admin);
        var (hk, housekeeper) = await AddHousekeeperAsync(admin);
        var first = await AddWorkAsync(admin, hk.Housekeeper.UserId);
        var second = await AddWorkAsync(admin, hk.Housekeeper.UserId, "Second job");

        (await manager.DeleteAsync($"/api/maintenance/work/{first.WorkItemId}")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await admin.DeleteAsync($"/api/maintenance/work/{first.WorkItemId}")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await admin.GetAsync($"/api/maintenance/work/{first.WorkItemId}")).StatusCode.Should().Be(HttpStatusCode.NotFound);

        await PostAsync(housekeeper, $"/api/maintenance/work/{second.WorkItemId}/start");
        (await admin.DeleteAsync($"/api/maintenance/work/{second.WorkItemId}")).StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        var cancelled = await PostAsync(admin, $"/api/maintenance/work/{second.WorkItemId}/cancel", new MaintenanceReasonRequest("Not needed."));
        cancelled.Body.Data!.Status.Should().Be((byte)MaintenanceWorkStatus.Cancelled);
    }

    [Fact]
    public async Task PastDueWork_ReadsOverdue_AndTheSweepMarksItAndAlerts()
    {
        var admin = await AdminAsync();
        var (hk, _) = await AddHousekeeperAsync(admin);
        var work = await AddWorkAsync(admin, hk.Housekeeper.UserId, "Overdue probe");

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<RegalisDbContext>();
            await db.MaintenanceWorkItems.Where(w => w.MaintenanceWorkItemId == work.WorkItemId)
                .ExecuteUpdateAsync(s => s.SetProperty(w => w.DueUtc, DateTime.UtcNow.AddMinutes(-30)));
        }

        // Effective status before the sweep reaches it.
        (await admin.GetFromJsonAsync<ApiResponse<MaintenanceWorkDto>>($"/api/maintenance/work/{work.WorkItemId}"))!
            .Data!.Status.Should().Be((byte)MaintenanceWorkStatus.Overdue);

        using (var scope = factory.Services.CreateScope())
        {
            (await scope.ServiceProvider.GetRequiredService<IMaintenanceService>().SweepOverdueAsync()).Should().BeGreaterThan(0);
            var db = scope.ServiceProvider.GetRequiredService<RegalisDbContext>();
            var row = await db.MaintenanceWorkItems.AsNoTracking().SingleAsync(w => w.MaintenanceWorkItemId == work.WorkItemId);
            row.Status.Should().Be(MaintenanceWorkStatus.Overdue);
            row.OverdueSinceUtc.Should().NotBeNull();
            (await db.Notifications.AnyAsync(n => n.ReferenceType == "MaintenanceWork" && n.Message.Contains(work.WorkNumber)))
                .Should().BeTrue();
        }

        var overdueList = (await admin.GetFromJsonAsync<ApiResponse<MaintenanceWorkPageDto>>(
            "/api/maintenance/work?all=true&status=6&pageSize=100"))!.Data!;
        overdueList.Work.Items.Should().Contain(w => w.WorkItemId == work.WorkItemId);
    }

    [Fact]
    public async Task Deactivating_RequiresReassigningOpenTasks()
    {
        var admin = await AdminAsync();
        var (leaving, _) = await AddHousekeeperAsync(admin);
        var (staying, _) = await AddHousekeeperAsync(admin);
        var work = await AddWorkAsync(admin, leaving.Housekeeper.UserId);

        var refused = await admin.PostAsJsonAsync($"/api/maintenance/housekeepers/{leaving.Housekeeper.UserId}/deactivate",
            new DeactivateHousekeeperRequest(null));
        refused.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);

        (await admin.PostAsJsonAsync($"/api/maintenance/housekeepers/{leaving.Housekeeper.UserId}/deactivate",
            new DeactivateHousekeeperRequest(staying.Housekeeper.UserId))).EnsureSuccessStatusCode();
        (await admin.GetFromJsonAsync<ApiResponse<MaintenanceWorkDto>>($"/api/maintenance/work/{work.WorkItemId}"))!
            .Data!.AssignedToUserId.Should().Be(staying.Housekeeper.UserId);

        // An inactive housekeeper can no longer be assigned.
        var assign = await admin.PostAsJsonAsync("/api/maintenance/work", new SaveMaintenanceWorkRequest(
            "Nope", "Lobby", null, "Cleaning", 1, leaving.Housekeeper.UserId, DateTime.UtcNow.AddHours(2), null, false, null, false));
        assign.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CleaningSchedule_AppearsOnTheChecklist_AndCancellingItCancelsTheTask()
    {
        var admin = await AdminAsync();
        var (hk, _) = await AddHousekeeperAsync(admin);
        var start = BookingTestHelpers.NextMondayUtc(10);
        var created = await admin.PostAsJsonAsync("/api/housekeeping/schedules", new SaveHousekeepingScheduleRequest(
            hk.Housekeeper.UserId, 1, null, "Terrace", start, start.AddHours(1), (byte)HousekeepingTaskType.Cleaning,
            "Sweep terrace", null, false));
        created.StatusCode.Should().Be(HttpStatusCode.Created, await created.Content.ReadAsStringAsync());
        var scheduleId = (await created.Content.ReadFromJsonAsync<ApiResponse<HousekeepingScheduleDto>>())!.Data!.HousekeepingScheduleId;

        var list = (await admin.GetFromJsonAsync<ApiResponse<MaintenanceWorkPageDto>>(
            $"/api/maintenance/work?all=true&assigneeId={hk.Housekeeper.UserId}"))!.Data!;
        var linked = list.Work.Items.Should().ContainSingle(w => w.LinkedScheduleId == scheduleId).Subject;
        linked.Title.Should().Be("Sweep terrace");
        linked.DueUtc.Should().BeCloseTo(start.AddHours(1), TimeSpan.FromSeconds(1));

        (await admin.PostAsync($"/api/housekeeping/schedules/{scheduleId}/cancel", null)).EnsureSuccessStatusCode();
        (await admin.GetFromJsonAsync<ApiResponse<MaintenanceWorkDto>>($"/api/maintenance/work/{linked.WorkItemId}"))!
            .Data!.Status.Should().Be((byte)MaintenanceWorkStatus.Cancelled);
    }

    [Fact]
    public async Task Summary_CountsDoneAndVerifiedAsComplete()
    {
        var admin = await AdminAsync();
        var (hk, housekeeper) = await AddHousekeeperAsync(admin);
        var due = BookingTestHelpers.NextMondayUtc(12);
        var a = await AddWorkAsync(admin, hk.Housekeeper.UserId, "A", due);
        var b = await AddWorkAsync(admin, hk.Housekeeper.UserId, "B", due);
        await AddWorkAsync(admin, hk.Housekeeper.UserId, "C", due);
        foreach (var w in new[] { a, b })
        {
            await PostAsync(housekeeper, $"/api/maintenance/work/{w.WorkItemId}/start");
            await PostAsync(housekeeper, $"/api/maintenance/work/{w.WorkItemId}/done", DoneForm("ok", false));
        }
        await PostAsync(admin, $"/api/maintenance/work/{a.WorkItemId}/verify");

        var date = Shared.IstClock.IstDateOf(due).ToString("yyyy-MM-dd");
        var page = (await housekeeper.GetFromJsonAsync<ApiResponse<MaintenanceWorkPageDto>>($"/api/maintenance/work?date={date}"))!.Data!;
        page.Summary.Total.Should().Be(3);
        page.Summary.Completed.Should().Be(2);
        page.Summary.Verified.Should().Be(1);
        page.Summary.WaitingForVerification.Should().Be(1);
        page.Summary.Percent.Should().Be(67);
    }

    [Fact]
    public async Task CustomerToken_IsRefused()
    {
        var customer = factory.CreateClient();
        var (_, _, session) = await customer.RegisterCustomerAsync(factory);
        customer.UseToken(session.AccessToken);
        (await customer.GetAsync("/api/maintenance/work")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
