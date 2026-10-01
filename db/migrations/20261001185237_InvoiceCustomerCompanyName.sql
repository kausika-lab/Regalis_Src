BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261001185237_InvoiceCustomerCompanyName'
)
BEGIN
    ALTER TABLE [TRN_Invoice] ADD [CustomerCompanyName] nvarchar(200) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261001185237_InvoiceCustomerCompanyName'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261001185237_InvoiceCustomerCompanyName', N'8.0.19');
END;
GO

COMMIT;
GO

