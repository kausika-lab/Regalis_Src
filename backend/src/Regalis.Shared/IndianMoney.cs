namespace Regalis.Shared;

/// <summary>
/// The two Indian conventions every printed money document here needs: the financial
/// year that runs April to March, and an amount spelled out in the lakh/crore system
/// (document §3.8, §5.3). Kept next to <see cref="IstClock"/> because both exist for
/// the same reason — the invariant is local, not cultural-neutral.
/// </summary>
public static class IndianMoney
{
    /// <summary>"2026-27" for any date from 1 April 2026 to 31 March 2027.</summary>
    public static string FinancialYearOf(DateOnly date)
    {
        var startYear = date.Month >= 4 ? date.Year : date.Year - 1;
        return $"{startYear}-{(startYear + 1) % 100:D2}";
    }

    /// <summary>First day of the financial year the date falls in.</summary>
    public static DateOnly FinancialYearStart(DateOnly date) =>
        new(date.Month >= 4 ? date.Year : date.Year - 1, 4, 1);

    /// <summary>Last day of the financial year the date falls in.</summary>
    public static DateOnly FinancialYearEnd(DateOnly date) =>
        FinancialYearStart(date).AddYears(1).AddDays(-1);

    private static readonly string[] Ones =
    [
        "", "One", "Two", "Three", "Four", "Five", "Six", "Seven", "Eight", "Nine", "Ten",
        "Eleven", "Twelve", "Thirteen", "Fourteen", "Fifteen", "Sixteen", "Seventeen",
        "Eighteen", "Nineteen"
    ];

    private static readonly string[] Tens =
    [
        "", "", "Twenty", "Thirty", "Forty", "Fifty", "Sixty", "Seventy", "Eighty", "Ninety"
    ];

    /// <summary>"Rupees One Lakh Twenty One Thousand Two Hundred Ninety Two and Fifty
    /// Paise Only". Groups by crore/lakh/thousand, not by the western thousands.</summary>
    public static string ToWords(decimal amount)
    {
        var negative = amount < 0;
        amount = Math.Abs(Math.Round(amount, 2, MidpointRounding.AwayFromZero));

        var rupees = (long)decimal.Truncate(amount);
        var paise = (int)Math.Round((amount - rupees) * 100m, 0, MidpointRounding.AwayFromZero);
        // Rounding the fraction can carry into the rupees (0.999 → 100 paise).
        if (paise == 100)
        {
            rupees++;
            paise = 0;
        }

        var words = rupees == 0 ? "Zero" : GroupToWords(rupees);
        var text = $"Rupees {words}";
        if (paise > 0)
        {
            text += $" and {GroupToWords(paise)} Paise";
        }
        return (negative ? "Minus " : "") + text + " Only";
    }

    private static string GroupToWords(long value)
    {
        var parts = new List<string>();
        // Crore and lakh first, then the western hundreds inside the last three digits.
        var crore = value / 10_000_000;
        if (crore > 0)
        {
            parts.Add($"{GroupToWords(crore)} Crore");
            value %= 10_000_000;
        }
        var lakh = value / 100_000;
        if (lakh > 0)
        {
            parts.Add($"{BelowHundred(lakh)} Lakh");
            value %= 100_000;
        }
        var thousand = value / 1_000;
        if (thousand > 0)
        {
            parts.Add($"{BelowHundred(thousand)} Thousand");
            value %= 1_000;
        }
        var hundred = value / 100;
        if (hundred > 0)
        {
            parts.Add($"{Ones[hundred]} Hundred");
            value %= 100;
        }
        if (value > 0)
        {
            parts.Add(BelowHundred(value));
        }
        return string.Join(' ', parts);
    }

    private static string BelowHundred(long value) =>
        value < 20
            ? Ones[value]
            : (Tens[value / 10] + (value % 10 > 0 ? " " + Ones[value % 10] : "")).Trim();
}
