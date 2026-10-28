BEGIN TRANSACTION;
GO

IF EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261027182059_PayLaterCash'
)
BEGIN
    DROP TABLE [TRN_CashCollection];
END;
GO

IF EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261027182059_PayLaterCash'
)
BEGIN
    DROP TABLE [TRN_CashRegisterSession];
END;
GO

IF EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261027182059_PayLaterCash'
)
BEGIN
    DROP INDEX [IX_Booking_PayLater_Due] ON [TRN_Booking];
END;
GO

IF EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261027182059_PayLaterCash'
)
BEGIN
    ALTER TABLE [TRN_Booking] DROP CONSTRAINT [CK_Booking_AmountPaid];
END;
GO

IF EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261027182059_PayLaterCash'
)
BEGIN
    DECLARE @var0 sysname;
    SELECT @var0 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[TRN_Booking]') AND [c].[name] = N'AmountPaid');
    IF @var0 IS NOT NULL EXEC(N'ALTER TABLE [TRN_Booking] DROP CONSTRAINT [' + @var0 + '];');
    ALTER TABLE [TRN_Booking] DROP COLUMN [AmountPaid];
END;
GO

IF EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261027182059_PayLaterCash'
)
BEGIN
    DECLARE @var1 sysname;
    SELECT @var1 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[TRN_Booking]') AND [c].[name] = N'IsPayLater');
    IF @var1 IS NOT NULL EXEC(N'ALTER TABLE [TRN_Booking] DROP CONSTRAINT [' + @var1 + '];');
    ALTER TABLE [TRN_Booking] DROP COLUMN [IsPayLater];
END;
GO

IF EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261027182059_PayLaterCash'
)
BEGIN
    DECLARE @var2 sysname;
    SELECT @var2 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[TRN_Booking]') AND [c].[name] = N'PayLaterReminderSentUtc');
    IF @var2 IS NOT NULL EXEC(N'ALTER TABLE [TRN_Booking] DROP CONSTRAINT [' + @var2 + '];');
    ALTER TABLE [TRN_Booking] DROP COLUMN [PayLaterReminderSentUtc];
END;
GO

IF EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261027182059_PayLaterCash'
)
BEGIN
    DECLARE @var3 sysname;
    SELECT @var3 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[TRN_Booking]') AND [c].[name] = N'PaymentDueUtc');
    IF @var3 IS NOT NULL EXEC(N'ALTER TABLE [TRN_Booking] DROP CONSTRAINT [' + @var3 + '];');
    ALTER TABLE [TRN_Booking] DROP COLUMN [PaymentDueUtc];
END;
GO

IF EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261027182059_PayLaterCash'
)
BEGIN
    DECLARE @var4 sysname;
    SELECT @var4 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[MST_User]') AND [c].[name] = N'PayLaterAllowed');
    IF @var4 IS NOT NULL EXEC(N'ALTER TABLE [MST_User] DROP CONSTRAINT [' + @var4 + '];');
    ALTER TABLE [MST_User] DROP COLUMN [PayLaterAllowed];
END;
GO

IF EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261027182059_PayLaterCash'
)
BEGIN
    DELETE FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261027182059_PayLaterCash';
END;
GO

COMMIT;
GO

