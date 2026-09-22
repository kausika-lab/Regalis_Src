/* ---------------------------------------------------------------------------
   Rollback for 20260920010000_AddStaffScreenMenu.sql.

   Removes the Staff sidebar entry. The /admin/staff route stays compiled into
   the SPA bundle, so the screen remains reachable by typing the URL -- it is
   the sidebar item that disappears. Removing the route needs a bundle change,
   not a data one.

   The menu row is SOFT-deleted, matching how the application treats
   soft-deletable rows everywhere else: the save-changes interceptor converts
   hard deletes into soft deletes, and the sidebar query filters IsDeleted = 0.
   Re-running the forward script revives this row rather than inserting a
   duplicate.

   The MAP_RoleMenu grants are hard-deleted -- that table is a pure mapping with
   no soft-delete columns (RoleMenuId, RoleId, MenuId only).

   Idempotent -- safe to re-run, and safe if the migration was never applied.
   --------------------------------------------------------------------------- */
SET NOCOUNT ON;
SET XACT_ABORT ON;

DECLARE @Now  datetime2 = SYSUTCDATETIME();
DECLARE @By   bigint    = (SELECT TOP 1 UserId FROM MST_User WHERE Email = 'admin@regalis.in');
DECLARE @Menu int       = (SELECT MenuId FROM MST_Menu WHERE MenuCode = 'ADM_ADMIN_STAFF');

IF @Menu IS NOT NULL
BEGIN
    DELETE FROM MAP_RoleMenu WHERE MenuId = @Menu;

    UPDATE MST_Menu
    SET    IsDeleted   = 1,
           IsActive    = 0,
           IsVisible   = 0,
           DeletedBy   = @By,
           DeletedDate = @Now
    WHERE  MenuId = @Menu
      AND  IsDeleted = 0;
END

/* --- verification -------------------------------------------------------- */
SELECT MenuId, MenuCode, MenuName, Route, IsVisible, IsActive, IsDeleted
FROM   MST_Menu
WHERE  MenuCode = 'ADM_ADMIN_STAFF';

SELECT COUNT(*) AS RemainingStaffRoleMenuRows
FROM   MAP_RoleMenu
WHERE  MenuId = @Menu;
