using System.Net.Http.Json;
using Regalis.Application.Masters;
using Regalis.Shared;
using Regalis.Shared.Api;

namespace Regalis.IntegrationTests.Infrastructure;

public static class BookingTestHelpers
{
    /// <summary>Creates a bookable desk at the seeded location via the API and
    /// returns its id. Unique code per call.</summary>
    public static async Task<int> CreateWorkspaceAsync(this HttpClient adminClient,
        decimal pricePerHour = 500m)
    {
        var types = (await adminClient.GetFromJsonAsync<ApiResponse<List<WorkspaceTypeDto>>>(
            "/api/workspace-types"))!.Data!;
        var deskTypeId = types.Single(t => t.TypeCode == "DESK").WorkspaceTypeId;

        var code = $"T-{Guid.NewGuid().ToString("N")[..8].ToUpperInvariant()}";
        var response = await adminClient.PostAsJsonAsync("/api/workspaces", new SaveWorkspaceRequest(
            LocationId: 1, WorkspaceTypeId: deskTypeId, WorkspaceCode: code,
            WorkspaceName: $"Test Desk {code}", FloorNumber: "1", Capacity: 1,
            PricePerHour: pricePerHour, PricePerDay: null, PricePerMonth: null, Equipment: null,
            Description: null, IsBookable: true, AmenityIds: []));
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<ApiResponse<WorkspaceDto>>())!.Data!.WorkspaceId;
    }

    /// <summary>A UTC instant at the given IST wall-clock time on the next Monday at
    /// least a week out — always a seeded working day (MON–SAT, 08:00–21:00 IST),
    /// always in the future, within the 60-day advance window.</summary>
    public static DateTime NextMondayUtc(int hourIst, int minuteIst = 0)
    {
        var todayIst = DateOnly.FromDateTime(IstClock.ToIst(DateTime.UtcNow));
        var daysUntilMonday = ((int)DayOfWeek.Monday - (int)todayIst.DayOfWeek + 7) % 7;
        var monday = todayIst.AddDays(daysUntilMonday == 0 ? 7 : daysUntilMonday + 7);
        return IstClock.ToUtc(monday.ToDateTime(new TimeOnly(hourIst, minuteIst)));
    }
}
