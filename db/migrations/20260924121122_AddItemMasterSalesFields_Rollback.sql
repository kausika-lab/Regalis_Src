BEGIN TRANSACTION;
GO

IF EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260924121122_AddItemMasterSalesFields'
)
BEGIN
    DROP INDEX [IX_QuotationLine_InventoryItem] ON [TRN_QuotationLine];
END;
GO

IF EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260924121122_AddItemMasterSalesFields'
)
BEGIN
    DECLARE @var0 sysname;
    SELECT @var0 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[TRN_QuotationLine]') AND [c].[name] = N'InventoryItemId');
    IF @var0 IS NOT NULL EXEC(N'ALTER TABLE [TRN_QuotationLine] DROP CONSTRAINT [' + @var0 + '];');
    ALTER TABLE [TRN_QuotationLine] DROP COLUMN [InventoryItemId];
END;
GO

IF EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260924121122_AddItemMasterSalesFields'
)
BEGIN
    DECLARE @var1 sysname;
    SELECT @var1 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[TRN_QuotationLine]') AND [c].[name] = N'IsNonTaxable');
    IF @var1 IS NOT NULL EXEC(N'ALTER TABLE [TRN_QuotationLine] DROP CONSTRAINT [' + @var1 + '];');
    ALTER TABLE [TRN_QuotationLine] DROP COLUMN [IsNonTaxable];
END;
GO

IF EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260924121122_AddItemMasterSalesFields'
)
BEGIN
    DECLARE @var2 sysname;
    SELECT @var2 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[TRN_QuotationLine]') AND [c].[name] = N'ItemType');
    IF @var2 IS NOT NULL EXEC(N'ALTER TABLE [TRN_QuotationLine] DROP CONSTRAINT [' + @var2 + '];');
    ALTER TABLE [TRN_QuotationLine] DROP COLUMN [ItemType];
END;
GO

IF EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260924121122_AddItemMasterSalesFields'
)
BEGIN
    DECLARE @var3 sysname;
    SELECT @var3 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[TRN_QuotationLine]') AND [c].[name] = N'Unit');
    IF @var3 IS NOT NULL EXEC(N'ALTER TABLE [TRN_QuotationLine] DROP CONSTRAINT [' + @var3 + '];');
    ALTER TABLE [TRN_QuotationLine] DROP COLUMN [Unit];
END;
GO

IF EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260924121122_AddItemMasterSalesFields'
)
BEGIN
    DECLARE @var4 sysname;
    SELECT @var4 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[MST_InventoryItem]') AND [c].[name] = N'HsnSacCode');
    IF @var4 IS NOT NULL EXEC(N'ALTER TABLE [MST_InventoryItem] DROP CONSTRAINT [' + @var4 + '];');
    ALTER TABLE [MST_InventoryItem] DROP COLUMN [HsnSacCode];
END;
GO

IF EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260924121122_AddItemMasterSalesFields'
)
BEGIN
    DECLARE @var5 sysname;
    SELECT @var5 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[MST_InventoryItem]') AND [c].[name] = N'IsNonTaxable');
    IF @var5 IS NOT NULL EXEC(N'ALTER TABLE [MST_InventoryItem] DROP CONSTRAINT [' + @var5 + '];');
    ALTER TABLE [MST_InventoryItem] DROP COLUMN [IsNonTaxable];
END;
GO

IF EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260924121122_AddItemMasterSalesFields'
)
BEGIN
    DECLARE @var6 sysname;
    SELECT @var6 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[MST_InventoryItem]') AND [c].[name] = N'ItemType');
    IF @var6 IS NOT NULL EXEC(N'ALTER TABLE [MST_InventoryItem] DROP CONSTRAINT [' + @var6 + '];');
    ALTER TABLE [MST_InventoryItem] DROP COLUMN [ItemType];
END;
GO

IF EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260924121122_AddItemMasterSalesFields'
)
BEGIN
    DECLARE @var7 sysname;
    SELECT @var7 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[MST_InventoryItem]') AND [c].[name] = N'SellingRate');
    IF @var7 IS NOT NULL EXEC(N'ALTER TABLE [MST_InventoryItem] DROP CONSTRAINT [' + @var7 + '];');
    ALTER TABLE [MST_InventoryItem] DROP COLUMN [SellingRate];
END;
GO

IF EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260924121122_AddItemMasterSalesFields'
)
BEGIN
    DECLARE @var8 sysname;
    SELECT @var8 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[MST_InventoryItem]') AND [c].[name] = N'TaxPercent');
    IF @var8 IS NOT NULL EXEC(N'ALTER TABLE [MST_InventoryItem] DROP CONSTRAINT [' + @var8 + '];');
    ALTER TABLE [MST_InventoryItem] DROP COLUMN [TaxPercent];
END;
GO

IF EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260924121122_AddItemMasterSalesFields'
)
BEGIN
    DELETE FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260924121122_AddItemMasterSalesFields';
END;
GO

COMMIT;
GO

