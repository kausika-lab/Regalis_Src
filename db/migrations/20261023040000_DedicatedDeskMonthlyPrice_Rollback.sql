/* 20261023040000_DedicatedDeskMonthlyPrice_Rollback.sql
   Undoes 20261023040000_DedicatedDeskMonthlyPrice.sql: every dedicated desk
   goes back to the 2,000 a month it carried before (the seeded value on 09-12
   and the value 01-08 were created with). */
SET NOCOUNT ON;
SET XACT_ABORT ON;
BEGIN TRAN;

UPDATE MST_Workspace
   SET PricePerMonth = 2000.00, ModifiedDate = GETUTCDATE()
 WHERE WorkspaceCode LIKE N'DDK-D-%'
   AND IsDeleted = 0
   AND (PricePerMonth IS NULL OR PricePerMonth <> 2000.00);
PRINT CONCAT('Dedicated desks back to 2,000 / month: ', @@ROWCOUNT);

COMMIT;
