/* ---------------------------------------------------------------------------
   Add the admin sidebar entry for the Support & Contact screen
   (/admin/support-contact, AdminSupportContactPage-SC1.js).

   The screen edits the Support.* rows of MST_AppSetting through /api/settings,
   the same endpoints Admin -> Settings uses, so it needs no API change. A data
   insert is the supported way to add a screen (see 20260920010000_AddStaffScreenMenu.sql
   for why it is not a SeedCatalogue change).

   THE ROW
   -------
   Placed under Engagement, right after Support / Tickets (ADM_TICKETS, 20):

     ParentMenuId  ADM_SEC_ENGAGEMENT  looked up by code, not by id
     PermissionId  ADM_SETTINGS's      SETTING.VIEW -- what GET /api/settings needs;
                                       saving also needs SETTING.EDIT, which the
                                       screen checks before showing Save Changes
     PortalType    2                   Admin
     MenuLevel     1                   matches ADM_TICKETS
     DisplayOrder  25                  after Support / Tickets (20)
     Icon          Phone               resolves in the SPA icon map

   ROLE GRANTS
   -----------
   MAP_RoleMenu mirrors ADM_SETTINGS exactly, so whoever can open Settings sees
   this item too.

   Idempotent -- re-running inserts nothing.
   Rollback: 20261106120000_AddSupportContactMenu_Rollback.sql
   --------------------------------------------------------------------------- */
SET NOCOUNT ON;
SET XACT_ABORT ON;

DECLARE @Now      datetime2 = SYSUTCDATETIME();
DECLARE @By       bigint    = (SELECT TOP 1 UserId FROM MST_User WHERE Email = 'admin@regalis.in');
DECLARE @Parent   int       = (SELECT MenuId FROM MST_Menu WHERE MenuCode = 'ADM_SEC_ENGAGEMENT' AND IsDeleted = 0);
DECLARE @Settings int       = (SELECT MenuId FROM MST_Menu WHERE MenuCode = 'ADM_SETTINGS' AND IsDeleted = 0);
DECLARE @Perm     int       = (SELECT PermissionId FROM MST_Menu WHERE MenuId = @Settings);

IF @Parent IS NULL OR @Settings IS NULL OR @Perm IS NULL
BEGIN
    RAISERROR ('AddSupportContactMenu: ADM_SEC_ENGAGEMENT or ADM_SETTINGS not found - nothing changed.', 16, 1);
    RETURN;
END

/* --- the menu row -------------------------------------------------------- */
IF NOT EXISTS (SELECT 1 FROM MST_Menu WHERE MenuCode = 'ADM_SUPPORT_CONTACT')
BEGIN
    INSERT INTO MST_Menu
        (ParentMenuId, MenuCode, MenuName, DisplayName, Route, Icon,
         DisplayOrder, PermissionId, PortalType, MenuLevel, IsVisible,
         CreatedBy, CreatedDate, IsActive, IsDeleted)
    VALUES
        (@Parent, 'ADM_SUPPORT_CONTACT', 'Support & Contact', 'Support & Contact', '/admin/support-contact', 'Phone',
         25, @Perm, 2, 1, 1,
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
    WHERE  MenuCode = 'ADM_SUPPORT_CONTACT'
      AND  (IsDeleted = 1 OR IsActive = 0 OR IsVisible = 0);
END

/* --- role grants, mirroring ADM_SETTINGS --------------------------------- */
DECLARE @MenuId int = (SELECT MenuId FROM MST_Menu WHERE MenuCode = 'ADM_SUPPORT_CONTACT');

INSERT INTO MAP_RoleMenu (RoleId, MenuId)
SELECT rm.RoleId, @MenuId
FROM   MAP_RoleMenu rm
WHERE  rm.MenuId = @Settings
  AND  NOT EXISTS (SELECT 1 FROM MAP_RoleMenu x
                   WHERE x.RoleId = rm.RoleId AND x.MenuId = @MenuId);

/* --- verification -------------------------------------------------------- */
SELECT MenuId, ParentMenuId, MenuCode, DisplayName, Route, Icon, DisplayOrder,
       PermissionId, PortalType, MenuLevel, IsVisible, IsActive, IsDeleted
FROM   MST_Menu
WHERE  MenuCode IN ('ADM_TICKETS', 'ADM_SUPPORT_CONTACT')
ORDER  BY DisplayOrder;

SELECT m.MenuCode, r.RoleId, r.RoleCode
FROM   MAP_RoleMenu rm
JOIN   MST_Menu m ON m.MenuId = rm.MenuId
JOIN   MST_Role r ON r.RoleId = rm.RoleId
WHERE  m.MenuCode IN ('ADM_SETTINGS', 'ADM_SUPPORT_CONTACT')
ORDER  BY m.MenuCode, r.RoleId;
