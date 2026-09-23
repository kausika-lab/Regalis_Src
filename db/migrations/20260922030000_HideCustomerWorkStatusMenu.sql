/* 20260922030000_HideCustomerWorkStatusMenu.sql
   Takes "My Work Status" out of the customer sidebar.

   The sidebar is data-driven: GET /api/menus/my returns the MST_Menu rows mapped to the
   caller's role, so hiding the module needs this row gone as well as the SPA route. The
   route is commented out in frontend/assets/index-DI5X4c9f.js ("work-status module
   hidden"); both halves are required. The row alone would leave /work-status reachable
   by typing it, the route alone would leave a sidebar link leading nowhere.

   IsVisible = 0 rather than IsDeleted = 1, deliberately:
     - it is the column that exists for exactly this, and MenuLevel/ParentMenuId stay
       intact so nothing else in the tree shifts;
     - MAP_RoleMenu is left alone, so the grant is still on file and restoring the module
       is one UPDATE rather than a re-grant;
     - the startup seeder reconciles MST_Menu against SeedCatalogue.cs and resurrects
       soft-deleted seed rows as duplicates. UAT has Database__SeedOnStartup=false so it
       does not run today, but IsVisible survives a reconcile where IsDeleted may not.

   This touches ONLY the customer module (CUST_WORKSTATUS, /work-status). The admin
   Attendance screens — ADM_ATTENDANCE and ADM_ATTENDANCE_PAYROLL under /admin/attendance
   — are a different module and are left exactly as they are.

   Nothing is deleted: TRN_Attendance and the attendance API are untouched, so the data
   keeps accruing and the admin side still reports on it.

   Idempotent. Rollback: 20260922030000_HideCustomerWorkStatusMenu_Rollback.sql */

SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO

SET NOCOUNT ON;
SET XACT_ABORT ON;

IF NOT EXISTS (SELECT 1 FROM MST_Menu WHERE MenuCode = N'CUST_WORKSTATUS')
BEGIN
    PRINT 'HideCustomerWorkStatusMenu: no CUST_WORKSTATUS row, nothing to do.';
    RETURN;
END

IF EXISTS (SELECT 1 FROM MST_Menu WHERE MenuCode = N'CUST_WORKSTATUS' AND IsVisible = 0)
BEGIN
    PRINT 'HideCustomerWorkStatusMenu: already applied.';
    RETURN;
END

DECLARE @Admin bigint = (SELECT TOP 1 UserId FROM MST_User WHERE Email = N'admin@regalis.in' AND IsDeleted = 0);
IF @Admin IS NULL SET @Admin = (SELECT TOP 1 UserId FROM MST_User WHERE UserType = 2 AND IsDeleted = 0 ORDER BY UserId);

BEGIN TRAN;

UPDATE MST_Menu
SET IsVisible = 0,
    ModifiedBy = @Admin,
    ModifiedDate = SYSUTCDATETIME()
WHERE MenuCode = N'CUST_WORKSTATUS';

COMMIT;

SELECT Hidden = MenuCode, DisplayName, Route, IsVisible, IsActive, IsDeleted
FROM MST_Menu WHERE MenuCode = N'CUST_WORKSTATUS';

SELECT StillInTheCustomerSidebar = m.DisplayName, m.Route
FROM MST_Menu m
JOIN MAP_RoleMenu rm ON rm.MenuId = m.MenuId
JOIN MST_Role r ON r.RoleId = rm.RoleId
WHERE r.RoleCode = 'CUSTOMER' AND m.IsVisible = 1 AND m.IsActive = 1 AND m.IsDeleted = 0
ORDER BY m.DisplayOrder;

PRINT 'HideCustomerWorkStatusMenu: applied.';
