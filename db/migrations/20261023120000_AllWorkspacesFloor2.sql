/* 20261023120000_AllWorkspacesFloor2.sql
   Everything is on floor 2 (23 Oct 2026). The Board Room (BDR-M-03) and the five hot
   desks (DEK-D-01..05) still carried the seeded floor 1; the desks, cabins and
   everything else were already floor 2. Idempotent.
   Rollback: 20261023120000_AllWorkspacesFloor2_Rollback.sql */
SET NOCOUNT ON;
SET XACT_ABORT ON;
BEGIN TRAN;
UPDATE MST_Workspace
   SET FloorNumber = N'2', ModifiedDate = SYSUTCDATETIME()
 WHERE WorkspaceCode IN (N'BDR-M-03', N'DEK-D-01', N'DEK-D-02', N'DEK-D-03', N'DEK-D-04', N'DEK-D-05')
   AND IsDeleted = 0 AND FloorNumber <> N'2';
PRINT CONCAT('Moved to floor 2: ', @@ROWCOUNT);
COMMIT;
