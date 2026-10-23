using FluentAssertions;
using Regalis.Application.Operations;
using Regalis.Domain.Enums;
using Xunit;

namespace Regalis.UnitTests.Operations;

/// <summary>Assets spec §9.1 (QR text), §11.3 (book value), §1.2 and §12 (codes).</summary>
public class AssetRulesTests
{
    private static readonly DateOnly Today = new(2026, 10, 15);

    [Fact]
    public void BookValue_IsStraightLine_OverTheCategorysLife()
    {
        // ₹45,000 on a 7-year life, bought 42 days ago: 45000 × (1 − (42/365)/7).
        AssetRules.BookValue(45000m, Today.AddDays(-42), 7m, AssetStatus.InUse, Today)
            .Should().Be(44260.27m);
        AssetRules.BookValue(45000m, Today, 7m, AssetStatus.InUse, Today).Should().Be(45000m);
    }

    [Fact]
    public void BookValue_NeverFallsBelowFivePercent_WhileInTheRegister()
    {
        AssetRules.BookValue(10000m, Today.AddYears(-20), 7m, AssetStatus.InUse, Today).Should().Be(500m);
        AssetRules.BookValue(10000m, Today.AddYears(-20), 7m, AssetStatus.InRepair, Today).Should().Be(500m);
    }

    [Theory]
    [InlineData(AssetStatus.Scrapped)]
    [InlineData(AssetStatus.Lost)]
    [InlineData(AssetStatus.ReturnedToSupplier)]
    public void BookValue_IsZero_OnceTheAssetHasLeft(AssetStatus status) =>
        AssetRules.BookValue(45000m, Today.AddDays(-42), 7m, status, Today).Should().Be(0m);

    [Fact]
    public void BookValue_IsADash_WithNoCost_AndTheCost_WithNoLifeOrDate()
    {
        AssetRules.BookValue(null, Today.AddDays(-42), 7m, AssetStatus.InUse, Today).Should().BeNull();
        AssetRules.BookValue(500m, Today.AddDays(-400), null, AssetStatus.InUse, Today).Should().Be(500m);
        AssetRules.BookValue(500m, null, 7m, AssetStatus.InUse, Today).Should().Be(500m);
    }

    [Fact]
    public void QrText_IsPlainAsciiText_OneFieldPerLine()
    {
        AssetRules.QrText("R-2W-E-EAC-001", "Air Conditioner 2.0 TR", "Blue Star", "MD Cabin",
                new DateOnly(2026, 9, 1), "Regalis Workspace", "admin@regalis.in")
            .Should().Be("REGALIS WS ASSET\nCode: R-2W-E-EAC-001\nItem: Air Conditioner 2.0 TR\nBrand: Blue Star\n" +
                         "Where: MD Cabin\nBought: 2026-09-01\nProperty of Regalis Workspace\nIf found, contact admin@regalis.in");
    }

    [Fact]
    public void QrText_SkipsAMissingBrand_AndReplacesWhatScannersMisread()
    {
        var text = AssetRules.QrText("R-2W-E-FT-001", "CEO Desk – teak ₹ café", null, "Cabin × 2", null,
            "Regalis Workspace", "admin@regalis.in");
        text.Should().NotContain("Brand:").And.NotContain("Bought:");
        text.Should().Contain("Item: CEO Desk - teak Rs. cafe").And.Contain("Where: Cabin x 2");
        text.All(c => c == '\n' || (c >= ' ' && c <= '~')).Should().BeTrue();
    }

    [Theory]
    [InlineData("R-2W-E-ES ", "R-2W-E-ES")]
    [InlineData(" r-2w-e-eac", "R-2W-E-EAC")]
    [InlineData("R-2W-E-ESB-", "R-2W-E-ESB")]
    [InlineData("R - 2W - E", "R-2W-E")]
    [InlineData("   ", null)]
    public void NormalisePrefix_ReadsTheSheetsPrefixesAsOne(string raw, string? expected) =>
        AssetRules.NormalisePrefix(raw).Should().Be(expected);

    [Fact]
    public void Codes_AreThreeDigitRunningNumbers_ShownAsARange()
    {
        AssetRules.AssetCode("R-2W-E-EGC", 15).Should().Be("R-2W-E-EGC-015");
        AssetRules.AssetCode("R-2W-E-EGC", 1000).Should().Be("R-2W-E-EGC-1000");
        AssetRules.DocumentNumber("IN-2026", 7).Should().Be("IN-2026-0007");
        AssetRules.CodeRange(["R-2W-E-EGC-015", "R-2W-E-EGC-022"]).Should().Be("R-2W-E-EGC-015 to -022");
        AssetRules.CodeRange(["R-2W-E-EGC-015"]).Should().Be("R-2W-E-EGC-015");
    }

    [Theory]
    [InlineData("REGALIS WS ASSET\nCode: R-2W-E-EAC-001\nItem: Air Conditioner", "R-2W-E-EAC-001")]
    [InlineData("REGALIS WS ASSET\r\nCode:  r-2w-e-eac-002 \r\nItem: x", "R-2W-E-EAC-002")]
    [InlineData("  coffee-07 ", "COFFEE-07")]
    [InlineData("", null)]
    public void CodeFromScan_FindsTheCode_InTheStickerOrABareCode(string text, string? expected) =>
        AssetRules.CodeFromScan(text).Should().Be(expected);
}
