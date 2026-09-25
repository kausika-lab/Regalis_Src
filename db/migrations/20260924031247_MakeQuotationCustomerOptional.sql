BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260924031247_MakeQuotationCustomerOptional'
)
BEGIN
    DECLARE @var0 sysname;
    SELECT @var0 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[TRN_Quotation]') AND [c].[name] = N'CustomerId');
    IF @var0 IS NOT NULL EXEC(N'ALTER TABLE [TRN_Quotation] DROP CONSTRAINT [' + @var0 + '];');
    ALTER TABLE [TRN_Quotation] ALTER COLUMN [CustomerId] bigint NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260924031247_MakeQuotationCustomerOptional'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260924031247_MakeQuotationCustomerOptional', N'8.0.19');
END;
GO

COMMIT;
GO

