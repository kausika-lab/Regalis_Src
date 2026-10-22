/* 20261023030000_CabinMonthlyPrices_Rollback.sql
   Undoes 20261023030000_CabinMonthlyPrices.sql: the four cabins had no monthly
   price before it, so PricePerMonth goes back to NULL (not bookable by the month). */
SET NOCOUNT ON;
SET XACT_ABORT ON;
BEGIN TRAN;

UPDATE MST_Workspace
   SET PricePerMonth = NULL, ModifiedDate = GETUTCDATE()
 WHERE WorkspaceCode IN (N'CAB-C-01', N'CAB-C-02', N'CAB-C-03', N'CAB-C-04')
   AND PricePerMonth IS NOT NULL;
PRINT CONCAT('Monthly prices cleared: ', @@ROWCOUNT);

COMMIT;
