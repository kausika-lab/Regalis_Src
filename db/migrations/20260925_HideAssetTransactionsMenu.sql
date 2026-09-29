-- Removes Assets > Transactions (/admin/inventory/transactions) from the admin sidebar.
--
-- Hidden with IsVisible = 0, not soft-deleted: DatabaseSeeder matches menus by name
-- and skips soft-deleted rows, so a deleted row would come back as a duplicate.
-- The route itself still works if typed in. Undo: set IsVisible back to 1.
-- Safe to run more than once. Run with:
--   sqlcmd -S localhost -d RegalisWorkSpace_UAT -E -C -I -b -i 20260925_HideAssetTransactionsMenu.sql

UPDATE MST_Menu SET IsVisible = 0
WHERE MenuCode = N'ADM_INVENTORY_TXNS' AND PortalType = 2;

SELECT MenuId, MenuCode, DisplayName, IsVisible FROM MST_Menu WHERE MenuCode = N'ADM_INVENTORY_TXNS';
