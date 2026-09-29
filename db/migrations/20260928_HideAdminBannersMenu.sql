-- Removes Engagement > Banners (/admin/banners) from the admin sidebar.
--
-- Hidden with IsVisible = 0, not soft-deleted: DatabaseSeeder matches menus by name
-- and skips soft-deleted rows, so a deleted row would come back as a duplicate.
-- Only the admin menu entry is hidden: existing banners (TRN/MST banner rows) are
-- untouched, and the customer home-screen carousel keeps showing the live ones.
-- Undo: set IsVisible back to 1. Safe to run more than once. Run with:
--   sqlcmd -S localhost -d RegalisWorkSpace_UAT -E -C -I -b -i 20260928_HideAdminBannersMenu.sql

UPDATE MST_Menu SET IsVisible = 0
WHERE MenuCode = N'ADM_BANNERS' AND PortalType = 2;

SELECT MenuId, MenuCode, DisplayName, IsVisible FROM MST_Menu WHERE MenuCode = N'ADM_BANNERS';
