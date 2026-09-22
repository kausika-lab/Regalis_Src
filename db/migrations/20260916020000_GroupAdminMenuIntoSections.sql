/* ---------------------------------------------------------------------------
   Group the admin sidebar into sections
   (Overview / Operations / Finance / Engagement / Analytics / System)

   Six routeless rows are added at level 1 and every existing top-level item is
   reparented under one of them. Routeless parents used to be INVISIBLE -- the
   renderer dropped the label and spliced the children into the flat list -- so
   this script only works together with the matching change to `iv` in
   frontend/assets/index-DI5X4c9f.js, which renders a routeless top-level
   parent as a section heading. Applying one without the other gives either a
   flat list again (data only) or nothing new (code only).

   Three existing routeless parents -- Access Control, Inventory, Administration
   -- would themselves flatten inside their new section, so each is given the
   route of its own first child and added to RGgrouped in the same frontend
   change. They then render as one collapsible row, the way Billing & Finance
   already does, instead of spilling their children into the section.

   Renames, to match the requested layout:
       Day Pass        -> Passes
       Monthly Pass    -> Membership
       Inventory       -> Assets
       Access Control  -> Device / Access Control

   MenuLevel is deliberately NOT rewritten. The tree is built from
   ParentMenuId; the 20260915 billing regroup reparented nine rows without
   touching MenuLevel and the sidebar rendered correctly, so the column is
   left as the seed wrote it rather than invented here.

   Not created, because no screen exists behind them -- a menu row whose route
   has no page is a dead link:
       CRM     (no controller, no route, no page)

   Kept although the requested layout omits them, because deleting them would
   strand working screens:
       Services / Coffee -> Operations
       Banners           -> Engagement
       Administration    -> System   (Users, Roles & Permissions, Menu
                                      Management, Audit Trail live under it)

   Every section is granted to exactly the admin-portal roles that already hold
   at least one child inside it, so no role gains a screen and none sees an
   empty heading.

   Idempotent -- re-running changes nothing.
   Rollback: 20260916020000_GroupAdminMenuIntoSections_Rollback.sql
   --------------------------------------------------------------------------- */
SET NOCOUNT ON;
SET XACT_ABORT ON;

BEGIN TRANSACTION;

DECLARE @Now datetime2 = SYSUTCDATETIME();
DECLARE @By  bigint    = (SELECT TOP 1 UserId FROM MST_User WHERE Email = 'admin@regalis.in');

/* 1. The six section rows. Routeless and iconless on purpose: the renderer
      draws them as a label, never as a link. */
DECLARE @Sections TABLE (MenuCode nvarchar(100), DisplayName nvarchar(200), DisplayOrder int);
INSERT INTO @Sections (MenuCode, DisplayName, DisplayOrder) VALUES
    ('ADM_SEC_OVERVIEW',   'Overview',   100),
    ('ADM_SEC_OPERATIONS', 'Operations', 200),
    ('ADM_SEC_FINANCE',    'Finance',    300),
    ('ADM_SEC_ENGAGEMENT', 'Engagement', 400),
    ('ADM_SEC_ANALYTICS',  'Analytics',  500),
    ('ADM_SEC_SYSTEM',     'System',     600);

INSERT INTO MST_Menu (ParentMenuId, MenuCode, MenuName, DisplayName, Route, Icon,
                      DisplayOrder, PermissionId, PortalType, MenuLevel,
                      IsVisible, CreatedBy, CreatedDate, IsActive, IsDeleted)
SELECT NULL, s.MenuCode, s.DisplayName, s.DisplayName, NULL, NULL,
       s.DisplayOrder, NULL, 2, 1,
       1, @By, @Now, 1, 0
FROM   @Sections AS s
WHERE  NOT EXISTS (SELECT 1 FROM MST_Menu AS m
                   WHERE m.MenuCode = s.MenuCode AND m.PortalType = 2);

/* Revive a section that a previous rollback soft-deleted. */
UPDATE m
SET    m.IsDeleted = 0, m.IsActive = 1, m.IsVisible = 1,
       m.ParentMenuId = NULL, m.Route = NULL,
       m.DisplayOrder = s.DisplayOrder,
       m.ModifiedBy = @By, m.ModifiedDate = @Now
FROM   MST_Menu AS m
JOIN   @Sections AS s ON s.MenuCode = m.MenuCode
WHERE  m.PortalType = 2;

/* 2. Reparent every top-level item under its section, in the requested order.
      Route/Icon are only written where this script actually changes them. */
DECLARE @Plan TABLE (MenuId int, SectionCode nvarchar(100), DisplayName nvarchar(200),
                     Route nvarchar(300), DisplayOrder int);
INSERT INTO @Plan (MenuId, SectionCode, DisplayName, Route, DisplayOrder) VALUES
    (17, 'ADM_SEC_OVERVIEW',   'Dashboard',              '/admin',                   10),

    (18, 'ADM_SEC_OPERATIONS', 'Customers',              '/admin/customers',         10),
    (19, 'ADM_SEC_OPERATIONS', 'Workspaces',             '/admin/workspaces',        20),
    (23, 'ADM_SEC_OPERATIONS', 'Calendar',               '/admin/calendar',          30),
    (24, 'ADM_SEC_OPERATIONS', 'Bookings',               '/admin/bookings',          40),
    (28, 'ADM_SEC_OPERATIONS', 'Attendance',             '/admin/attendance',        50),
    (34, 'ADM_SEC_OPERATIONS', 'Passes',                 '/admin/passes/day',        60),
    (35, 'ADM_SEC_OPERATIONS', 'Membership',             '/admin/passes/monthly',    70),
    (39, 'ADM_SEC_OPERATIONS', 'Maintenance',            '/admin/housekeeping',      80),
    (36, 'ADM_SEC_OPERATIONS', 'Assets',                 '/admin/inventory/items',   90),
    (40, 'ADM_SEC_OPERATIONS', 'Locations',              '/admin/locations',        100),
    (41, 'ADM_SEC_OPERATIONS', 'Services / Coffee',      '/admin/services',         110),

    (30, 'ADM_SEC_FINANCE',    'Billing & Finance',      '/admin/billing',           10),

    (43, 'ADM_SEC_ENGAGEMENT', 'Notifications',          '/admin/notifications',     10),
    (44, 'ADM_SEC_ENGAGEMENT', 'Support / Tickets',      '/admin/tickets',           20),
    (42, 'ADM_SEC_ENGAGEMENT', 'Banners',                '/admin/banners',           30),

    (45, 'ADM_SEC_ANALYTICS',  'Reports & Analysis',     '/admin/reports',           10),

    (25, 'ADM_SEC_SYSTEM',     'Device / Access Control','/admin/access',            10),
    (46, 'ADM_SEC_SYSTEM',     'Administration',         '/admin/users',             20),
    (51, 'ADM_SEC_SYSTEM',     'Settings',               '/admin/settings',          30),
    (52, 'ADM_SEC_SYSTEM',     'Admin Profile',          '/admin/profile',           40);

UPDATE m
SET    m.ParentMenuId = sec.MenuId,
       m.MenuName     = p.DisplayName,
       m.DisplayName  = p.DisplayName,
       m.Route        = p.Route,
       m.DisplayOrder = p.DisplayOrder,
       m.ModifiedBy   = @By,
       m.ModifiedDate = @Now
FROM   MST_Menu AS m
JOIN   @Plan    AS p   ON p.MenuId = m.MenuId
JOIN   MST_Menu AS sec ON sec.MenuCode = p.SectionCode AND sec.PortalType = 2
WHERE  m.IsDeleted = 0;

/* 3. Grant each section to every admin-portal role that already holds at least
      one item inside it. A role with nothing in a section never sees it. */
INSERT INTO MAP_RoleMenu (RoleId, MenuId)
SELECT DISTINCT rm.RoleId, sec.MenuId
FROM   MAP_RoleMenu AS rm
JOIN   MST_Menu     AS child ON child.MenuId = rm.MenuId AND child.IsDeleted = 0
JOIN   MST_Menu     AS sec   ON sec.MenuId   = child.ParentMenuId
JOIN   @Sections    AS s     ON s.MenuCode   = sec.MenuCode
WHERE  NOT EXISTS (SELECT 1 FROM MAP_RoleMenu AS x
                   WHERE x.RoleId = rm.RoleId AND x.MenuId = sec.MenuId);

COMMIT TRANSACTION;

/* Verification -- the sidebar as each admin role will now see it. */
SELECT sec.DisplayOrder AS SecOrd, sec.DisplayName AS Section,
       m.DisplayOrder AS Ord, m.DisplayName AS Item, m.Route, m.Icon
FROM   MST_Menu AS m
JOIN   MST_Menu AS sec ON sec.MenuId = m.ParentMenuId
JOIN   @Sections AS s  ON s.MenuCode = sec.MenuCode
WHERE  m.IsDeleted = 0
ORDER  BY sec.DisplayOrder, m.DisplayOrder;

/* Anything still sitting at the top level that is not a section is a bug. */
SELECT MenuId, MenuCode, DisplayName, Route
FROM   MST_Menu
WHERE  PortalType = 2 AND IsDeleted = 0 AND ParentMenuId IS NULL
  AND  MenuCode NOT LIKE 'ADM_SEC_%';
