BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260929181738_AnnouncementChannelDelivery'
)
BEGIN
    ALTER TABLE [TRN_Announcement] ADD [ChannelsDeliveredUtc] datetime2(3) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260929181738_AnnouncementChannelDelivery'
)
BEGIN
    ALTER TABLE [TRN_Announcement] ADD [DeliveryNote] nvarchar(300) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260929181738_AnnouncementChannelDelivery'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260929181738_AnnouncementChannelDelivery', N'8.0.19');
END;
GO

COMMIT;
GO

