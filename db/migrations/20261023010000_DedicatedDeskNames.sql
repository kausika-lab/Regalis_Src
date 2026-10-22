/* 20261023010000_DedicatedDeskNames.sql
   Named dedicated desks (23 Oct 2026).

   Each dedicated desk gets the name from the list supplied on 23 Oct 2026; hot
   desks keep their names. Only Dedicated Desk 09-12 exist, so desks 01-08 are
   created as copies of the first existing dedicated desk (same location, type,
   floor, capacity, prices, equipment, description, bookable) under the codes
   DDK-D-01..08, and 09-12 are renamed in place, so their bookings still point at
   the same rows.

     DDK-D-01  Aurelia      DDK-D-05  Noble        DDK-D-09  Pinnacle
     DDK-D-02  Sterling     DDK-D-06  Majestic     DDK-D-10  Legacy
     DDK-D-03  Crest        DDK-D-07  Meridian     DDK-D-11  Sovereign
     DDK-D-04  Regent       DDK-D-08  Imperial     DDK-D-12  Ascend

   Idempotent: re-running changes nothing. A soft-deleted DDK-D-NN row is renamed
   and reactivated rather than duplicated (UX_Workspace_Location_Code).
   Rollback: 20261023010000_DedicatedDeskNames_Rollback.sql */
SET NOCOUNT ON;
SET XACT_ABORT ON;
BEGIN TRAN;

DECLARE @names TABLE (Code NVARCHAR(40) NOT NULL, Name NVARCHAR(120) NOT NULL);
INSERT INTO @names (Code, Name) VALUES
    (N'DDK-D-01', N'Aurelia'),  (N'DDK-D-02', N'Sterling'), (N'DDK-D-03', N'Crest'),
    (N'DDK-D-04', N'Regent'),   (N'DDK-D-05', N'Noble'),    (N'DDK-D-06', N'Majestic'),
    (N'DDK-D-07', N'Meridian'), (N'DDK-D-08', N'Imperial'), (N'DDK-D-09', N'Pinnacle'),
    (N'DDK-D-10', N'Legacy'),   (N'DDK-D-11', N'Sovereign'),(N'DDK-D-12', N'Ascend');

/* Template for the new desks: the lowest-numbered dedicated desk that exists. */
DECLARE @tpl INT = (SELECT TOP (1) WorkspaceId
                      FROM MST_Workspace
                     WHERE WorkspaceCode LIKE N'DDK-D-%' AND IsDeleted = 0
                     ORDER BY WorkspaceCode);
IF @tpl IS NULL
    THROW 50000, 'No dedicated desk (DDK-D-*) exists to copy the new desks from.', 1;
DECLARE @loc INT = (SELECT LocationId FROM MST_Workspace WHERE WorkspaceId = @tpl);

/* 1. Existing codes: rename (and bring back a soft-deleted or inactive one). */
UPDATE w
   SET WorkspaceName = n.Name, IsActive = 1, IsDeleted = 0, DeletedBy = NULL, DeletedDate = NULL,
       ModifiedDate = GETUTCDATE()
  FROM MST_Workspace w
  JOIN @names n ON n.Code = w.WorkspaceCode
 WHERE w.LocationId = @loc
   AND (w.WorkspaceName <> n.Name OR w.IsDeleted = 1 OR w.IsActive = 0);
PRINT CONCAT('Renamed or reactivated: ', @@ROWCOUNT);

/* 2. Missing codes: create from the template. */
INSERT INTO MST_Workspace
    (LocationId, WorkspaceTypeId, WorkspaceCode, WorkspaceName, FloorNumber, Capacity,
     PricePerHour, PricePerDay, PricePerMonth, Equipment, Description, ImagePath,
     Status, IsBookable, CreatedBy, CreatedDate, IsActive, IsDeleted)
SELECT t.LocationId, t.WorkspaceTypeId, n.Code, n.Name, t.FloorNumber, t.Capacity,
       t.PricePerHour, t.PricePerDay, t.PricePerMonth, t.Equipment, t.Description, NULL,
       t.Status, t.IsBookable, NULL, GETUTCDATE(), 1, 0
  FROM @names n
 CROSS JOIN MST_Workspace t
 WHERE t.WorkspaceId = @tpl
   AND NOT EXISTS (SELECT 1 FROM MST_Workspace w
                    WHERE w.LocationId = t.LocationId AND w.WorkspaceCode = n.Code);
PRINT CONCAT('Created: ', @@ROWCOUNT);

COMMIT;
