BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260930123139_ExpenseAdjustments'
)
BEGIN
    ALTER TABLE [TRN_Expense] ADD [AdjustmentTotal] decimal(18,2) NOT NULL DEFAULT 0.0;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260930123139_ExpenseAdjustments'
)
BEGIN
    CREATE TABLE [TRN_ExpenseAdjustment] (
        [ExpenseAdjustmentId] bigint NOT NULL IDENTITY,
        [ExpenseId] bigint NOT NULL,
        [AdjustmentType] tinyint NOT NULL,
        [Amount] decimal(18,2) NOT NULL,
        [AdjustmentDate] date NOT NULL,
        [Reason] nvarchar(500) NOT NULL,
        [Remarks] nvarchar(500) NULL,
        [CreatedBy] bigint NULL,
        [CreatedDate] datetime2(3) NOT NULL,
        [ModifiedBy] bigint NULL,
        [ModifiedDate] datetime2(3) NULL,
        [IsActive] bit NOT NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedBy] bigint NULL,
        [DeletedDate] datetime2(3) NULL,
        CONSTRAINT [PK_TRN_ExpenseAdjustment] PRIMARY KEY ([ExpenseAdjustmentId]),
        CONSTRAINT [CK_ExpenseAdjustment_Amount] CHECK ([Amount] > 0),
        CONSTRAINT [CK_ExpenseAdjustment_Type] CHECK ([AdjustmentType] IN (1, 2)),
        CONSTRAINT [FK_TRN_ExpenseAdjustment_TRN_Expense_ExpenseId] FOREIGN KEY ([ExpenseId]) REFERENCES [TRN_Expense] ([ExpenseId]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260930123139_ExpenseAdjustments'
)
BEGIN
    EXEC(N'ALTER TABLE [TRN_Expense] ADD CONSTRAINT [CK_Expense_NetAmount] CHECK ([Amount] + [AdjustmentTotal] >= 0)');
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260930123139_ExpenseAdjustments'
)
BEGIN
    CREATE INDEX [IX_ExpenseAdjustment_Date] ON [TRN_ExpenseAdjustment] ([AdjustmentDate]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260930123139_ExpenseAdjustments'
)
BEGIN
    CREATE INDEX [IX_ExpenseAdjustment_Expense_Date] ON [TRN_ExpenseAdjustment] ([ExpenseId], [AdjustmentDate]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260930123139_ExpenseAdjustments'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260930123139_ExpenseAdjustments', N'8.0.19');
END;
GO

COMMIT;
GO

