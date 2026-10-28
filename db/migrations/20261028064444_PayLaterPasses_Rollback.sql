BEGIN TRANSACTION;
GO

IF EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261028064444_PayLaterPasses'
)
BEGIN
    ALTER TABLE [TRN_CashCollection] DROP CONSTRAINT [FK_TRN_CashCollection_TRN_CustomerPass_CustomerPassId];
END;
GO

IF EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261028064444_PayLaterPasses'
)
BEGIN
    DROP INDEX [IX_CustomerPass_PayLater_Due] ON [TRN_CustomerPass];
END;
GO

IF EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261028064444_PayLaterPasses'
)
BEGIN
    ALTER TABLE [TRN_CustomerPass] DROP CONSTRAINT [CK_CustomerPass_AmountPaid];
END;
GO

IF EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261028064444_PayLaterPasses'
)
BEGIN
    DROP INDEX [IX_CashCollection_CustomerPass] ON [TRN_CashCollection];
END;
GO

IF EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261028064444_PayLaterPasses'
)
BEGIN
    ALTER TABLE [TRN_CashCollection] DROP CONSTRAINT [CK_CashCollection_Target];
END;
GO

IF EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261028064444_PayLaterPasses'
)
BEGIN
    DECLARE @var0 sysname;
    SELECT @var0 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[TRN_CustomerPass]') AND [c].[name] = N'AmountPaid');
    IF @var0 IS NOT NULL EXEC(N'ALTER TABLE [TRN_CustomerPass] DROP CONSTRAINT [' + @var0 + '];');
    ALTER TABLE [TRN_CustomerPass] DROP COLUMN [AmountPaid];
END;
GO

IF EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261028064444_PayLaterPasses'
)
BEGIN
    DECLARE @var1 sysname;
    SELECT @var1 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[TRN_CustomerPass]') AND [c].[name] = N'IsPayLater');
    IF @var1 IS NOT NULL EXEC(N'ALTER TABLE [TRN_CustomerPass] DROP CONSTRAINT [' + @var1 + '];');
    ALTER TABLE [TRN_CustomerPass] DROP COLUMN [IsPayLater];
END;
GO

IF EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261028064444_PayLaterPasses'
)
BEGIN
    DECLARE @var2 sysname;
    SELECT @var2 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[TRN_CustomerPass]') AND [c].[name] = N'PaymentDueUtc');
    IF @var2 IS NOT NULL EXEC(N'ALTER TABLE [TRN_CustomerPass] DROP CONSTRAINT [' + @var2 + '];');
    ALTER TABLE [TRN_CustomerPass] DROP COLUMN [PaymentDueUtc];
END;
GO

IF EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261028064444_PayLaterPasses'
)
BEGIN
    DECLARE @var3 sysname;
    SELECT @var3 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[TRN_CashCollection]') AND [c].[name] = N'CustomerPassId');
    IF @var3 IS NOT NULL EXEC(N'ALTER TABLE [TRN_CashCollection] DROP CONSTRAINT [' + @var3 + '];');
    ALTER TABLE [TRN_CashCollection] DROP COLUMN [CustomerPassId];
END;
GO

IF EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261028064444_PayLaterPasses'
)
BEGIN
    DROP INDEX [IX_CashCollection_Booking] ON [TRN_CashCollection];
    DECLARE @var4 sysname;
    SELECT @var4 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[TRN_CashCollection]') AND [c].[name] = N'BookingId');
    IF @var4 IS NOT NULL EXEC(N'ALTER TABLE [TRN_CashCollection] DROP CONSTRAINT [' + @var4 + '];');
    EXEC(N'UPDATE [TRN_CashCollection] SET [BookingId] = CAST(0 AS bigint) WHERE [BookingId] IS NULL');
    ALTER TABLE [TRN_CashCollection] ALTER COLUMN [BookingId] bigint NOT NULL;
    ALTER TABLE [TRN_CashCollection] ADD DEFAULT CAST(0 AS bigint) FOR [BookingId];
    CREATE INDEX [IX_CashCollection_Booking] ON [TRN_CashCollection] ([BookingId]);
END;
GO

IF EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261028064444_PayLaterPasses'
)
BEGIN
    DELETE FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261028064444_PayLaterPasses';
END;
GO

COMMIT;
GO

