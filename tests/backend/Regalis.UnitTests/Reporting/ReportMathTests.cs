using FluentAssertions;
using Regalis.Domain.Enums;
using Regalis.Infrastructure.Reporting.Analytics;

namespace Regalis.UnitTests.Reporting;

/// <summary>The rules every number on Reports &amp; Analysis follows (spec §8).</summary>
public class ReportMathTests
{
    [Fact]
    public void Compare_GivesPercentToOneDecimal()
    {
        ReportMath.Compare(115m, 100m).Should().Be(("percent", 15.0m));
        ReportMath.Compare(2m, 3m).Should().Be(("percent", -33.3m));
    }

    [Fact]
    public void Compare_PreviousZero_IsNoComparisonNeverInfinity()
    {
        ReportMath.Compare(500m, 0m).Should().Be(("none", (decimal?)null));
    }

    [Fact]
    public void Compare_PreviousNegative_GivesTheAbsoluteChange()
    {
        ReportMath.Compare(200m, -300m).Should().Be(("absolute", 500m));
    }

    [Fact]
    public void Compare_UnknownIsNotZero()
    {
        ReportMath.Compare(null, 100m).Should().Be(((string?)null, (decimal?)null));
    }

    [Fact]
    public void Shares_AddToExactly100_RemainderToTheLargest()
    {
        var shares = ReportMath.SharesTo100([1m, 1m, 1m]);
        shares.Sum().Should().Be(100m);
        shares.Should().Equal(34m, 33m, 33m);

        var uneven = ReportMath.SharesTo100([12.5m, 37.5m, 50m, 0.4m]);
        uneven.Sum().Should().Be(100m);
        ReportMath.SharesTo100([0m, 0m]).Should().Equal(0m, 0m);
    }

    [Fact]
    public void PreviousPeriod_IsTheSameNumberOfDaysImmediatelyBefore()
    {
        // 1–30 Sep is 30 days, inclusive at both ends.
        ReportMath.DaysIn(new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 30)).Should().Be(30);
        ReportMath.PreviousPeriod(new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 30))
            .Should().Be((new DateOnly(2026, 8, 2), new DateOnly(2026, 8, 31)));
    }

    [Fact]
    public void FinancialYear_RunsAprilToMarch()
    {
        ReportMath.FinancialYear(new DateOnly(2026, 10, 24))
            .Should().Be((new DateOnly(2026, 4, 1), new DateOnly(2027, 3, 31)));
        ReportMath.FinancialYear(new DateOnly(2027, 2, 1))
            .Should().Be((new DateOnly(2026, 4, 1), new DateOnly(2027, 3, 31)));
    }

    [Fact]
    public void Money_UsesIndianGrouping()
    {
        ReportMath.Money(270900m).Should().Be("₹2,70,900");
        ReportMath.Money(12345678.5m, paise: true).Should().Be("₹1,23,45,678.50");
        ReportMath.Money(null).Should().Be("—");
    }

    [Fact]
    public void Monthly_ComparesInIst_NotUtc()
    {
        // 03:00 IST on the 1st is 21:30 UTC on the last day of the month before. A job
        // comparing in UTC would send November's report on 31 October.
        var next = ScheduleClock.NextRunUtc(ReportFrequency.Monthly, null, 1, new TimeOnly(3, 0),
            new DateTime(2026, 10, 20, 0, 0, 0, DateTimeKind.Utc));
        next.Should().Be(new DateTime(2026, 10, 31, 21, 30, 0, DateTimeKind.Utc));
    }

    [Fact]
    public void Monthly_LastDayOfTheMonth()
    {
        var next = ScheduleClock.NextRunUtc(ReportFrequency.Monthly, null, 0, new TimeOnly(9, 0),
            new DateTime(2027, 2, 10, 0, 0, 0, DateTimeKind.Utc));
        next.Should().Be(new DateTime(2027, 2, 28, 3, 30, 0, DateTimeKind.Utc));
    }

    [Fact]
    public void Weekly_RunsOnItsDayAfterTheGivenTime()
    {
        // Monday 07:00 IST, asked at Monday 08:00 IST: next Monday.
        var next = ScheduleClock.NextRunUtc(ReportFrequency.Weekly, (byte)DayOfWeek.Monday, null, new TimeOnly(7, 0),
            new DateTime(2026, 10, 19, 2, 30, 0, DateTimeKind.Utc));
        next.Should().Be(new DateTime(2026, 10, 26, 1, 30, 0, DateTimeKind.Utc));
    }

    [Fact]
    public void Periods_MoveWithEachRun()
    {
        var monday = new DateTime(2026, 10, 19, 1, 30, 0, DateTimeKind.Utc);
        ScheduleClock.PeriodFor(ReportFrequency.Daily, monday)
            .Should().Be((new DateOnly(2026, 10, 18), new DateOnly(2026, 10, 18)));
        ScheduleClock.PeriodFor(ReportFrequency.Weekly, monday)
            .Should().Be((new DateOnly(2026, 10, 12), new DateOnly(2026, 10, 18)));
        ScheduleClock.PeriodFor(ReportFrequency.Monthly, new DateTime(2026, 10, 31, 21, 30, 0, DateTimeKind.Utc))
            .Should().Be((new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 31)));
    }

    [Fact]
    public void HowOften_ReadsAsTheLibraryShowsIt()
    {
        ScheduleClock.HowOften(ReportFrequency.Weekly, 1, null, new TimeOnly(7, 0)).Should().Be("Weekly on Monday, 07:00");
        ScheduleClock.HowOften(ReportFrequency.Monthly, null, 1, new TimeOnly(7, 30)).Should().Be("Monthly on the 1st, 07:30");
        ScheduleClock.HowOften(ReportFrequency.Monthly, null, 0, new TimeOnly(9, 0)).Should().Be("Monthly on the last day, 09:00");
    }
}
