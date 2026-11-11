/* ---------------------------------------------------------------------------
   Rollback for 20261106120000_AddSupportContactMenu.sql.

   Removes the Support & Contact sidebar entry. The /admin/support-contact route
   stays in the SPA bundle, so the screen is still reachable by URL for anyone
   with SETTING.VIEW; the Support group in Admin -> Settings is unaffected.

   The menu row is SOFT-deleted (the sidebar query filters IsDeleted = 0) and
   re-running the forward script revives it. MAP_RoleMenu grants are
   hard-deleted -- that table has no soft-delete columns.

   Idempotent -- safe to re-run, and safe if the migration was never applied.
   --------------------------------------------------------------------------- */
SET NOCOUNT ON;
SET XACT_ABORT ON;

DECLARE @Now  datetime2 = SYSUTCDATETIME();
DECLARE @By   bigint    = (SELECT TOP 1 UserId FROM MST_User WHERE Email = 'admin@regalis.in');
DECLARE @Menu int       = (SELECT MenuId FROM MST_Menu WHERE MenuCode = 'ADM_SUPPORT_CONTACT');

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
SELECT MenuId, MenuCode, DisplayName, Route, IsVisible, IsActive, IsDeleted
FROM   MST_Menu
WHERE  MenuCode = 'ADM_SUPPORT_CONTACT';

SELECT COUNT(*) AS RemainingRoleMenuRows
FROM   MAP_RoleMenu
WHERE  MenuId = @Menu;
