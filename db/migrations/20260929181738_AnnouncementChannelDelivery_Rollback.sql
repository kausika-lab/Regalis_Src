BEGIN TRANSACTION;
GO

IF EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260929181738_AnnouncementChannelDelivery'
)
BEGIN
    DECLARE @var0 sysname;
    SELECT @var0 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[TRN_Announcement]') AND [c].[name] = N'ChannelsDeliveredUtc');
    IF @var0 IS NOT NULL EXEC(N'ALTER TABLE [TRN_Announcement] DROP CONSTRAINT [' + @var0 + '];');
    ALTER TABLE [TRN_Announcement] DROP COLUMN [ChannelsDeliveredUtc];
END;
GO

IF EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260929181738_AnnouncementChannelDelivery'
)
BEGIN
    DECLARE @var1 sysname;
    SELECT @var1 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[TRN_Announcement]') AND [c].[name] = N'DeliveryNote');
    IF @var1 IS NOT NULL EXEC(N'ALTER TABLE [TRN_Announcement] DROP CONSTRAINT [' + @var1 + '];');
    ALTER TABLE [TRN_Announcement] DROP COLUMN [DeliveryNote];
END;
GO

IF EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260929181738_AnnouncementChannelDelivery'
)
BEGIN
    DELETE FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260929181738_AnnouncementChannelDelivery';
END;
GO

COMMIT;
GO

