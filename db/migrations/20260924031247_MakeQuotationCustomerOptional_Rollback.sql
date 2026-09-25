BEGIN TRANSACTION;
GO

IF EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260924031247_MakeQuotationCustomerOptional'
)
BEGIN
    DROP INDEX [IX_Quotation_Customer_Date] ON [TRN_Quotation];
    DECLARE @var0 sysname;
    SELECT @var0 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[TRN_Quotation]') AND [c].[name] = N'CustomerId');
    IF @var0 IS NOT NULL EXEC(N'ALTER TABLE [TRN_Quotation] DROP CONSTRAINT [' + @var0 + '];');
    EXEC(N'UPDATE [TRN_Quotation] SET [CustomerId] = CAST(0 AS bigint) WHERE [CustomerId] IS NULL');
    ALTER TABLE [TRN_Quotation] ALTER COLUMN [CustomerId] bigint NOT NULL;
    ALTER TABLE [TRN_Quotation] ADD DEFAULT CAST(0 AS bigint) FOR [CustomerId];
    CREATE INDEX [IX_Quotation_Customer_Date] ON [TRN_Quotation] ([CustomerId], [QuotationDate]);
END;
GO

IF EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260924031247_MakeQuotationCustomerOptional'
)
BEGIN
    DELETE FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260924031247_MakeQuotationCustomerOptional';
END;
GO

COMMIT;
GO

