namespace Regalis.Application.Reporting;

/// <summary>One admin dashboard card. Generic on purpose: the card row grows
/// without a contract change.</summary>
public record DashboardCard(string Key, string Label, string Value, string? Hint, string? Route);

public record AdminDashboardDto(List<DashboardCard> Cards, DateTime GeneratedUtc);

public record DashboardBookingDto(
    long BookingId,
    string BookingNumber,
    string WorkspaceName,
    string WorkspaceTypeName,
    DateTime StartUtc,
    DateTime EndUtc,
    // BookingStatus.
    byte Status);

/// <summary>The customer home in one round trip (API_PLAN §12).</summary>
public record CustomerDashboardDto(
    decimal WalletBalance,
    int ActivePasses,
    int UnreadNotifications,
    int OpenTickets,
    DashboardBookingDto? TodaysBooking,
    DashboardBookingDto? NextBooking);

public interface IDashboardService
{
    Task<AdminDashboardDto> GetAdminAsync(CancellationToken ct = default);

    /// <summary>The Billing &amp; Finance summary (document §16).</summary>
    Task<AdminDashboardDto> GetFinanceAsync(CancellationToken ct = default);
    Task<CustomerDashboardDto> GetCustomerAsync(CancellationToken ct = default);
}
