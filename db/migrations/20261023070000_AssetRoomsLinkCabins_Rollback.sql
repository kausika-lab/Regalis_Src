/* 20261023070000_AssetRoomsLinkCabins_Rollback.sql
   Undoes 20261023070000_AssetRoomsLinkCabins.sql: the four rooms lose their link
   to the cabins (only where it still points at the cabin this script chose). */
SET NOCOUNT ON;
SET XACT_ABORT ON;
BEGIN TRAN;

IF OBJECT_ID(N'MST_AssetLocation') IS NOT NULL
BEGIN
    UPDATE l
       SET WorkspaceId = NULL, ModifiedDate = SYSUTCDATETIME()
      FROM MST_AssetLocation l
      JOIN (VALUES (N'Private Cabin 1', N'CAB-C-01'), (N'Private Cabin 2', N'CAB-C-02'),
                   (N'Private Cabin 3', N'CAB-C-03'), (N'8 Seater Cabin',  N'CAB-C-04')) m(Room, Code)
        ON m.Room = l.LocationName
      JOIN MST_Workspace w ON w.WorkspaceId = l.WorkspaceId AND w.WorkspaceCode = m.Code;
    PRINT CONCAT('Room links removed: ', @@ROWCOUNT);
END

COMMIT;
