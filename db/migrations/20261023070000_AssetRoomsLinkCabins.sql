/* 20261023070000_AssetRoomsLinkCabins.sql
   Assets rooms -> renamed cabins (23 Oct 2026). Run after 20261016030926_AssetsModule.

   The Assets migration links a room to a workspace when the names match
   ("Private Cabin 3" = "Private Cabin 03"). On 23 Oct 2026 the cabins were renamed
   (The Monarch, The Sovereign, The Throne, Crown), so nothing matches any more.
   This links the team's room names to the cabins by workspace code instead:
     Private Cabin 1  -> CAB-C-01 The Monarch
     Private Cabin 2  -> CAB-C-02 The Sovereign
     Private Cabin 3  -> CAB-C-03 The Throne
     8 Seater Cabin   -> CAB-C-04 Crown (the eight-seater)
   Room names stay as the team writes them. Only rooms with no link are touched,
   so an admin's own choice is never overwritten. Idempotent.
   Rollback: 20261023070000_AssetRoomsLinkCabins_Rollback.sql */
SET NOCOUNT ON;
SET XACT_ABORT ON;
BEGIN TRAN;

IF OBJECT_ID(N'MST_AssetLocation') IS NULL
    THROW 50000, 'MST_AssetLocation is missing: run 20261016030926_AssetsModule.sql first.', 1;

DECLARE @map TABLE (Room NVARCHAR(100) NOT NULL, Code NVARCHAR(40) NOT NULL);
INSERT INTO @map (Room, Code) VALUES
    (N'Private Cabin 1', N'CAB-C-01'), (N'Private Cabin 2', N'CAB-C-02'),
    (N'Private Cabin 3', N'CAB-C-03'), (N'8 Seater Cabin',  N'CAB-C-04');

UPDATE l
   SET WorkspaceId = w.WorkspaceId, ModifiedDate = SYSUTCDATETIME()
  FROM MST_AssetLocation l
  JOIN @map m ON m.Room = l.LocationName
  JOIN MST_Workspace w ON w.WorkspaceCode = m.Code AND w.IsDeleted = 0
 WHERE l.WorkspaceId IS NULL AND l.IsDeleted = 0;
PRINT CONCAT('Rooms linked to cabins: ', @@ROWCOUNT);

COMMIT;
