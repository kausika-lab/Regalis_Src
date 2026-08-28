namespace Regalis.Shared.Constants;

/// <summary>
/// Keys of the seeded <c>MST_AppSetting</c> rows — the "no hard-coding" table
/// (Design Invariant 6). Values are configured by the admin, never compiled in.
/// </summary>
public static class AppSettingKeys
{
    public static class Company
    {
        public const string Name = "Company.Name";
        public const string Gstin = "Company.Gstin";
        public const string Address = "Company.Address";
        public const string Email = "Company.Email";
        public const string Phone = "Company.Phone";
        public const string LogoPath = "Company.LogoPath";
    }

    public static class Tax
    {
        public const string CgstPercent = "Tax.CgstPercent";
        public const string SgstPercent = "Tax.SgstPercent";
        public const string IgstPercent = "Tax.IgstPercent";
        public const string PlaceOfSupply = "Tax.PlaceOfSupply";
    }

    public static class Invoice
    {
        public const string NumberPrefix = "Invoice.NumberPrefix";
        public const string DueDays = "Invoice.DueDays";
    }

    public static class Booking
    {
        public const string NumberPrefix = "Booking.NumberPrefix";
        public const string SlotGranularityMinutes = "Booking.SlotGranularityMinutes";
        public const string MinMinutes = "Booking.MinMinutes";
        public const string MaxMinutes = "Booking.MaxMinutes";
        public const string AdvanceDays = "Booking.AdvanceDays";
        public const string PendingHoldMinutes = "Booking.PendingHoldMinutes";
    }

    public static class Cancellation
    {
        public const string CutoffHours = "Cancellation.CutoffHours";
        public const string RefundPercentBeforeCutoff = "Cancellation.RefundPercentBeforeCutoff";
        public const string RefundPercentAfterCutoff = "Cancellation.RefundPercentAfterCutoff";
    }

    public static class Wallet
    {
        public const string MinRecharge = "Wallet.MinRecharge";
        public const string MaxRecharge = "Wallet.MaxRecharge";
        public const string Enabled = "Wallet.Enabled";
        public const string Withdrawable = "Wallet.Withdrawable";
    }

    public static class Support
    {
        public const string ContactNumber = "Support.ContactNumber";
        public const string Hours = "Support.Hours";
        public const string Message = "Support.Message";
        public const string Email = "Support.Email";
    }

    public static class Security
    {
        public const string MaxFailedLogins = "Security.MaxFailedLogins";
        public const string LockoutMinutes = "Security.LockoutMinutes";
        public const string PasswordMinLength = "Security.PasswordMinLength";
        public const string OtpTtlMinutes = "Security.OtpTtlMinutes";
        public const string OtpMaxAttempts = "Security.OtpMaxAttempts";
    }

    public static class Attendance
    {
        public const string AllowMultipleCheckInPerDay = "Attendance.AllowMultipleCheckInPerDay";
        public const string AutoCheckoutHour = "Attendance.AutoCheckoutHour";
    }
}
