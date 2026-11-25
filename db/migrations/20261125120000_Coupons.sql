-- Coupon codes (Coupon Code brief, 25 Nov 2026; kit deploy/coupons).
-- Tables for coupons, the services (modules) they apply to, and their redemptions. Coupons live in
-- the Offers screen (Sales > Offers, tab "Coupon codes") and use the offers permissions OFFER.VIEW /
-- OFFER.MANAGE, so no permission or menu row is added.
-- Needs the offers migration (20261123120000_Offers.sql) first. Nothing existing is renamed, moved
-- or changed: bookings and invoices keep using their existing DiscountAmount columns.
--
-- Idempotent; one transaction. ASCII only.
--   sqlcmd -S localhost -d RegalisWorkSpace_UAT -E -C -I -b -i 20261125120000_Coupons.sql
-- Rollback: 20261125120000_Coupons_Rollback.sql
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
SET NOCOUNT ON;
GO
SET XACT_ABORT ON;
BEGIN TRANSACTION;

IF OBJECT_ID(N'dbo.MST_Offer', N'U') IS NULL
BEGIN
    RAISERROR('MST_Offer not found - run 20261123120000_Offers.sql first. Nothing changed.', 16, 1);
    ROLLBACK TRANSACTION;
    RETURN;
END;

IF OBJECT_ID(N'dbo.MST_Coupon', N'U') IS NULL
    CREATE TABLE dbo.MST_Coupon
    (
        CouponId             int IDENTITY(1,1) NOT NULL CONSTRAINT PK_MST_Coupon PRIMARY KEY,
        Code                 nvarchar(30)   NOT NULL,             -- upper case; unique while not deleted
        Description          nvarchar(300)  NULL,                 -- what the customer sees next to the code
        DiscountType         tinyint        NOT NULL,             -- 1 percentage, 2 fixed amount
        DiscountValue        decimal(18,2)  NOT NULL,             -- off the price before GST
        MaxDiscountAmount    decimal(18,2)  NULL,                 -- cap for a percentage
        MinAmount            decimal(18,2)  NULL,                 -- minimum booking amount, before GST
        ValidFrom            date           NOT NULL,             -- IST, inclusive
        ValidUntil           date           NOT NULL,             -- IST, inclusive
        TotalUses            int            NULL,                 -- usage limit, everyone together
        UsesPerCustomer      int            NULL,                 -- usage limit, each customer
        ShowInPortal         bit            NOT NULL CONSTRAINT DF_MST_Coupon_ShowInPortal DEFAULT (1),
        CreatedBy            bigint         NULL,
        CreatedDate          datetime2(3)   NOT NULL,
        ModifiedBy           bigint         NULL,
        ModifiedDate         datetime2(3)   NULL,
        IsActive             bit            NOT NULL,
        IsDeleted            bit            NOT NULL CONSTRAINT DF_MST_Coupon_IsDeleted DEFAULT (0),
        DeletedBy            bigint         NULL,
        DeletedDate          datetime2(3)   NULL,
        CONSTRAINT CK_MST_Coupon_Type   CHECK (DiscountType IN (1, 2)),
        CONSTRAINT CK_MST_Coupon_Value  CHECK (DiscountValue > 0 AND (DiscountType = 2 OR DiscountValue <= 100)),
        CONSTRAINT CK_MST_Coupon_Dates  CHECK (ValidUntil >= ValidFrom),
        CONSTRAINT CK_MST_Coupon_Limits CHECK ((TotalUses IS NULL OR TotalUses > 0) AND (UsesPerCustomer IS NULL OR UsesPerCustomer > 0))
    );
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UX_Coupon_Code' AND object_id = OBJECT_ID(N'dbo.MST_Coupon'))
    CREATE UNIQUE INDEX UX_Coupon_Code ON dbo.MST_Coupon (Code) WHERE IsDeleted = 0;
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Coupon_Live' AND object_id = OBJECT_ID(N'dbo.MST_Coupon'))
    CREATE INDEX IX_Coupon_Live ON dbo.MST_Coupon (IsActive, ValidFrom, ValidUntil) WHERE IsDeleted = 0;

IF OBJECT_ID(N'dbo.MST_CouponModule', N'U') IS NULL
    CREATE TABLE dbo.MST_CouponModule
    (
        CouponModuleId       int IDENTITY(1,1) NOT NULL CONSTRAINT PK_MST_CouponModule PRIMARY KEY,
        CouponId             int     NOT NULL CONSTRAINT FK_CouponModule_Coupon REFERENCES dbo.MST_Coupon (CouponId),
        Module               tinyint NOT NULL,                    -- 1 My Bookings, 2 Passes, 3 Membership
        CONSTRAINT UX_CouponModule UNIQUE (CouponId, Module),
        CONSTRAINT CK_CouponModule_Module CHECK (Module IN (1, 2, 3))
    );

IF OBJECT_ID(N'dbo.TRN_CouponRedemption', N'U') IS NULL
    CREATE TABLE dbo.TRN_CouponRedemption
    (
        RedemptionId         bigint IDENTITY(1,1) NOT NULL CONSTRAINT PK_TRN_CouponRedemption PRIMARY KEY,
        CouponId             int              NOT NULL CONSTRAINT FK_CouponRedemption_Coupon REFERENCES dbo.MST_Coupon (CouponId),
        CustomerId           bigint           NOT NULL CONSTRAINT FK_CouponRedemption_Customer REFERENCES dbo.MST_User (UserId),
        Module               tinyint          NOT NULL,
        BookingId            bigint           NULL CONSTRAINT FK_CouponRedemption_Booking REFERENCES dbo.TRN_Booking (BookingId),
        CustomerPassId       bigint           NULL CONSTRAINT FK_CouponRedemption_Pass REFERENCES dbo.TRN_CustomerPass (CustomerPassId),
        PaymentId            bigint           NULL CONSTRAINT FK_CouponRedemption_Payment REFERENCES dbo.TRN_Payment (PaymentId),
        PassPlanId           int              NULL,
        PurchaseKey          uniqueidentifier NOT NULL,           -- one purchase (all days of a multi-day booking)
        BaseAmount           decimal(18,2)    NOT NULL,
        DiscountAmount       decimal(18,2)    NOT NULL,
        CouponCode           nvarchar(30)     NOT NULL,
        CreatedUtc           datetime2(3)     NOT NULL,
        CONSTRAINT CK_CouponRedemption_Module CHECK (Module IN (1, 2, 3))
    );
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_CouponRedemption_Coupon' AND object_id = OBJECT_ID(N'dbo.TRN_CouponRedemption'))
    CREATE INDEX IX_CouponRedemption_Coupon ON dbo.TRN_CouponRedemption (CouponId) INCLUDE (CustomerId, PurchaseKey, DiscountAmount);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_CouponRedemption_Customer' AND object_id = OBJECT_ID(N'dbo.TRN_CouponRedemption'))
    CREATE INDEX IX_CouponRedemption_Customer ON dbo.TRN_CouponRedemption (CustomerId);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_CouponRedemption_Booking' AND object_id = OBJECT_ID(N'dbo.TRN_CouponRedemption'))
    CREATE INDEX IX_CouponRedemption_Booking ON dbo.TRN_CouponRedemption (BookingId) WHERE BookingId IS NOT NULL;
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_CouponRedemption_Pass' AND object_id = OBJECT_ID(N'dbo.TRN_CouponRedemption'))
    CREATE INDEX IX_CouponRedemption_Pass ON dbo.TRN_CouponRedemption (CustomerPassId) WHERE CustomerPassId IS NOT NULL;
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_CouponRedemption_Payment' AND object_id = OBJECT_ID(N'dbo.TRN_CouponRedemption'))
    CREATE INDEX IX_CouponRedemption_Payment ON dbo.TRN_CouponRedemption (PaymentId) WHERE PaymentId IS NOT NULL;

COMMIT TRANSACTION;
PRINT 'Coupons migration applied.';
