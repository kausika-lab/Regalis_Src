using Regalis.Application.Finance;

namespace Regalis.Infrastructure.Services;

/// <summary>
/// The GST state codes, for the place-of-supply picker on a quotation (document §2.2).
/// These are statutory and do not change with configuration, so they are a constant
/// rather than an MST_ table: Invariant 6 is about business choices, and the state a
/// customer is in is not one Regalis gets to make.
///
/// Code 29 (Karnataka) is the one that makes a quotation intra-state, which is a
/// setting (<c>Quotation.HomeStateCode</c>) because Regalis could move.
/// </summary>
public static class GstStates
{
    public static readonly IReadOnlyList<QuotationStateOptionDto> All =
    [
        new("01", "Jammu and Kashmir"),
        new("02", "Himachal Pradesh"),
        new("03", "Punjab"),
        new("04", "Chandigarh"),
        new("05", "Uttarakhand"),
        new("06", "Haryana"),
        new("07", "Delhi"),
        new("08", "Rajasthan"),
        new("09", "Uttar Pradesh"),
        new("10", "Bihar"),
        new("11", "Sikkim"),
        new("12", "Arunachal Pradesh"),
        new("13", "Nagaland"),
        new("14", "Manipur"),
        new("15", "Mizoram"),
        new("16", "Tripura"),
        new("17", "Meghalaya"),
        new("18", "Assam"),
        new("19", "West Bengal"),
        new("20", "Jharkhand"),
        new("21", "Odisha"),
        new("22", "Chhattisgarh"),
        new("23", "Madhya Pradesh"),
        new("24", "Gujarat"),
        new("26", "Dadra and Nagar Haveli and Daman and Diu"),
        new("27", "Maharashtra"),
        new("29", "Karnataka"),
        new("30", "Goa"),
        new("31", "Lakshadweep"),
        new("32", "Kerala"),
        new("33", "Tamil Nadu"),
        new("34", "Puducherry"),
        new("35", "Andaman and Nicobar Islands"),
        new("36", "Telangana"),
        new("37", "Andhra Pradesh"),
        new("38", "Ladakh"),
        new("97", "Other Territory")
    ];

    private static readonly Dictionary<string, string> ByCode =
        All.ToDictionary(state => state.Code, state => state.Name);

    /// <summary>The state's name, or the code itself when it is not one we know —
    /// a quotation must still print something sensible.</summary>
    public static string NameOf(string? code) =>
        code is not null && ByCode.TryGetValue(code, out var name) ? name : code ?? "";
}
