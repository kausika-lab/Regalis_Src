/* ---------------------------------------------------------------------------
   Rollback: flatten the admin sidebar back to one top-level list.

   Puts every item back at the top level with the DisplayOrder it carried before
   the regroup, restores the four renamed labels and the three routes that were
   pointed at a first child, then soft-deletes the six section rows and drops
   their role grants.

   Pair this with reverting `iv` and RGgrouped in
   frontend/assets/index-DI5X4c9f.js, and the .portal__nav-section rules in
   frontend/assets/index-BgQ7mK4p.css. Data alone is safe to revert first: the
   patched renderer simply finds no routeless top-level rows to draw.

   Idempotent -- re-running changes nothing.
   Forward: 20260916020000_GroupAdminMenuIntoSections.sql
   --------------------------------------------------------------------------- */
SET NOCOUNT ON;
SET XACT_ABORT ON;

BEGIN TRANSACTION;

DECLARE @Now datetime2 = SYSUTCDATETIME();
DECLARE @By  bigint    = (SELECT TOP 1 UserId FROM MST_User WHERE Email = 'admin@regalis.in');

DECLARE @Prior TABLE (MenuId int, DisplayName nvarchar(200), Route nvarchar(300), DisplayOrder int);
INSERT INTO @Prior (MenuId, DisplayName, Route, DisplayOrder) VALUES
    (17, 'Dashboard',          '/admin',                 170),
    (18, 'Customers',          '/admin/customers',       180),
    (19, 'Workspaces',         '/admin/workspaces',      190),
    (23, 'Calendar',           '/admin/calendar',        230),
    (24, 'Bookings',           '/admin/bookings',        240),
    (25, 'Access Control',     NULL,                     250),
    (28, 'Attendance',         '/admin/attendance',      280),
    (34, 'Day Pass',           '/admin/passes/day',      340),
    (35, 'Monthly Pass',       '/admin/passes/monthly',  350),
    (30, 'Billing & Finance',  '/admin/billing',         355),
    (36, 'Inventory',          NULL,                     360),
    (39, 'Maintenance',        '/admin/housekeeping',    390),
    (40, 'Locations',          '/admin/locations',       400),
    (41, 'Services / Coffee',  '/admin/services',        410),
    (42, 'Banners',            '/admin/banners',         420),
    (43, 'Notifications',      '/admin/notifications',   430),
    (44, 'Support / Tickets',  '/admin/tickets',         440),
    (45, 'Reports & Analysis', '/admin/reports',         450),
    (46, 'Administration',     NULL,                     460),
    (51, 'Settings',           '/admin/settings',        510),
    (52, 'Admin Profile',      '/admin/profile',         520);

UPDATE m
SET    m.ParentMenuId = NULL,
       m.MenuName     = p.DisplayName,
       m.DisplayName  = p.DisplayName,
       m.Route        = p.Route,
       m.DisplayOrder = p.DisplayOrder,
       m.ModifiedBy   = @By,
       m.ModifiedDate = @Now
FROM   MST_Menu AS m
JOIN   @Prior   AS p ON p.MenuId = m.MenuId
WHERE  m.IsDeleted = 0;

DELETE rm
FROM   MAP_RoleMenu AS rm
JOIN   MST_Menu     AS m ON m.MenuId = rm.MenuId
WHERE  m.PortalType = 2 AND m.MenuCode LIKE 'ADM_SEC_%';

UPDATE MST_Menu
SET    IsActive    = 0,
       IsDeleted   = 1,
       DeletedBy   = @By,
       DeletedDate = @Now
WHERE  PortalType = 2 AND MenuCode LIKE 'ADM_SEC_%' AND IsDeleted = 0;

COMMIT TRANSACTION;

/* Verification -- the flat top-level list, as it was. */
SELECT MenuId, DisplayName, Route, DisplayOrder
FROM   MST_Menu
WHERE  PortalType = 2 AND IsDeleted = 0 AND ParentMenuId IS NULL
ORDER  BY DisplayOrder;
