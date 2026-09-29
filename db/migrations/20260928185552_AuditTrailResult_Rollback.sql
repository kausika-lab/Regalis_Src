BEGIN TRANSACTION;
GO

DROP INDEX [IX_AuditLog_Action_Timestamp] ON [TRN_AuditLog];
GO

DROP INDEX [IX_AuditLog_Module_Timestamp] ON [TRN_AuditLog];
GO

DROP INDEX [IX_AuditLog_Result_Timestamp] ON [TRN_AuditLog];
GO

DECLARE @var0 sysname;
SELECT @var0 = [d].[name]
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[TRN_AuditLog]') AND [c].[name] = N'ChangedFields');
IF @var0 IS NOT NULL EXEC(N'ALTER TABLE [TRN_AuditLog] DROP CONSTRAINT [' + @var0 + '];');
ALTER TABLE [TRN_AuditLog] DROP COLUMN [ChangedFields];
GO

DECLARE @var1 sysname;
SELECT @var1 = [d].[name]
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[TRN_AuditLog]') AND [c].[name] = N'FailureReason');
IF @var1 IS NOT NULL EXEC(N'ALTER TABLE [TRN_AuditLog] DROP CONSTRAINT [' + @var1 + '];');
ALTER TABLE [TRN_AuditLog] DROP COLUMN [FailureReason];
GO

DECLARE @var2 sysname;
SELECT @var2 = [d].[name]
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[TRN_AuditLog]') AND [c].[name] = N'Result');
IF @var2 IS NOT NULL EXEC(N'ALTER TABLE [TRN_AuditLog] DROP CONSTRAINT [' + @var2 + '];');
ALTER TABLE [TRN_AuditLog] DROP COLUMN [Result];
GO

DELETE FROM [__EFMigrationsHistory]
WHERE [MigrationId] = N'20260928185552_AuditTrailResult';
GO

COMMIT;
GO

