/* 20261023040000_DedicatedDeskMonthlyPrice.sql
   Dedicated desk monthly price (23 Oct 2026).

   Every dedicated desk (DDK-D-01 to DDK-D-12: Aurelia .. Ascend) is 9,000 a
   month; they carried the seeded 2,000. Hourly and daily rates are not changed,
   and hot desks (DEK-D-*) are not touched. Idempotent: re-running changes nothing.
   Rollback: 20261023040000_DedicatedDeskMonthlyPrice_Rollback.sql */
SET NOCOUNT ON;
SET XACT_ABORT ON;
BEGIN TRAN;

UPDATE MST_Workspace
   SET PricePerMonth = 9000.00, ModifiedDate = GETUTCDATE()
 WHERE WorkspaceCode LIKE N'DDK-D-%'
   AND IsDeleted = 0
   AND (PricePerMonth IS NULL OR PricePerMonth <> 9000.00);
PRINT CONCAT('Dedicated desks priced at 9,000 / month: ', @@ROWCOUNT);

COMMIT;
