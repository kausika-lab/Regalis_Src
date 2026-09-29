BEGIN TRANSACTION;
GO

DROP TABLE [MST_HousekeeperProfile];
GO

DROP TABLE [TRN_MaintenanceWorkItem];
GO

DELETE FROM [__EFMigrationsHistory]
WHERE [MigrationId] = N'20260928172213_AddMaintenanceChecklist';
GO

COMMIT;
GO

