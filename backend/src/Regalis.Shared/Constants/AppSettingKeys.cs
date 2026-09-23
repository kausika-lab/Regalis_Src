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
        public const string DueDays = "Invoice.DueDays";
    }

    public static class Quotation
    {
        public const string ValidityDays = "Quotation.ValidityDays";
        public const string TermsAndConditions = "Quotation.TermsAndConditions";
        /// <summary>Default GST rate for coworking services, 18 (document §2.3).</summary>
        public const string DefaultGstPercent = "Quotation.DefaultGstPercent";
        /// <summary>GST rates the line dropdown offers, e.g. "0,5,12,18,28".</summary>
        public const string GstRateOptions = "Quotation.GstRateOptions";
        /// <summary>The state Regalis supplies from; a matching place of supply is
        /// intra-state (CGST + SGST), anything else inter-state (IGST). "29" is Karnataka.</summary>
        public const string HomeStateCode = "Quotation.HomeStateCode";
        /// <summary>Round the total to the nearest rupee; switchable per §2.3.</summary>
        public const string RoundOffEnabled = "Quotation.RoundOffEnabled";
        /// <summary>Pre-filled customer notes, printed on the PDF.</summary>
        public const string CustomerNotes = "Quotation.CustomerNotes";
        /// <summary>TDS options as "code|label|percent" rows separated by ';', e.g.
        /// "194I|194-I Rent 10%|10;194J|194-J Professional 10%|10" (document §2.3).</summary>
        public const string TdsOptions = "Quotation.TdsOptions";
        /// <summary>Send templates; {customer_name} {quote_number} {total}
        /// {valid_until} {portal_link} are substituted (document §3.5).</summary>
        public const string EmailSubjectTemplate = "Quotation.EmailSubjectTemplate";
        public const string EmailBodyTemplate = "Quotation.EmailBodyTemplate";
        public const string WhatsAppTemplate = "Quotation.WhatsAppTemplate";
        /// <summary>Base URL the customer portal link is built on.</summary>
        public const string PortalBaseUrl = "Quotation.PortalBaseUrl";
        /// <summary>Days before expiry the reminder goes out (document §5.3).</summary>
        public const string ExpiryReminderDays = "Quotation.ExpiryReminderDays";
        /// <summary>Default months of rent for the advance/security-deposit invoice.</summary>
        public const string AdvanceMonths = "Quotation.AdvanceMonths";
    }

    public static class Booking
    {
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
