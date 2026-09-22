/* ---------------------------------------------------------------------------
   Add the admin sidebar entry for the new Staff screen (/admin/staff).

   WHY A MIGRATION AND NOT SeedCatalogue
   -------------------------------------
   The brief says to add this to SeedCatalogue. This deployment ships api/ as
   compiled DLLs with no C# source, so the seeder cannot be edited. UAT also
   runs with Database__SeedOnStartup=false (api/web.config), so the seeder would
   not insert it on this host anyway. A data insert is the supported mechanism
   -- adding a screen has always been a data change, not a redeploy.

   If the API source is ever restored, add the same row to SeedCatalogue so a
   fresh database gets it too. Seeding is idempotent and will not duplicate
   this row, because it matches on MenuCode.

   THE ROW
   -------
   Modelled on its sibling ADM_ADMIN_USERS (MenuId 47), which is the row that
   actually renders today:

     ParentMenuId  46          ADM_ADMINISTRATION
     PermissionId  2           USER.VIEW -- the same code the endpoints require
     PortalType    2           Admin
     MenuLevel     2           matches the sibling
     DisplayOrder  471         immediately after Users (470)
     Icon          UsersRound  resolves in the SPA icon map; unknown names fall
                               back to LayoutGrid, so a typo degrades silently

   Note on MenuLevel: the brief specifies MenuLevel Screen (4). Every rendering
   sibling under ADM_ADMINISTRATION uses 2, and the sidebar builds from that, so
   this row follows the sibling. Change both together if the convention moves.

   MenuId and RoleMenuId are IDENTITY columns -- neither is supplied here.

   ROLE GRANTS
   -----------
   MAP_RoleMenu mirrors menu 47 exactly: RoleId 2 (ADMIN) only. Deliberately not
   granted to RoleId 1 (CUSTOMER) -- see the note in
   20260920020000_RevokeCustomerAdminPermissions.sql about that role currently
   holding every permission.

   Idempotent -- re-running inserts nothing.
   Rollback: 20260920010000_AddStaffScreenMenu_Rollback.sql
   --------------------------------------------------------------------------- */
SET NOCOUNT ON;
SET XACT_ABORT ON;

DECLARE @Now datetime2 = SYSUTCDATETIME();
DECLARE @By  bigint    = (SELECT TOP 1 UserId FROM MST_User WHERE Email = 'admin@regalis.in');

/* --- the menu row -------------------------------------------------------- */
IF NOT EXISTS (SELECT 1 FROM MST_Menu WHERE MenuCode = 'ADM_ADMIN_STAFF')
BEGIN
    INSERT INTO MST_Menu
        (ParentMenuId, MenuCode, MenuName, DisplayName, Route, Icon,
         DisplayOrder, PermissionId, PortalType, MenuLevel, IsVisible,
         CreatedBy, CreatedDate, IsActive, IsDeleted)
    VALUES
        (46, 'ADM_ADMIN_STAFF', 'Staff', 'Staff', '/admin/staff', 'UsersRound',
         471, 2, 2, 2, 1,
         @By, @Now, 1, 0);
END
ELSE
BEGIN
    /* Revive a previously rolled-back row rather than inserting a duplicate. */
    UPDATE MST_Menu
    SET    IsDeleted    = 0,
           DeletedBy    = NULL,
           DeletedDate  = NULL,
           IsActive     = 1,
           IsVisible    = 1,
           ModifiedBy   = @By,
           ModifiedDate = @Now
    WHERE  MenuCode = 'ADM_ADMIN_STAFF'
      AND  (IsDeleted = 1 OR IsActive = 0 OR IsVisible = 0);
END

/* --- role grants, mirroring ADM_ADMIN_USERS ------------------------------ */
DECLARE @StaffMenuId int = (SELECT MenuId FROM MST_Menu WHERE MenuCode = 'ADM_ADMIN_STAFF');

INSERT INTO MAP_RoleMenu (RoleId, MenuId)
SELECT rm.RoleId, @StaffMenuId
FROM   MAP_RoleMenu rm
WHERE  rm.MenuId = (SELECT MenuId FROM MST_Menu WHERE MenuCode = 'ADM_ADMIN_USERS')
  AND  NOT EXISTS (SELECT 1 FROM MAP_RoleMenu x
                   WHERE x.RoleId = rm.RoleId AND x.MenuId = @StaffMenuId);

/* --- verification -------------------------------------------------------- */
SELECT MenuId, ParentMenuId, MenuCode, MenuName, Route, Icon, DisplayOrder,
       PermissionId, PortalType, MenuLevel, IsVisible, IsActive, IsDeleted
FROM   MST_Menu
WHERE  MenuCode IN ('ADM_ADMIN_USERS', 'ADM_ADMIN_STAFF')
ORDER  BY DisplayOrder;

SELECT m.MenuCode, r.RoleId, r.RoleCode
FROM   MAP_RoleMenu rm
JOIN   MST_Menu m ON m.MenuId = rm.MenuId
JOIN   MST_Role r ON r.RoleId = rm.RoleId
WHERE  m.MenuCode IN ('ADM_ADMIN_USERS', 'ADM_ADMIN_STAFF')
ORDER  BY m.MenuCode, r.RoleId;
