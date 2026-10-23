/* 20261023080000_WorkspaceInventoryCleanup_Rollback.sql
   Undoes 20261023080000_WorkspaceInventoryCleanup.sql: desks DDK-D-10..12, Crown and
   the Board Room come back, the three cabins return to 3 seats, and the Assets room
   "8 Seater Cabin" is linked to Crown again.
   Bookings the forward script moved to desks 01-09 stay where they are (on UAT on
   23 Oct 2026 that was REG-000077 and REG-000078); move them by hand if needed. */
SET NOCOUNT ON;
SET XACT_ABORT ON;
BEGIN TRAN;

UPDATE MST_Workspace
   SET IsActive = 1, IsDeleted = 0, DeletedBy = NULL, DeletedDate = NULL, ModifiedDate = SYSUTCDATETIME()
 WHERE WorkspaceCode IN (N'DDK-D-10', N'DDK-D-11', N'DDK-D-12', N'CAB-C-04', N'BDR-M-03') AND IsDeleted = 1;
PRINT CONCAT('Workspaces restored: ', @@ROWCOUNT);

UPDATE MST_Workspace
   SET Capacity    = 3,
       Equipment   = REPLACE(Equipment, N'4 seats', N'3 seats'),
       Description = REPLACE(Description, N'four-seater', N'three-seater'),
       ModifiedDate = SYSUTCDATETIME()
 WHERE WorkspaceCode IN (N'CAB-C-01', N'CAB-C-02', N'CAB-C-03') AND IsDeleted = 0 AND Capacity <> 3;
PRINT CONCAT('Cabins back to 3 seats: ', @@ROWCOUNT);

IF OBJECT_ID(N'MST_AssetLocation') IS NOT NULL
    UPDATE l SET WorkspaceId = w.WorkspaceId, ModifiedDate = SYSUTCDATETIME()
      FROM MST_AssetLocation l JOIN MST_Workspace w ON w.WorkspaceCode = N'CAB-C-04' AND w.IsDeleted = 0
     WHERE l.LocationName = N'8 Seater Cabin' AND l.WorkspaceId IS NULL;

COMMIT;
