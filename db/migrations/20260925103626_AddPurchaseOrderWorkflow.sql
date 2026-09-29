BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260925103626_AddPurchaseOrderWorkflow'
)
BEGIN
    ALTER TABLE [TRN_PurchaseOrderLine] ADD [AccountCategory] nvarchar(100) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260925103626_AddPurchaseOrderWorkflow'
)
BEGIN
    ALTER TABLE [TRN_PurchaseOrderLine] ADD [CgstAmount] decimal(18,2) NOT NULL DEFAULT 0.0;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260925103626_AddPurchaseOrderWorkflow'
)
BEGIN
    ALTER TABLE [TRN_PurchaseOrderLine] ADD [IgstAmount] decimal(18,2) NOT NULL DEFAULT 0.0;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260925103626_AddPurchaseOrderWorkflow'
)
BEGIN
    ALTER TABLE [TRN_PurchaseOrderLine] ADD [SgstAmount] decimal(18,2) NOT NULL DEFAULT 0.0;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260925103626_AddPurchaseOrderWorkflow'
)
BEGIN
    ALTER TABLE [TRN_PurchaseOrder] ADD [AdjustmentAmount] decimal(18,2) NOT NULL DEFAULT 0.0;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260925103626_AddPurchaseOrderWorkflow'
)
BEGIN
    ALTER TABLE [TRN_PurchaseOrder] ADD [AdjustmentLabel] nvarchar(60) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260925103626_AddPurchaseOrderWorkflow'
)
BEGIN
    ALTER TABLE [TRN_PurchaseOrder] ADD [ApprovedByUserId] bigint NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260925103626_AddPurchaseOrderWorkflow'
)
BEGIN
    ALTER TABLE [TRN_PurchaseOrder] ADD [ApprovedUtc] datetime2(3) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260925103626_AddPurchaseOrderWorkflow'
)
BEGIN
    ALTER TABLE [TRN_PurchaseOrder] ADD [CgstAmount] decimal(18,2) NOT NULL DEFAULT 0.0;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260925103626_AddPurchaseOrderWorkflow'
)
BEGIN
    ALTER TABLE [TRN_PurchaseOrder] ADD [DeliveryAddress] nvarchar(600) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260925103626_AddPurchaseOrderWorkflow'
)
BEGIN
    ALTER TABLE [TRN_PurchaseOrder] ADD [DiscountType] tinyint NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260925103626_AddPurchaseOrderWorkflow'
)
BEGIN
    ALTER TABLE [TRN_PurchaseOrder] ADD [DiscountValue] decimal(18,2) NOT NULL DEFAULT 0.0;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260925103626_AddPurchaseOrderWorkflow'
)
BEGIN
    ALTER TABLE [TRN_PurchaseOrder] ADD [IgstAmount] decimal(18,2) NOT NULL DEFAULT 0.0;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260925103626_AddPurchaseOrderWorkflow'
)
BEGIN
    ALTER TABLE [TRN_PurchaseOrder] ADD [IsInterState] bit NOT NULL DEFAULT CAST(0 AS bit);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260925103626_AddPurchaseOrderWorkflow'
)
BEGIN
    ALTER TABLE [TRN_PurchaseOrder] ADD [IsReverseCharge] bit NOT NULL DEFAULT CAST(0 AS bit);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260925103626_AddPurchaseOrderWorkflow'
)
BEGIN
    ALTER TABLE [TRN_PurchaseOrder] ADD [ReferenceNumber] nvarchar(80) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260925103626_AddPurchaseOrderWorkflow'
)
BEGIN
    ALTER TABLE [TRN_PurchaseOrder] ADD [SgstAmount] decimal(18,2) NOT NULL DEFAULT 0.0;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260925103626_AddPurchaseOrderWorkflow'
)
BEGIN
    ALTER TABLE [TRN_PurchaseOrder] ADD [ShipmentPreference] nvarchar(60) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260925103626_AddPurchaseOrderWorkflow'
)
BEGIN
    ALTER TABLE [TRN_PurchaseOrder] ADD [SubmittedUtc] datetime2(3) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260925103626_AddPurchaseOrderWorkflow'
)
BEGIN
    ALTER TABLE [TRN_PurchaseOrder] ADD [TdsAmount] decimal(18,2) NOT NULL DEFAULT 0.0;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260925103626_AddPurchaseOrderWorkflow'
)
BEGIN
    ALTER TABLE [TRN_PurchaseOrder] ADD [TdsCode] nvarchar(20) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260925103626_AddPurchaseOrderWorkflow'
)
BEGIN
    ALTER TABLE [TRN_PurchaseOrder] ADD [TdsLabel] nvarchar(100) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260925103626_AddPurchaseOrderWorkflow'
)
BEGIN
    ALTER TABLE [TRN_PurchaseOrder] ADD [TdsPercent] decimal(5,2) NOT NULL DEFAULT 0.0;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260925103626_AddPurchaseOrderWorkflow'
)
BEGIN
    ALTER TABLE [TRN_PurchaseOrder] ADD [TermsAndConditions] nvarchar(4000) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260925103626_AddPurchaseOrderWorkflow'
)
BEGIN
    CREATE TABLE [TRN_PurchaseOrderAttachment] (
        [PurchaseOrderAttachmentId] bigint NOT NULL IDENTITY,
        [PurchaseOrderId] bigint NOT NULL,
        [FileName] nvarchar(260) NOT NULL,
        [StoredFileName] nvarchar(100) NOT NULL,
        [FilePath] nvarchar(500) NOT NULL,
        [ContentType] nvarchar(150) NOT NULL,
        [FileSizeBytes] bigint NOT NULL,
        [UploadedByUserId] bigint NOT NULL,
        [UploadedUtc] datetime2(3) NOT NULL,
        CONSTRAINT [PK_TRN_PurchaseOrderAttachment] PRIMARY KEY ([PurchaseOrderAttachmentId]),
        CONSTRAINT [FK_TRN_PurchaseOrderAttachment_TRN_PurchaseOrder_PurchaseOrderId] FOREIGN KEY ([PurchaseOrderId]) REFERENCES [TRN_PurchaseOrder] ([PurchaseOrderId]) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260925103626_AddPurchaseOrderWorkflow'
)
BEGIN
    CREATE TABLE [TRN_PurchaseOrderStatusHistory] (
        [PurchaseOrderStatusHistoryId] bigint NOT NULL IDENTITY,
        [PurchaseOrderId] bigint NOT NULL,
        [FromStatus] tinyint NULL,
        [ToStatus] tinyint NOT NULL,
        [Action] nvarchar(40) NOT NULL,
        [ChangedByUserId] bigint NOT NULL,
        [ChangedUtc] datetime2(3) NOT NULL,
        [Reason] nvarchar(500) NULL,
        CONSTRAINT [PK_TRN_PurchaseOrderStatusHistory] PRIMARY KEY ([PurchaseOrderStatusHistoryId]),
        CONSTRAINT [FK_TRN_PurchaseOrderStatusHistory_TRN_PurchaseOrder_PurchaseOrderId] FOREIGN KEY ([PurchaseOrderId]) REFERENCES [TRN_PurchaseOrder] ([PurchaseOrderId]) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260925103626_AddPurchaseOrderWorkflow'
)
BEGIN
    CREATE INDEX [IX_PurchaseOrderAttachment_PurchaseOrder] ON [TRN_PurchaseOrderAttachment] ([PurchaseOrderId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260925103626_AddPurchaseOrderWorkflow'
)
BEGIN
    CREATE INDEX [IX_PurchaseOrderStatusHistory_PurchaseOrder] ON [TRN_PurchaseOrderStatusHistory] ([PurchaseOrderId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260925103626_AddPurchaseOrderWorkflow'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260925103626_AddPurchaseOrderWorkflow', N'8.0.19');
END;
GO

COMMIT;
GO

