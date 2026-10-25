-- Reports & Analysis: permissions and sidebar (spec sections 2.1 and 13).
--
--   REPORT.FINANCE    the Revenue & tax group, the money figures on the overview, Money owed
--   REPORT.CUSTOMERS  the Customers group (names, phones, emails)
--   REPORT.MANAGE     edit or stop any schedule, including another admin's
--
-- Grants: ADMIN gets all three. FINANCE gets REPORT.FINANCE (finance = view, finance and
-- export). MANAGER and SUPPORT keep what they have (view + export). Who holds what is
-- open item 6 of the spec - change it in Roles & Permissions, no deploy needed.
--
-- Sidebar: five children under Analytics > Reports & Analysis, each with its own route so
-- exactly one highlights. Revenue goes to ADMIN + FINANCE and Customers to ADMIN only,
-- because the sidebar is filtered by role, not by permission.
--
-- Idempotent; one transaction. ASCII only (sqlcmd reads a BOM-less file as ANSI).
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO
SET XACT_ABORT ON;
BEGIN TRANSACTION;

DECLARE @now datetime2(3) = SYSUTCDATETIME();

-- 1. Permissions
DECLARE @perm TABLE (Code nvarchar(100), ActionName nvarchar(50), DisplayOrder int);
INSERT INTO @perm VALUES
    (N'REPORT.FINANCE', N'FINANCE', 1001),
    (N'REPORT.CUSTOMERS', N'CUSTOMERS', 1002),
    (N'REPORT.MANAGE', N'MANAGE', 1003);

INSERT INTO MST_Permission (PermissionCode, ModuleName, ActionName, Description, DisplayOrder, CreatedDate, IsActive, IsDeleted)
SELECT p.Code, N'REPORT', p.ActionName, p.ActionName + N' on REPORT', p.DisplayOrder, @now, 1, 0
FROM @perm p
WHERE NOT EXISTS (SELECT 1 FROM MST_Permission x WHERE x.PermissionCode = p.Code);

-- 2. Role grants
DECLARE @grant TABLE (RoleCode nvarchar(50), Code nvarchar(100));
INSERT INTO @grant VALUES
    (N'ADMIN', N'REPORT.FINANCE'), (N'ADMIN', N'REPORT.CUSTOMERS'), (N'ADMIN', N'REPORT.MANAGE'),
    (N'FINANCE', N'REPORT.FINANCE');

INSERT INTO MAP_RolePermission (RoleId, PermissionId)
SELECT r.RoleId, p.PermissionId
FROM @grant g
JOIN MST_Role r ON r.RoleCode = g.RoleCode
JOIN MST_Permission p ON p.PermissionCode = g.Code
WHERE NOT EXISTS (SELECT 1 FROM MAP_RolePermission x WHERE x.RoleId = r.RoleId AND x.PermissionId = p.PermissionId);

-- 3. Sidebar children under ADM_REPORTS
DECLARE @parent int = (SELECT MenuId FROM MST_Menu WHERE MenuCode = N'ADM_REPORTS' AND IsDeleted = 0);
IF @parent IS NULL
BEGIN
    RAISERROR('ADM_REPORTS menu not found - nothing changed.', 16, 1);
    ROLLBACK TRANSACTION;
    RETURN;
END;

DECLARE @menu TABLE (Code nvarchar(100), Display nvarchar(100), Route nvarchar(200), Icon nvarchar(50), Perm nvarchar(100), DisplayOrder int);
INSERT INTO @menu VALUES
    (N'ADM_REPORTS_OVERVIEW', N'Overview', N'/admin/reports/overview', N'LayoutDashboard', N'REPORT.VIEW', 10),
    (N'ADM_REPORTS_REVENUE', N'Revenue', N'/admin/reports/charts/revenue', N'IndianRupee', N'REPORT.FINANCE', 20),
    (N'ADM_REPORTS_OCCUPANCY', N'Occupancy', N'/admin/reports/charts/occupancy', N'Armchair', N'REPORT.VIEW', 30),
    (N'ADM_REPORTS_CUSTOMERS', N'Customers', N'/admin/reports/charts/customers', N'UsersRound', N'REPORT.CUSTOMERS', 40),
    (N'ADM_REPORTS_LIBRARY', N'Report library', N'/admin/reports/library', N'FileSpreadsheet', N'REPORT.VIEW', 50);

INSERT INTO MST_Menu (ParentMenuId, MenuCode, MenuName, DisplayName, Route, Icon, DisplayOrder, PermissionId, PortalType, MenuLevel, IsVisible, CreatedDate, IsActive, IsDeleted)
SELECT @parent, m.Code, m.Display, m.Display, m.Route, m.Icon, m.DisplayOrder,
       (SELECT PermissionId FROM MST_Permission WHERE PermissionCode = m.Perm), 2, 2, 1, @now, 1, 0
FROM @menu m
WHERE NOT EXISTS (SELECT 1 FROM MST_Menu x WHERE x.MenuCode = m.Code AND x.IsDeleted = 0);

-- Who sees each child: the roles that see Reports & Analysis today, narrowed for
-- Revenue (ADMIN, FINANCE) and Customers (ADMIN).
INSERT INTO MAP_RoleMenu (RoleId, MenuId)
SELECT rmp.RoleId, child.MenuId
FROM MAP_RoleMenu rmp
JOIN MST_Role r ON r.RoleId = rmp.RoleId
JOIN MST_Menu child ON child.ParentMenuId = @parent AND child.IsDeleted = 0
WHERE rmp.MenuId = @parent
  AND child.MenuCode IN (N'ADM_REPORTS_OVERVIEW', N'ADM_REPORTS_OCCUPANCY', N'ADM_REPORTS_LIBRARY')
  AND NOT EXISTS (SELECT 1 FROM MAP_RoleMenu x WHERE x.RoleId = rmp.RoleId AND x.MenuId = child.MenuId);

INSERT INTO MAP_RoleMenu (RoleId, MenuId)
SELECT r.RoleId, child.MenuId
FROM MST_Role r
JOIN MST_Menu child ON child.ParentMenuId = @parent AND child.IsDeleted = 0
WHERE ((child.MenuCode = N'ADM_REPORTS_REVENUE' AND r.RoleCode IN (N'ADMIN', N'FINANCE'))
    OR (child.MenuCode = N'ADM_REPORTS_CUSTOMERS' AND r.RoleCode = N'ADMIN'))
  AND NOT EXISTS (SELECT 1 FROM MAP_RoleMenu x WHERE x.RoleId = r.RoleId AND x.MenuId = child.MenuId);

COMMIT TRANSACTION;

SELECT p.PermissionCode, COUNT(rp.RoleId) AS Roles
FROM MST_Permission p LEFT JOIN MAP_RolePermission rp ON rp.PermissionId = p.PermissionId
WHERE p.PermissionCode LIKE N'REPORT.%' GROUP BY p.PermissionCode ORDER BY p.PermissionCode;
SELECT m.MenuCode, m.Route, COUNT(rm.RoleId) AS Roles
FROM MST_Menu m LEFT JOIN MAP_RoleMenu rm ON rm.MenuId = m.MenuId
WHERE m.MenuCode LIKE N'ADM_REPORTS%' AND m.IsDeleted = 0 GROUP BY m.MenuCode, m.Route ORDER BY m.MenuCode;
