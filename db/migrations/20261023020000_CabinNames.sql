/* 20261023020000_CabinNames.sql
   Named cabins (23 Oct 2026).

   The cabins take the names supplied on 23 Oct 2026. Crown is the eight-seater;
   the other three are three-seaters, so Capacity moves from 2 to 3 (or 8), and
   the stock "2 seats" / "two-seater" wording in Equipment and Description is
   updated to match (admin-edited text that no longer contains those words is
   left alone). Prices are not changed: Crown keeps the two-seater cabin's hourly
   and daily rates until the team prices it in Admin > Workspaces.

     CAB-C-01  The Monarch    3 seats
     CAB-C-02  The Sovereign  3 seats
     CAB-C-03  The Throne     3 seats
     CAB-C-04  Crown          8 seats

   CAB-C-04 is soft-deleted on UAT and absent on production, so an existing row
   is renamed and reactivated in place and a missing one is created as a copy of
   the first existing cabin (same location, type, floor, prices, bookable).
   Team Cabin 05 / 06 (soft-deleted four-seaters) are not touched.

   Idempotent: re-running changes nothing.
   Rollback: 20261023020000_CabinNames_Rollback.sql */
SET NOCOUNT ON;
SET XACT_ABORT ON;
BEGIN TRAN;

DECLARE @cabins TABLE (Code NVARCHAR(40) NOT NULL, Name NVARCHAR(120) NOT NULL,
                       Seats INT NOT NULL, SeatsWord NVARCHAR(20) NOT NULL);
INSERT INTO @cabins (Code, Name, Seats, SeatsWord) VALUES
    (N'CAB-C-01', N'The Monarch',   3, N'three'),
    (N'CAB-C-02', N'The Sovereign', 3, N'three'),
    (N'CAB-C-03', N'The Throne',    3, N'three'),
    (N'CAB-C-04', N'Crown',         8, N'eight');

DECLARE @tpl INT = (SELECT TOP (1) WorkspaceId
                      FROM MST_Workspace
                     WHERE WorkspaceCode LIKE N'CAB-C-%' AND IsDeleted = 0
                     ORDER BY WorkspaceCode);
IF @tpl IS NULL
    THROW 50000, 'No cabin (CAB-C-*) exists to copy the new cabin from.', 1;
DECLARE @loc INT = (SELECT LocationId FROM MST_Workspace WHERE WorkspaceId = @tpl);

/* 1. Existing codes: rename, reseat, and bring back a soft-deleted or inactive one. */
UPDATE w
   SET WorkspaceName = c.Name,
       Capacity      = c.Seats,
       Equipment     = REPLACE(w.Equipment, N'2 seats', CONCAT(c.Seats, N' seats')),
       Description   = REPLACE(w.Description, N'two-seater', c.SeatsWord + N'-seater'),
       IsActive = 1, IsDeleted = 0, DeletedBy = NULL, DeletedDate = NULL,
       ModifiedDate = GETUTCDATE()
  FROM MST_Workspace w
  JOIN @cabins c ON c.Code = w.WorkspaceCode
 WHERE w.LocationId = @loc
   AND (w.WorkspaceName <> c.Name OR w.Capacity <> c.Seats OR w.IsDeleted = 1 OR w.IsActive = 0);
PRINT CONCAT('Renamed or reactivated: ', @@ROWCOUNT);

/* 2. Missing codes: create from the template. */
INSERT INTO MST_Workspace
    (LocationId, WorkspaceTypeId, WorkspaceCode, WorkspaceName, FloorNumber, Capacity,
     PricePerHour, PricePerDay, PricePerMonth, Equipment, Description, ImagePath,
     Status, IsBookable, CreatedBy, CreatedDate, IsActive, IsDeleted)
SELECT t.LocationId, t.WorkspaceTypeId, c.Code, c.Name, t.FloorNumber, c.Seats,
       t.PricePerHour, t.PricePerDay, t.PricePerMonth,
       REPLACE(t.Equipment, N'2 seats', CONCAT(c.Seats, N' seats')),
       REPLACE(t.Description, N'two-seater', c.SeatsWord + N'-seater'),
       NULL, t.Status, t.IsBookable, NULL, GETUTCDATE(), 1, 0
  FROM @cabins c
 CROSS JOIN MST_Workspace t
 WHERE t.WorkspaceId = @tpl
   AND NOT EXISTS (SELECT 1 FROM MST_Workspace w
                    WHERE w.LocationId = t.LocationId AND w.WorkspaceCode = c.Code);
PRINT CONCAT('Created: ', @@ROWCOUNT);

COMMIT;
