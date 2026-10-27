-- Adds Maintenance.ChecklistTasks: the recurring tasks shown on the monthly sheet
-- (Admin -> Operations -> Maintenance -> Checklist). JSON array, starts empty; the
-- page edits it through PUT /api/settings, so it needs the row to exist first, and
-- IsEditableByAdmin must be 1 (the API refuses to save a non-editable key). It
-- therefore also shows on the Settings screen; the description says where to edit it.
-- Each tick on the sheet is an ordinary maintenance work item; this row only holds
-- the task list. Data only; safe to run more than once.
-- UAT runs with Database__SeedOnStartup=false, so the seeder will not add this row.
--   sqlcmd -S localhost -d RegalisWorkSpace_UAT -E -C -I -b -i 20261025000000_MaintenanceChecklistTasksSetting.sql

INSERT INTO MST_AppSetting (SettingKey, SettingValue, SettingGroup, DataType, Description,
                            IsEditableByAdmin, IsSecret, CreatedDate, IsActive, IsDeleted)
SELECT N'Maintenance.ChecklistTasks', N'[]', N'Maintenance', N'json',
       N'Monthly checklist tasks. Edit them on Maintenance -> Checklist, not here.', 1, 0, SYSUTCDATETIME(), 1, 0
WHERE NOT EXISTS (SELECT 1 FROM MST_AppSetting WHERE SettingKey = N'Maintenance.ChecklistTasks');

SELECT SettingKey, SettingValue, SettingGroup, IsEditableByAdmin FROM MST_AppSetting WHERE SettingKey = N'Maintenance.ChecklistTasks';
