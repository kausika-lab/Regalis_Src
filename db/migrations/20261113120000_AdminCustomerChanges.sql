/* ---------------------------------------------------------------------------
   Admin + customer changes (13 Nov 2026), kit deploy/admin-customer.

   1. Customer sidebar: "My QR / Access" (CUST_QR) is hidden -- MST_Menu.IsVisible = 0.
      Nothing is deleted: the /qr page, its API and the access tokens keep working, and
      the rollback shows the item again.
   2. New table TRN_SeatWaitlist: the seat waiting list for day passes (a customer waits
      for a desk on a first-visit date that is full). Read and written by the API through
      SeatWaitlistEntryConfiguration; there is no EF migration for it (see the kit README).
   3. Admin sidebar: "Waiting List" (ADM_WAITLIST, /admin/waitlist) under Operations, right
      after Passes. Same permission as Passes (PASS.VIEW); MAP_RoleMenu mirrors ADM_PASS_DAY,
      so whoever sees Passes sees the waiting list. Removing an entry needs PASS.EDIT.

   Idempotent; ASCII only.
     sqlcmd -S localhost -d RegalisWorkSpace_UAT -E -C -I -b -i 20261113120000_AdminCustomerChanges.sql
   Rollback: 20261113120000_AdminCustomerChanges_Rollback.sql
   --------------------------------------------------------------------------- */
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
SET NOCOUNT ON;
GO
SET XACT_ABORT ON;
BEGIN TRANSACTION;

DECLARE @Now datetime2 = SYSUTCDATETIME();
DECLARE @By  bigint    = (SELECT TOP 1 UserId FROM MST_User WHERE Email = 'admin@regalis.in');

/* --- 1. hide My QR / Access ---------------------------------------------- */
UPDATE MST_Menu
SET    IsVisible = 0, ModifiedBy = @By, ModifiedDate = @Now
WHERE  MenuCode = 'CUST_QR' AND IsVisible = 1;

/* --- 2. TRN_SeatWaitlist -------------------------------------------------- */
IF OBJECT_ID('dbo.TRN_SeatWaitlist', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.TRN_SeatWaitlist
    (
        SeatWaitlistId   bigint IDENTITY(1,1) NOT NULL CONSTRAINT PK_TRN_SeatWaitlist PRIMARY KEY,
        CustomerId       bigint        NOT NULL CONSTRAINT FK_SeatWaitlist_Customer      REFERENCES dbo.MST_User (UserId),
        LocationId       int           NOT NULL CONSTRAINT FK_SeatWaitlist_Location      REFERENCES dbo.MST_Location (LocationId),
        WorkspaceTypeId  int           NOT NULL CONSTRAINT FK_SeatWaitlist_WorkspaceType REFERENCES dbo.MST_WorkspaceType (WorkspaceTypeId),
        PassPlanId       int           NULL,
        WaitDate         date          NOT NULL,
        Status           tinyint       NOT NULL CONSTRAINT DF_SeatWaitlist_Status DEFAULT (1),
        IsSeatFree       bit           NOT NULL CONSTRAINT DF_SeatWaitlist_IsSeatFree DEFAULT (0),
        LastNotifiedUtc  datetime2(3)  NULL,
        NotifyCount      int           NOT NULL CONSTRAINT DF_SeatWaitlist_NotifyCount DEFAULT (0),
        ClosedUtc        datetime2(3)  NULL,
        ClosedByUserId   bigint        NULL,
        CloseReason      nvarchar(300) NULL,
        CreatedBy        bigint        NULL,
        CreatedDate      datetime2(3)  NOT NULL CONSTRAINT DF_SeatWaitlist_CreatedDate DEFAULT (SYSUTCDATETIME()),
        ModifiedBy       bigint        NULL,
        ModifiedDate     datetime2(3)  NULL,
        IsActive         bit           NOT NULL CONSTRAINT DF_SeatWaitlist_IsActive DEFAULT (1),
        IsDeleted        bit           NOT NULL CONSTRAINT DF_SeatWaitlist_IsDeleted DEFAULT (0),
        DeletedBy        bigint        NULL,
        DeletedDate      datetime2(3)  NULL,
        CONSTRAINT CK_SeatWaitlist_Status CHECK (Status BETWEEN 1 AND 5)
    );
    CREATE INDEX IX_SeatWaitlist_Status_Date ON dbo.TRN_SeatWaitlist (Status, WaitDate);
    CREATE INDEX IX_SeatWaitlist_Customer    ON dbo.TRN_SeatWaitlist (CustomerId, Status);
    /* One open entry per customer, location, type and date. */
    CREATE UNIQUE INDEX UX_SeatWaitlist_OpenEntry
        ON dbo.TRN_SeatWaitlist (CustomerId, LocationId, WorkspaceTypeId, WaitDate)
        WHERE Status = 1 AND IsDeleted = 0;
END

/* --- 3. admin menu: Waiting List ------------------------------------------ */
DECLARE @Parent int = (SELECT MenuId FROM MST_Menu WHERE MenuCode = 'ADM_SEC_OPERATIONS' AND IsDeleted = 0);
DECLARE @Passes int = (SELECT MenuId FROM MST_Menu WHERE MenuCode = 'ADM_PASS_DAY' AND IsDeleted = 0);
DECLARE @Perm   int = (SELECT PermissionId FROM MST_Menu WHERE MenuId = @Passes);

IF @Parent IS NULL OR @Passes IS NULL OR @Perm IS NULL
BEGIN
    RAISERROR ('AdminCustomerChanges: ADM_SEC_OPERATIONS or ADM_PASS_DAY not found - rolled back.', 16, 1);
    ROLLBACK TRANSACTION;
    RETURN;
END

IF NOT EXISTS (SELECT 1 FROM MST_Menu WHERE MenuCode = 'ADM_WAITLIST')
BEGIN
    INSERT INTO MST_Menu
        (ParentMenuId, MenuCode, MenuName, DisplayName, Route, Icon,
         DisplayOrder, PermissionId, PortalType, MenuLevel, IsVisible,
         CreatedBy, CreatedDate, IsActive, IsDeleted)
    VALUES
        (@Parent, 'ADM_WAITLIST', 'Waiting List', 'Waiting List', '/admin/waitlist', 'ListChecks',
         65, @Perm, 2, 1, 1,
         @By, @Now, 1, 0);
END
ELSE
BEGIN
    UPDATE MST_Menu
    SET    IsDeleted = 0, DeletedBy = NULL, DeletedDate = NULL, IsActive = 1, IsVisible = 1,
           ModifiedBy = @By, ModifiedDate = @Now
    WHERE  MenuCode = 'ADM_WAITLIST' AND (IsDeleted = 1 OR IsActive = 0 OR IsVisible = 0);
END

DECLARE @MenuId int = (SELECT MenuId FROM MST_Menu WHERE MenuCode = 'ADM_WAITLIST');
INSERT INTO MAP_RoleMenu (RoleId, MenuId)
SELECT rm.RoleId, @MenuId
FROM   MAP_RoleMenu rm
WHERE  rm.MenuId = @Passes
  AND  NOT EXISTS (SELECT 1 FROM MAP_RoleMenu x WHERE x.RoleId = rm.RoleId AND x.MenuId = @MenuId);

COMMIT TRANSACTION;

SELECT MenuCode, DisplayName, Route, IsVisible FROM MST_Menu WHERE MenuCode IN ('CUST_QR', 'ADM_WAITLIST');
SELECT CASE WHEN OBJECT_ID('dbo.TRN_SeatWaitlist', 'U') IS NULL THEN 'missing' ELSE 'present' END AS TRN_SeatWaitlist;
