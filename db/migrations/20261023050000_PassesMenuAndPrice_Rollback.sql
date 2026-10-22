/* 20261023050000_PassesMenuAndPrice_Rollback.sql
   Undoes 20261023050000_PassesMenuAndPrice.sql: Single Day back to 800 (the
   packs were already at their values), the sidebar entry back to "Passes", and
   the My Passes entry soft-deleted and unmapped from the Customer role. */
SET NOCOUNT ON;
SET XACT_ABORT ON;
BEGIN TRAN;

UPDATE MST_PassPlan
   SET Price = 800.00, ModifiedDate = GETUTCDATE()
 WHERE PlanCode = N'DAYPASS-1' AND IsDeleted = 0 AND Price <> 800.00;
PRINT CONCAT('Single Day back to 800: ', @@ROWCOUNT);

UPDATE MST_Menu
   SET MenuName = N'Passes', DisplayName = N'Passes', ModifiedDate = GETUTCDATE()
 WHERE MenuCode = N'CUST_PASSES' AND IsDeleted = 0
   AND (MenuName <> N'Passes' OR DisplayName <> N'Passes');
PRINT CONCAT('Book Pass menu renamed back: ', @@ROWCOUNT);

DECLARE @my INT = (SELECT TOP (1) MenuId FROM MST_Menu WHERE MenuCode = N'CUST_MYPASSES' AND IsDeleted = 0);
IF @my IS NOT NULL
BEGIN
    DELETE FROM MAP_RoleMenu WHERE MenuId = @my;
    UPDATE MST_Menu
       SET IsActive = 0, IsDeleted = 1, DeletedDate = GETUTCDATE()
     WHERE MenuId = @my;
    PRINT 'My Passes menu soft-deleted and unmapped';
END
ELSE
    PRINT 'My Passes menu not present';

COMMIT;
