BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261016030926_AssetsModule'
)
BEGIN
    ALTER TABLE [TRN_InventoryTransaction] ADD [AssetLocationId] int NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261016030926_AssetsModule'
)
BEGIN
    ALTER TABLE [TRN_InventoryTransaction] ADD [IssueReason] tinyint NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261016030926_AssetsModule'
)
BEGIN
    ALTER TABLE [TRN_InventoryTransaction] ADD [SourceId] bigint NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261016030926_AssetsModule'
)
BEGIN
    ALTER TABLE [TRN_InventoryTransaction] ADD [SourceType] tinyint NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261016030926_AssetsModule'
)
BEGIN
    ALTER TABLE [MST_InventoryItem] ADD [AssetCategoryId] int NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261016030926_AssetsModule'
)
BEGIN
    ALTER TABLE [MST_InventoryItem] ADD [BrandModel] nvarchar(150) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261016030926_AssetsModule'
)
BEGIN
    ALTER TABLE [MST_InventoryItem] ADD [KeptInLocationId] int NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261016030926_AssetsModule'
)
BEGIN
    ALTER TABLE [MST_InventoryItem] ADD [Kind] tinyint NOT NULL DEFAULT CAST(1 AS tinyint);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261016030926_AssetsModule'
)
BEGIN
    ALTER TABLE [MST_InventoryItem] ADD [Remarks] nvarchar(500) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261016030926_AssetsModule'
)
BEGIN
    ALTER TABLE [MST_InventoryItem] ADD [SupplierId] int NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261016030926_AssetsModule'
)
BEGIN
    ALTER TABLE [MST_InventoryItem] ADD [VendorName] nvarchar(150) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261016030926_AssetsModule'
)
BEGIN
    CREATE TABLE [MST_AssetCategory] (
        [AssetCategoryId] int NOT NULL IDENTITY,
        [CategoryName] nvarchar(60) NOT NULL,
        [DefaultKind] tinyint NOT NULL,
        [CodePrefix] nvarchar(30) NOT NULL,
        [UsefulLifeYears] decimal(5,2) NULL,
        [DisplayOrder] int NOT NULL,
        [CreatedBy] bigint NULL,
        [CreatedDate] datetime2(3) NOT NULL,
        [ModifiedBy] bigint NULL,
        [ModifiedDate] datetime2(3) NULL,
        [IsActive] bit NOT NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedBy] bigint NULL,
        [DeletedDate] datetime2(3) NULL,
        CONSTRAINT [PK_MST_AssetCategory] PRIMARY KEY ([AssetCategoryId])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261016030926_AssetsModule'
)
BEGIN
    CREATE TABLE [MST_AssetLocation] (
        [AssetLocationId] int NOT NULL IDENTITY,
        [LocationName] nvarchar(100) NOT NULL,
        [WorkspaceId] int NULL,
        [IsStore] bit NOT NULL,
        [DisplayOrder] int NOT NULL,
        [CreatedBy] bigint NULL,
        [CreatedDate] datetime2(3) NOT NULL,
        [ModifiedBy] bigint NULL,
        [ModifiedDate] datetime2(3) NULL,
        [IsActive] bit NOT NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedBy] bigint NULL,
        [DeletedDate] datetime2(3) NULL,
        CONSTRAINT [PK_MST_AssetLocation] PRIMARY KEY ([AssetLocationId]),
        CONSTRAINT [FK_MST_AssetLocation_MST_Workspace_WorkspaceId] FOREIGN KEY ([WorkspaceId]) REFERENCES [MST_Workspace] ([WorkspaceId]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261016030926_AssetsModule'
)
BEGIN
    CREATE TABLE [MST_CodeSequence] (
        [Prefix] nvarchar(40) NOT NULL,
        [LastNumber] int NOT NULL,
        CONSTRAINT [PK_MST_CodeSequence] PRIMARY KEY ([Prefix])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261016030926_AssetsModule'
)
BEGIN
    CREATE TABLE [TRN_ItemDocument] (
        [ItemDocumentId] bigint NOT NULL IDENTITY,
        [InventoryItemId] int NOT NULL,
        [DocumentType] tinyint NOT NULL,
        [FileName] nvarchar(255) NOT NULL,
        [StoredFileName] nvarchar(100) NOT NULL,
        [FilePath] nvarchar(500) NOT NULL,
        [ContentType] nvarchar(100) NOT NULL,
        [FileSizeBytes] bigint NOT NULL,
        [UploadedByUserId] bigint NOT NULL,
        [UploadedUtc] datetime2(3) NOT NULL,
        [CreatedBy] bigint NULL,
        [CreatedDate] datetime2(3) NOT NULL,
        [ModifiedBy] bigint NULL,
        [ModifiedDate] datetime2(3) NULL,
        [IsActive] bit NOT NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedBy] bigint NULL,
        [DeletedDate] datetime2(3) NULL,
        CONSTRAINT [PK_TRN_ItemDocument] PRIMARY KEY ([ItemDocumentId]),
        CONSTRAINT [FK_TRN_ItemDocument_MST_InventoryItem_InventoryItemId] FOREIGN KEY ([InventoryItemId]) REFERENCES [MST_InventoryItem] ([InventoryItemId]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261016030926_AssetsModule'
)
BEGIN
    CREATE TABLE [TRN_ItemEvent] (
        [ItemEventId] bigint NOT NULL IDENTITY,
        [InventoryItemId] int NOT NULL,
        [EventType] nvarchar(40) NOT NULL,
        [EventDate] date NOT NULL,
        [Summary] nvarchar(300) NOT NULL,
        [FromText] nvarchar(150) NULL,
        [ToText] nvarchar(150) NULL,
        [DocumentNumber] nvarchar(30) NULL,
        [MetaJson] nvarchar(max) NULL,
        [ActorUserId] bigint NULL,
        [CreatedUtc] datetime2(3) NOT NULL,
        CONSTRAINT [PK_TRN_ItemEvent] PRIMARY KEY ([ItemEventId]),
        CONSTRAINT [FK_TRN_ItemEvent_MST_InventoryItem_InventoryItemId] FOREIGN KEY ([InventoryItemId]) REFERENCES [MST_InventoryItem] ([InventoryItemId]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261016030926_AssetsModule'
)
BEGIN
    CREATE TABLE [TRN_GoodsIssue] (
        [GoodsIssueId] bigint NOT NULL IDENTITY,
        [IssueNumber] nvarchar(30) NOT NULL,
        [IssueDate] date NOT NULL,
        [Reason] tinyint NOT NULL,
        [FromLocationId] int NULL,
        [ToLocationId] int NULL,
        [RepairVendor] nvarchar(150) NULL,
        [ExpectedCost] decimal(18,2) NULL,
        [CreditNoteRaised] bit NULL,
        [AmountRecovered] decimal(18,2) NULL,
        [ReportedBy] nvarchar(150) NULL,
        [ReasonText] nvarchar(300) NULL,
        [Note] nvarchar(500) NULL,
        [PostedByUserId] bigint NOT NULL,
        [PostedUtc] datetime2(3) NOT NULL,
        [ReversesIssueId] bigint NULL,
        [ReversedByIssueId] bigint NULL,
        [CreatedBy] bigint NULL,
        [CreatedDate] datetime2(3) NOT NULL,
        [ModifiedBy] bigint NULL,
        [ModifiedDate] datetime2(3) NULL,
        [IsActive] bit NOT NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedBy] bigint NULL,
        [DeletedDate] datetime2(3) NULL,
        CONSTRAINT [PK_TRN_GoodsIssue] PRIMARY KEY ([GoodsIssueId]),
        CONSTRAINT [FK_TRN_GoodsIssue_MST_AssetLocation_FromLocationId] FOREIGN KEY ([FromLocationId]) REFERENCES [MST_AssetLocation] ([AssetLocationId]) ON DELETE NO ACTION,
        CONSTRAINT [FK_TRN_GoodsIssue_MST_AssetLocation_ToLocationId] FOREIGN KEY ([ToLocationId]) REFERENCES [MST_AssetLocation] ([AssetLocationId]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261016030926_AssetsModule'
)
BEGIN
    CREATE TABLE [TRN_GoodsReceipt] (
        [GoodsReceiptId] bigint NOT NULL IDENTITY,
        [ReceiptNumber] nvarchar(30) NOT NULL,
        [ReceiptDate] date NOT NULL,
        [Status] tinyint NOT NULL,
        [Source] tinyint NOT NULL,
        [IsOpeningBalance] bit NOT NULL,
        [SupplierId] int NULL,
        [InvoiceNumber] nvarchar(80) NULL,
        [PurchaseOrderId] bigint NULL,
        [IntoLocationId] int NULL,
        [TotalAmount] decimal(18,2) NOT NULL,
        [Notes] nvarchar(500) NULL,
        [PostedByUserId] bigint NULL,
        [PostedUtc] datetime2(3) NULL,
        [ReversesReceiptId] bigint NULL,
        [ReversedByReceiptId] bigint NULL,
        [CreatedBy] bigint NULL,
        [CreatedDate] datetime2(3) NOT NULL,
        [ModifiedBy] bigint NULL,
        [ModifiedDate] datetime2(3) NULL,
        [IsActive] bit NOT NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedBy] bigint NULL,
        [DeletedDate] datetime2(3) NULL,
        CONSTRAINT [PK_TRN_GoodsReceipt] PRIMARY KEY ([GoodsReceiptId]),
        CONSTRAINT [FK_TRN_GoodsReceipt_MST_AssetLocation_IntoLocationId] FOREIGN KEY ([IntoLocationId]) REFERENCES [MST_AssetLocation] ([AssetLocationId]) ON DELETE NO ACTION,
        CONSTRAINT [FK_TRN_GoodsReceipt_MST_Supplier_SupplierId] FOREIGN KEY ([SupplierId]) REFERENCES [MST_Supplier] ([SupplierId]) ON DELETE NO ACTION,
        CONSTRAINT [FK_TRN_GoodsReceipt_TRN_PurchaseOrder_PurchaseOrderId] FOREIGN KEY ([PurchaseOrderId]) REFERENCES [TRN_PurchaseOrder] ([PurchaseOrderId]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261016030926_AssetsModule'
)
BEGIN
    CREATE TABLE [TRN_StockCount] (
        [StockCountId] bigint NOT NULL IDENTITY,
        [CountNumber] nvarchar(30) NOT NULL,
        [CountDate] date NOT NULL,
        [AssetLocationId] int NULL,
        [CountedBy] nvarchar(150) NOT NULL,
        [Status] tinyint NOT NULL,
        [Notes] nvarchar(500) NULL,
        [SubmittedUtc] datetime2(3) NULL,
        [ConfirmedUtc] datetime2(3) NULL,
        [ConfirmedByUserId] bigint NULL,
        [CreatedBy] bigint NULL,
        [CreatedDate] datetime2(3) NOT NULL,
        [ModifiedBy] bigint NULL,
        [ModifiedDate] datetime2(3) NULL,
        [IsActive] bit NOT NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedBy] bigint NULL,
        [DeletedDate] datetime2(3) NULL,
        CONSTRAINT [PK_TRN_StockCount] PRIMARY KEY ([StockCountId]),
        CONSTRAINT [FK_TRN_StockCount_MST_AssetLocation_AssetLocationId] FOREIGN KEY ([AssetLocationId]) REFERENCES [MST_AssetLocation] ([AssetLocationId]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261016030926_AssetsModule'
)
BEGIN
    CREATE TABLE [TRN_AssetRepair] (
        [AssetRepairId] bigint NOT NULL IDENTITY,
        [InventoryItemId] int NOT NULL,
        [GoodsIssueId] bigint NOT NULL,
        [SentOn] date NOT NULL,
        [RepairVendor] nvarchar(150) NOT NULL,
        [Description] nvarchar(500) NULL,
        [ExpectedCost] decimal(18,2) NULL,
        [Cost] decimal(18,2) NULL,
        [IsOpen] bit NOT NULL,
        [ClosedOn] date NULL,
        [ClosedNote] nvarchar(500) NULL,
        [ReturnStatus] tinyint NOT NULL,
        [CreatedBy] bigint NULL,
        [CreatedDate] datetime2(3) NOT NULL,
        [ModifiedBy] bigint NULL,
        [ModifiedDate] datetime2(3) NULL,
        [IsActive] bit NOT NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedBy] bigint NULL,
        [DeletedDate] datetime2(3) NULL,
        CONSTRAINT [PK_TRN_AssetRepair] PRIMARY KEY ([AssetRepairId]),
        CONSTRAINT [FK_TRN_AssetRepair_MST_InventoryItem_InventoryItemId] FOREIGN KEY ([InventoryItemId]) REFERENCES [MST_InventoryItem] ([InventoryItemId]) ON DELETE NO ACTION,
        CONSTRAINT [FK_TRN_AssetRepair_TRN_GoodsIssue_GoodsIssueId] FOREIGN KEY ([GoodsIssueId]) REFERENCES [TRN_GoodsIssue] ([GoodsIssueId]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261016030926_AssetsModule'
)
BEGIN
    CREATE TABLE [TRN_GoodsIssueLine] (
        [GoodsIssueLineId] bigint NOT NULL IDENTITY,
        [GoodsIssueId] bigint NOT NULL,
        [InventoryItemId] int NOT NULL,
        [Quantity] decimal(18,3) NOT NULL,
        [UnitCost] decimal(18,2) NULL,
        [Amount] decimal(18,2) NULL,
        [PreviousStatus] tinyint NULL,
        [PreviousLocationId] int NULL,
        [NewStatus] tinyint NULL,
        [NewLocationId] int NULL,
        [CreatedBy] bigint NULL,
        [CreatedDate] datetime2(3) NOT NULL,
        [ModifiedBy] bigint NULL,
        [ModifiedDate] datetime2(3) NULL,
        [IsActive] bit NOT NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedBy] bigint NULL,
        [DeletedDate] datetime2(3) NULL,
        CONSTRAINT [PK_TRN_GoodsIssueLine] PRIMARY KEY ([GoodsIssueLineId]),
        CONSTRAINT [CK_GoodsIssueLine_Quantity] CHECK ([Quantity] > 0),
        CONSTRAINT [FK_TRN_GoodsIssueLine_MST_InventoryItem_InventoryItemId] FOREIGN KEY ([InventoryItemId]) REFERENCES [MST_InventoryItem] ([InventoryItemId]) ON DELETE NO ACTION,
        CONSTRAINT [FK_TRN_GoodsIssueLine_TRN_GoodsIssue_GoodsIssueId] FOREIGN KEY ([GoodsIssueId]) REFERENCES [TRN_GoodsIssue] ([GoodsIssueId]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261016030926_AssetsModule'
)
BEGIN
    CREATE TABLE [TRN_GoodsReceiptLine] (
        [GoodsReceiptLineId] bigint NOT NULL IDENTITY,
        [GoodsReceiptId] bigint NOT NULL,
        [LineNumber] int NOT NULL,
        [Kind] tinyint NOT NULL,
        [InventoryItemId] int NULL,
        [Quantity] decimal(18,3) NOT NULL,
        [Rate] decimal(18,2) NULL,
        [Amount] decimal(18,2) NOT NULL,
        [PurchaseOrderLineId] bigint NULL,
        [ItemName] nvarchar(150) NULL,
        [AssetCategoryId] int NULL,
        [CodePrefix] nvarchar(30) NULL,
        [BrandModel] nvarchar(150) NULL,
        [Description] nvarchar(500) NULL,
        [SerialNumber] nvarchar(100) NULL,
        [AssetLocationId] int NULL,
        [Condition] tinyint NULL,
        [PurchaseDate] date NULL,
        [WarrantyUntil] date NULL,
        [VendorName] nvarchar(150) NULL,
        [Remarks] nvarchar(500) NULL,
        [FirstCode] nvarchar(40) NULL,
        [LastCode] nvarchar(40) NULL,
        [CreatedBy] bigint NULL,
        [CreatedDate] datetime2(3) NOT NULL,
        [ModifiedBy] bigint NULL,
        [ModifiedDate] datetime2(3) NULL,
        [IsActive] bit NOT NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedBy] bigint NULL,
        [DeletedDate] datetime2(3) NULL,
        CONSTRAINT [PK_TRN_GoodsReceiptLine] PRIMARY KEY ([GoodsReceiptLineId]),
        CONSTRAINT [CK_GoodsReceiptLine_Quantity] CHECK ([Quantity] > 0),
        CONSTRAINT [FK_TRN_GoodsReceiptLine_MST_AssetCategory_AssetCategoryId] FOREIGN KEY ([AssetCategoryId]) REFERENCES [MST_AssetCategory] ([AssetCategoryId]) ON DELETE NO ACTION,
        CONSTRAINT [FK_TRN_GoodsReceiptLine_MST_AssetLocation_AssetLocationId] FOREIGN KEY ([AssetLocationId]) REFERENCES [MST_AssetLocation] ([AssetLocationId]) ON DELETE NO ACTION,
        CONSTRAINT [FK_TRN_GoodsReceiptLine_MST_InventoryItem_InventoryItemId] FOREIGN KEY ([InventoryItemId]) REFERENCES [MST_InventoryItem] ([InventoryItemId]) ON DELETE NO ACTION,
        CONSTRAINT [FK_TRN_GoodsReceiptLine_TRN_GoodsReceipt_GoodsReceiptId] FOREIGN KEY ([GoodsReceiptId]) REFERENCES [TRN_GoodsReceipt] ([GoodsReceiptId]) ON DELETE NO ACTION,
        CONSTRAINT [FK_TRN_GoodsReceiptLine_TRN_PurchaseOrderLine_PurchaseOrderLineId] FOREIGN KEY ([PurchaseOrderLineId]) REFERENCES [TRN_PurchaseOrderLine] ([PurchaseOrderLineId]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261016030926_AssetsModule'
)
BEGIN
    CREATE TABLE [TRN_StockCountLine] (
        [StockCountLineId] bigint NOT NULL IDENTITY,
        [StockCountId] bigint NOT NULL,
        [InventoryItemId] int NOT NULL,
        [CountedQuantity] decimal(18,3) NULL,
        [RecordedQuantity] decimal(18,3) NULL,
        [Difference] decimal(18,3) NULL,
        [CreatedBy] bigint NULL,
        [CreatedDate] datetime2(3) NOT NULL,
        [ModifiedBy] bigint NULL,
        [ModifiedDate] datetime2(3) NULL,
        [IsActive] bit NOT NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedBy] bigint NULL,
        [DeletedDate] datetime2(3) NULL,
        CONSTRAINT [PK_TRN_StockCountLine] PRIMARY KEY ([StockCountLineId]),
        CONSTRAINT [FK_TRN_StockCountLine_MST_InventoryItem_InventoryItemId] FOREIGN KEY ([InventoryItemId]) REFERENCES [MST_InventoryItem] ([InventoryItemId]) ON DELETE NO ACTION,
        CONSTRAINT [FK_TRN_StockCountLine_TRN_StockCount_StockCountId] FOREIGN KEY ([StockCountId]) REFERENCES [TRN_StockCount] ([StockCountId]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261016030926_AssetsModule'
)
BEGIN
    CREATE TABLE [MST_AssetDetail] (
        [InventoryItemId] int NOT NULL,
        [CodePrefix] nvarchar(30) NOT NULL,
        [CodeNumber] int NOT NULL,
        [SerialNumber] nvarchar(100) NULL,
        [AssetLocationId] int NOT NULL,
        [Condition] tinyint NOT NULL,
        [Status] tinyint NOT NULL,
        [PurchaseDate] date NULL,
        [WarrantyUntil] date NULL,
        [LabelPrintedUtc] datetime2(3) NULL,
        [GoodsReceiptLineId] bigint NULL,
        [RetiredReason] nvarchar(300) NULL,
        [RetiredValue] decimal(18,2) NULL,
        [RetiredUtc] datetime2(3) NULL,
        [CreatedBy] bigint NULL,
        [CreatedDate] datetime2(3) NOT NULL,
        [ModifiedBy] bigint NULL,
        [ModifiedDate] datetime2(3) NULL,
        CONSTRAINT [PK_MST_AssetDetail] PRIMARY KEY ([InventoryItemId]),
        CONSTRAINT [FK_MST_AssetDetail_MST_AssetLocation_AssetLocationId] FOREIGN KEY ([AssetLocationId]) REFERENCES [MST_AssetLocation] ([AssetLocationId]) ON DELETE NO ACTION,
        CONSTRAINT [FK_MST_AssetDetail_MST_InventoryItem_InventoryItemId] FOREIGN KEY ([InventoryItemId]) REFERENCES [MST_InventoryItem] ([InventoryItemId]) ON DELETE NO ACTION,
        CONSTRAINT [FK_MST_AssetDetail_TRN_GoodsReceiptLine_GoodsReceiptLineId] FOREIGN KEY ([GoodsReceiptLineId]) REFERENCES [TRN_GoodsReceiptLine] ([GoodsReceiptLineId]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261016030926_AssetsModule'
)
BEGIN
    EXEC(N'CREATE INDEX [IX_InvTxn_Source] ON [TRN_InventoryTransaction] ([SourceType], [SourceId]) WHERE [SourceType] IS NOT NULL');
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261016030926_AssetsModule'
)
BEGIN
    CREATE INDEX [IX_TRN_InventoryTransaction_AssetLocationId] ON [TRN_InventoryTransaction] ([AssetLocationId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261016030926_AssetsModule'
)
BEGIN
    CREATE INDEX [IX_InventoryItem_Kind_Category] ON [MST_InventoryItem] ([Kind], [AssetCategoryId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261016030926_AssetsModule'
)
BEGIN
    CREATE INDEX [IX_MST_InventoryItem_AssetCategoryId] ON [MST_InventoryItem] ([AssetCategoryId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261016030926_AssetsModule'
)
BEGIN
    CREATE INDEX [IX_MST_InventoryItem_KeptInLocationId] ON [MST_InventoryItem] ([KeptInLocationId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261016030926_AssetsModule'
)
BEGIN
    CREATE INDEX [IX_MST_InventoryItem_SupplierId] ON [MST_InventoryItem] ([SupplierId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261016030926_AssetsModule'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [UX_AssetCategory_Name] ON [MST_AssetCategory] ([CategoryName]) WHERE [IsDeleted] = 0');
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261016030926_AssetsModule'
)
BEGIN
    CREATE INDEX [IX_AssetDetail_Location_Status] ON [MST_AssetDetail] ([AssetLocationId], [Status]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261016030926_AssetsModule'
)
BEGIN
    EXEC(N'CREATE INDEX [IX_AssetDetail_Serial] ON [MST_AssetDetail] ([SerialNumber]) WHERE [SerialNumber] IS NOT NULL');
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261016030926_AssetsModule'
)
BEGIN
    CREATE INDEX [IX_MST_AssetDetail_GoodsReceiptLineId] ON [MST_AssetDetail] ([GoodsReceiptLineId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261016030926_AssetsModule'
)
BEGIN
    CREATE UNIQUE INDEX [UX_AssetDetail_Prefix_Number] ON [MST_AssetDetail] ([CodePrefix], [CodeNumber]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261016030926_AssetsModule'
)
BEGIN
    CREATE INDEX [IX_MST_AssetLocation_WorkspaceId] ON [MST_AssetLocation] ([WorkspaceId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261016030926_AssetsModule'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [UX_AssetLocation_Name] ON [MST_AssetLocation] ([LocationName]) WHERE [IsDeleted] = 0');
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261016030926_AssetsModule'
)
BEGIN
    CREATE INDEX [IX_AssetRepair_Item_Open] ON [TRN_AssetRepair] ([InventoryItemId], [IsOpen]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261016030926_AssetsModule'
)
BEGIN
    CREATE INDEX [IX_TRN_AssetRepair_GoodsIssueId] ON [TRN_AssetRepair] ([GoodsIssueId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261016030926_AssetsModule'
)
BEGIN
    CREATE INDEX [IX_GoodsIssue_Date_Reason] ON [TRN_GoodsIssue] ([IssueDate], [Reason]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261016030926_AssetsModule'
)
BEGIN
    CREATE INDEX [IX_TRN_GoodsIssue_FromLocationId] ON [TRN_GoodsIssue] ([FromLocationId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261016030926_AssetsModule'
)
BEGIN
    CREATE INDEX [IX_TRN_GoodsIssue_ToLocationId] ON [TRN_GoodsIssue] ([ToLocationId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261016030926_AssetsModule'
)
BEGIN
    CREATE UNIQUE INDEX [UX_GoodsIssue_Number] ON [TRN_GoodsIssue] ([IssueNumber]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261016030926_AssetsModule'
)
BEGIN
    CREATE INDEX [IX_GoodsIssueLine_Item] ON [TRN_GoodsIssueLine] ([InventoryItemId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261016030926_AssetsModule'
)
BEGIN
    CREATE INDEX [IX_TRN_GoodsIssueLine_GoodsIssueId] ON [TRN_GoodsIssueLine] ([GoodsIssueId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261016030926_AssetsModule'
)
BEGIN
    CREATE INDEX [IX_GoodsReceipt_Date_Status] ON [TRN_GoodsReceipt] ([ReceiptDate], [Status]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261016030926_AssetsModule'
)
BEGIN
    CREATE INDEX [IX_TRN_GoodsReceipt_IntoLocationId] ON [TRN_GoodsReceipt] ([IntoLocationId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261016030926_AssetsModule'
)
BEGIN
    CREATE INDEX [IX_TRN_GoodsReceipt_PurchaseOrderId] ON [TRN_GoodsReceipt] ([PurchaseOrderId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261016030926_AssetsModule'
)
BEGIN
    CREATE INDEX [IX_TRN_GoodsReceipt_SupplierId] ON [TRN_GoodsReceipt] ([SupplierId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261016030926_AssetsModule'
)
BEGIN
    CREATE UNIQUE INDEX [UX_GoodsReceipt_Number] ON [TRN_GoodsReceipt] ([ReceiptNumber]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261016030926_AssetsModule'
)
BEGIN
    CREATE INDEX [IX_GoodsReceiptLine_Item] ON [TRN_GoodsReceiptLine] ([InventoryItemId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261016030926_AssetsModule'
)
BEGIN
    CREATE INDEX [IX_TRN_GoodsReceiptLine_AssetCategoryId] ON [TRN_GoodsReceiptLine] ([AssetCategoryId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261016030926_AssetsModule'
)
BEGIN
    CREATE INDEX [IX_TRN_GoodsReceiptLine_AssetLocationId] ON [TRN_GoodsReceiptLine] ([AssetLocationId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261016030926_AssetsModule'
)
BEGIN
    CREATE INDEX [IX_TRN_GoodsReceiptLine_GoodsReceiptId] ON [TRN_GoodsReceiptLine] ([GoodsReceiptId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261016030926_AssetsModule'
)
BEGIN
    CREATE INDEX [IX_TRN_GoodsReceiptLine_PurchaseOrderLineId] ON [TRN_GoodsReceiptLine] ([PurchaseOrderLineId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261016030926_AssetsModule'
)
BEGIN
    CREATE INDEX [IX_ItemDocument_Item] ON [TRN_ItemDocument] ([InventoryItemId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261016030926_AssetsModule'
)
BEGIN
    CREATE INDEX [IX_ItemEvent_Item] ON [TRN_ItemEvent] ([InventoryItemId], [ItemEventId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261016030926_AssetsModule'
)
BEGIN
    CREATE INDEX [IX_TRN_StockCount_AssetLocationId] ON [TRN_StockCount] ([AssetLocationId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261016030926_AssetsModule'
)
BEGIN
    CREATE UNIQUE INDEX [UX_StockCount_Number] ON [TRN_StockCount] ([CountNumber]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261016030926_AssetsModule'
)
BEGIN
    CREATE INDEX [IX_TRN_StockCountLine_InventoryItemId] ON [TRN_StockCountLine] ([InventoryItemId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261016030926_AssetsModule'
)
BEGIN
    CREATE UNIQUE INDEX [UX_StockCountLine_Count_Item] ON [TRN_StockCountLine] ([StockCountId], [InventoryItemId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261016030926_AssetsModule'
)
BEGIN
    ALTER TABLE [MST_InventoryItem] ADD CONSTRAINT [FK_MST_InventoryItem_MST_AssetCategory_AssetCategoryId] FOREIGN KEY ([AssetCategoryId]) REFERENCES [MST_AssetCategory] ([AssetCategoryId]) ON DELETE NO ACTION;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261016030926_AssetsModule'
)
BEGIN
    ALTER TABLE [MST_InventoryItem] ADD CONSTRAINT [FK_MST_InventoryItem_MST_AssetLocation_KeptInLocationId] FOREIGN KEY ([KeptInLocationId]) REFERENCES [MST_AssetLocation] ([AssetLocationId]) ON DELETE NO ACTION;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261016030926_AssetsModule'
)
BEGIN
    ALTER TABLE [MST_InventoryItem] ADD CONSTRAINT [FK_MST_InventoryItem_MST_Supplier_SupplierId] FOREIGN KEY ([SupplierId]) REFERENCES [MST_Supplier] ([SupplierId]) ON DELETE NO ACTION;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261016030926_AssetsModule'
)
BEGIN
    ALTER TABLE [TRN_InventoryTransaction] ADD CONSTRAINT [FK_TRN_InventoryTransaction_MST_AssetLocation_AssetLocationId] FOREIGN KEY ([AssetLocationId]) REFERENCES [MST_AssetLocation] ([AssetLocationId]) ON DELETE NO ACTION;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261016030926_AssetsModule'
)
BEGIN

    IF NOT EXISTS (SELECT 1 FROM [MST_AssetCategory])
    INSERT INTO [MST_AssetCategory] ([CategoryName], [DefaultKind], [CodePrefix], [UsefulLifeYears], [DisplayOrder], [CreatedDate], [IsActive], [IsDeleted])
    VALUES (N'Office Interior', 2, N'R-2W-E-I', 10, 1, SYSUTCDATETIME(), 1, 0),
           (N'Electrical / Appliance', 2, N'R-2W-E-E', 7, 2, SYSUTCDATETIME(), 1, 0),
           (N'Furniture', 2, N'R-2W-E-F', 10, 3, SYSUTCDATETIME(), 1, 0),
           (N'IT Equipment', 2, N'R-2W-E-IT', 4, 4, SYSUTCDATETIME(), 1, 0),
           (N'Pantry', 1, N'PAN', NULL, 5, SYSUTCDATETIME(), 1, 0),
           (N'Housekeeping', 1, N'HK', NULL, 6, SYSUTCDATETIME(), 1, 0),
           (N'Stationery', 1, N'ST', NULL, 7, SYSUTCDATETIME(), 1, 0);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261016030926_AssetsModule'
)
BEGIN

    IF NOT EXISTS (SELECT 1 FROM [MST_AssetLocation])
    INSERT INTO [MST_AssetLocation] ([LocationName], [IsStore], [DisplayOrder], [CreatedDate], [IsActive], [IsDeleted])
    SELECT v.[Name], v.[IsStore], v.[Ord], SYSUTCDATETIME(), 1, 0
    FROM (VALUES (N'Store', 1, 1), (N'MD Cabin', 0, 2), (N'Meeting Room', 0, 3), (N'Conference Room', 0, 4),
                 (N'Private Cabin 1', 0, 5), (N'Private Cabin 2', 0, 6), (N'Private Cabin 3', 0, 7),
                 (N'Private Workstation', 0, 8), (N'Open Work Station', 0, 9), (N'Work Station', 0, 10),
                 (N'8 Seater Cabin', 0, 11), (N'Reception Area', 0, 12), (N'Server Room', 0, 13),
                 (N'Pantry Area', 0, 14), (N'Rest Room', 0, 15), (N'Rest Area', 0, 16), (N'Walking Space', 0, 17),
                 (N'Walking Workspace', 0, 18), (N'Custom Space', 0, 19), (N'Amstag Area', 0, 20), (N'Entrance', 0, 21)
         ) v([Name], [IsStore], [Ord]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261016030926_AssetsModule'
)
BEGIN

    UPDATE l SET [WorkspaceId] = w.[WorkspaceId]
    FROM [MST_AssetLocation] l
    CROSS APPLY (SELECT TOP 1 ws.[WorkspaceId] FROM [MST_Workspace] ws
                 WHERE ws.[IsDeleted] = 0
                   AND (ws.[WorkspaceName] = l.[LocationName] OR REPLACE(ws.[WorkspaceName], N' 0', N' ') = l.[LocationName])
                 ORDER BY ws.[WorkspaceId]) w
    WHERE l.[WorkspaceId] IS NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261016030926_AssetsModule'
)
BEGIN

    UPDATE i SET [AssetCategoryId] = c.[AssetCategoryId], [Category] = c.[CategoryName]
    FROM [MST_InventoryItem] i
    JOIN [MST_AssetCategory] c ON c.[CategoryName] = LTRIM(RTRIM(i.[Category]))
    WHERE i.[AssetCategoryId] IS NULL;

    UPDATE i SET [KeptInLocationId] = COALESCE(
            CASE WHEN c.[CategoryName] = N'Pantry' THEN (SELECT TOP 1 [AssetLocationId] FROM [MST_AssetLocation] WHERE [LocationName] = N'Pantry Area') END,
            (SELECT TOP 1 [AssetLocationId] FROM [MST_AssetLocation] WHERE [IsStore] = 1 ORDER BY [DisplayOrder]))
    FROM [MST_InventoryItem] i
    LEFT JOIN [MST_AssetCategory] c ON c.[AssetCategoryId] = i.[AssetCategoryId]
    WHERE i.[KeptInLocationId] IS NULL AND i.[ItemType] = 1;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261016030926_AssetsModule'
)
BEGIN

    DECLARE @admin bigint = (SELECT TOP 1 [UserId] FROM [MST_User] WHERE [UserType] = 2 AND [IsDeleted] = 0 ORDER BY [UserId]);
    IF @admin IS NOT NULL
       AND EXISTS (SELECT 1 FROM [MST_InventoryItem] WHERE [IsDeleted] = 0 AND [OpeningQuantity] > 0)
       AND EXISTS (SELECT 1 FROM [MST_NumberSequence] WHERE [SequenceKey] = N'InventoryTransaction')
    BEGIN
        DECLARE @before TABLE ([ItemId] int PRIMARY KEY, [Stock] decimal(18,3));
        INSERT INTO @before
        SELECT i.[InventoryItemId], i.[OpeningQuantity] + ISNULL((SELECT SUM(CASE WHEN t.[TransactionType] IN (2, 3) THEN -t.[Quantity] ELSE t.[Quantity] END)
                                                                FROM [TRN_InventoryTransaction] t WHERE t.[InventoryItemId] = i.[InventoryItemId] AND t.[IsDeleted] = 0), 0)
        FROM [MST_InventoryItem] i WHERE i.[IsDeleted] = 0;

        DECLARE @items TABLE ([ItemId] int PRIMARY KEY, [Qty] decimal(18,3), [Cost] decimal(18,2), [Day] date, [Branch] int, [Room] int, [Rn] int);
        INSERT INTO @items
        SELECT i.[InventoryItemId], i.[OpeningQuantity], i.[UnitCost],
               CASE WHEN f.[FirstDay] IS NOT NULL AND f.[FirstDay] < CAST(DATEADD(MINUTE, 330, i.[CreatedDate]) AS date)
                    THEN f.[FirstDay] ELSE CAST(DATEADD(MINUTE, 330, i.[CreatedDate]) AS date) END,
               i.[LocationId], i.[KeptInLocationId],
               ROW_NUMBER() OVER (ORDER BY i.[InventoryItemId])
        FROM [MST_InventoryItem] i
        OUTER APPLY (SELECT MIN(t.[TransactionDate]) AS [FirstDay] FROM [TRN_InventoryTransaction] t WHERE t.[InventoryItemId] = i.[InventoryItemId]) f
        WHERE i.[IsDeleted] = 0 AND i.[OpeningQuantity] > 0;

        DECLARE @day date = (SELECT MIN([Day]) FROM @items);
        DECLARE @prefix nvarchar(40) = N'IN-' + CAST(YEAR(@day) AS nvarchar(4));
        IF NOT EXISTS (SELECT 1 FROM [MST_CodeSequence] WHERE [Prefix] = @prefix)
            INSERT INTO [MST_CodeSequence] ([Prefix], [LastNumber]) VALUES (@prefix, 0);
        UPDATE [MST_CodeSequence] SET [LastNumber] = [LastNumber] + 1 WHERE [Prefix] = @prefix;
        DECLARE @number nvarchar(30) = @prefix + N'-' + RIGHT(N'0000' + CAST((SELECT [LastNumber] FROM [MST_CodeSequence] WHERE [Prefix] = @prefix) AS nvarchar(10)), 4);

        INSERT INTO [TRN_GoodsReceipt] ([ReceiptNumber], [ReceiptDate], [Status], [Source], [IsOpeningBalance], [TotalAmount], [Notes],
                                        [PostedByUserId], [PostedUtc], [CreatedBy], [CreatedDate], [IsActive], [IsDeleted])
        VALUES (@number, @day, 2, 6, 1, (SELECT ISNULL(SUM(ROUND([Qty] * ISNULL([Cost], 0), 2)), 0) FROM @items),
                N'Opening quantities from the old item form, moved onto the ledger', @admin, SYSUTCDATETIME(), @admin, SYSUTCDATETIME(), 1, 0);
        DECLARE @receipt bigint = SCOPE_IDENTITY();

        INSERT INTO [TRN_GoodsReceiptLine] ([GoodsReceiptId], [LineNumber], [Kind], [InventoryItemId], [Quantity], [Rate], [Amount],
                                            [CreatedBy], [CreatedDate], [IsActive], [IsDeleted])
        SELECT @receipt, [Rn], 1, [ItemId], [Qty], [Cost], ROUND([Qty] * ISNULL([Cost], 0), 2), @admin, SYSUTCDATETIME(), 1, 0 FROM @items;

        DECLARE @seq bigint = (SELECT [CurrentValue] FROM [MST_NumberSequence] WHERE [SequenceKey] = N'InventoryTransaction');
        DECLARE @itx nvarchar(10) = (SELECT [Prefix] FROM [MST_NumberSequence] WHERE [SequenceKey] = N'InventoryTransaction');
        INSERT INTO [TRN_InventoryTransaction] ([InventoryItemId], [TransactionNumber], [TransactionType], [Quantity], [TransactionDate],
                [SourceOrDestination], [ReferenceNumber], [UnitCost], [LocationId], [EnteredByUserId], [Notes],
                [SourceType], [SourceId], [AssetLocationId], [CreatedBy], [CreatedDate], [IsActive], [IsDeleted])
        SELECT [ItemId], @itx + RIGHT(N'000000' + CAST(@seq + [Rn] AS nvarchar(20)), 6), 4, [Qty], [Day],
               N'Opening balance', @number, [Cost], [Branch], @admin, N'Opening quantity moved onto the ledger',
               1, @receipt, [Room], @admin, SYSUTCDATETIME(), 1, 0
        FROM @items;
        UPDATE [MST_NumberSequence] SET [CurrentValue] = @seq + (SELECT COUNT(*) FROM @items) WHERE [SequenceKey] = N'InventoryTransaction';

        INSERT INTO [TRN_ItemEvent] ([InventoryItemId], [EventType], [EventDate], [Summary], [FromText], [DocumentNumber], [ActorUserId], [CreatedUtc])
        SELECT [ItemId], N'RECEIVED', [Day], N'Opening balance (moved from the old opening quantity)', @number, @number, @admin, SYSUTCDATETIME() FROM @items;

        UPDATE i SET [OpeningQuantity] = 0 FROM [MST_InventoryItem] i JOIN @items x ON x.[ItemId] = i.[InventoryItemId];

        IF EXISTS (
            SELECT 1 FROM @before b
            JOIN [MST_InventoryItem] i ON i.[InventoryItemId] = b.[ItemId]
            WHERE b.[Stock] <> i.[OpeningQuantity] + ISNULL((SELECT SUM(CASE WHEN t.[TransactionType] IN (2, 3) THEN -t.[Quantity] ELSE t.[Quantity] END)
                                                           FROM [TRN_InventoryTransaction] t WHERE t.[InventoryItemId] = b.[ItemId] AND t.[IsDeleted] = 0), 0))
            THROW 50101, N'AssetsModule: moving opening quantities onto the ledger changed a stock figure.', 1;
    END;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261016030926_AssetsModule'
)
BEGIN

    IF EXISTS (SELECT 1 FROM [MST_Permission])
    BEGIN
        DECLARE @base int = (SELECT ISNULL(MAX([DisplayOrder]), 0) FROM [MST_Permission]);
        INSERT INTO [MST_Permission] ([PermissionCode], [ModuleName], [ActionName], [Description], [DisplayOrder], [CreatedDate], [IsActive], [IsDeleted])
        SELECT v.[Code], N'INVENTORY', v.[Action], v.[Action] + N' on INVENTORY', @base + v.[Ord], SYSUTCDATETIME(), 1, 0
        FROM (VALUES (N'INVENTORY.RECEIVE', N'RECEIVE', 1), (N'INVENTORY.ISSUE', N'ISSUE', 2), (N'INVENTORY.LABELS', N'LABELS', 3),
                     (N'INVENTORY.COUNT', N'COUNT', 4), (N'INVENTORY.REVERSE', N'REVERSE', 5)) v([Code], [Action], [Ord])
        WHERE NOT EXISTS (SELECT 1 FROM [MST_Permission] p WHERE p.[PermissionCode] = v.[Code]);

        -- A role that may add items today may also receive, issue, print labels and count.
        INSERT INTO [MAP_RolePermission] ([RoleId], [PermissionId])
        SELECT DISTINCT rp.[RoleId], np.[PermissionId]
        FROM [MAP_RolePermission] rp
        JOIN [MST_Permission] cp ON cp.[PermissionId] = rp.[PermissionId] AND cp.[PermissionCode] = N'INVENTORY.CREATE'
        JOIN [MST_Permission] np ON np.[PermissionCode] IN (N'INVENTORY.RECEIVE', N'INVENTORY.ISSUE', N'INVENTORY.LABELS', N'INVENTORY.COUNT')
        JOIN [MST_Role] r ON r.[RoleId] = rp.[RoleId] AND r.[RoleCode] <> N'CUSTOMER'
        WHERE NOT EXISTS (SELECT 1 FROM [MAP_RolePermission] x WHERE x.[RoleId] = rp.[RoleId] AND x.[PermissionId] = np.[PermissionId]);

        -- Reversing changes history: the administrator only.
        INSERT INTO [MAP_RolePermission] ([RoleId], [PermissionId])
        SELECT r.[RoleId], p.[PermissionId]
        FROM [MST_Role] r JOIN [MST_Permission] p ON p.[PermissionCode] = N'INVENTORY.REVERSE'
        WHERE r.[RoleCode] = N'ADMIN'
          AND NOT EXISTS (SELECT 1 FROM [MAP_RolePermission] x WHERE x.[RoleId] = r.[RoleId] AND x.[PermissionId] = p.[PermissionId]);
    END;

    IF EXISTS (SELECT 1 FROM [MST_AppSetting])
    INSERT INTO [MST_AppSetting] ([SettingKey], [SettingValue], [SettingGroup], [DataType], [Description], [IsEditableByAdmin], [IsSecret], [CreatedDate], [IsActive], [IsDeleted])
    SELECT v.[K], v.[V], N'Assets', v.[T], v.[D], 1, 0, SYSUTCDATETIME(), 1, 0
    FROM (VALUES (N'Assets.LabelContactEmail', N'admin@regalis.in', N'string', N'QR labels: If found, contact this address'),
                 (N'Assets.LabelOwnerName', N'Regalis Workspace', N'string', N'QR labels: Property of this name'),
                 (N'Assets.WarrantyAlertDays', N'60', N'int', N'Days before Warranty until that an asset is flagged Expiring soon'),
                 (N'Assets.Units', N'kg,litre,piece,box,bottle,ream,packet,Nos', N'string', N'Units the consumable form offers, comma-separated')
         ) v([K], [V], [T], [D])
    WHERE NOT EXISTS (SELECT 1 FROM [MST_AppSetting] s WHERE s.[SettingKey] = v.[K]);

    UPDATE [MST_Menu] SET [Route] = N'/admin/assets/items' WHERE [MenuCode] IN (N'ADM_INVENTORY', N'ADM_INVENTORY_ITEMS') AND [Route] = N'/admin/inventory/items';
    UPDATE [MST_Menu] SET [Route] = N'/admin/assets/incoming' WHERE [MenuCode] = N'ADM_INVENTORY_INCOMING' AND [Route] = N'/admin/inventory/incoming';
    UPDATE [MST_Menu] SET [Route] = N'/admin/assets/outgoing' WHERE [MenuCode] = N'ADM_INVENTORY_OUTGOING' AND [Route] = N'/admin/inventory/outgoing';
    UPDATE [MST_Menu] SET [Route] = N'/admin/assets/stock' WHERE [MenuCode] = N'ADM_INVENTORY_STOCK' AND [Route] = N'/admin/inventory/stock';
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261016030926_AssetsModule'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261016030926_AssetsModule', N'8.0.19');
END;
GO

COMMIT;
GO

