BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260928185552_AuditTrailResult'
)
BEGIN
    ALTER TABLE [TRN_AuditLog] ADD [ChangedFields] nvarchar(1000) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260928185552_AuditTrailResult'
)
BEGIN
    ALTER TABLE [TRN_AuditLog] ADD [FailureReason] nvarchar(500) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260928185552_AuditTrailResult'
)
BEGIN
    ALTER TABLE [TRN_AuditLog] ADD [Result] nvarchar(10) NOT NULL DEFAULT N'Success';
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260928185552_AuditTrailResult'
)
BEGIN
    CREATE INDEX [IX_AuditLog_Action_Timestamp] ON [TRN_AuditLog] ([Action], [TimestampUtc]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260928185552_AuditTrailResult'
)
BEGIN
    CREATE INDEX [IX_AuditLog_Module_Timestamp] ON [TRN_AuditLog] ([ModuleName], [TimestampUtc]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260928185552_AuditTrailResult'
)
BEGIN
    CREATE INDEX [IX_AuditLog_Result_Timestamp] ON [TRN_AuditLog] ([Result], [TimestampUtc]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260928185552_AuditTrailResult'
)
BEGIN
    UPDATE TRN_AuditLog
    SET Result = N'Denied', FailureReason = N'Missing permission: ' + ISNULL(EntityId, N'?')
    WHERE Action = N'AUTHORIZATION_FAILED' AND FailureReason IS NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260928185552_AuditTrailResult'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260928185552_AuditTrailResult', N'8.0.19');
END;
GO

COMMIT;
GO

