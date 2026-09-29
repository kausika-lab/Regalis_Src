using FluentAssertions;
using Regalis.Domain.Enums;
using Regalis.Infrastructure.Services;
using Xunit;
using Line = Regalis.Infrastructure.Services.PurchaseOrderPricing.LineInput;

namespace Regalis.UnitTests.Finance;

public class PurchaseOrderPricingTests
{
    // The three lines from the purchase-order reference screen.
    private static readonly Line[] ReferenceLines =
    [
        new(20m, 850m, 0m, 5m),     // Arabica coffee beans   17,000 @ 5%
        new(6m, 7500m, 0m, 18m),    // Mesh office chair      45,000 @ 18%
        new(4m, 2200m, 0m, 18m)     // Printer cartridge       8,800 @ 18%
    ];

    [Fact]
    public void IntraState_SplitsGstIntoCgstAndSgst_AndMatchesTheReferenceTotal()
    {
        var result = PurchaseOrderPricing.Compute(ReferenceLines, null, 0m,
            interState: false, reverseCharge: false, tdsPercent: 0m, adjustment: 0m);

        result.SubTotal.Should().Be(70_800m);
        result.Tax.Should().Be(850m + 8_100m + 1_584m);
        result.Cgst.Should().Be(5_267m);
        result.Sgst.Should().Be(5_267m);
        result.Igst.Should().Be(0m);
        result.Total.Should().Be(81_334m);
    }

    [Fact]
    public void InterState_ChargesEverythingAsIgst()
    {
        var result = PurchaseOrderPricing.Compute(ReferenceLines, null, 0m,
            interState: true, reverseCharge: false, tdsPercent: 0m, adjustment: 0m);

        result.Igst.Should().Be(10_534m);
        result.Cgst.Should().Be(0m);
        result.Sgst.Should().Be(0m);
        result.Total.Should().Be(81_334m);
    }

    [Fact]
    public void PercentageDiscount_IsTakenBeforeGst_AndSharesAddBackExactly()
    {
        var result = PurchaseOrderPricing.Compute(ReferenceLines,
            PurchaseOrderDiscountType.Percentage, 5m, false, false, 0m, 0m);

        result.Discount.Should().Be(3_540m);
        result.Lines.Sum(l => l.Discount).Should().Be(3_540m);
        result.Taxable.Should().Be(67_260m);
        // 16,150 @ 5% + 42,750 @ 18% + 8,360 @ 18%
        result.Tax.Should().Be(807.50m + 7_695m + 1_504.80m);
    }

    [Fact]
    public void FlatDiscount_ThatDoesNotDivideEvenly_LandsOnTheLargestLine()
    {
        var result = PurchaseOrderPricing.Compute(
            [new(1m, 100m, 0m, 18m), new(1m, 100m, 0m, 18m), new(1m, 100m, 0m, 18m)],
            PurchaseOrderDiscountType.Amount, 100m, false, false, 0m, 0m);

        result.Lines.Select(l => l.Discount).Should().Equal(33.34m, 33.33m, 33.33m);
        result.Discount.Should().Be(100m);
        result.Taxable.Should().Be(200m);
    }

    [Fact]
    public void Tds_IsDeductedOnTheValueBeforeGst_AndAdjustmentIsAdded()
    {
        var result = PurchaseOrderPricing.Compute([new(1m, 10_000m, 0m, 18m)],
            null, 0m, false, false, tdsPercent: 2m, adjustment: -0.50m);

        result.Tds.Should().Be(200m);
        result.Total.Should().Be(10_000m + 1_800m - 200m - 0.50m);
    }

    [Fact]
    public void ReverseCharge_KeepsTheGstFigures_ButLeavesThemOutOfTheSupplierTotal()
    {
        var result = PurchaseOrderPricing.Compute([new(1m, 10_000m, 0m, 18m)],
            null, 0m, false, reverseCharge: true, 0m, 0m);

        result.Tax.Should().Be(1_800m);
        result.Total.Should().Be(10_000m);
    }

    [Fact]
    public void OddTax_SplitsSoCgstPlusSgstIsExactlyTheTax()
    {
        var result = PurchaseOrderPricing.Compute([new(1m, 0.05m, 0m, 18m)],
            null, 0m, false, false, 0m, 0m);

        result.Tax.Should().Be(0.01m);
        (result.Cgst + result.Sgst).Should().Be(0.01m);
    }

    [Fact]
    public void DiscountAboveTheSubTotal_IsFlagged()
    {
        var result = PurchaseOrderPricing.Compute([new(1m, 100m, 0m, 18m)],
            PurchaseOrderDiscountType.Amount, 150m, false, false, 0m, 0m);

        result.ExcessDiscount.Should().BeTrue();
    }
}
