/* ---------------------------------------------------------------------------
   Rename the admin sidebar's "Housekeeping" item to "Maintenance".

   One row: MenuId 39, MenuCode ADM_HOUSEKEEPING, PortalType 2, DisplayOrder
   390. Both label columns carry "Housekeeping" and both are updated -- the
   admin dashboard's module tiles render DisplayName (AdminDashboardPage reads
   s.displayName), so leaving MenuName behind would rename the sidebar and not
   the tile.

   Deliberately NOT changed:

     MenuCode  ADM_HOUSEKEEPING      a stable identifier; other data and the
                                     seed catalogue match on it.
     Route     /admin/housekeeping   the SPA route is compiled into
                                     index-DI5X4c9f.js. Changing it here
                                     without rebuilding the bundle would give
                                     the item a dead link.
     Icon      Sparkles              still resolves in the frontend icon map.
     PermissionId / HOUSEKEEPING.*   permission codes are internal and guard
                                     the API, not the label.

   Also left alone, because they are different concepts that merely share the
   word, and renaming them would misdescribe the data:

     - the calendar block reason 6 = "Housekeeping" (sits beside "Cleaning"
       and "Repair" in AdminCalendarPage)
     - notification type 12 = "Housekeeping"
     - QR/access scan reason 4 = "Housekeeping"

   Not covered by this script: the admin dashboard's "Housekeeping today" stat
   card. That label is built server-side and arrives in the reports payload
   (reports.admin() -> cards[].label), so it needs an API change, not a data
   one.

   If Database__SeedOnStartup is ever turned back on, check whether
   SeedCatalogue re-asserts MenuName for ADM_HOUSEKEEPING before trusting this
   to stick. UAT currently runs with it false (api/web.config).

   Idempotent -- re-running changes nothing.
   Rollback: 20260916010000_RenameHousekeepingMenuToMaintenance_Rollback.sql
   --------------------------------------------------------------------------- */
SET NOCOUNT ON;
SET XACT_ABORT ON;

DECLARE @Now datetime2 = SYSUTCDATETIME();
DECLARE @By  bigint    = (SELECT TOP 1 UserId FROM MST_User WHERE Email = 'admin@regalis.in');

UPDATE MST_Menu
SET    MenuName     = 'Maintenance',
       DisplayName  = 'Maintenance',
       ModifiedBy   = @By,
       ModifiedDate = @Now
WHERE  MenuCode  = 'ADM_HOUSEKEEPING'
  AND  IsDeleted = 0
  AND  (MenuName <> 'Maintenance' OR DisplayName <> 'Maintenance');

/* Verification -- the renamed row, and anything still labelled Housekeeping. */
SELECT MenuId, MenuCode, MenuName, DisplayName, Route, Icon, PortalType, DisplayOrder
FROM   MST_Menu
WHERE  MenuCode = 'ADM_HOUSEKEEPING';

SELECT MenuId, MenuCode, MenuName, DisplayName
FROM   MST_Menu
WHERE  IsDeleted = 0
  AND  (MenuName LIKE '%ousekeep%' OR DisplayName LIKE '%ousekeep%');
