using Regalis.Domain.Enums;
using Regalis.Shared.Constants;

namespace Regalis.Infrastructure.Persistence.Seed;

/// <summary>
/// The seeded state from ROLE_PERMISSION_MATRIX.md and DATABASE_DESIGN.md,
/// expressed as data. Nothing here is consulted at request time — the engine
/// reads only the database.
/// </summary>
internal static class SeedCatalogue
{
    // ── Permission catalogue (matrix §2) ────────────────────────────────────
    internal static readonly (string Module, string[] Actions)[] Permissions =
    [
        ("DASHBOARD", ["VIEW"]),
        ("USER", ["VIEW", "CREATE", "EDIT", "DELETE", "EXPORT"]),
        ("ROLE", ["VIEW", "CREATE", "EDIT", "DELETE"]),
        ("PERMISSION", ["VIEW", "EDIT"]),
        ("MENU", ["VIEW", "CREATE", "EDIT", "DELETE"]),
        ("CUSTOMER", ["VIEW", "CREATE", "EDIT", "DELETE", "EXPORT"]),
        ("LOCATION", ["VIEW", "CREATE", "EDIT", "DELETE"]),
        ("WORKSPACE", ["VIEW", "CREATE", "EDIT", "DELETE"]),
        ("CALENDAR", ["VIEW", "CREATE", "EDIT"]),
        ("BOOKING", ["VIEW", "CREATE", "EDIT", "DELETE", "APPROVE", "EXPORT", "PRINT"]),
        ("PAYMENT", ["VIEW", "CREATE", "EXPORT"]),
        ("WALLET", ["VIEW", "CREATE", "EDIT", "EXPORT"]),
        ("INVOICE", ["VIEW", "CREATE", "EDIT", "EXPORT", "PRINT"]),
        ("PASS", ["VIEW", "CREATE", "EDIT", "DELETE", "EXPORT"]),
        ("ACCESS", ["VIEW", "CREATE", "EDIT", "OVERRIDE", "EXPORT"]),
        ("ATTENDANCE", ["VIEW", "CREATE", "EDIT", "APPROVE", "EXPORT"]),
        ("PAYROLL", ["VIEW", "EXPORT"]),
        ("INVENTORY", ["VIEW", "CREATE", "EDIT", "DELETE", "EXPORT"]),
        ("HOUSEKEEPING", ["VIEW", "CREATE", "EDIT", "DELETE", "APPROVE"]),
        ("SERVICE", ["VIEW", "CREATE", "EDIT", "DELETE"]),
        ("BANNER", ["VIEW", "CREATE", "EDIT", "DELETE"]),
        ("NOTIFICATION", ["VIEW", "CREATE", "EDIT", "DELETE"]),
        ("TICKET", ["VIEW", "CREATE", "EDIT", "DELETE", "APPROVE", "EXPORT"]),
        ("REPORT", ["VIEW", "EXPORT", "PRINT"]),
        ("SETTING", ["VIEW", "EDIT"]),
        ("AUDIT", ["VIEW", "EXPORT"])
    ];

    // ── Roles (matrix §3) ───────────────────────────────────────────────────
    internal static readonly (string Code, string Name, string Description, PortalType Portal)[] Roles =
    [
        (RoleCodes.Customer, "Customer", "A registered member. Sees only their own data.", PortalType.Customer),
        (RoleCodes.Admin, "Administrator", "Full operational control.", PortalType.Admin),
        (RoleCodes.Manager, "Operations Manager",
            "Day-to-day operations, no user/role/menu/settings administration.", PortalType.Admin),
        (RoleCodes.Finance, "Finance", "Money and reporting, read-only on operations.", PortalType.Admin),
        (RoleCodes.Support, "Support Executive", "Tickets, customers, bookings — read-mostly.", PortalType.Admin),
        (RoleCodes.Housekeeping, "Housekeeping Staff",
            "Owns a login and access tokens; sees only their own schedule.", PortalType.Admin)
    ];

    // ── Role → permission matrix (matrix §4). "*" = every action of the module. ─
    internal static readonly Dictionary<string, (string Module, string[] Actions)[]> RolePermissions = new()
    {
        [RoleCodes.Customer] =
        [
            ("DASHBOARD", ["VIEW"]), ("LOCATION", ["VIEW"]), ("WORKSPACE", ["VIEW"]),
            ("CALENDAR", ["VIEW"]), ("BOOKING", ["VIEW", "CREATE", "EDIT"]),
            ("PAYMENT", ["VIEW", "CREATE"]), ("WALLET", ["VIEW", "CREATE"]),
            ("INVOICE", ["VIEW", "PRINT"]), ("PASS", ["VIEW", "CREATE"]),
            ("ACCESS", ["VIEW"]), ("ATTENDANCE", ["VIEW"]), ("SERVICE", ["VIEW", "CREATE"]),
            ("BANNER", ["VIEW"]), ("NOTIFICATION", ["VIEW", "EDIT"]),
            ("TICKET", ["VIEW", "CREATE", "EDIT"])
        ],
        [RoleCodes.Admin] = Permissions.Select(p => (p.Module, new[] { "*" })).ToArray(),
        [RoleCodes.Manager] =
        [
            ("DASHBOARD", ["VIEW"]), ("CUSTOMER", ["VIEW", "EDIT", "EXPORT"]),
            ("LOCATION", ["VIEW", "EDIT"]), ("WORKSPACE", ["*"]), ("CALENDAR", ["*"]),
            ("BOOKING", ["*"]), ("PAYMENT", ["VIEW"]), ("WALLET", ["VIEW"]),
            ("INVOICE", ["VIEW", "PRINT"]), ("PASS", ["*"]),
            ("ACCESS", ["VIEW", "CREATE", "EDIT", "OVERRIDE"]), ("ATTENDANCE", ["*"]),
            ("INVENTORY", ["*"]), ("HOUSEKEEPING", ["*"]), ("SERVICE", ["*"]),
            ("BANNER", ["*"]), ("NOTIFICATION", ["VIEW", "CREATE"]), ("TICKET", ["*"]),
            ("REPORT", ["VIEW", "EXPORT", "PRINT"])
        ],
        [RoleCodes.Finance] =
        [
            ("DASHBOARD", ["VIEW"]), ("CUSTOMER", ["VIEW", "EXPORT"]), ("LOCATION", ["VIEW"]),
            ("WORKSPACE", ["VIEW"]), ("CALENDAR", ["VIEW"]), ("BOOKING", ["VIEW", "EXPORT"]),
            ("PAYMENT", ["*"]), ("WALLET", ["*"]), ("INVOICE", ["*"]),
            ("PASS", ["VIEW", "EXPORT"]), ("ACCESS", ["VIEW"]),
            ("ATTENDANCE", ["VIEW", "EXPORT"]), ("PAYROLL", ["*"]),
            ("INVENTORY", ["VIEW", "EXPORT"]), ("REPORT", ["*"]), ("AUDIT", ["VIEW", "EXPORT"])
        ],
        [RoleCodes.Support] =
        [
            ("DASHBOARD", ["VIEW"]), ("CUSTOMER", ["VIEW", "EDIT"]), ("LOCATION", ["VIEW"]),
            ("WORKSPACE", ["VIEW"]), ("CALENDAR", ["VIEW"]), ("BOOKING", ["VIEW", "EDIT"]),
            ("PAYMENT", ["VIEW"]), ("WALLET", ["VIEW"]), ("INVOICE", ["VIEW"]),
            ("PASS", ["VIEW"]), ("ACCESS", ["VIEW"]), ("HOUSEKEEPING", ["VIEW"]),
            ("SERVICE", ["VIEW"]), ("NOTIFICATION", ["VIEW", "CREATE"]), ("TICKET", ["*"]),
            ("REPORT", ["VIEW", "EXPORT"])
        ],
        [RoleCodes.Housekeeping] =
        [
            ("WORKSPACE", ["VIEW"]), ("CALENDAR", ["VIEW"]), ("ACCESS", ["VIEW"]),
            ("ATTENDANCE", ["VIEW"]), ("INVENTORY", ["VIEW"]), ("HOUSEKEEPING", ["VIEW"]),
            ("NOTIFICATION", ["VIEW"])
        ]
    };

    // ── Menus (matrix §5; routes from MODULE_PLAN §3) ───────────────────────
    // (Code, Parent, Display, Route, Icon, Permission, roles...)
    internal record MenuSeed(
        string Code,
        string? ParentCode,
        string DisplayName,
        string? Route,
        string Icon,
        string? PermissionCode,
        PortalType Portal,
        string[] Roles);

    private static readonly string[] AdminOnly = [RoleCodes.Admin];
    private static readonly string[] AdminManager = [RoleCodes.Admin, RoleCodes.Manager];
    private static readonly string[] AdminFinance = [RoleCodes.Admin, RoleCodes.Finance];
    private static readonly string[] AdminManagerSupport = [RoleCodes.Admin, RoleCodes.Manager, RoleCodes.Support];
    private static readonly string[] AdminManagerFinance = [RoleCodes.Admin, RoleCodes.Manager, RoleCodes.Finance];
    private static readonly string[] AllAdminPortal =
        [RoleCodes.Admin, RoleCodes.Manager, RoleCodes.Finance, RoleCodes.Support];
    private static readonly string[] CustomerOnly = [RoleCodes.Customer];

    internal static readonly MenuSeed[] Menus =
    [
        // Customer portal — Contact Us ships with M23 (Day 8) as a data insert.
        new("CUST_DASHBOARD", null, "Dashboard", "/", "LayoutDashboard", "DASHBOARD.VIEW", PortalType.Customer, CustomerOnly),
        new("CUST_BOOK", null, "Book Workspace", "/book", "CalendarDays", "BOOKING.CREATE", PortalType.Customer, CustomerOnly),
        new("CUST_BOOKINGS", null, "My Bookings", "/bookings", "ClipboardList", "BOOKING.VIEW", PortalType.Customer, CustomerOnly),
        new("CUST_DAYPASS", null, "Day Pass", "/day-pass", "Ticket", "PASS.VIEW", PortalType.Customer, CustomerOnly),
        new("CUST_MONTHLYPASS", null, "Monthly Pass", "/monthly-pass", "CalendarRange", "PASS.VIEW", PortalType.Customer, CustomerOnly),
        new("CUST_WALLET", null, "Wallet", "/wallet", "Wallet", "WALLET.VIEW", PortalType.Customer, CustomerOnly),
        new("CUST_INVOICES", null, "Invoices", "/invoices", "ReceiptText", "INVOICE.VIEW", PortalType.Customer, CustomerOnly),
        new("CUST_QR", null, "My QR / Access", "/qr", "QrCode", "ACCESS.VIEW", PortalType.Customer, CustomerOnly),
        new("CUST_WORKSTATUS", null, "My Work Status", "/work-status", "Clock", "ATTENDANCE.VIEW", PortalType.Customer, CustomerOnly),
        new("CUST_SERVICES", null, "Services", "/services", "Coffee", "SERVICE.VIEW", PortalType.Customer, CustomerOnly),
        new("CUST_NOTIFICATIONS", null, "Notifications", "/notifications", "Bell", "NOTIFICATION.VIEW", PortalType.Customer, CustomerOnly),
        new("CUST_SUPPORT", null, "Support", "/support", "LifeBuoy", "TICKET.VIEW", PortalType.Customer, CustomerOnly),
        new("CUST_SUPPORT_RAISE", "CUST_SUPPORT", "Raise Ticket", "/support/raise", "MessageSquarePlus", "TICKET.CREATE", PortalType.Customer, CustomerOnly),
        new("CUST_SUPPORT_ALL", "CUST_SUPPORT", "All Tickets", "/support/tickets", "Tickets", "TICKET.VIEW", PortalType.Customer, CustomerOnly),
        new("CUST_PROFILE", null, "Profile", "/profile", "UserRound", null, PortalType.Customer, CustomerOnly),

        // Admin portal
        new("ADM_DASHBOARD", null, "Dashboard", "/admin", "LayoutDashboard", "DASHBOARD.VIEW", PortalType.Admin, AllAdminPortal),
        new("ADM_CUSTOMERS", null, "Customers", "/admin/customers", "UsersRound", "CUSTOMER.VIEW", PortalType.Admin, AllAdminPortal),
        new("ADM_WORKSPACES", null, "Workspaces", "/admin/workspaces", "Building2", "WORKSPACE.VIEW", PortalType.Admin, AdminManager),
        new("ADM_WORKSPACES_DESKS", "ADM_WORKSPACES", "Desks", "/admin/workspaces/desks", "Armchair", "WORKSPACE.VIEW", PortalType.Admin, AdminManager),
        new("ADM_WORKSPACES_CABINS", "ADM_WORKSPACES", "Cabins", "/admin/workspaces/cabins", "DoorClosed", "WORKSPACE.VIEW", PortalType.Admin, AdminManager),
        new("ADM_WORKSPACES_MEETING", "ADM_WORKSPACES", "Meeting Rooms", "/admin/workspaces/meeting-rooms", "Presentation", "WORKSPACE.VIEW", PortalType.Admin, AdminManager),
        new("ADM_CALENDAR", null, "Calendar", "/admin/calendar", "CalendarDays", "CALENDAR.VIEW", PortalType.Admin, AdminManager),
        new("ADM_BOOKINGS", null, "Bookings", "/admin/bookings", "ClipboardList", "BOOKING.VIEW", PortalType.Admin, AdminManagerSupport),
        new("ADM_ACCESS", null, "Access Control", null, "ScanLine", "ACCESS.VIEW", PortalType.Admin, AdminManager),
        new("ADM_ACCESS_SCAN", "ADM_ACCESS", "Scan / Validate", "/admin/access", "ScanLine", "ACCESS.CREATE", PortalType.Admin, AdminManager),
        new("ADM_ACCESS_LOGS", "ADM_ACCESS", "Access Logs", "/admin/access/logs", "ListChecks", "ACCESS.VIEW", PortalType.Admin, AdminManager),
        new("ADM_ATTENDANCE", null, "Attendance", "/admin/attendance", "Clock", "ATTENDANCE.VIEW", PortalType.Admin, AdminManagerFinance),
        new("ADM_ATTENDANCE_PAYROLL", "ADM_ATTENDANCE", "Payroll Export", "/admin/attendance/payroll", "FileSpreadsheet", "PAYROLL.EXPORT", PortalType.Admin, AdminFinance),
        new("ADM_FINANCE", null, "Billing & Finance", null, "IndianRupee", "PAYMENT.VIEW", PortalType.Admin, AdminFinance),
        new("ADM_FINANCE_PAYMENTS", "ADM_FINANCE", "Payments", "/admin/payments", "CreditCard", "PAYMENT.VIEW", PortalType.Admin, AdminFinance),
        new("ADM_FINANCE_INVOICES", "ADM_FINANCE", "Invoices", "/admin/invoices", "ReceiptText", "INVOICE.VIEW", PortalType.Admin, AdminFinance),
        new("ADM_FINANCE_WALLET", "ADM_FINANCE", "Wallet", "/admin/wallet", "Wallet", "WALLET.VIEW", PortalType.Admin, AdminFinance),
        new("ADM_PASS_DAY", null, "Day Pass", "/admin/passes/day", "Ticket", "PASS.VIEW", PortalType.Admin, AdminManager),
        new("ADM_PASS_MONTHLY", null, "Monthly Pass", "/admin/passes/monthly", "CalendarRange", "PASS.VIEW", PortalType.Admin, AdminManager),
        new("ADM_INVENTORY", null, "Inventory", null, "Package", "INVENTORY.VIEW", PortalType.Admin, AdminManager),
        new("ADM_INVENTORY_ITEMS", "ADM_INVENTORY", "Items", "/admin/inventory/items", "Boxes", "INVENTORY.VIEW", PortalType.Admin, AdminManager),
        new("ADM_INVENTORY_TXNS", "ADM_INVENTORY", "Transactions", "/admin/inventory/transactions", "ArrowLeftRight", "INVENTORY.VIEW", PortalType.Admin, AdminManager),
        new("ADM_HOUSEKEEPING", null, "Housekeeping", "/admin/housekeeping", "Sparkles", "HOUSEKEEPING.VIEW", PortalType.Admin, AdminManager),
        new("ADM_LOCATIONS", null, "Locations", "/admin/locations", "MapPin", "LOCATION.VIEW", PortalType.Admin, AdminOnly),
        new("ADM_SERVICES", null, "Services / Coffee", "/admin/services", "Coffee", "SERVICE.VIEW", PortalType.Admin, AdminManager),
        new("ADM_BANNERS", null, "Banners", "/admin/banners", "Image", "BANNER.VIEW", PortalType.Admin, AdminManager),
        new("ADM_NOTIFICATIONS", null, "Notifications", "/admin/notifications", "Bell", "NOTIFICATION.VIEW", PortalType.Admin, AdminManagerSupport),
        new("ADM_TICKETS", null, "Support / Tickets", "/admin/tickets", "LifeBuoy", "TICKET.VIEW", PortalType.Admin, AdminManagerSupport),
        new("ADM_REPORTS", null, "Reports & Analysis", "/admin/reports", "BarChart3", "REPORT.VIEW", PortalType.Admin, AdminManagerFinance),
        new("ADM_ADMINISTRATION", null, "Administration", null, "ShieldCheck", "USER.VIEW", PortalType.Admin, AdminOnly),
        new("ADM_ADMIN_USERS", "ADM_ADMINISTRATION", "Users", "/admin/users", "UserCog", "USER.VIEW", PortalType.Admin, AdminOnly),
        new("ADM_ADMIN_ROLES", "ADM_ADMINISTRATION", "Roles & Permissions", "/admin/roles", "KeyRound", "ROLE.VIEW", PortalType.Admin, AdminOnly),
        new("ADM_ADMIN_MENUS", "ADM_ADMINISTRATION", "Menu Management", "/admin/menus", "PanelLeft", "MENU.VIEW", PortalType.Admin, AdminOnly),
        new("ADM_ADMIN_AUDIT", "ADM_ADMINISTRATION", "Audit Trail", "/admin/audit", "History", "AUDIT.VIEW", PortalType.Admin, AdminOnly),
        new("ADM_SETTINGS", null, "Settings", "/admin/settings", "Settings", "SETTING.VIEW", PortalType.Admin, AdminOnly),
        new("ADM_PROFILE", null, "Admin Profile", "/admin/profile", "UserRound", null, PortalType.Admin, AllAdminPortal)
    ];

    // ── App settings (DATABASE_DESIGN — MST_AppSetting seed) ───────────────
    // (Key, Value, Group, DataType, Description, EditableByAdmin)
    internal static readonly (string Key, string Value, string Group, string DataType, string Description)[]
        AppSettings =
        [
            (AppSettingKeys.Company.Name, "Regalis WorkSpace", "Company", "string", "Legal / display company name"),
            (AppSettingKeys.Company.Gstin, "", "Company", "string", "Company GSTIN printed on invoices"),
            (AppSettingKeys.Company.Address, "", "Company", "string", "Registered address printed on invoices"),
            (AppSettingKeys.Company.Email, "enquiry@amstag.in", "Company", "string", "Company contact email"),
            (AppSettingKeys.Company.Phone, "", "Company", "string", "Company contact phone"),
            (AppSettingKeys.Company.LogoPath, "", "Company", "string", "Logo used on invoices and portal"),
            (AppSettingKeys.Tax.CgstPercent, "9", "Tax", "decimal", "CGST percentage"),
            (AppSettingKeys.Tax.SgstPercent, "9", "Tax", "decimal", "SGST percentage"),
            (AppSettingKeys.Tax.IgstPercent, "18", "Tax", "decimal", "IGST percentage (inter-state)"),
            (AppSettingKeys.Tax.PlaceOfSupply, "", "Tax", "string", "Place of supply shown on invoices"),
            (AppSettingKeys.Invoice.NumberPrefix, "REG/INV", "Invoice", "string", "Invoice number prefix"),
            (AppSettingKeys.Invoice.DueDays, "7", "Invoice", "int", "Days until an invoice is due"),
            (AppSettingKeys.Booking.NumberPrefix, "REG-", "Booking", "string", "Booking number prefix"),
            (AppSettingKeys.Booking.SlotGranularityMinutes, "30", "Booking", "int", "Calendar slot granularity"),
            (AppSettingKeys.Booking.MinMinutes, "30", "Booking", "int", "Minimum booking duration"),
            (AppSettingKeys.Booking.MaxMinutes, "480", "Booking", "int", "Maximum booking duration"),
            (AppSettingKeys.Booking.AdvanceDays, "60", "Booking", "int", "How far ahead a booking may start"),
            (AppSettingKeys.Booking.PendingHoldMinutes, "10", "Booking", "int", "Minutes a Pending booking holds the slot"),
            (AppSettingKeys.Cancellation.CutoffHours, "24", "Cancellation", "int", "Hours before start for full refund"),
            (AppSettingKeys.Cancellation.RefundPercentBeforeCutoff, "100", "Cancellation", "decimal", "Refund % before cutoff"),
            (AppSettingKeys.Cancellation.RefundPercentAfterCutoff, "0", "Cancellation", "decimal", "Refund % after cutoff"),
            (AppSettingKeys.Wallet.MinRecharge, "100", "Wallet", "decimal", "Minimum wallet recharge"),
            (AppSettingKeys.Wallet.MaxRecharge, "50000", "Wallet", "decimal", "Maximum wallet recharge"),
            (AppSettingKeys.Wallet.Enabled, "true", "Wallet", "bool", "Wallet feature toggle"),
            (AppSettingKeys.Wallet.Withdrawable, "false", "Wallet", "bool", "Doc-1 §22: wallet is non-withdrawable"),
            (AppSettingKeys.Support.ContactNumber, "", "Support", "string", "Support phone shown to customers"),
            (AppSettingKeys.Support.Hours, "Mon–Sat 09:00–19:00 IST", "Support", "string", "Support hours"),
            (AppSettingKeys.Support.Message, "We usually respond within one business day.", "Support", "string", "Support banner message"),
            (AppSettingKeys.Support.Email, "enquiry@amstag.in", "Support", "string", "Support email"),
            (AppSettingKeys.Security.MaxFailedLogins, "5", "Security", "int", "Failures before lockout"),
            (AppSettingKeys.Security.LockoutMinutes, "15", "Security", "int", "Lockout duration"),
            (AppSettingKeys.Security.PasswordMinLength, "8", "Security", "int", "Minimum password length"),
            (AppSettingKeys.Security.OtpTtlMinutes, "10", "Security", "int", "OTP validity window"),
            (AppSettingKeys.Security.OtpMaxAttempts, "5", "Security", "int", "OTP verification attempts"),
            (AppSettingKeys.Attendance.AllowMultipleCheckInPerDay, "false", "Attendance", "bool", "Allow multiple check-ins per day"),
            (AppSettingKeys.Attendance.AutoCheckoutHour, "23", "Attendance", "int", "Hour (IST) when open attendance auto-closes")
        ];

    // ── Masters ─────────────────────────────────────────────────────────────
    internal static readonly (string Code, string Name, string Icon, int Order, bool RequiresCapacity)[]
        WorkspaceTypes =
        [
            ("DESK", "Desk", "Armchair", 1, false),
            ("CABIN", "Cabin", "DoorClosed", 2, true),
            ("MEETING_ROOM", "Meeting Room", "Presentation", 3, true)
        ];

    /// <summary>Ticket categories per DATABASE_DESIGN. Doc-1 lists concrete issue
    /// types only for "Booking Related"; other categories get a General type so the
    /// raise-ticket flow works — Regalis Ops can refine them (they are data).</summary>
    internal static readonly (string Category, string[] IssueTypes)[] TicketCategories =
    [
        ("Booking Related",
        [
            "Password Reset / Login Issue", "Billing / Invoice Issue", "Booking Cancellation",
            "Booking Modification", "Booking Confirmation Not Received", "Booking Payment Failed",
            "Booking Extension Request", "Other Booking Issues"
        ]),
        ("Building Management", ["General"]),
        ("Electricity", ["General"]),
        ("Food & Beverage (F&B)", ["General"]),
        ("Housekeeping & Pantry", ["General"]),
        ("Infrastructure & Design Issue", ["General"]),
        ("Meeting Room", ["General"]),
        ("Parking", ["General"]),
        ("Procurement", ["General"]),
        ("Product / Technical Support", ["General"]),
        ("Project Delivery", ["General"]),
        ("Sales Related", ["General"])
    ];

    // (Key, Prefix, FinancialYear) — customer-visible numbers, gap-free.
    internal static readonly (string Key, string Prefix, string? Fy)[] NumberSequences =
    [
        ("Booking", "REG-", null),
        ("Invoice", "REG/INV", "FY"), // FY placeholder resolved at seed time
        ("Payment", "PAY-", null),
        ("WalletTransaction", "WTX-", null),
        ("CustomerPass", "PASS-", null),
        ("ServiceOrder", "ORD-", null),
        ("Ticket", "TKT-", null),
        ("HousekeepingSchedule", "HKS-", null),
        ("InventoryTransaction", "ITX-", null)
    ];
}
