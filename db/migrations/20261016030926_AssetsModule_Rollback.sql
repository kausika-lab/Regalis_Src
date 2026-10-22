-- Rollback of 20261016030926_AssetsModule. Run with: sqlcmd -I -b -i <this file>
-- Emergency use only - the backup taken by deploy-assets.cmd is the clean way back.
-- What it does before dropping the new tables and columns:
--   * puts the opening quantities the migration moved onto the ledger back onto the
--     items, and removes those ledger rows, so the old screens show the same stock;
--   * points the Assets menu back at the old /admin/inventory/* routes.
-- What it cannot carry back: receipts, movements, repairs, counts and documents
-- recorded on the new screens (their tables are dropped). Their ledger rows stay in
-- TRN_InventoryTransaction, so every stock figure still adds up; asset units created
-- after the deploy become ordinary items with a stock of 1 - make them inactive.
-- The Assets permissions and settings rows are left in place; the old code ignores them.
BEGIN TRANSACTION;
GO

IF EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261016030926_AssetsModule'
)
BEGIN
    DECLARE @legacy TABLE ([ReceiptId] bigint PRIMARY KEY);
    INSERT INTO @legacy SELECT [GoodsReceiptId] FROM [TRN_GoodsReceipt] WHERE [Source] = 6;

    UPDATE i SET [OpeningQuantity] = i.[OpeningQuantity] + l.[Quantity]
    FROM [MST_InventoryItem] i
    JOIN [TRN_GoodsReceiptLine] l ON l.[InventoryItemId] = i.[InventoryItemId]
    WHERE l.[GoodsReceiptId] IN (SELECT [ReceiptId] FROM @legacy) AND l.[IsDeleted] = 0;

    DELETE FROM [TRN_InventoryTransaction]
    WHERE [SourceType] = 1 AND [SourceId] IN (SELECT [ReceiptId] FROM @legacy);

    UPDATE [MST_Menu] SET [Route] = N'/admin/inventory/items' WHERE [MenuCode] IN (N'ADM_INVENTORY', N'ADM_INVENTORY_ITEMS') AND [Route] = N'/admin/assets/items';
    UPDATE [MST_Menu] SET [Route] = N'/admin/inventory/incoming' WHERE [MenuCode] = N'ADM_INVENTORY_INCOMING' AND [Route] = N'/admin/assets/incoming';
    UPDATE [MST_Menu] SET [Route] = N'/admin/inventory/outgoing' WHERE [MenuCode] = N'ADM_INVENTORY_OUTGOING' AND [Route] = N'/admin/assets/outgoing';
    UPDATE [MST_Menu] SET [Route] = N'/admin/inventory/stock' WHERE [MenuCode] = N'ADM_INVENTORY_STOCK' AND [Route] = N'/admin/assets/stock';
END;
GO


IF EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261016030926_AssetsModule'
)
BEGIN
    ALTER TABLE [MST_InventoryItem] DROP CONSTRAINT [FK_MST_InventoryItem_MST_AssetCategory_AssetCategoryId];
END;
GO

IF EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261016030926_AssetsModule'
)
BEGIN
    ALTER TABLE [MST_InventoryItem] DROP CONSTRAINT [FK_MST_InventoryItem_MST_AssetLocation_KeptInLocationId];
END;
GO

IF EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261016030926_AssetsModule'
)
BEGIN
    ALTER TABLE [MST_InventoryItem] DROP CONSTRAINT [FK_MST_InventoryItem_MST_Supplier_SupplierId];
END;
GO

IF EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261016030926_AssetsModule'
)
BEGIN
    ALTER TABLE [TRN_InventoryTransaction] DROP CONSTRAINT [FK_TRN_InventoryTransaction_MST_AssetLocation_AssetLocationId];
END;
GO

IF EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261016030926_AssetsModule'
)
BEGIN
    DROP TABLE [MST_AssetDetail];
END;
GO

IF EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261016030926_AssetsModule'
)
BEGIN
    DROP TABLE [MST_CodeSequence];
END;
GO

IF EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261016030926_AssetsModule'
)
BEGIN
    DROP TABLE [TRN_AssetRepair];
END;
GO

IF EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261016030926_AssetsModule'
)
BEGIN
    DROP TABLE [TRN_GoodsIssueLine];
END;
GO

IF EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261016030926_AssetsModule'
)
BEGIN
    DROP TABLE [TRN_ItemDocument];
END;
GO

IF EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261016030926_AssetsModule'
)
BEGIN
    DROP TABLE [TRN_ItemEvent];
END;
GO

IF EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261016030926_AssetsModule'
)
BEGIN
    DROP TABLE [TRN_StockCountLine];
END;
GO

IF EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261016030926_AssetsModule'
)
BEGIN
    DROP TABLE [TRN_GoodsReceiptLine];
END;
GO

IF EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261016030926_AssetsModule'
)
BEGIN
    DROP TABLE [TRN_GoodsIssue];
END;
GO

IF EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261016030926_AssetsModule'
)
BEGIN
    DROP TABLE [TRN_StockCount];
END;
GO

IF EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261016030926_AssetsModule'
)
BEGIN
    DROP TABLE [MST_AssetCategory];
END;
GO

IF EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261016030926_AssetsModule'
)
BEGIN
    DROP TABLE [TRN_GoodsReceipt];
END;
GO

IF EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261016030926_AssetsModule'
)
BEGIN
    DROP TABLE [MST_AssetLocation];
END;
GO

IF EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261016030926_AssetsModule'
)
BEGIN
    DROP INDEX [IX_InvTxn_Source] ON [TRN_InventoryTransaction];
END;
GO

IF EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261016030926_AssetsModule'
)
BEGIN
    DROP INDEX [IX_TRN_InventoryTransaction_AssetLocationId] ON [TRN_InventoryTransaction];
END;
GO

IF EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261016030926_AssetsModule'
)
BEGIN
    DROP INDEX [IX_InventoryItem_Kind_Category] ON [MST_InventoryItem];
END;
GO

IF EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261016030926_AssetsModule'
)
BEGIN
    DROP INDEX [IX_MST_InventoryItem_AssetCategoryId] ON [MST_InventoryItem];
END;
GO

IF EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261016030926_AssetsModule'
)
BEGIN
    DROP INDEX [IX_MST_InventoryItem_KeptInLocationId] ON [MST_InventoryItem];
END;
GO

IF EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261016030926_AssetsModule'
)
BEGIN
    DROP INDEX [IX_MST_InventoryItem_SupplierId] ON [MST_InventoryItem];
END;
GO

IF EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261016030926_AssetsModule'
)
BEGIN
    DECLARE @var0 sysname;
    SELECT @var0 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[TRN_InventoryTransaction]') AND [c].[name] = N'AssetLocationId');
    IF @var0 IS NOT NULL EXEC(N'ALTER TABLE [TRN_InventoryTransaction] DROP CONSTRAINT [' + @var0 + '];');
    ALTER TABLE [TRN_InventoryTransaction] DROP COLUMN [AssetLocationId];
END;
GO

IF EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261016030926_AssetsModule'
)
BEGIN
    DECLARE @var1 sysname;
    SELECT @var1 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[TRN_InventoryTransaction]') AND [c].[name] = N'IssueReason');
    IF @var1 IS NOT NULL EXEC(N'ALTER TABLE [TRN_InventoryTransaction] DROP CONSTRAINT [' + @var1 + '];');
    ALTER TABLE [TRN_InventoryTransaction] DROP COLUMN [IssueReason];
END;
GO

IF EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261016030926_AssetsModule'
)
BEGIN
    DECLARE @var2 sysname;
    SELECT @var2 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[TRN_InventoryTransaction]') AND [c].[name] = N'SourceId');
    IF @var2 IS NOT NULL EXEC(N'ALTER TABLE [TRN_InventoryTransaction] DROP CONSTRAINT [' + @var2 + '];');
    ALTER TABLE [TRN_InventoryTransaction] DROP COLUMN [SourceId];
END;
GO

IF EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261016030926_AssetsModule'
)
BEGIN
    DECLARE @var3 sysname;
    SELECT @var3 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[TRN_InventoryTransaction]') AND [c].[name] = N'SourceType');
    IF @var3 IS NOT NULL EXEC(N'ALTER TABLE [TRN_InventoryTransaction] DROP CONSTRAINT [' + @var3 + '];');
    ALTER TABLE [TRN_InventoryTransaction] DROP COLUMN [SourceType];
END;
GO

IF EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261016030926_AssetsModule'
)
BEGIN
    DECLARE @var4 sysname;
    SELECT @var4 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[MST_InventoryItem]') AND [c].[name] = N'AssetCategoryId');
    IF @var4 IS NOT NULL EXEC(N'ALTER TABLE [MST_InventoryItem] DROP CONSTRAINT [' + @var4 + '];');
    ALTER TABLE [MST_InventoryItem] DROP COLUMN [AssetCategoryId];
END;
GO

IF EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261016030926_AssetsModule'
)
BEGIN
    DECLARE @var5 sysname;
    SELECT @var5 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[MST_InventoryItem]') AND [c].[name] = N'BrandModel');
    IF @var5 IS NOT NULL EXEC(N'ALTER TABLE [MST_InventoryItem] DROP CONSTRAINT [' + @var5 + '];');
    ALTER TABLE [MST_InventoryItem] DROP COLUMN [BrandModel];
END;
GO

IF EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261016030926_AssetsModule'
)
BEGIN
    DECLARE @var6 sysname;
    SELECT @var6 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[MST_InventoryItem]') AND [c].[name] = N'KeptInLocationId');
    IF @var6 IS NOT NULL EXEC(N'ALTER TABLE [MST_InventoryItem] DROP CONSTRAINT [' + @var6 + '];');
    ALTER TABLE [MST_InventoryItem] DROP COLUMN [KeptInLocationId];
END;
GO

IF EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261016030926_AssetsModule'
)
BEGIN
    DECLARE @var7 sysname;
    SELECT @var7 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[MST_InventoryItem]') AND [c].[name] = N'Kind');
    IF @var7 IS NOT NULL EXEC(N'ALTER TABLE [MST_InventoryItem] DROP CONSTRAINT [' + @var7 + '];');
    ALTER TABLE [MST_InventoryItem] DROP COLUMN [Kind];
END;
GO

IF EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261016030926_AssetsModule'
)
BEGIN
    DECLARE @var8 sysname;
    SELECT @var8 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[MST_InventoryItem]') AND [c].[name] = N'Remarks');
    IF @var8 IS NOT NULL EXEC(N'ALTER TABLE [MST_InventoryItem] DROP CONSTRAINT [' + @var8 + '];');
    ALTER TABLE [MST_InventoryItem] DROP COLUMN [Remarks];
END;
GO

IF EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261016030926_AssetsModule'
)
BEGIN
    DECLARE @var9 sysname;
    SELECT @var9 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[MST_InventoryItem]') AND [c].[name] = N'SupplierId');
    IF @var9 IS NOT NULL EXEC(N'ALTER TABLE [MST_InventoryItem] DROP CONSTRAINT [' + @var9 + '];');
    ALTER TABLE [MST_InventoryItem] DROP COLUMN [SupplierId];
END;
GO

IF EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261016030926_AssetsModule'
)
BEGIN
    DECLARE @var10 sysname;
    SELECT @var10 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[MST_InventoryItem]') AND [c].[name] = N'VendorName');
    IF @var10 IS NOT NULL EXEC(N'ALTER TABLE [MST_InventoryItem] DROP CONSTRAINT [' + @var10 + '];');
    ALTER TABLE [MST_InventoryItem] DROP COLUMN [VendorName];
END;
GO

IF EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261016030926_AssetsModule'
)
BEGIN
    DELETE FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261016030926_AssetsModule';
END;
GO

COMMIT;
GO

