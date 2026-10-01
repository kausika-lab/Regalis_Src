BEGIN TRANSACTION;
GO

IF EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261001185237_InvoiceCustomerCompanyName'
)
BEGIN
    DECLARE @var0 sysname;
    SELECT @var0 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[TRN_Invoice]') AND [c].[name] = N'CustomerCompanyName');
    IF @var0 IS NOT NULL EXEC(N'ALTER TABLE [TRN_Invoice] DROP CONSTRAINT [' + @var0 + '];');
    ALTER TABLE [TRN_Invoice] DROP COLUMN [CustomerCompanyName];
END;
GO

IF EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261001185237_InvoiceCustomerCompanyName'
)
BEGIN
    DELETE FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261001185237_InvoiceCustomerCompanyName';
END;
GO

COMMIT;
GO

