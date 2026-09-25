BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260924121122_AddItemMasterSalesFields'
)
BEGIN
    ALTER TABLE [TRN_QuotationLine] ADD [InventoryItemId] int NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260924121122_AddItemMasterSalesFields'
)
BEGIN
    ALTER TABLE [TRN_QuotationLine] ADD [IsNonTaxable] bit NOT NULL DEFAULT CAST(0 AS bit);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260924121122_AddItemMasterSalesFields'
)
BEGIN
    ALTER TABLE [TRN_QuotationLine] ADD [ItemType] tinyint NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260924121122_AddItemMasterSalesFields'
)
BEGIN
    ALTER TABLE [TRN_QuotationLine] ADD [Unit] nvarchar(20) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260924121122_AddItemMasterSalesFields'
)
BEGIN
    ALTER TABLE [MST_InventoryItem] ADD [HsnSacCode] nvarchar(20) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260924121122_AddItemMasterSalesFields'
)
BEGIN
    ALTER TABLE [MST_InventoryItem] ADD [IsNonTaxable] bit NOT NULL DEFAULT CAST(1 AS bit);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260924121122_AddItemMasterSalesFields'
)
BEGIN
    ALTER TABLE [MST_InventoryItem] ADD [ItemType] tinyint NOT NULL DEFAULT CAST(1 AS tinyint);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260924121122_AddItemMasterSalesFields'
)
BEGIN
    ALTER TABLE [MST_InventoryItem] ADD [SellingRate] decimal(14,2) NOT NULL DEFAULT 0.0;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260924121122_AddItemMasterSalesFields'
)
BEGIN
    ALTER TABLE [MST_InventoryItem] ADD [TaxPercent] decimal(5,2) NOT NULL DEFAULT 0.0;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260924121122_AddItemMasterSalesFields'
)
BEGIN
    EXEC(N'CREATE INDEX [IX_QuotationLine_InventoryItem] ON [TRN_QuotationLine] ([InventoryItemId]) WHERE [InventoryItemId] IS NOT NULL');
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260924121122_AddItemMasterSalesFields'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260924121122_AddItemMasterSalesFields', N'8.0.19');
END;
GO

COMMIT;
GO

