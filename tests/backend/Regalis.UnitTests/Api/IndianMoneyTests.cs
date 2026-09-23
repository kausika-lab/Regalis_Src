using FluentAssertions;
using Regalis.Shared;

namespace Regalis.UnitTests.Api;

/// <summary>The lakh/crore spelling and the April–March year that every printed
/// money document depends on (quotation specification §3.8, §5.3).</summary>
public class IndianMoneyTests
{
    [Theory]
    [InlineData(0, "Rupees Zero Only")]
    [InlineData(1, "Rupees One Only")]
    [InlineData(19, "Rupees Nineteen Only")]
    [InlineData(20, "Rupees Twenty Only")]
    [InlineData(95, "Rupees Ninety Five Only")]
    [InlineData(100, "Rupees One Hundred Only")]
    [InlineData(1000, "Rupees One Thousand Only")]
    // Grouped the Indian way: one lakh, not one hundred thousand.
    [InlineData(100000, "Rupees One Lakh Only")]
    [InlineData(10000000, "Rupees One Crore Only")]
    [InlineData(121292, "Rupees One Lakh Twenty One Thousand Two Hundred Ninety Two Only")]
    public void ToWords_SpellsWholeRupeesTheIndianWay(int amount, string expected) =>
        IndianMoney.ToWords(amount).Should().Be(expected);

    [Fact]
    public void ToWords_IncludesPaiseWhenThereAreAny() =>
        IndianMoney.ToWords(112.10m).Should()
            .Be("Rupees One Hundred Twelve and Ten Paise Only");

    [Fact]
    public void ToWords_CarriesPaiseThatRoundUpIntoTheRupees() =>
        IndianMoney.ToWords(99.999m).Should().Be("Rupees One Hundred Only");

    [Fact]
    public void ToWords_MarksANegativeAmount() =>
        IndianMoney.ToWords(-5).Should().Be("Minus Rupees Five Only");

    [Theory]
    // April starts the year; March is still the previous one.
    [InlineData(2026, 4, 1, "2026-27")]
    [InlineData(2026, 9, 23, "2026-27")]
    [InlineData(2027, 3, 31, "2026-27")]
    [InlineData(2027, 4, 1, "2027-28")]
    [InlineData(2026, 1, 15, "2025-26")]
    public void FinancialYearOf_RunsAprilToMarch(int year, int month, int day, string expected) =>
        IndianMoney.FinancialYearOf(new DateOnly(year, month, day)).Should().Be(expected);

    [Fact]
    public void FinancialYear_StartsOnOneAprilAndEndsOnThirtyOneMarch()
    {
        var date = new DateOnly(2026, 9, 23);
        IndianMoney.FinancialYearStart(date).Should().Be(new DateOnly(2026, 4, 1));
        IndianMoney.FinancialYearEnd(date).Should().Be(new DateOnly(2027, 3, 31));
    }
}
