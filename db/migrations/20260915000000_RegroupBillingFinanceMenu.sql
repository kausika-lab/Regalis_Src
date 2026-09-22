/* ---------------------------------------------------------------------------
   Regroup the admin sidebar's finance items under one collapsible parent.

   Before this script "Billing & Finance" (MenuId 30) was parented under
   Inventory (MenuId 36) and carried no route. The sidebar renderer flattens a
   routeless parent -- it drops the parent label and splices the children into
   the top level -- so all nine finance items appeared as separate flat entries
   sitting inside the Inventory block.

   After this script MenuId 30 is a top-level item WITH a route
   (/admin/billing, the new landing screen), and owns exactly nine children in
   the order the UI expects:

       Invoices, Payments, Wallet, Quotations, Expenses,
       Purchase Orders, Suppliers, Bank Accounts, Transactions

   Three rows are retired:
     54  "Dashboard" (/admin/finance) -- the parent's own route is now the
         landing screen, so a separate child for it is redundant.
     59  "Bank Statement" -- an intermediate wrapper; its two useful children
         are promoted to sit directly under Billing & Finance.
     62  "Reconciliation" -- leaves the sidebar by request. The ROUTE stays
         alive (/admin/billing/reconciliation) and the landing screen links to
         it, so the page is not orphaned.

   Icons are set to names the frontend icon map actually resolves. Names
   outside that map (FileSignature, Banknote, FileText, Factory, Landmark...)
   silently fall back to a generic glyph, which is why several finance rows
   looked identical before.

   MAP_RoleMenu is untouched: every row here already exists and keeps its
   mappings. Roles 2 (Administrator), 3 (Operations Manager) and 4 (Finance)
   already carry MenuId 30, so the parent shows for exactly the roles that
   could see the children before. A role with no finance child mapped still
   sees no finance items.

   NOTE: the startup seeder resurrects soft-deleted master rows as duplicates.
   UAT runs with Database__SeedOnStartup=false (api/web.config) so the three
   retirements below stick. Confirm that flag before running this anywhere
   else.

   Idempotent -- re-running changes nothing.
   Rollback: 20260915000000_RegroupBillingFinanceMenu_Rollback.sql
   --------------------------------------------------------------------------- */
SET NOCOUNT ON;
SET XACT_ABORT ON;

BEGIN TRANSACTION;

DECLARE @Now datetime2 = SYSUTCDATETIME();
DECLARE @By  bigint    = (SELECT TOP 1 UserId FROM MST_User WHERE Email = 'admin@regalis.in');

/* 1. Promote "Billing & Finance" to a top-level, routed, collapsible parent. */
UPDATE MST_Menu
SET    ParentMenuId = NULL,
       Route        = '/admin/billing',
       Icon         = 'IndianRupee',
       DisplayOrder = 355,          -- keeps the block where staff see it today
       IsActive     = 1,
       ModifiedBy   = @By,
       ModifiedDate = @Now
WHERE  MenuId = 30;

/* 2. Point the nine children at their new routes, in the required order. */
;WITH Target(MenuId, MenuName, Route, Icon, DisplayOrder) AS (
    SELECT 32, 'Invoices',        '/admin/billing/invoices',        'ReceiptText',    10
    UNION ALL SELECT 31, 'Payments',        '/admin/billing/payments',        'CreditCard',     20
    UNION ALL SELECT 33, 'Wallet',          '/admin/billing/wallet',          'Wallet',         30
    UNION ALL SELECT 55, 'Quotations',      '/admin/billing/quotations',      'FileSpreadsheet',40
    UNION ALL SELECT 56, 'Expenses',        '/admin/billing/expenses',        'Receipt',        50
    UNION ALL SELECT 57, 'Purchase Orders', '/admin/billing/purchase-orders', 'ShoppingCart',   60
    UNION ALL SELECT 58, 'Suppliers',       '/admin/billing/suppliers',       'Building2',      70
    UNION ALL SELECT 60, 'Bank Accounts',   '/admin/billing/bank-accounts',   'Landmark',       80
    UNION ALL SELECT 61, 'Transactions',    '/admin/billing/transactions',    'ArrowLeftRight', 90
)
UPDATE m
SET    m.ParentMenuId = 30,
       m.MenuName     = t.MenuName,
       m.Route        = t.Route,
       m.Icon         = t.Icon,
       m.DisplayOrder = t.DisplayOrder,
       m.IsActive     = 1,
       m.IsDeleted    = 0,
       m.ModifiedBy   = @By,
       m.ModifiedDate = @Now
FROM   MST_Menu AS m
JOIN   Target   AS t ON t.MenuId = m.MenuId;

/* 3. Retire the three rows that no longer belong in the tree. */
UPDATE MST_Menu
SET    IsActive    = 0,
       IsDeleted   = 1,
       DeletedBy   = @By,
       DeletedDate = @Now
WHERE  MenuId IN (54, 59, 62)
  AND  IsDeleted = 0;

/* 4. Safety net: nothing may still hang off the retired "Bank Statement"
      wrapper. Reconciliation (62) is retired above; anything else that shows
      up here would otherwise vanish from the sidebar silently. */
UPDATE MST_Menu
SET    ParentMenuId = 30,
       ModifiedBy   = @By,
       ModifiedDate = @Now
WHERE  ParentMenuId = 59
  AND  IsDeleted    = 0;

COMMIT TRANSACTION;

/* Verification -- the finance tree exactly as the sidebar will render it. */
SELECT p.MenuId       AS ParentId,
       p.MenuName     AS Parent,
       p.Route        AS ParentRoute,
       c.DisplayOrder AS Ord,
       c.MenuId       AS ChildId,
       c.MenuName     AS Child,
       c.Route        AS ChildRoute,
       c.Icon
FROM   MST_Menu AS p
LEFT   JOIN MST_Menu AS c ON c.ParentMenuId = p.MenuId AND c.IsDeleted = 0
WHERE  p.MenuId = 30
ORDER  BY c.DisplayOrder;

/* Verification -- no finance leftovers anywhere else in the tree. */
SELECT MenuId, ParentMenuId, MenuName, Route, IsActive, IsDeleted
FROM   MST_Menu
WHERE  MenuId IN (54, 59, 62)
    OR Route LIKE '/admin/bank%'
    OR Route = '/admin/finance'
ORDER  BY MenuId;
