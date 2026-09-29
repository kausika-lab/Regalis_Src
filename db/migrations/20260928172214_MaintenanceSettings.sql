-- Maintenance checklist: the "Maintenance" settings rows.
--
-- Run AFTER 20260928172213_AddMaintenanceChecklist.sql. UAT runs with
-- Database__SeedOnStartup=false, so the seeder will not add these rows; this script
-- does. Safe to run more than once: each setting is inserted only when its key is missing.
--
-- ASCII only on purpose (see sqlcmd notes).
--   sqlcmd -S localhost -d RegalisWorkSpace_UAT -E -C -I -b -i 20260928172214_MaintenanceSettings.sql

SET XACT_ABORT ON;
BEGIN TRANSACTION;

DECLARE @now datetime2(3) = SYSUTCDATETIME();

DECLARE @Settings TABLE (SettingKey nvarchar(100), SettingValue nvarchar(max), DataType nvarchar(20), Description nvarchar(500));
INSERT INTO @Settings VALUES
 (N'Maintenance.Categories', N'Cleaning,Plumbing,Electrical,Laundry,Other', N'string',
  N'Maintenance work categories, comma-separated'),
 (N'Maintenance.Areas', N'Lobby,Reception,Pantry,Washroom 1F,Washroom 2F,Floor 1,Floor 2,Corridor,Terrace', N'string',
  N'Common areas for maintenance work, comma-separated; bookable workspaces are added automatically'),
 (N'Maintenance.OverdueGraceMinutes', N'0', N'int',
  N'Minutes after the due time before a task is marked Overdue'),
 (N'Maintenance.ShiftHours', N'Morning=06:00-14:00;Evening=14:00-22:00;Night=22:00-06:00', N'string',
  N'Housekeeper shift hours (IST), used for On duty now');

INSERT INTO MST_AppSetting (SettingKey, SettingValue, SettingGroup, DataType, Description,
                            IsEditableByAdmin, IsSecret, CreatedDate, IsActive, IsDeleted)
SELECT s.SettingKey, s.SettingValue, N'Maintenance', s.DataType, s.Description, 1, 0, @now, 1, 0
FROM @Settings s
WHERE NOT EXISTS (SELECT 1 FROM MST_AppSetting a WHERE a.SettingKey = s.SettingKey);

COMMIT TRANSACTION;
