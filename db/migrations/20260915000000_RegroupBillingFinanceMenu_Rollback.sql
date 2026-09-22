/* ---------------------------------------------------------------------------
   Rollback for 20260915000000_RegroupBillingFinanceMenu.sql

   Restores the sidebar to the flat finance layout: "Billing & Finance" goes
   back under Inventory with no route, the nine children return to their
   original /admin/* routes and display orders, and the three retired rows
   (54 Dashboard, 59 Bank Statement, 62 Reconciliation) are un-deleted.

   Run this ONLY together with reverting the frontend patch -- on its own it
   leaves the sidebar pointing at /admin/payments while the bundle serves
   /admin/billing/payments. The old routes still redirect, so the app stays
   usable either way, but the pairing is what was tested.

   Idempotent -- re-running changes nothing.
   --------------------------------------------------------------------------- */
SET NOCOUNT ON;
SET XACT_ABORT ON;

BEGIN TRANSACTION;

DECLARE @Now datetime2 = SYSUTCDATETIME();
DECLARE @By  bigint    = (SELECT TOP 1 UserId FROM MST_User WHERE Email = 'admin@regalis.in');

/* 1. Billing & Finance back under Inventory, routeless. */
UPDATE MST_Menu
SET    ParentMenuId = 36,
       Route        = NULL,
       Icon         = 'IndianRupee',
       DisplayOrder = 300,
       IsActive     = 1,
       ModifiedBy   = @By,
       ModifiedDate = @Now
WHERE  MenuId = 30;

/* 2. Children back to their original parents, routes, icons and ordering. */
;WITH Prev(MenuId, ParentMenuId, MenuName, Route, Icon, DisplayOrder) AS (
    SELECT 31, 30, 'Payments',        '/admin/payments',        'CreditCard',      310
    UNION ALL SELECT 32, 30, 'Invoices',        '/admin/invoices',        'ReceiptText',     320
    UNION ALL SELECT 54, 30, 'Dashboard',       '/admin/finance',         'LayoutDashboard', 320
    UNION ALL SELECT 33, 30, 'Wallet',          '/admin/wallet',          'Wallet',          330
    UNION ALL SELECT 55, 30, 'Quotations',      '/admin/quotations',      'FileSignature',   340
    UNION ALL SELECT 56, 30, 'Expenses',        '/admin/expenses',        'Banknote',        370
    UNION ALL SELECT 57, 30, 'Purchase Orders', '/admin/purchase-orders', 'FileText',        380
    UNION ALL SELECT 58, 30, 'Suppliers',       '/admin/suppliers',       'Factory',         390
    UNION ALL SELECT 59, 30, 'Bank Statement',  NULL,                     'Landmark',        400
    UNION ALL SELECT 60, 59, 'Bank Accounts',   '/admin/bank/accounts',   'Landmark',        410
    UNION ALL SELECT 61, 59, 'Transactions',    '/admin/bank/transactions','ArrowLeftRight', 420
    UNION ALL SELECT 62, 59, 'Reconciliation',  '/admin/bank/reconciliation','ListChecks',   430
)
UPDATE m
SET    m.ParentMenuId = p.ParentMenuId,
       m.MenuName     = p.MenuName,
       m.Route        = p.Route,
       m.Icon         = p.Icon,
       m.DisplayOrder = p.DisplayOrder,
       m.IsActive     = 1,
       m.IsDeleted    = 0,
       m.DeletedBy    = NULL,
       m.DeletedDate  = NULL,
       m.ModifiedBy   = @By,
       m.ModifiedDate = @Now
FROM   MST_Menu AS m
JOIN   Prev     AS p ON p.MenuId = m.MenuId;

COMMIT TRANSACTION;

/* Verification -- the tree as it stood before the regrouping. */
SELECT MenuId, ParentMenuId, MenuName, Route, Icon, DisplayOrder, IsActive, IsDeleted
FROM   MST_Menu
WHERE  MenuId IN (30, 31, 32, 33, 54, 55, 56, 57, 58, 59, 60, 61, 62)
ORDER  BY ISNULL(ParentMenuId, MenuId), DisplayOrder, MenuId;
