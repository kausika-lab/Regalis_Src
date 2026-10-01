BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260930183340_MonthlyWorkspaceBooking'
)
BEGIN
    ALTER TABLE [TRN_Booking] ADD [BillingMonths] tinyint NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260930183340_MonthlyWorkspaceBooking'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260930183340_MonthlyWorkspaceBooking', N'8.0.19');
END;
GO

COMMIT;
GO

