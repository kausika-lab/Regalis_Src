namespace Regalis.Shared.Constants;

/// <summary>
/// The seeded permission catalogue (ROLE_PERMISSION_MATRIX §2). Format: <c>MODULE.ACTION</c>.
/// These constants exist so controllers reference a compile-checked name; the catalogue
/// itself lives in <c>MST_Permission</c> and is resolved dynamically at login.
/// </summary>
public static class PermissionCodes
{
    public static class Dashboard
    {
        public const string View = "DASHBOARD.VIEW";
    }

    public static class User
    {
        public const string View = "USER.VIEW";
        public const string Create = "USER.CREATE";
        public const string Edit = "USER.EDIT";
        public const string Delete = "USER.DELETE";
        public const string Export = "USER.EXPORT";
    }

    public static class Role
    {
        public const string View = "ROLE.VIEW";
        public const string Create = "ROLE.CREATE";
        public const string Edit = "ROLE.EDIT";
        public const string Delete = "ROLE.DELETE";
    }

    public static class Permission
    {
        public const string View = "PERMISSION.VIEW";
        public const string Edit = "PERMISSION.EDIT";
    }

    public static class Menu
    {
        public const string View = "MENU.VIEW";
        public const string Create = "MENU.CREATE";
        public const string Edit = "MENU.EDIT";
        public const string Delete = "MENU.DELETE";
    }

    public static class Customer
    {
        public const string View = "CUSTOMER.VIEW";
        public const string Create = "CUSTOMER.CREATE";
        public const string Edit = "CUSTOMER.EDIT";
        public const string Delete = "CUSTOMER.DELETE";
        public const string Export = "CUSTOMER.EXPORT";
    }

    public static class Location
    {
        public const string View = "LOCATION.VIEW";
        public const string Create = "LOCATION.CREATE";
        public const string Edit = "LOCATION.EDIT";
        public const string Delete = "LOCATION.DELETE";
    }

    public static class Workspace
    {
        public const string View = "WORKSPACE.VIEW";
        public const string Create = "WORKSPACE.CREATE";
        public const string Edit = "WORKSPACE.EDIT";
        public const string Delete = "WORKSPACE.DELETE";
    }

    public static class Calendar
    {
        public const string View = "CALENDAR.VIEW";
        public const string Create = "CALENDAR.CREATE";
        public const string Edit = "CALENDAR.EDIT";
    }

    public static class Booking
    {
        public const string View = "BOOKING.VIEW";
        public const string Create = "BOOKING.CREATE";
        public const string Edit = "BOOKING.EDIT";
        public const string Delete = "BOOKING.DELETE";
        public const string Approve = "BOOKING.APPROVE";
        public const string Export = "BOOKING.EXPORT";
        public const string Print = "BOOKING.PRINT";
    }

    public static class Payment
    {
        public const string View = "PAYMENT.VIEW";
        public const string Create = "PAYMENT.CREATE";
        public const string Export = "PAYMENT.EXPORT";
        public const string CashCollect = "PAYMENT.CASH_COLLECT";
        public const string CashClose = "PAYMENT.CASH_CLOSE";
        public const string CashVoid = "PAYMENT.CASH_VOID";
        public const string CashApprove = "PAYMENT.CASH_APPROVE";
    }

    public static class Wallet
    {
        public const string View = "WALLET.VIEW";
        public const string Create = "WALLET.CREATE";
        public const string Edit = "WALLET.EDIT";
        public const string Export = "WALLET.EXPORT";
    }

    public static class Invoice
    {
        public const string View = "INVOICE.VIEW";
        public const string Create = "INVOICE.CREATE";
        public const string Edit = "INVOICE.EDIT";
        public const string Export = "INVOICE.EXPORT";
        public const string Print = "INVOICE.PRINT";
    }

    public static class Pass
    {
        public const string View = "PASS.VIEW";
        public const string Create = "PASS.CREATE";
        public const string Edit = "PASS.EDIT";
        public const string Delete = "PASS.DELETE";
        public const string Export = "PASS.EXPORT";
    }

    public static class Access
    {
        public const string View = "ACCESS.VIEW";
        public const string Create = "ACCESS.CREATE";
        public const string Edit = "ACCESS.EDIT";
        /// <summary>The device-down manual fallback — a distinct, separately grantable,
        /// separately audited privilege (Design Invariant 9).</summary>
        public const string Override = "ACCESS.OVERRIDE";
        public const string Export = "ACCESS.EXPORT";
    }

    public static class Attendance
    {
        public const string View = "ATTENDANCE.VIEW";
        public const string Create = "ATTENDANCE.CREATE";
        public const string Edit = "ATTENDANCE.EDIT";
        public const string Approve = "ATTENDANCE.APPROVE";
        public const string Export = "ATTENDANCE.EXPORT";
    }

    public static class Payroll
    {
        public const string View = "PAYROLL.VIEW";
        public const string Export = "PAYROLL.EXPORT";
    }

    public static class Inventory
    {
        public const string View = "INVENTORY.VIEW";
        public const string Create = "INVENTORY.CREATE";
        public const string Edit = "INVENTORY.EDIT";
        public const string Delete = "INVENTORY.DELETE";
        public const string Export = "INVENTORY.EXPORT";

        // Assets spec §13. VIEW is assets.view; CREATE and EDIT together are assets.manage.
        /// <summary>Record and post Incoming Goods.</summary>
        public const string Receive = "INVENTORY.RECEIVE";
        /// <summary>Record and post Outgoing Goods, including moves and repairs.</summary>
        public const string Issue = "INVENTORY.ISSUE";
        /// <summary>Print QR labels.</summary>
        public const string Labels = "INVENTORY.LABELS";
        /// <summary>Start and confirm a stock count.</summary>
        public const string Count = "INVENTORY.COUNT";
        /// <summary>Reverse a posted receipt or issue — it changes history.</summary>
        public const string Reverse = "INVENTORY.REVERSE";
    }

    public static class Supplier
    {
        public const string View = "SUPPLIER.VIEW";
        public const string Create = "SUPPLIER.CREATE";
        public const string Edit = "SUPPLIER.EDIT";
        public const string Delete = "SUPPLIER.DELETE";
    }

    public static class Purchase
    {
        public const string View = "PURCHASE.VIEW";
        public const string Create = "PURCHASE.CREATE";
        public const string Edit = "PURCHASE.EDIT";
        public const string Delete = "PURCHASE.DELETE";
        public const string Approve = "PURCHASE.APPROVE";
        public const string Export = "PURCHASE.EXPORT";
    }

    public static class Expense
    {
        public const string View = "EXPENSE.VIEW";
        public const string Create = "EXPENSE.CREATE";
        public const string Edit = "EXPENSE.EDIT";
        public const string Delete = "EXPENSE.DELETE";
        public const string Export = "EXPENSE.EXPORT";
    }

    public static class Quotation
    {
        public const string View = "QUOTATION.VIEW";
        public const string Create = "QUOTATION.CREATE";
        public const string Edit = "QUOTATION.EDIT";
        public const string Delete = "QUOTATION.DELETE";
        /// <summary>Held by the customer: this is the accept/reject decision.</summary>
        public const string Approve = "QUOTATION.APPROVE";
        public const string Export = "QUOTATION.EXPORT";
        public const string Print = "QUOTATION.PRINT";
    }

    public static class Bank
    {
        public const string View = "BANK.VIEW";
        public const string Create = "BANK.CREATE";
        public const string Edit = "BANK.EDIT";
        public const string Delete = "BANK.DELETE";
        /// <summary>Confirming a match -- the reconciliation sign-off.</summary>
        public const string Reconcile = "BANK.RECONCILE";
        public const string Export = "BANK.EXPORT";
    }

    public static class Housekeeping
    {
        public const string View = "HOUSEKEEPING.VIEW";
        public const string Create = "HOUSEKEEPING.CREATE";
        public const string Edit = "HOUSEKEEPING.EDIT";
        public const string Delete = "HOUSEKEEPING.DELETE";
        public const string Approve = "HOUSEKEEPING.APPROVE";
    }

    public static class Service
    {
        public const string View = "SERVICE.VIEW";
        public const string Create = "SERVICE.CREATE";
        public const string Edit = "SERVICE.EDIT";
        public const string Delete = "SERVICE.DELETE";
    }

    public static class Banner
    {
        public const string View = "BANNER.VIEW";
        public const string Create = "BANNER.CREATE";
        public const string Edit = "BANNER.EDIT";
        public const string Delete = "BANNER.DELETE";
    }

    public static class Notification
    {
        public const string View = "NOTIFICATION.VIEW";
        public const string Create = "NOTIFICATION.CREATE";
        public const string Edit = "NOTIFICATION.EDIT";
        public const string Delete = "NOTIFICATION.DELETE";
    }

    public static class Ticket
    {
        public const string View = "TICKET.VIEW";
        public const string Create = "TICKET.CREATE";
        public const string Edit = "TICKET.EDIT";
        public const string Delete = "TICKET.DELETE";
        public const string Approve = "TICKET.APPROVE";
        public const string Export = "TICKET.EXPORT";
    }

    public static class Report
    {
        public const string View = "REPORT.VIEW";
        public const string Export = "REPORT.EXPORT";
        public const string Print = "REPORT.PRINT";
        /// <summary>The Revenue &amp; tax group, the money figures on the overview and
        /// Money owed (Reports &amp; Analysis spec §13).</summary>
        public const string Finance = "REPORT.FINANCE";
        /// <summary>The Customers group: names, phone numbers and email addresses.</summary>
        public const string Customers = "REPORT.CUSTOMERS";
        /// <summary>Edit or stop any schedule, including another admin's.</summary>
        public const string Manage = "REPORT.MANAGE";
    }

    public static class Setting
    {
        public const string View = "SETTING.VIEW";
        public const string Edit = "SETTING.EDIT";
    }

    public static class Audit
    {
        public const string View = "AUDIT.VIEW";
        public const string Export = "AUDIT.EXPORT";
    }
}
