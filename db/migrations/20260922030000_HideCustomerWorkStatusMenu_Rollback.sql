/* 20260922030000_HideCustomerWorkStatusMenu_Rollback.sql
   Puts "My Work Status" back in the customer sidebar.

   Loss-free — the row was only hidden, and MAP_RoleMenu was never touched.

   Uncomment the SPA route as well, or the restored menu item leads to a 404. In
   frontend/assets/index-DI5X4c9f.js there are two spans marked
   "work-status module hidden 2026-09-22": the WorkStatusPage lazy import and the
   /work-status route. Remove the surrounding comment markers from both, then bump that
   file's revision in frontend/sw.js so browsers pick the change up. */

SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO

SET NOCOUNT ON;
SET XACT_ABORT ON;

IF NOT EXISTS (SELECT 1 FROM MST_Menu WHERE MenuCode = N'CUST_WORKSTATUS' AND IsVisible = 0)
BEGIN
    PRINT 'HideCustomerWorkStatusMenu rollback: nothing to restore.';
    RETURN;
END

BEGIN TRAN;

UPDATE MST_Menu
SET IsVisible = 1,
    ModifiedDate = SYSUTCDATETIME()
WHERE MenuCode = N'CUST_WORKSTATUS';

COMMIT;

SELECT Restored = MenuCode, DisplayName, Route, IsVisible
FROM MST_Menu WHERE MenuCode = N'CUST_WORKSTATUS';

PRINT 'HideCustomerWorkStatusMenu rollback: restored. Now uncomment the SPA route.';
