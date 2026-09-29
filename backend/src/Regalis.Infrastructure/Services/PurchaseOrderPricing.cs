using Regalis.Domain.Enums;

namespace Regalis.Infrastructure.Services;

/// <summary>
/// Purchase-order money, as a pure function so it can be unit-tested and mirrored
/// exactly by the form's live preview (AdminPurchaseOrderPages chunk, poCompute).
/// Every step rounds to the paisa, half away from zero:
///
///   gross     = qty × rate                         Sub total = Σ gross
///   base      = gross − line discount
///   discount  = % of Σ base, or the flat amount; spread over lines by base, the
///               largest line absorbing the paisa difference
///   taxable   = base − share
///   tax       = taxable × GST% (CGST = tax / 2, SGST = tax − CGST; or all IGST)
///   TDS       = Σ taxable × TDS%   (deducted on the value before GST)
///   total     = Σ taxable + tax (not under reverse charge) − TDS + adjustment
/// </summary>
public static class PurchaseOrderPricing
{
    public record LineInput(decimal Quantity, decimal Rate, decimal LineDiscount, decimal TaxPercent);

    public record LineResult(decimal Gross, decimal Discount, decimal Taxable, decimal Tax,
        decimal Cgst, decimal Sgst, decimal Igst, decimal Total);

    public record Result(IReadOnlyList<LineResult> Lines, decimal SubTotal, decimal Discount,
        decimal Taxable, decimal Cgst, decimal Sgst, decimal Igst, decimal Tax, decimal Tds,
        decimal Adjustment, decimal Total, bool ExcessDiscount);

    public static Result Compute(IReadOnlyList<LineInput> lines, PurchaseOrderDiscountType? discountType,
        decimal discountValue, bool interState, bool reverseCharge, decimal tdsPercent,
        decimal adjustment)
    {
        var gross = lines.Select(l => R(l.Quantity * l.Rate)).ToList();
        var lineDiscount = lines.Select((l, i) => Math.Min(l.LineDiscount, gross[i])).ToList();
        var bases = gross.Select((g, i) => g - lineDiscount[i]).ToList();
        var baseTotal = bases.Sum();

        var discount = discountType switch
        {
            PurchaseOrderDiscountType.Percentage => R(baseTotal * discountValue / 100m),
            PurchaseOrderDiscountType.Amount => R(discountValue),
            _ => 0m
        };
        var excess = discount > baseTotal;
        if (excess)
        {
            discount = baseTotal;
        }

        var shares = bases.Select(b => baseTotal == 0m ? 0m : R(discount * b / baseTotal)).ToList();
        if (shares.Count > 0)
        {
            var largest = bases.IndexOf(bases.Max());
            shares[largest] += discount - shares.Sum();
        }

        var results = new List<LineResult>(lines.Count);
        for (var i = 0; i < lines.Count; i++)
        {
            var taxable = bases[i] - shares[i];
            var tax = R(taxable * lines[i].TaxPercent / 100m);
            decimal cgst = 0m, sgst = 0m, igst = 0m;
            if (interState)
            {
                igst = tax;
            }
            else
            {
                // Halve once and derive the other half, so CGST + SGST is always the tax.
                cgst = R(tax / 2m);
                sgst = tax - cgst;
            }
            results.Add(new LineResult(gross[i], lineDiscount[i] + shares[i], taxable, tax,
                cgst, sgst, igst, taxable + tax));
        }

        var taxableTotal = results.Sum(r => r.Taxable);
        var taxTotal = results.Sum(r => r.Tax);
        var tds = R(taxableTotal * tdsPercent / 100m);
        var total = taxableTotal + (reverseCharge ? 0m : taxTotal) - tds + adjustment;

        return new Result(results, gross.Sum(), results.Sum(r => r.Discount), taxableTotal,
            results.Sum(r => r.Cgst), results.Sum(r => r.Sgst), results.Sum(r => r.Igst), taxTotal,
            tds, adjustment, total, excess);
    }

    private static decimal R(decimal value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);
}
