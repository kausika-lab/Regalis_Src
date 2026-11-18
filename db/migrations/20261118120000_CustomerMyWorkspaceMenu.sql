/* ---------------------------------------------------------------------------
   Customer sidebar: "My Workspace" pop-out (commit bfe39fa, "Sibar changes").

   The SPA (index-DI5X4c9f.js, RGwsMenu) draws a pop-out with My Bookings and
   My Passes for the menu row whose Route is '/my-workspace'. Without that row
   the code does nothing, which is why the pop-out showed on the local/UAT
   database but not on production. This adds the row.

   THE ROW
   -------
     MenuCode      CUST_MYWORKSPACE
     Route         /my-workspace       the SPA also redirects this path to /bookings
     Icon          Building2           resolves in the SPA icon map
     DisplayOrder  30                  where My Bookings sits today
     PermissionId  CUST_BOOKINGS's     BOOKING.VIEW
     PortalType    1                   Customer
     MenuLevel     1

   ROLE GRANTS
   -----------
   MAP_RoleMenu mirrors CUST_BOOKINGS, so whoever saw My Bookings sees this.

   THE TWO ITEMS NOW INSIDE THE POP-OUT
   ------------------------------------
   CUST_BOOKINGS (/bookings) and CUST_MYPASSES (/passes/my) are hidden from the
   sidebar (IsVisible = 0) so they are not listed twice. Their pages, routes and
   permissions are unchanged; the pop-out links to them.

   Idempotent -- re-running changes nothing.
   Rollback: 20261118120000_CustomerMyWorkspaceMenu_Rollback.sql
   --------------------------------------------------------------------------- */
SET NOCOUNT ON;
SET XACT_ABORT ON;

DECLARE @Now      datetime2 = SYSUTCDATETIME();
DECLARE @By       bigint    = (SELECT TOP 1 UserId FROM MST_User WHERE Email = 'admin@regalis.in');
DECLARE @Bookings int       = (SELECT MenuId FROM MST_Menu WHERE MenuCode = 'CUST_BOOKINGS' AND IsDeleted = 0);
DECLARE @Perm     int       = (SELECT PermissionId FROM MST_Menu WHERE MenuId = @Bookings);

IF @Bookings IS NULL OR @Perm IS NULL
BEGIN
    RAISERROR ('CustomerMyWorkspaceMenu: CUST_BOOKINGS not found - nothing changed.', 16, 1);
    RETURN;
END

BEGIN TRANSACTION;

/* --- the menu row -------------------------------------------------------- */
IF NOT EXISTS (SELECT 1 FROM MST_Menu WHERE MenuCode = 'CUST_MYWORKSPACE')
BEGIN
    INSERT INTO MST_Menu
        (ParentMenuId, MenuCode, MenuName, DisplayName, Route, Icon,
         DisplayOrder, PermissionId, PortalType, MenuLevel, IsVisible,
         CreatedBy, CreatedDate, IsActive, IsDeleted)
    VALUES
        (NULL, 'CUST_MYWORKSPACE', 'My Workspace', 'My Workspace', '/my-workspace', 'Building2',
         30, @Perm, 1, 1, 1,
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
    WHERE  MenuCode = 'CUST_MYWORKSPACE'
      AND  (IsDeleted = 1 OR IsActive = 0 OR IsVisible = 0);
END

/* --- role grants, mirroring CUST_BOOKINGS -------------------------------- */
DECLARE @MenuId int = (SELECT MenuId FROM MST_Menu WHERE MenuCode = 'CUST_MYWORKSPACE');

INSERT INTO MAP_RoleMenu (RoleId, MenuId)
SELECT rm.RoleId, @MenuId
FROM   MAP_RoleMenu rm
WHERE  rm.MenuId = @Bookings
  AND  NOT EXISTS (SELECT 1 FROM MAP_RoleMenu x
                   WHERE x.RoleId = rm.RoleId AND x.MenuId = @MenuId);

/* --- hide the two items that now live in the pop-out --------------------- */
UPDATE MST_Menu
SET    IsVisible    = 0,
       ModifiedBy   = @By,
       ModifiedDate = @Now
WHERE  MenuCode IN ('CUST_BOOKINGS', 'CUST_MYPASSES')
  AND  IsVisible = 1;

COMMIT TRANSACTION;

/* --- verification -------------------------------------------------------- */
SELECT MenuId, MenuCode, DisplayName, Route, Icon, DisplayOrder, PermissionId,
       PortalType, IsVisible, IsActive, IsDeleted
FROM   MST_Menu
WHERE  MenuCode IN ('CUST_MYWORKSPACE', 'CUST_BOOKINGS', 'CUST_MYPASSES')
ORDER  BY DisplayOrder;

SELECT m.MenuCode, r.RoleId, r.RoleCode
FROM   MAP_RoleMenu rm
JOIN   MST_Menu m ON m.MenuId = rm.MenuId
JOIN   MST_Role r ON r.RoleId = rm.RoleId
WHERE  m.MenuCode IN ('CUST_BOOKINGS', 'CUST_MYWORKSPACE')
ORDER  BY m.MenuCode, r.RoleId;
