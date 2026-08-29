namespace Regalis.Shared;

/// <summary>India Standard Time conversions (A13): storage is always UTC, business
/// rules (working hours, booking dates) evaluate in IST. IST has no DST — a fixed
/// +05:30 offset is correct and avoids timezone-database drift across servers.</summary>
public static class IstClock
{
    public static readonly TimeSpan Offset = new(5, 30, 0);

    public static DateTime ToIst(DateTime utc) => utc + Offset;

    public static DateTime ToUtc(DateTime ist) => ist - Offset;

    /// <summary>The IST calendar date of a UTC instant — TRN_Booking.BookingDate.</summary>
    public static DateOnly IstDateOf(DateTime utc) => DateOnly.FromDateTime(ToIst(utc));
}
