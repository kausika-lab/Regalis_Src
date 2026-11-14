/* Rollback of 20261113120000_AdminCustomerChanges.sql.
   - Shows "My QR / Access" in the customer sidebar again.
   - Soft-deletes the ADM_WAITLIST menu row and removes its role grants.
   - TRN_SeatWaitlist is KEPT (it may hold customer entries). To drop it as well, run the
     commented block at the end once you are sure nothing in it is needed.
   Run the old API (without the waiting list) after this. Safe to run more than once. ASCII only. */
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
SET NOCOUNT ON;
GO
SET XACT_ABORT ON;
BEGIN TRANSACTION;

DECLARE @Now datetime2 = SYSUTCDATETIME();
DECLARE @By  bigint    = (SELECT TOP 1 UserId FROM MST_User WHERE Email = 'admin@regalis.in');

UPDATE MST_Menu SET IsVisible = 1, ModifiedBy = @By, ModifiedDate = @Now
WHERE  MenuCode = 'CUST_QR' AND IsVisible = 0;

DECLARE @MenuId int = (SELECT MenuId FROM MST_Menu WHERE MenuCode = 'ADM_WAITLIST');
IF @MenuId IS NOT NULL
BEGIN
    DELETE FROM MAP_RoleMenu WHERE MenuId = @MenuId;
    UPDATE MST_Menu SET IsDeleted = 1, IsActive = 0, DeletedBy = @By, DeletedDate = @Now
    WHERE  MenuId = @MenuId;
END

COMMIT TRANSACTION;

/* Optional, destructive:
IF OBJECT_ID('dbo.TRN_SeatWaitlist', 'U') IS NOT NULL DROP TABLE dbo.TRN_SeatWaitlist;
*/
SELECT MenuCode, IsVisible, IsDeleted FROM MST_Menu WHERE MenuCode IN ('CUST_QR', 'ADM_WAITLIST');
