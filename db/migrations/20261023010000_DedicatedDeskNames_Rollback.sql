/* 20261023010000_DedicatedDeskNames_Rollback.sql
   Undoes 20261023010000_DedicatedDeskNames.sql.

   Dedicated Desk 09-12 get their old names back. Desks 01-08 are soft-deleted,
   never hard-deleted (bookings, blocks and maintenance rows may point at them);
   re-running the forward script reactivates and renames them again. */
SET NOCOUNT ON;
SET XACT_ABORT ON;
BEGIN TRAN;

UPDATE MST_Workspace
   SET WorkspaceName = N'Dedicated Desk ' + RIGHT(WorkspaceCode, 2), ModifiedDate = GETUTCDATE()
 WHERE WorkspaceCode IN (N'DDK-D-09', N'DDK-D-10', N'DDK-D-11', N'DDK-D-12')
   AND IsDeleted = 0 AND WorkspaceName <> N'Dedicated Desk ' + RIGHT(WorkspaceCode, 2);
PRINT CONCAT('Renamed back: ', @@ROWCOUNT);

UPDATE MST_Workspace
   SET IsActive = 0, IsDeleted = 1, DeletedDate = GETUTCDATE()
 WHERE WorkspaceCode IN (N'DDK-D-01', N'DDK-D-02', N'DDK-D-03', N'DDK-D-04',
                         N'DDK-D-05', N'DDK-D-06', N'DDK-D-07', N'DDK-D-08')
   AND IsDeleted = 0;
PRINT CONCAT('Soft-deleted: ', @@ROWCOUNT);

COMMIT;
