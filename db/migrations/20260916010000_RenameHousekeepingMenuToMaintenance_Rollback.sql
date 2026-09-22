/* ---------------------------------------------------------------------------
   Rollback: put the admin sidebar item back to "Housekeeping".

   Pair this with reverting the PageHeader title in
   frontend/assets/AdminHousekeepingPage-BVEQUG24.js and bumping that chunk's
   revision in frontend/sw.js, or the menu and the page it opens will disagree.

   Idempotent -- re-running changes nothing.
   Forward: 20260916010000_RenameHousekeepingMenuToMaintenance.sql
   --------------------------------------------------------------------------- */
SET NOCOUNT ON;
SET XACT_ABORT ON;

DECLARE @Now datetime2 = SYSUTCDATETIME();
DECLARE @By  bigint    = (SELECT TOP 1 UserId FROM MST_User WHERE Email = 'admin@regalis.in');

UPDATE MST_Menu
SET    MenuName     = 'Housekeeping',
       DisplayName  = 'Housekeeping',
       ModifiedBy   = @By,
       ModifiedDate = @Now
WHERE  MenuCode  = 'ADM_HOUSEKEEPING'
  AND  IsDeleted = 0
  AND  (MenuName <> 'Housekeeping' OR DisplayName <> 'Housekeeping');

/* Verification */
SELECT MenuId, MenuCode, MenuName, DisplayName, Route, Icon, PortalType, DisplayOrder
FROM   MST_Menu
WHERE  MenuCode = 'ADM_HOUSEKEEPING';
