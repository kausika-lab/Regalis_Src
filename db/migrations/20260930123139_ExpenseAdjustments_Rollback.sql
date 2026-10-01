BEGIN TRANSACTION;
GO

IF EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260930123139_ExpenseAdjustments'
)
BEGIN
    DROP TABLE [TRN_ExpenseAdjustment];
END;
GO

IF EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260930123139_ExpenseAdjustments'
)
BEGIN
    ALTER TABLE [TRN_Expense] DROP CONSTRAINT [CK_Expense_NetAmount];
END;
GO

IF EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260930123139_ExpenseAdjustments'
)
BEGIN
    DECLARE @var0 sysname;
    SELECT @var0 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[TRN_Expense]') AND [c].[name] = N'AdjustmentTotal');
    IF @var0 IS NOT NULL EXEC(N'ALTER TABLE [TRN_Expense] DROP CONSTRAINT [' + @var0 + '];');
    ALTER TABLE [TRN_Expense] DROP COLUMN [AdjustmentTotal];
END;
GO

IF EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260930123139_ExpenseAdjustments'
)
BEGIN
    DELETE FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260930123139_ExpenseAdjustments';
END;
GO

COMMIT;
GO

