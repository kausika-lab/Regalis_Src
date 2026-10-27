-- Rollback of 20261025000000_MaintenanceChecklistTasksSetting.sql.
-- Removes the monthly-sheet task list. Ticks already made stay: they are ordinary
-- maintenance work items and still show on the work list.
--   sqlcmd -S localhost -d RegalisWorkSpace_UAT -E -C -I -b -i 20261025000000_MaintenanceChecklistTasksSetting_Rollback.sql

DELETE FROM MST_AppSetting WHERE SettingKey = N'Maintenance.ChecklistTasks';

SELECT COUNT(*) AS RemainingRows FROM MST_AppSetting WHERE SettingKey = N'Maintenance.ChecklistTasks';
