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
        // Printed in the bank-details strip of the tax invoice; nothing prints until the
        // account number is set.
        public const string BankAccountName = "Company.BankAccountName";
        public const string BankAccountNumber = "Company.BankAccountNumber";
        public const string BankIfsc = "Company.BankIfsc";
        public const string BankBranch = "Company.BankBranch";
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
        /// <summary>Printed on an invoice that carries no terms of its own.</summary>
        public const string TermsAndConditions = "Invoice.TermsAndConditions";
    }

    /// <summary>Purchase orders. Shown under Settings → Billing (SettingGroup "Billing").</summary>
    public static class Purchase
    {
        /// <summary>true: Draft → Submit → Approve → Send. false: an approver approves a Draft directly.</summary>
        public const string RequireApproval = "Purchase.RequireApproval";
        /// <summary>Default terms copied onto each new PO; editing a PO's copy never changes this.</summary>
        public const string TermsAndConditions = "Purchase.TermsAndConditions";
        /// <summary>"code|label|percent;…" — TDS Regalis deducts from supplier payments.</summary>
        public const string TdsOptions = "Purchase.TdsOptions";
        /// <summary>"0,5,12,18,28".</summary>
        public const string GstRateOptions = "Purchase.GstRateOptions";
        public const string DefaultGstPercent = "Purchase.DefaultGstPercent";
        /// <summary>Comma-separated shipment methods.</summary>
        public const string ShipmentPreferences = "Purchase.ShipmentPreferences";
        /// <summary>Comma-separated accounting heads for PO lines.</summary>
        public const string AccountCategories = "Purchase.AccountCategories";
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

    /// <summary>Maintenance &amp; housekeeping checklist (Settings, group "Maintenance").</summary>
    public static class Maintenance
    {
        /// <summary>Comma-separated work categories.</summary>
        public const string Categories = "Maintenance.Categories";
        /// <summary>Comma-separated common areas; bookable workspaces are added automatically.</summary>
        public const string Areas = "Maintenance.Areas";
        /// <summary>Minutes after the due time before a task is marked Overdue.</summary>
        public const string OverdueGraceMinutes = "Maintenance.OverdueGraceMinutes";
        /// <summary>"Morning=06:00-14:00;Evening=14:00-22:00;Night=22:00-06:00" (IST) — drives "On duty now".</summary>
        public const string ShiftHours = "Maintenance.ShiftHours";
    }

    /// <summary>Assets: QR labels and warranty alerts (Settings, group "Assets").</summary>
    public static class Assets
    {
        /// <summary>"If found, contact …" on every QR label.</summary>
        public const string LabelContactEmail = "Assets.LabelContactEmail";
        /// <summary>"Property of …" on every QR label.</summary>
        public const string LabelOwnerName = "Assets.LabelOwnerName";
        /// <summary>Days before Warranty until that the item is flagged "Expiring soon".</summary>
        public const string WarrantyAlertDays = "Assets.WarrantyAlertDays";
        /// <summary>Comma-separated units the consumable form offers.</summary>
        public const string Units = "Assets.Units";
    }

    public static class Wallet
    {
        public const string MinRecharge = "Wallet.MinRecharge";
        public const string MaxRecharge = "Wallet.MaxRecharge";
        public const string Enabled = "Wallet.Enabled";
        public const string Withdrawable = "Wallet.Withdrawable";
        /// <summary>A balance below this is "low": the admin list filter, the amber
        /// highlight and any low-balance reminder all read it. Shown under Settings → Billing.</summary>
        public const string LowBalanceThreshold = "Wallet.LowBalanceThreshold";
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
