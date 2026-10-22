/* 20261023020000_CabinNames_Rollback.sql
   Undoes 20261023020000_CabinNames.sql, restoring what UAT and production held
   before it: Private Cabin 01-03 as active two-seaters, and Private Cabin 04 as a
   soft-deleted two-seater (it was soft-deleted on UAT and absent on production;
   it is never hard-deleted because bookings may point at it). */
SET NOCOUNT ON;
SET XACT_ABORT ON;
BEGIN TRAN;

UPDATE MST_Workspace
   SET WorkspaceName = N'Private Cabin ' + RIGHT(WorkspaceCode, 2),
       Capacity      = 2,
       Equipment     = N'Wi-Fi, 2 seats, storage',
       Description   = N'Lockable two-seater cabin.',
       ModifiedDate  = GETUTCDATE()
 WHERE WorkspaceCode IN (N'CAB-C-01', N'CAB-C-02', N'CAB-C-03', N'CAB-C-04')
   AND IsDeleted = 0;
PRINT CONCAT('Restored: ', @@ROWCOUNT);

UPDATE MST_Workspace
   SET IsActive = 0, IsDeleted = 1, DeletedDate = GETUTCDATE()
 WHERE WorkspaceCode = N'CAB-C-04' AND IsDeleted = 0;
PRINT CONCAT('Cabin 04 soft-deleted: ', @@ROWCOUNT);

COMMIT;
