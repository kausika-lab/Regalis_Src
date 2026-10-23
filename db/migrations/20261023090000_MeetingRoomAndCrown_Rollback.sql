/* 20261023090000_MeetingRoomAndCrown_Rollback.sql
   Undoes 20261023090000_MeetingRoomAndCrown.sql: Meeting Room 01 comes back (still
   under Maintenance, as an admin left it), Board Room and Crown are removed again,
   and the Assets room "8 Seater Cabin" loses its link. */
SET NOCOUNT ON;
SET XACT_ABORT ON;
BEGIN TRAN;

UPDATE MST_Workspace
   SET IsActive = 1, IsDeleted = 0, DeletedBy = NULL, DeletedDate = NULL, ModifiedDate = SYSUTCDATETIME()
 WHERE WorkspaceCode = N'MER-M-01' AND IsDeleted = 1;

IF OBJECT_ID(N'MST_AssetLocation') IS NOT NULL
    UPDATE l SET WorkspaceId = NULL, ModifiedDate = SYSUTCDATETIME()
      FROM MST_AssetLocation l JOIN MST_Workspace w ON w.WorkspaceId = l.WorkspaceId
     WHERE w.WorkspaceCode = N'CAB-C-04';

UPDATE MST_Workspace
   SET IsActive = 0, IsDeleted = 1, DeletedDate = SYSUTCDATETIME()
 WHERE WorkspaceCode IN (N'BDR-M-03', N'CAB-C-04') AND IsDeleted = 0;

COMMIT;
