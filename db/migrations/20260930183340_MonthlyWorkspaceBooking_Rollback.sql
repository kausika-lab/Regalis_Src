BEGIN TRANSACTION;
GO

IF EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260930183340_MonthlyWorkspaceBooking'
)
BEGIN
    DECLARE @var0 sysname;
    SELECT @var0 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[TRN_Booking]') AND [c].[name] = N'BillingMonths');
    IF @var0 IS NOT NULL EXEC(N'ALTER TABLE [TRN_Booking] DROP CONSTRAINT [' + @var0 + '];');
    ALTER TABLE [TRN_Booking] DROP COLUMN [BillingMonths];
END;
GO

IF EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260930183340_MonthlyWorkspaceBooking'
)
BEGIN
    DELETE FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260930183340_MonthlyWorkspaceBooking';
END;
GO

COMMIT;
GO

