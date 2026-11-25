-- Rollback of 20261123120000_Offers.sql. Run it together with going back to an API without the offers kit.
-- WARNING: drops the offer tables, so the offers and the record of who used them are lost. Bookings,
-- passes and invoices that were bought with an offer KEEP their discounted amounts (their
-- DiscountAmount / Price / TotalAmount columns are existing columns and are not touched).
-- Idempotent; one transaction. ASCII only.
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
SET NOCOUNT ON;
GO
SET XACT_ABORT ON;
BEGIN TRANSACTION;

DELETE rm FROM dbo.MAP_RoleMenu rm JOIN dbo.MST_Menu m ON m.MenuId = rm.MenuId WHERE m.MenuCode = N'ADM_OFFERS';
DELETE FROM dbo.MST_Menu WHERE MenuCode = N'ADM_OFFERS';
-- The Sales band is left: CRM gives it to the same roles.

DELETE rp FROM dbo.MAP_RolePermission rp JOIN dbo.MST_Permission p ON p.PermissionId = rp.PermissionId WHERE p.PermissionCode LIKE N'OFFER.%';
DELETE FROM dbo.MST_Permission WHERE PermissionCode LIKE N'OFFER.%';

IF OBJECT_ID(N'dbo.TRN_OfferRedemption', N'U') IS NOT NULL DROP TABLE dbo.TRN_OfferRedemption;
IF OBJECT_ID(N'dbo.TRN_OfferImage', N'U') IS NOT NULL DROP TABLE dbo.TRN_OfferImage;
IF OBJECT_ID(N'dbo.MST_OfferScope', N'U') IS NOT NULL DROP TABLE dbo.MST_OfferScope;
IF OBJECT_ID(N'dbo.MST_OfferModule', N'U') IS NOT NULL DROP TABLE dbo.MST_OfferModule;
IF OBJECT_ID(N'dbo.MST_Offer', N'U') IS NOT NULL DROP TABLE dbo.MST_Offer;

COMMIT TRANSACTION;
PRINT 'Offers rolled back.';
