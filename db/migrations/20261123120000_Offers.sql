-- Offers (Admin-controlled offers brief, 23 Nov 2026; kit deploy/offers).
-- Tables for offers, the modules / plans they apply to, their banner picture and their redemptions;
-- permissions OFFER.VIEW / OFFER.MANAGE; the admin menu row Sales > Offers (/admin/offers).
-- Nothing existing is renamed, moved or changed. Bookings already had DiscountAmount (always 0 until
-- now) and invoices already had DiscountAmount; the offers kit is the first thing to fill them.
--
-- Idempotent; one transaction. ASCII only.
--   sqlcmd -S localhost -d RegalisWorkSpace_UAT -E -C -I -b -i 20261123120000_Offers.sql
-- Rollback: 20261123120000_Offers_Rollback.sql
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
SET NOCOUNT ON;
GO
SET XACT_ABORT ON;
BEGIN TRANSACTION;

DECLARE @now datetime2(3) = SYSUTCDATETIME();

-- 1. Tables -----------------------------------------------------------------------------------------
IF OBJECT_ID(N'dbo.MST_Offer', N'U') IS NULL
    CREATE TABLE dbo.MST_Offer
    (
        OfferId              int IDENTITY(1,1) NOT NULL CONSTRAINT PK_MST_Offer PRIMARY KEY,
        OfferName            nvarchar(120)  NOT NULL,             -- internal name (admin only)
        Title                nvarchar(150)  NOT NULL,             -- what the customer sees
        Description          nvarchar(1000) NULL,
        Terms                nvarchar(4000) NULL,
        Code                 nvarchar(30)   NULL,                 -- upper case; unique while not deleted
        ShowInPortal         bit            NOT NULL CONSTRAINT DF_MST_Offer_ShowInPortal DEFAULT (1),
        DiscountType         tinyint        NOT NULL,             -- 1 percentage, 2 fixed amount
        DiscountValue        decimal(18,2)  NOT NULL,             -- off the price before GST
        MaxDiscountAmount    decimal(18,2)  NULL,
        MinAmount            decimal(18,2)  NULL,                 -- price before GST
        StartDate            date           NOT NULL,             -- IST, inclusive
        EndDate              date           NOT NULL,             -- IST, inclusive
        CanCombine           bit            NOT NULL CONSTRAINT DF_MST_Offer_CanCombine DEFAULT (0),
        SortOrder            int            NOT NULL CONSTRAINT DF_MST_Offer_SortOrder DEFAULT (100),
        UsesPerCustomer      int            NULL,
        TotalUses            int            NULL,
        ImageVersion         int            NOT NULL CONSTRAINT DF_MST_Offer_ImageVersion DEFAULT (0),
        HasImage             bit            NOT NULL CONSTRAINT DF_MST_Offer_HasImage DEFAULT (0),
        CreatedBy            bigint         NULL,
        CreatedDate          datetime2(3)   NOT NULL,
        ModifiedBy           bigint         NULL,
        ModifiedDate         datetime2(3)   NULL,
        IsActive             bit            NOT NULL,
        IsDeleted            bit            NOT NULL CONSTRAINT DF_MST_Offer_IsDeleted DEFAULT (0),
        DeletedBy            bigint         NULL,
        DeletedDate          datetime2(3)   NULL,
        CONSTRAINT CK_MST_Offer_Type  CHECK (DiscountType IN (1, 2)),
        CONSTRAINT CK_MST_Offer_Value CHECK (DiscountValue > 0 AND (DiscountType = 2 OR DiscountValue <= 100)),
        CONSTRAINT CK_MST_Offer_Dates CHECK (EndDate >= StartDate)
    );
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UX_Offer_Code' AND object_id = OBJECT_ID(N'dbo.MST_Offer'))
    CREATE UNIQUE INDEX UX_Offer_Code ON dbo.MST_Offer (Code) WHERE Code IS NOT NULL AND IsDeleted = 0;
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Offer_Live' AND object_id = OBJECT_ID(N'dbo.MST_Offer'))
    CREATE INDEX IX_Offer_Live ON dbo.MST_Offer (IsActive, StartDate, EndDate) WHERE IsDeleted = 0;

IF OBJECT_ID(N'dbo.MST_OfferModule', N'U') IS NULL
    CREATE TABLE dbo.MST_OfferModule
    (
        OfferModuleId        int IDENTITY(1,1) NOT NULL CONSTRAINT PK_MST_OfferModule PRIMARY KEY,
        OfferId              int     NOT NULL CONSTRAINT FK_OfferModule_Offer REFERENCES dbo.MST_Offer (OfferId),
        Module               tinyint NOT NULL,                    -- 1 My Bookings, 2 Passes, 3 Membership
        CONSTRAINT UX_OfferModule UNIQUE (OfferId, Module),
        CONSTRAINT CK_OfferModule_Module CHECK (Module IN (1, 2, 3))
    );

IF OBJECT_ID(N'dbo.MST_OfferScope', N'U') IS NULL
    CREATE TABLE dbo.MST_OfferScope
    (
        OfferScopeId         int IDENTITY(1,1) NOT NULL CONSTRAINT PK_MST_OfferScope PRIMARY KEY,
        OfferId              int     NOT NULL CONSTRAINT FK_OfferScope_Offer REFERENCES dbo.MST_Offer (OfferId),
        Module               tinyint NOT NULL,
        RefId                int     NOT NULL,                    -- WorkspaceTypeId (module 1) or PassPlanId (2, 3)
        CONSTRAINT UX_OfferScope UNIQUE (OfferId, Module, RefId),
        CONSTRAINT CK_OfferScope_Module CHECK (Module IN (1, 2, 3))
    );

IF OBJECT_ID(N'dbo.TRN_OfferImage', N'U') IS NULL
    CREATE TABLE dbo.TRN_OfferImage
    (
        OfferId              int            NOT NULL CONSTRAINT PK_TRN_OfferImage PRIMARY KEY
                                            CONSTRAINT FK_OfferImage_Offer REFERENCES dbo.MST_Offer (OfferId),
        ContentType          nvarchar(50)   NOT NULL,
        Data                 varbinary(max) NOT NULL,
        UpdatedUtc           datetime2(3)   NOT NULL
    );

IF OBJECT_ID(N'dbo.TRN_OfferRedemption', N'U') IS NULL
    CREATE TABLE dbo.TRN_OfferRedemption
    (
        RedemptionId         bigint IDENTITY(1,1) NOT NULL CONSTRAINT PK_TRN_OfferRedemption PRIMARY KEY,
        OfferId              int              NOT NULL CONSTRAINT FK_OfferRedemption_Offer REFERENCES dbo.MST_Offer (OfferId),
        CustomerId           bigint           NOT NULL,
        Module               tinyint          NOT NULL,
        BookingId            bigint           NULL,
        CustomerPassId       bigint           NULL,
        PaymentId            bigint           NULL,               -- online pass order the discount was priced into
        PassPlanId           int              NULL,
        PurchaseKey          uniqueidentifier NOT NULL,           -- one purchase (all days of a multi-day booking)
        BaseAmount           decimal(18,2)    NOT NULL,
        DiscountAmount       decimal(18,2)    NOT NULL,
        OfferLabel           nvarchar(160)    NOT NULL,
        CreatedUtc           datetime2(3)     NOT NULL
    );
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_OfferRedemption_Offer' AND object_id = OBJECT_ID(N'dbo.TRN_OfferRedemption'))
    CREATE INDEX IX_OfferRedemption_Offer ON dbo.TRN_OfferRedemption (OfferId) INCLUDE (CustomerId, PurchaseKey, DiscountAmount);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_OfferRedemption_Customer' AND object_id = OBJECT_ID(N'dbo.TRN_OfferRedemption'))
    CREATE INDEX IX_OfferRedemption_Customer ON dbo.TRN_OfferRedemption (CustomerId);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_OfferRedemption_Booking' AND object_id = OBJECT_ID(N'dbo.TRN_OfferRedemption'))
    CREATE INDEX IX_OfferRedemption_Booking ON dbo.TRN_OfferRedemption (BookingId) WHERE BookingId IS NOT NULL;
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_OfferRedemption_Pass' AND object_id = OBJECT_ID(N'dbo.TRN_OfferRedemption'))
    CREATE INDEX IX_OfferRedemption_Pass ON dbo.TRN_OfferRedemption (CustomerPassId) WHERE CustomerPassId IS NOT NULL;
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_OfferRedemption_Payment' AND object_id = OBJECT_ID(N'dbo.TRN_OfferRedemption'))
    CREATE INDEX IX_OfferRedemption_Payment ON dbo.TRN_OfferRedemption (PaymentId) WHERE PaymentId IS NOT NULL;

-- 2. Permissions ------------------------------------------------------------------------------------
DECLARE @perm TABLE (Code nvarchar(100), ActionName nvarchar(50), Description nvarchar(200), DisplayOrder int);
INSERT INTO @perm VALUES
    (N'OFFER.VIEW',   N'VIEW',   N'See the offers, their status and who used them', 1),
    (N'OFFER.MANAGE', N'MANAGE', N'Create, edit, activate, deactivate and delete offers', 2);

INSERT INTO dbo.MST_Permission (PermissionCode, ModuleName, ActionName, Description, DisplayOrder, CreatedDate, IsActive, IsDeleted)
SELECT p.Code, N'OFFER', p.ActionName, p.Description, p.DisplayOrder, @now, 1, 0
FROM @perm p
WHERE NOT EXISTS (SELECT 1 FROM dbo.MST_Permission x WHERE x.PermissionCode = p.Code);

DECLARE @grant TABLE (RoleCode nvarchar(50), Code nvarchar(100));
INSERT INTO @grant VALUES
    (N'ADMIN', N'OFFER.VIEW'), (N'ADMIN', N'OFFER.MANAGE'),
    (N'MANAGER', N'OFFER.VIEW'), (N'MANAGER', N'OFFER.MANAGE'),
    (N'SALES', N'OFFER.VIEW');    -- the front desk can see what is on offer

INSERT INTO dbo.MAP_RolePermission (RoleId, PermissionId)
SELECT DISTINCT r.RoleId, p.PermissionId
FROM @grant g
JOIN dbo.MST_Role r ON r.RoleCode = g.RoleCode
JOIN dbo.MST_Permission p ON p.PermissionCode = g.Code
WHERE NOT EXISTS (SELECT 1 FROM dbo.MAP_RolePermission x WHERE x.RoleId = r.RoleId AND x.PermissionId = p.PermissionId);

-- 3. Menu: Sales > Offers, after CRM ------------------------------------------------------------------
DECLARE @sales int = (SELECT MenuId FROM dbo.MST_Menu WHERE MenuCode = N'ADM_SEC_SALES' AND IsDeleted = 0);
IF @sales IS NULL
BEGIN
    RAISERROR('Sales menu section (ADM_SEC_SALES) not found - run the CRM migration first. Nothing changed.', 16, 1);
    ROLLBACK TRANSACTION;
    RETURN;
END;

INSERT INTO dbo.MST_Menu (ParentMenuId, MenuCode, MenuName, DisplayName, Route, Icon, DisplayOrder, PermissionId, PortalType, MenuLevel, IsVisible, CreatedDate, IsActive, IsDeleted)
SELECT @sales, N'ADM_OFFERS', N'Offers', N'Offers', N'/admin/offers', N'BadgePercent', 20,
       (SELECT PermissionId FROM dbo.MST_Permission WHERE PermissionCode = N'OFFER.VIEW'), 2, 1, 1, @now, 1, 0
WHERE NOT EXISTS (SELECT 1 FROM dbo.MST_Menu WHERE MenuCode = N'ADM_OFFERS' AND IsDeleted = 0);

-- The Offers row and the Sales band go to every staff role holding OFFER.VIEW.
INSERT INTO dbo.MAP_RoleMenu (RoleId, MenuId)
SELECT DISTINCT rp.RoleId, m.MenuId
FROM dbo.MAP_RolePermission rp
JOIN dbo.MST_Permission p ON p.PermissionId = rp.PermissionId AND p.PermissionCode = N'OFFER.VIEW'
JOIN dbo.MST_Role r ON r.RoleId = rp.RoleId AND r.RoleCode <> N'CUSTOMER'
JOIN dbo.MST_Menu m ON m.MenuCode IN (N'ADM_OFFERS', N'ADM_SEC_SALES') AND m.IsDeleted = 0
WHERE NOT EXISTS (SELECT 1 FROM dbo.MAP_RoleMenu x WHERE x.RoleId = rp.RoleId AND x.MenuId = m.MenuId);

COMMIT TRANSACTION;
PRINT 'Offers migration applied.';
