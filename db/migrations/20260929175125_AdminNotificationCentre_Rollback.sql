BEGIN TRANSACTION;
GO

IF EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260929175125_AdminNotificationCentre'
)
BEGIN
    DROP TABLE [MST_AdminNotificationSetting];
END;
GO

IF EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260929175125_AdminNotificationCentre'
)
BEGIN
    DROP TABLE [TRN_AdminNotificationRead];
END;
GO

IF EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260929175125_AdminNotificationCentre'
)
BEGIN
    DROP TABLE [TRN_Announcement];
END;
GO

IF EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260929175125_AdminNotificationCentre'
)
BEGIN
    DROP TABLE [TRN_AdminNotification];
END;
GO

IF EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260929175125_AdminNotificationCentre'
)
BEGIN
    DELETE FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260929175125_AdminNotificationCentre';
END;
GO

COMMIT;
GO

