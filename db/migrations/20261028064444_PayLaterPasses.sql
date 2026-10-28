BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261028064444_PayLaterPasses'
)
BEGIN
    ALTER TABLE [TRN_CustomerPass] ADD [AmountPaid] decimal(18,2) NOT NULL DEFAULT 0.0;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261028064444_PayLaterPasses'
)
BEGIN
    ALTER TABLE [TRN_CustomerPass] ADD [IsPayLater] bit NOT NULL DEFAULT CAST(0 AS bit);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261028064444_PayLaterPasses'
)
BEGIN
    ALTER TABLE [TRN_CustomerPass] ADD [PaymentDueUtc] datetime2(3) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261028064444_PayLaterPasses'
)
BEGIN
    DECLARE @var0 sysname;
    SELECT @var0 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[TRN_CashCollection]') AND [c].[name] = N'BookingId');
    IF @var0 IS NOT NULL EXEC(N'ALTER TABLE [TRN_CashCollection] DROP CONSTRAINT [' + @var0 + '];');
    ALTER TABLE [TRN_CashCollection] ALTER COLUMN [BookingId] bigint NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261028064444_PayLaterPasses'
)
BEGIN
    ALTER TABLE [TRN_CashCollection] ADD [CustomerPassId] bigint NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261028064444_PayLaterPasses'
)
BEGIN
    EXEC(N'CREATE INDEX [IX_CustomerPass_PayLater_Due] ON [TRN_CustomerPass] ([PaymentDueUtc]) INCLUDE ([CustomerId], [Status], [TotalAmount], [AmountPaid]) WHERE [IsPayLater] = 1 AND [IsDeleted] = 0');
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261028064444_PayLaterPasses'
)
BEGIN
    EXEC(N'ALTER TABLE [TRN_CustomerPass] ADD CONSTRAINT [CK_CustomerPass_AmountPaid] CHECK ([AmountPaid] >= 0)');
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261028064444_PayLaterPasses'
)
BEGIN
    CREATE INDEX [IX_CashCollection_CustomerPass] ON [TRN_CashCollection] ([CustomerPassId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261028064444_PayLaterPasses'
)
BEGIN
    EXEC(N'ALTER TABLE [TRN_CashCollection] ADD CONSTRAINT [CK_CashCollection_Target] CHECK (([BookingId] IS NOT NULL AND [CustomerPassId] IS NULL) OR ([BookingId] IS NULL AND [CustomerPassId] IS NOT NULL))');
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261028064444_PayLaterPasses'
)
BEGIN
    ALTER TABLE [TRN_CashCollection] ADD CONSTRAINT [FK_TRN_CashCollection_TRN_CustomerPass_CustomerPassId] FOREIGN KEY ([CustomerPassId]) REFERENCES [TRN_CustomerPass] ([CustomerPassId]) ON DELETE NO ACTION;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261028064444_PayLaterPasses'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261028064444_PayLaterPasses', N'8.0.19');
END;
GO

COMMIT;
GO

