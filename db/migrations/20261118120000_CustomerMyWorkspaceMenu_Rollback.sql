/* ---------------------------------------------------------------------------
   Rollback of 20261118120000_CustomerMyWorkspaceMenu.sql

   Removes the My Workspace pop-out row and its role grants, and shows
   My Bookings and My Passes in the customer sidebar again (both were visible
   before the migration). Idempotent.
   --------------------------------------------------------------------------- */
SET NOCOUNT ON;
SET XACT_ABORT ON;

DECLARE @Now    datetime2 = SYSUTCDATETIME();
DECLARE @By     bigint    = (SELECT TOP 1 UserId FROM MST_User WHERE Email = 'admin@regalis.in');
DECLARE @MenuId int       = (SELECT MenuId FROM MST_Menu WHERE MenuCode = 'CUST_MYWORKSPACE');

BEGIN TRANSACTION;

IF @MenuId IS NOT NULL
BEGIN
    DELETE FROM MAP_RoleMenu WHERE MenuId = @MenuId;
    DELETE FROM MST_Menu     WHERE MenuId = @MenuId;
END

UPDATE MST_Menu
SET    IsVisible    = 1,
       ModifiedBy   = @By,
       ModifiedDate = @Now
WHERE  MenuCode IN ('CUST_BOOKINGS', 'CUST_MYPASSES')
  AND  IsVisible = 0;

COMMIT TRANSACTION;

SELECT MenuId, MenuCode, DisplayName, Route, DisplayOrder, IsVisible, IsActive, IsDeleted
FROM   MST_Menu
WHERE  MenuCode IN ('CUST_MYWORKSPACE', 'CUST_BOOKINGS', 'CUST_MYPASSES')
ORDER  BY DisplayOrder;
