/* 20261023120000_AllWorkspacesFloor2_Rollback.sql
   Puts the Board Room and hot desks 01-05 back on floor 1. */
SET NOCOUNT ON;
SET XACT_ABORT ON;
BEGIN TRAN;
UPDATE MST_Workspace
   SET FloorNumber = N'1', ModifiedDate = SYSUTCDATETIME()
 WHERE WorkspaceCode IN (N'BDR-M-03', N'DEK-D-01', N'DEK-D-02', N'DEK-D-03', N'DEK-D-04', N'DEK-D-05')
   AND IsDeleted = 0 AND FloorNumber <> N'1';
COMMIT;
