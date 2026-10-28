BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261027182059_PayLaterCash'
)
BEGIN
    ALTER TABLE [TRN_Booking] ADD [AmountPaid] decimal(18,2) NOT NULL DEFAULT 0.0;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261027182059_PayLaterCash'
)
BEGIN
    ALTER TABLE [TRN_Booking] ADD [IsPayLater] bit NOT NULL DEFAULT CAST(0 AS bit);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261027182059_PayLaterCash'
)
BEGIN
    ALTER TABLE [TRN_Booking] ADD [PayLaterReminderSentUtc] datetime2(3) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261027182059_PayLaterCash'
)
BEGIN
    ALTER TABLE [TRN_Booking] ADD [PaymentDueUtc] datetime2(3) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261027182059_PayLaterCash'
)
BEGIN
    ALTER TABLE [MST_User] ADD [PayLaterAllowed] bit NOT NULL DEFAULT CAST(1 AS bit);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261027182059_PayLaterCash'
)
BEGIN
    CREATE TABLE [TRN_CashRegisterSession] (
        [CashRegisterSessionId] bigint NOT NULL IDENTITY,
        [SessionNumber] nvarchar(30) NOT NULL,
        [LocationId] int NOT NULL,
        [UserId] bigint NOT NULL,
        [BusinessDate] date NOT NULL,
        [OpeningFloat] decimal(18,2) NOT NULL,
        [OpenedUtc] datetime2(3) NOT NULL,
        [ExpectedCash] decimal(18,2) NULL,
        [CountedCash] decimal(18,2) NULL,
        [Variance] AS [CountedCash] - [ExpectedCash] PERSISTED,
        [VarianceReason] nvarchar(500) NULL,
        [Status] tinyint NOT NULL,
        [ClosedUtc] datetime2(3) NULL,
        [HandedOverToUserId] bigint NULL,
        [HandedOverUtc] datetime2(3) NULL,
        [RowVersion] rowversion NOT NULL,
        [CreatedBy] bigint NULL,
        [CreatedDate] datetime2(3) NOT NULL,
        [ModifiedBy] bigint NULL,
        [ModifiedDate] datetime2(3) NULL,
        [IsActive] bit NOT NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedBy] bigint NULL,
        [DeletedDate] datetime2(3) NULL,
        CONSTRAINT [PK_TRN_CashRegisterSession] PRIMARY KEY ([CashRegisterSessionId]),
        CONSTRAINT [CK_CashSession_Closed] CHECK ([Status] = 1 OR ([ClosedUtc] IS NOT NULL AND [ExpectedCash] IS NOT NULL AND [CountedCash] IS NOT NULL)),
        CONSTRAINT [CK_CashSession_Float] CHECK ([OpeningFloat] >= 0),
        CONSTRAINT [CK_CashSession_VarianceReason] CHECK ([Status] = 1 OR [CountedCash] = [ExpectedCash] OR [VarianceReason] IS NOT NULL),
        CONSTRAINT [FK_TRN_CashRegisterSession_MST_Location_LocationId] FOREIGN KEY ([LocationId]) REFERENCES [MST_Location] ([LocationId]) ON DELETE NO ACTION,
        CONSTRAINT [FK_TRN_CashRegisterSession_MST_User_HandedOverToUserId] FOREIGN KEY ([HandedOverToUserId]) REFERENCES [MST_User] ([UserId]) ON DELETE NO ACTION,
        CONSTRAINT [FK_TRN_CashRegisterSession_MST_User_UserId] FOREIGN KEY ([UserId]) REFERENCES [MST_User] ([UserId]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261027182059_PayLaterCash'
)
BEGIN
    CREATE TABLE [TRN_CashCollection] (
        [CashCollectionId] bigint NOT NULL IDENTITY,
        [ReceiptNumber] nvarchar(30) NOT NULL,
        [BookingId] bigint NOT NULL,
        [InvoiceId] bigint NULL,
        [PaymentId] bigint NULL,
        [CustomerId] bigint NOT NULL,
        [LocationId] int NOT NULL,
        [PaymentMethod] tinyint NOT NULL,
        [Amount] decimal(18,2) NOT NULL,
        [AmountTendered] decimal(18,2) NULL,
        [ChangeReturned] decimal(18,2) NULL,
        [ReferenceNumber] nvarchar(50) NULL,
        [CollectedByUserId] bigint NOT NULL,
        [CollectedUtc] datetime2(3) NOT NULL,
        [CashRegisterSessionId] bigint NULL,
        [Status] tinyint NOT NULL,
        [VoidedByUserId] bigint NULL,
        [VoidedUtc] datetime2(3) NULL,
        [VoidReason] nvarchar(500) NULL,
        [Notes] nvarchar(500) NULL,
        [CreatedBy] bigint NULL,
        [CreatedDate] datetime2(3) NOT NULL,
        [ModifiedBy] bigint NULL,
        [ModifiedDate] datetime2(3) NULL,
        [IsActive] bit NOT NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedBy] bigint NULL,
        [DeletedDate] datetime2(3) NULL,
        CONSTRAINT [PK_TRN_CashCollection] PRIMARY KEY ([CashCollectionId]),
        CONSTRAINT [CK_CashCollection_Amount] CHECK ([Amount] <> 0),
        CONSTRAINT [CK_CashCollection_Change] CHECK ([AmountTendered] IS NULL OR ([PaymentMethod] = 6 AND [AmountTendered] - ISNULL([ChangeReturned], 0) = [Amount])),
        CONSTRAINT [CK_CashCollection_Method] CHECK ([PaymentMethod] IN (2,3,4,6)),
        CONSTRAINT [CK_CashCollection_Reference] CHECK ([PaymentMethod] = 6 OR [ReferenceNumber] IS NOT NULL),
        CONSTRAINT [CK_CashCollection_Void] CHECK (([Status] = 2 AND [VoidedByUserId] IS NOT NULL AND [VoidedUtc] IS NOT NULL AND [VoidReason] IS NOT NULL) OR ([Status] = 1 AND [VoidedUtc] IS NULL)),
        CONSTRAINT [FK_TRN_CashCollection_MST_Location_LocationId] FOREIGN KEY ([LocationId]) REFERENCES [MST_Location] ([LocationId]) ON DELETE NO ACTION,
        CONSTRAINT [FK_TRN_CashCollection_MST_User_CollectedByUserId] FOREIGN KEY ([CollectedByUserId]) REFERENCES [MST_User] ([UserId]) ON DELETE NO ACTION,
        CONSTRAINT [FK_TRN_CashCollection_MST_User_CustomerId] FOREIGN KEY ([CustomerId]) REFERENCES [MST_User] ([UserId]) ON DELETE NO ACTION,
        CONSTRAINT [FK_TRN_CashCollection_TRN_Booking_BookingId] FOREIGN KEY ([BookingId]) REFERENCES [TRN_Booking] ([BookingId]) ON DELETE NO ACTION,
        CONSTRAINT [FK_TRN_CashCollection_TRN_CashRegisterSession_CashRegisterSessionId] FOREIGN KEY ([CashRegisterSessionId]) REFERENCES [TRN_CashRegisterSession] ([CashRegisterSessionId]) ON DELETE NO ACTION,
        CONSTRAINT [FK_TRN_CashCollection_TRN_Invoice_InvoiceId] FOREIGN KEY ([InvoiceId]) REFERENCES [TRN_Invoice] ([InvoiceId]) ON DELETE NO ACTION,
        CONSTRAINT [FK_TRN_CashCollection_TRN_Payment_PaymentId] FOREIGN KEY ([PaymentId]) REFERENCES [TRN_Payment] ([PaymentId]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261027182059_PayLaterCash'
)
BEGIN
    EXEC(N'CREATE INDEX [IX_Booking_PayLater_Due] ON [TRN_Booking] ([PaymentDueUtc]) INCLUDE ([CustomerId], [Status], [TotalAmount], [AmountPaid]) WHERE [IsPayLater] = 1 AND [IsDeleted] = 0');
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261027182059_PayLaterCash'
)
BEGIN
    EXEC(N'ALTER TABLE [TRN_Booking] ADD CONSTRAINT [CK_Booking_AmountPaid] CHECK ([AmountPaid] >= 0)');
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261027182059_PayLaterCash'
)
BEGIN
    CREATE INDEX [IX_CashCollection_Booking] ON [TRN_CashCollection] ([BookingId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261027182059_PayLaterCash'
)
BEGIN
    CREATE INDEX [IX_CashCollection_Collector_Date] ON [TRN_CashCollection] ([CollectedByUserId], [CollectedUtc]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261027182059_PayLaterCash'
)
BEGIN
    CREATE INDEX [IX_CashCollection_Location_Date] ON [TRN_CashCollection] ([LocationId], [CollectedUtc]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261027182059_PayLaterCash'
)
BEGIN
    CREATE INDEX [IX_CashCollection_Session] ON [TRN_CashCollection] ([CashRegisterSessionId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261027182059_PayLaterCash'
)
BEGIN
    CREATE INDEX [IX_TRN_CashCollection_CustomerId] ON [TRN_CashCollection] ([CustomerId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261027182059_PayLaterCash'
)
BEGIN
    CREATE INDEX [IX_TRN_CashCollection_InvoiceId] ON [TRN_CashCollection] ([InvoiceId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261027182059_PayLaterCash'
)
BEGIN
    CREATE INDEX [IX_TRN_CashCollection_PaymentId] ON [TRN_CashCollection] ([PaymentId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261027182059_PayLaterCash'
)
BEGIN
    CREATE UNIQUE INDEX [UX_CashCollection_Receipt] ON [TRN_CashCollection] ([ReceiptNumber]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261027182059_PayLaterCash'
)
BEGIN
    CREATE INDEX [IX_CashSession_Status_Date] ON [TRN_CashRegisterSession] ([Status], [BusinessDate]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261027182059_PayLaterCash'
)
BEGIN
    CREATE INDEX [IX_TRN_CashRegisterSession_HandedOverToUserId] ON [TRN_CashRegisterSession] ([HandedOverToUserId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261027182059_PayLaterCash'
)
BEGIN
    CREATE INDEX [IX_TRN_CashRegisterSession_UserId] ON [TRN_CashRegisterSession] ([UserId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261027182059_PayLaterCash'
)
BEGIN
    CREATE UNIQUE INDEX [UX_CashSession_Location_User_Date] ON [TRN_CashRegisterSession] ([LocationId], [UserId], [BusinessDate]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261027182059_PayLaterCash'
)
BEGIN
    CREATE UNIQUE INDEX [UX_CashSession_Number] ON [TRN_CashRegisterSession] ([SessionNumber]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261027182059_PayLaterCash'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261027182059_PayLaterCash', N'8.0.19');
END;
GO

COMMIT;
GO

