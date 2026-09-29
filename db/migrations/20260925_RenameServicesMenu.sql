-- Renames the admin sidebar entry "Services / Coffee" to "Services".
-- Keyed on MenuCode (unique), so the seeder cannot re-create the old name.
-- Undo: set both columns back to N'Services / Coffee'. Safe to run more than once.
--   sqlcmd -S localhost -d RegalisWorkSpace_UAT -E -C -I -b -i 20260925_RenameServicesMenu.sql

UPDATE MST_Menu SET MenuName = N'Services', DisplayName = N'Services'
WHERE MenuCode = N'ADM_SERVICES';

SELECT MenuId, MenuCode, MenuName, DisplayName FROM MST_Menu WHERE MenuCode = N'ADM_SERVICES';
