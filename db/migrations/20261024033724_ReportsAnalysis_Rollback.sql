-- Rolls back Reports & Analysis: the seven RPT_ tables, the invoice due-date index, the
-- three permissions and the five sidebar children. Schedules, favourites, preferences and
-- the export log are lost. Restore the old API binaries and index chunk as well.
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO
SET XACT_ABORT ON;
BEGIN TRANSACTION;

DELETE rm FROM MAP_RoleMenu rm JOIN MST_Menu m ON m.MenuId = rm.MenuId
WHERE m.MenuCode IN (N'ADM_REPORTS_OVERVIEW', N'ADM_REPORTS_REVENUE', N'ADM_REPORTS_OCCUPANCY', N'ADM_REPORTS_CUSTOMERS', N'ADM_REPORTS_LIBRARY');
DELETE FROM MST_Menu
WHERE MenuCode IN (N'ADM_REPORTS_OVERVIEW', N'ADM_REPORTS_REVENUE', N'ADM_REPORTS_OCCUPANCY', N'ADM_REPORTS_CUSTOMERS', N'ADM_REPORTS_LIBRARY');

DELETE rp FROM MAP_RolePermission rp JOIN MST_Permission p ON p.PermissionId = rp.PermissionId
WHERE p.PermissionCode IN (N'REPORT.FINANCE', N'REPORT.CUSTOMERS', N'REPORT.MANAGE');
UPDATE MST_Menu SET PermissionId = NULL
WHERE PermissionId IN (SELECT PermissionId FROM MST_Permission WHERE PermissionCode IN (N'REPORT.FINANCE', N'REPORT.CUSTOMERS', N'REPORT.MANAGE'));
DELETE FROM MST_Permission WHERE PermissionCode IN (N'REPORT.FINANCE', N'REPORT.CUSTOMERS', N'REPORT.MANAGE');

DROP TABLE IF EXISTS RPT_ScheduleRun;
DROP TABLE IF EXISTS RPT_Schedule;
DROP TABLE IF EXISTS RPT_ExportJob;
DROP TABLE IF EXISTS RPT_Export;
DROP TABLE IF EXISTS RPT_Preference;
DROP TABLE IF EXISTS RPT_Favourite;
DROP TABLE IF EXISTS RPT_DailyMetric;
IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Invoice_DueDate_Status' AND object_id = OBJECT_ID(N'TRN_Invoice'))
    DROP INDEX IX_Invoice_DueDate_Status ON TRN_Invoice;
DELETE FROM __EFMigrationsHistory WHERE MigrationId = N'20261024033724_ReportsAnalysis';

COMMIT TRANSACTION;
