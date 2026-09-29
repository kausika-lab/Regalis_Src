BEGIN TRANSACTION;
GO

IF EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260925103626_AddPurchaseOrderWorkflow'
)
BEGIN
    DROP TABLE [TRN_PurchaseOrderAttachment];
END;
GO

IF EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260925103626_AddPurchaseOrderWorkflow'
)
BEGIN
    DROP TABLE [TRN_PurchaseOrderStatusHistory];
END;
GO

IF EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260925103626_AddPurchaseOrderWorkflow'
)
BEGIN
    DECLARE @var0 sysname;
    SELECT @var0 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[TRN_PurchaseOrderLine]') AND [c].[name] = N'AccountCategory');
    IF @var0 IS NOT NULL EXEC(N'ALTER TABLE [TRN_PurchaseOrderLine] DROP CONSTRAINT [' + @var0 + '];');
    ALTER TABLE [TRN_PurchaseOrderLine] DROP COLUMN [AccountCategory];
END;
GO

IF EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260925103626_AddPurchaseOrderWorkflow'
)
BEGIN
    DECLARE @var1 sysname;
    SELECT @var1 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[TRN_PurchaseOrderLine]') AND [c].[name] = N'CgstAmount');
    IF @var1 IS NOT NULL EXEC(N'ALTER TABLE [TRN_PurchaseOrderLine] DROP CONSTRAINT [' + @var1 + '];');
    ALTER TABLE [TRN_PurchaseOrderLine] DROP COLUMN [CgstAmount];
END;
GO

IF EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260925103626_AddPurchaseOrderWorkflow'
)
BEGIN
    DECLARE @var2 sysname;
    SELECT @var2 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[TRN_PurchaseOrderLine]') AND [c].[name] = N'IgstAmount');
    IF @var2 IS NOT NULL EXEC(N'ALTER TABLE [TRN_PurchaseOrderLine] DROP CONSTRAINT [' + @var2 + '];');
    ALTER TABLE [TRN_PurchaseOrderLine] DROP COLUMN [IgstAmount];
END;
GO

IF EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260925103626_AddPurchaseOrderWorkflow'
)
BEGIN
    DECLARE @var3 sysname;
    SELECT @var3 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[TRN_PurchaseOrderLine]') AND [c].[name] = N'SgstAmount');
    IF @var3 IS NOT NULL EXEC(N'ALTER TABLE [TRN_PurchaseOrderLine] DROP CONSTRAINT [' + @var3 + '];');
    ALTER TABLE [TRN_PurchaseOrderLine] DROP COLUMN [SgstAmount];
END;
GO

IF EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260925103626_AddPurchaseOrderWorkflow'
)
BEGIN
    DECLARE @var4 sysname;
    SELECT @var4 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[TRN_PurchaseOrder]') AND [c].[name] = N'AdjustmentAmount');
    IF @var4 IS NOT NULL EXEC(N'ALTER TABLE [TRN_PurchaseOrder] DROP CONSTRAINT [' + @var4 + '];');
    ALTER TABLE [TRN_PurchaseOrder] DROP COLUMN [AdjustmentAmount];
END;
GO

IF EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260925103626_AddPurchaseOrderWorkflow'
)
BEGIN
    DECLARE @var5 sysname;
    SELECT @var5 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[TRN_PurchaseOrder]') AND [c].[name] = N'AdjustmentLabel');
    IF @var5 IS NOT NULL EXEC(N'ALTER TABLE [TRN_PurchaseOrder] DROP CONSTRAINT [' + @var5 + '];');
    ALTER TABLE [TRN_PurchaseOrder] DROP COLUMN [AdjustmentLabel];
END;
GO

IF EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260925103626_AddPurchaseOrderWorkflow'
)
BEGIN
    DECLARE @var6 sysname;
    SELECT @var6 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[TRN_PurchaseOrder]') AND [c].[name] = N'ApprovedByUserId');
    IF @var6 IS NOT NULL EXEC(N'ALTER TABLE [TRN_PurchaseOrder] DROP CONSTRAINT [' + @var6 + '];');
    ALTER TABLE [TRN_PurchaseOrder] DROP COLUMN [ApprovedByUserId];
END;
GO

IF EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260925103626_AddPurchaseOrderWorkflow'
)
BEGIN
    DECLARE @var7 sysname;
    SELECT @var7 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[TRN_PurchaseOrder]') AND [c].[name] = N'ApprovedUtc');
    IF @var7 IS NOT NULL EXEC(N'ALTER TABLE [TRN_PurchaseOrder] DROP CONSTRAINT [' + @var7 + '];');
    ALTER TABLE [TRN_PurchaseOrder] DROP COLUMN [ApprovedUtc];
END;
GO

IF EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260925103626_AddPurchaseOrderWorkflow'
)
BEGIN
    DECLARE @var8 sysname;
    SELECT @var8 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[TRN_PurchaseOrder]') AND [c].[name] = N'CgstAmount');
    IF @var8 IS NOT NULL EXEC(N'ALTER TABLE [TRN_PurchaseOrder] DROP CONSTRAINT [' + @var8 + '];');
    ALTER TABLE [TRN_PurchaseOrder] DROP COLUMN [CgstAmount];
END;
GO

IF EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260925103626_AddPurchaseOrderWorkflow'
)
BEGIN
    DECLARE @var9 sysname;
    SELECT @var9 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[TRN_PurchaseOrder]') AND [c].[name] = N'DeliveryAddress');
    IF @var9 IS NOT NULL EXEC(N'ALTER TABLE [TRN_PurchaseOrder] DROP CONSTRAINT [' + @var9 + '];');
    ALTER TABLE [TRN_PurchaseOrder] DROP COLUMN [DeliveryAddress];
END;
GO

IF EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260925103626_AddPurchaseOrderWorkflow'
)
BEGIN
    DECLARE @var10 sysname;
    SELECT @var10 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[TRN_PurchaseOrder]') AND [c].[name] = N'DiscountType');
    IF @var10 IS NOT NULL EXEC(N'ALTER TABLE [TRN_PurchaseOrder] DROP CONSTRAINT [' + @var10 + '];');
    ALTER TABLE [TRN_PurchaseOrder] DROP COLUMN [DiscountType];
END;
GO

IF EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260925103626_AddPurchaseOrderWorkflow'
)
BEGIN
    DECLARE @var11 sysname;
    SELECT @var11 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[TRN_PurchaseOrder]') AND [c].[name] = N'DiscountValue');
    IF @var11 IS NOT NULL EXEC(N'ALTER TABLE [TRN_PurchaseOrder] DROP CONSTRAINT [' + @var11 + '];');
    ALTER TABLE [TRN_PurchaseOrder] DROP COLUMN [DiscountValue];
END;
GO

IF EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260925103626_AddPurchaseOrderWorkflow'
)
BEGIN
    DECLARE @var12 sysname;
    SELECT @var12 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[TRN_PurchaseOrder]') AND [c].[name] = N'IgstAmount');
    IF @var12 IS NOT NULL EXEC(N'ALTER TABLE [TRN_PurchaseOrder] DROP CONSTRAINT [' + @var12 + '];');
    ALTER TABLE [TRN_PurchaseOrder] DROP COLUMN [IgstAmount];
END;
GO

IF EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260925103626_AddPurchaseOrderWorkflow'
)
BEGIN
    DECLARE @var13 sysname;
    SELECT @var13 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[TRN_PurchaseOrder]') AND [c].[name] = N'IsInterState');
    IF @var13 IS NOT NULL EXEC(N'ALTER TABLE [TRN_PurchaseOrder] DROP CONSTRAINT [' + @var13 + '];');
    ALTER TABLE [TRN_PurchaseOrder] DROP COLUMN [IsInterState];
END;
GO

IF EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260925103626_AddPurchaseOrderWorkflow'
)
BEGIN
    DECLARE @var14 sysname;
    SELECT @var14 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[TRN_PurchaseOrder]') AND [c].[name] = N'IsReverseCharge');
    IF @var14 IS NOT NULL EXEC(N'ALTER TABLE [TRN_PurchaseOrder] DROP CONSTRAINT [' + @var14 + '];');
    ALTER TABLE [TRN_PurchaseOrder] DROP COLUMN [IsReverseCharge];
END;
GO

IF EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260925103626_AddPurchaseOrderWorkflow'
)
BEGIN
    DECLARE @var15 sysname;
    SELECT @var15 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[TRN_PurchaseOrder]') AND [c].[name] = N'ReferenceNumber');
    IF @var15 IS NOT NULL EXEC(N'ALTER TABLE [TRN_PurchaseOrder] DROP CONSTRAINT [' + @var15 + '];');
    ALTER TABLE [TRN_PurchaseOrder] DROP COLUMN [ReferenceNumber];
END;
GO

IF EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260925103626_AddPurchaseOrderWorkflow'
)
BEGIN
    DECLARE @var16 sysname;
    SELECT @var16 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[TRN_PurchaseOrder]') AND [c].[name] = N'SgstAmount');
    IF @var16 IS NOT NULL EXEC(N'ALTER TABLE [TRN_PurchaseOrder] DROP CONSTRAINT [' + @var16 + '];');
    ALTER TABLE [TRN_PurchaseOrder] DROP COLUMN [SgstAmount];
END;
GO

IF EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260925103626_AddPurchaseOrderWorkflow'
)
BEGIN
    DECLARE @var17 sysname;
    SELECT @var17 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[TRN_PurchaseOrder]') AND [c].[name] = N'ShipmentPreference');
    IF @var17 IS NOT NULL EXEC(N'ALTER TABLE [TRN_PurchaseOrder] DROP CONSTRAINT [' + @var17 + '];');
    ALTER TABLE [TRN_PurchaseOrder] DROP COLUMN [ShipmentPreference];
END;
GO

IF EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260925103626_AddPurchaseOrderWorkflow'
)
BEGIN
    DECLARE @var18 sysname;
    SELECT @var18 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[TRN_PurchaseOrder]') AND [c].[name] = N'SubmittedUtc');
    IF @var18 IS NOT NULL EXEC(N'ALTER TABLE [TRN_PurchaseOrder] DROP CONSTRAINT [' + @var18 + '];');
    ALTER TABLE [TRN_PurchaseOrder] DROP COLUMN [SubmittedUtc];
END;
GO

IF EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260925103626_AddPurchaseOrderWorkflow'
)
BEGIN
    DECLARE @var19 sysname;
    SELECT @var19 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[TRN_PurchaseOrder]') AND [c].[name] = N'TdsAmount');
    IF @var19 IS NOT NULL EXEC(N'ALTER TABLE [TRN_PurchaseOrder] DROP CONSTRAINT [' + @var19 + '];');
    ALTER TABLE [TRN_PurchaseOrder] DROP COLUMN [TdsAmount];
END;
GO

IF EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260925103626_AddPurchaseOrderWorkflow'
)
BEGIN
    DECLARE @var20 sysname;
    SELECT @var20 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[TRN_PurchaseOrder]') AND [c].[name] = N'TdsCode');
    IF @var20 IS NOT NULL EXEC(N'ALTER TABLE [TRN_PurchaseOrder] DROP CONSTRAINT [' + @var20 + '];');
    ALTER TABLE [TRN_PurchaseOrder] DROP COLUMN [TdsCode];
END;
GO

IF EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260925103626_AddPurchaseOrderWorkflow'
)
BEGIN
    DECLARE @var21 sysname;
    SELECT @var21 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[TRN_PurchaseOrder]') AND [c].[name] = N'TdsLabel');
    IF @var21 IS NOT NULL EXEC(N'ALTER TABLE [TRN_PurchaseOrder] DROP CONSTRAINT [' + @var21 + '];');
    ALTER TABLE [TRN_PurchaseOrder] DROP COLUMN [TdsLabel];
END;
GO

IF EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260925103626_AddPurchaseOrderWorkflow'
)
BEGIN
    DECLARE @var22 sysname;
    SELECT @var22 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[TRN_PurchaseOrder]') AND [c].[name] = N'TdsPercent');
    IF @var22 IS NOT NULL EXEC(N'ALTER TABLE [TRN_PurchaseOrder] DROP CONSTRAINT [' + @var22 + '];');
    ALTER TABLE [TRN_PurchaseOrder] DROP COLUMN [TdsPercent];
END;
GO

IF EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260925103626_AddPurchaseOrderWorkflow'
)
BEGIN
    DECLARE @var23 sysname;
    SELECT @var23 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[TRN_PurchaseOrder]') AND [c].[name] = N'TermsAndConditions');
    IF @var23 IS NOT NULL EXEC(N'ALTER TABLE [TRN_PurchaseOrder] DROP CONSTRAINT [' + @var23 + '];');
    ALTER TABLE [TRN_PurchaseOrder] DROP COLUMN [TermsAndConditions];
END;
GO

IF EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260925103626_AddPurchaseOrderWorkflow'
)
BEGIN
    DELETE FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260925103626_AddPurchaseOrderWorkflow';
END;
GO

COMMIT;
GO

