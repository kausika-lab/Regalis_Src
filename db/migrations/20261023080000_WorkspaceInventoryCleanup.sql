/* 20261023080000_WorkspaceInventoryCleanup.sql
   The real workspace count (23 Oct 2026): 9 dedicated desks, 5 hot desks, three
   4-seater cabins and 1 meeting room.

   1. Dedicated desks DDK-D-10 Legacy, DDK-D-11 Sovereign and DDK-D-12 Ascend are
      removed (soft-deleted, so past bookings keep their history). Any running or
      future booking on them is first moved to a free dedicated desk among 01-09
      (lowest code free for the booking's whole window), so no customer is left
      holding a removed desk. The booking number, dates and amount do not change.
   2. Cabins: The Monarch, The Sovereign and The Throne become 4-seaters (capacity
      and the stock "3 seats" / "three-seater" wording). CAB-C-04 Crown is removed,
      and the Assets room "8 Seater Cabin" loses its link to it.
   3. Meeting rooms: only Meeting Room 01 (MER-M-01) stays. BDR-M-03 Board Room is
      removed.
   Prices are not changed. Nothing is hard-deleted.

   Stops (and changes nothing) if a booking on a removed desk cannot be moved, or
   if Crown or the Board Room still has a running or future booking.
   Idempotent. Rollback: 20261023080000_WorkspaceInventoryCleanup_Rollback.sql */
SET NOCOUNT ON;
SET XACT_ABORT ON;
BEGIN TRAN;

DECLARE @now DATETIME2 = SYSUTCDATETIME();
DECLARE @gone TABLE (Code NVARCHAR(40) NOT NULL);
INSERT INTO @gone (Code) VALUES (N'DDK-D-10'), (N'DDK-D-11'), (N'DDK-D-12'), (N'CAB-C-04'), (N'BDR-M-03');

/* Live = pending, confirmed or checked in, and not yet over. */
IF EXISTS (SELECT 1 FROM TRN_Booking b JOIN MST_Workspace w ON w.WorkspaceId = b.WorkspaceId
           WHERE w.WorkspaceCode IN (N'CAB-C-04', N'BDR-M-03') AND w.IsDeleted = 0
             AND b.Status IN (1, 2, 3) AND b.EndUtc >= @now)
    THROW 50000, 'Crown or the Board Room has a running or future booking; nothing was changed.', 1;

/* 1a. Move live bookings off desks 10-12, one at a time, to the lowest free desk 01-09. */
DECLARE @bid BIGINT, @s DATETIME2, @e DATETIME2, @to INT;
DECLARE moves CURSOR LOCAL FAST_FORWARD FOR
    SELECT b.BookingId, b.StartUtc, b.EndUtc
      FROM TRN_Booking b JOIN MST_Workspace w ON w.WorkspaceId = b.WorkspaceId
     WHERE w.WorkspaceCode IN (N'DDK-D-10', N'DDK-D-11', N'DDK-D-12') AND w.IsDeleted = 0
       AND b.Status IN (1, 2, 3) AND b.EndUtc >= @now
     ORDER BY b.StartUtc;
OPEN moves;
FETCH NEXT FROM moves INTO @bid, @s, @e;
WHILE @@FETCH_STATUS = 0
BEGIN
    SET @to = (SELECT TOP (1) w.WorkspaceId FROM MST_Workspace w
                WHERE w.WorkspaceCode IN (N'DDK-D-01', N'DDK-D-02', N'DDK-D-03', N'DDK-D-04', N'DDK-D-05',
                                          N'DDK-D-06', N'DDK-D-07', N'DDK-D-08', N'DDK-D-09')
                  AND w.IsDeleted = 0 AND w.IsActive = 1
                  AND NOT EXISTS (SELECT 1 FROM TRN_Booking x
                                   WHERE x.WorkspaceId = w.WorkspaceId AND x.Status IN (1, 2, 3)
                                     AND x.StartUtc < @e AND x.EndUtc > @s)
                ORDER BY w.WorkspaceCode);
    IF @to IS NULL
        THROW 50001, 'A booking on desks 10-12 has no free desk among 01-09 for its dates; nothing was changed.', 1;
    UPDATE TRN_Booking SET WorkspaceId = @to, ModifiedDate = @now WHERE BookingId = @bid;
    PRINT CONCAT('Booking ', @bid, ' moved to workspace ', @to);
    FETCH NEXT FROM moves INTO @bid, @s, @e;
END
CLOSE moves; DEALLOCATE moves;

/* 2. Three 4-seater cabins. */
UPDATE MST_Workspace
   SET Capacity    = 4,
       Equipment   = REPLACE(Equipment, N'3 seats', N'4 seats'),
       Description = REPLACE(Description, N'three-seater', N'four-seater'),
       ModifiedDate = @now
 WHERE WorkspaceCode IN (N'CAB-C-01', N'CAB-C-02', N'CAB-C-03') AND IsDeleted = 0 AND Capacity <> 4;
PRINT CONCAT('Cabins set to 4 seats: ', @@ROWCOUNT);

IF OBJECT_ID(N'MST_AssetLocation') IS NOT NULL
    UPDATE l SET WorkspaceId = NULL, ModifiedDate = @now
      FROM MST_AssetLocation l JOIN MST_Workspace w ON w.WorkspaceId = l.WorkspaceId
     WHERE w.WorkspaceCode = N'CAB-C-04';

/* 1b, 2, 3. Remove desks 10-12, Crown and the Board Room. */
UPDATE w SET IsActive = 0, IsDeleted = 1, DeletedDate = @now
  FROM MST_Workspace w JOIN @gone g ON g.Code = w.WorkspaceCode
 WHERE w.IsDeleted = 0;
PRINT CONCAT('Workspaces removed: ', @@ROWCOUNT);

COMMIT;
