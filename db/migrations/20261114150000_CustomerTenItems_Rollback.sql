/* Rollback of 20261114150000_CustomerTenItems.sql. Run BEFORE putting the old API back:
   it does not know pass status 5. Hides the Payment History menu (soft delete) and turns
   every awaiting-payment pass back into Active, as it was before. Idempotent; ASCII only. */
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
SET NOCOUNT ON;
GO
SET XACT_ABORT ON;
BEGIN TRANSACTION;
UPDATE dbo.MST_Menu SET IsVisible = 0, IsDeleted = 1, DeletedDate = SYSUTCDATETIME() WHERE MenuCode = 'CUST_PAYMENTS';
UPDATE dbo.TRN_CustomerPass SET Status = 1 WHERE Status = 5;
COMMIT;
PRINT 'customer-10 migration rolled back.';
