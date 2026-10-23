/* 20261023090000_MeetingRoomAndCrown.sql
   Corrects 20261023080000_WorkspaceInventoryCleanup (23 Oct 2026).

   1. The one meeting room kept there, MER-M-01 Meeting Room 01, had been set to
      Maintenance by an admin on 30 Sep, so it offers no slots and customers saw
      "Closed on this date" for every date. The meeting room actually in service
      was BDR-M-03 Board Room. Board Room comes back; Meeting Room 01 is removed.
   2. CAB-C-04 Crown, the 8-seater cabin (90,000 / month), comes back alongside the
      three 4-seater cabins, and the Assets room "8 Seater Cabin" links to it again.

   Stops (and changes nothing) if Meeting Room 01 has a running or future booking.
   Idempotent. Rollback: 20261023090000_MeetingRoomAndCrown_Rollback.sql */
SET NOCOUNT ON;
SET XACT_ABORT ON;
BEGIN TRAN;

DECLARE @now DATETIME2 = SYSUTCDATETIME();

IF EXISTS (SELECT 1 FROM TRN_Booking b JOIN MST_Workspace w ON w.WorkspaceId = b.WorkspaceId
           WHERE w.WorkspaceCode = N'MER-M-01' AND w.IsDeleted = 0
             AND b.Status IN (1, 2, 3) AND b.EndUtc >= @now)
    THROW 50000, 'Meeting Room 01 has a running or future booking; nothing was changed.', 1;

UPDATE MST_Workspace
   SET IsActive = 1, IsDeleted = 0, DeletedBy = NULL, DeletedDate = NULL, ModifiedDate = @now
 WHERE WorkspaceCode IN (N'BDR-M-03', N'CAB-C-04') AND IsDeleted = 1;
PRINT CONCAT('Board Room and Crown restored: ', @@ROWCOUNT);

UPDATE MST_Workspace
   SET IsActive = 0, IsDeleted = 1, DeletedDate = @now
 WHERE WorkspaceCode = N'MER-M-01' AND IsDeleted = 0;
PRINT CONCAT('Meeting Room 01 removed: ', @@ROWCOUNT);

IF OBJECT_ID(N'MST_AssetLocation') IS NOT NULL
    UPDATE l SET WorkspaceId = w.WorkspaceId, ModifiedDate = @now
      FROM MST_AssetLocation l
      JOIN MST_Workspace w ON w.WorkspaceCode = N'CAB-C-04' AND w.IsDeleted = 0
     WHERE l.LocationName = N'8 Seater Cabin' AND l.WorkspaceId IS NULL AND l.IsDeleted = 0;

COMMIT;
