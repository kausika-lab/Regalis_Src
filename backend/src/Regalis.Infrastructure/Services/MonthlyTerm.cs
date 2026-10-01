using Regalis.Application.Common.Exceptions;
using Regalis.Domain.Entities.Masters;
using Regalis.Shared;

namespace Regalis.Infrastructure.Services;

/// <summary>
/// The window a monthly desk or cabin booking occupies: from the location's opening time
/// on the start date to its closing time on the last day of the term, both IST. A term of
/// N months ends the day before the same date N months later (1 Oct + 1 month = 1–31 Oct).
/// Nights in between are inside the window, so nobody else can book the workspace for any
/// hour of the term. A term starting today begins now (rounded up to the minute) if the
/// location has already opened.
/// </summary>
public static class MonthlyTerm
{
    public static readonly int[] AllowedMonths = [1, 3, 6, 12];

    public static readonly string[] MonthlyTypeCodes = ["DESK", "CABIN"];

    /// <summary>Hot desks are never booked by the month: the pick list always shows them
    /// as booked. A hot desk is a DESK whose name starts "Hot Desk" (there is no type of
    /// its own).</summary>
    public static bool IsHotDesk(string? workspaceName) =>
        workspaceName is not null &&
        workspaceName.TrimStart().StartsWith("Hot Desk", StringComparison.OrdinalIgnoreCase);

    public sealed record Window(DateOnly StartDate, DateOnly EndDate, DateTime StartUtc, DateTime EndUtc)
    {
        public int DurationMinutes => (int)Math.Round((EndUtc - StartUtc).TotalMinutes);
    }

    public static Window For(Location location, DateOnly startDate, int months, DateTime nowUtc,
        int advanceDays)
    {
        if (!AllowedMonths.Contains(months))
        {
            throw new BusinessRuleException("Choose a term of 1, 3, 6 or 12 months.");
        }
        var todayIst = IstClock.IstDateOf(nowUtc);
        if (startDate < todayIst)
        {
            throw new BusinessRuleException("The start date cannot be in the past.");
        }
        if (startDate > todayIst.AddDays(advanceDays))
        {
            throw new BusinessRuleException($"Bookings open up to {advanceDays} days in advance.");
        }

        var endDate = startDate.AddMonths(months).AddDays(-1);
        var startUtc = IstClock.ToUtc(startDate.ToDateTime(location.WorkingHoursStart));
        var endUtc = IstClock.ToUtc(endDate.ToDateTime(location.WorkingHoursEnd));
        if (startUtc <= nowUtc)
        {
            // Today, after opening: the term starts now.
            startUtc = new DateTime(nowUtc.Ticks - nowUtc.Ticks % TimeSpan.TicksPerMinute, DateTimeKind.Utc)
                .AddMinutes(1);
        }
        if (endUtc <= startUtc)
        {
            throw new BusinessRuleException("The location has closed for today. Start the term tomorrow.");
        }
        return new Window(startDate, endDate, startUtc, endUtc);
    }

    public static string Term(int months) => months == 1 ? "1 month" : $"{months} months";
}
