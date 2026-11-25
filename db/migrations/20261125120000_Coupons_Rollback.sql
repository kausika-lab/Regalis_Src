-- Rollback of 20261125120000_Coupons.sql. Run it together with going back to an API without the coupons kit.
-- WARNING: drops the coupon tables, so the coupons and the record of who used them are lost. Bookings,
-- passes and invoices that were bought with a coupon KEEP their discounted amounts (their
-- DiscountAmount / TotalAmount columns are existing columns and are not touched).
-- Idempotent; one transaction. ASCII only.
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
SET NOCOUNT ON;
GO
SET XACT_ABORT ON;
BEGIN TRANSACTION;

IF OBJECT_ID(N'dbo.TRN_CouponRedemption', N'U') IS NOT NULL DROP TABLE dbo.TRN_CouponRedemption;
IF OBJECT_ID(N'dbo.MST_CouponModule', N'U') IS NOT NULL DROP TABLE dbo.MST_CouponModule;
IF OBJECT_ID(N'dbo.MST_Coupon', N'U') IS NOT NULL DROP TABLE dbo.MST_Coupon;

COMMIT TRANSACTION;
PRINT 'Coupons rolled back.';
