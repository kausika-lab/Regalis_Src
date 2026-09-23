/* 20260922_AddUserProfileFields_Rollback.sql
   Reverses 20260922_AddUserProfileFields.sql.

   DESTRUCTIVE. Dropping the columns discards every address, date of birth and GSTIN
   collected since the forward script ran. There is no way back: the legacy single-line
   Address keeps a flattened copy of the address, but the parts, the DOB and the GSTIN
   are lost. Export MST_User first if any of it matters.

   Deploy the previous API binaries BEFORE running this. The shipped API selects these
   columns by name on every MST_User query, so dropping them under a running new API
   breaks login for everyone. */

BEGIN TRANSACTION;
GO

IF EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260922172428_AddUserProfileFields'
)
BEGIN
    DECLARE @var0 sysname;
    SELECT @var0 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[MST_User]') AND [c].[name] = N'AddressLine1');
    IF @var0 IS NOT NULL EXEC(N'ALTER TABLE [MST_User] DROP CONSTRAINT [' + @var0 + '];');
    ALTER TABLE [MST_User] DROP COLUMN [AddressLine1];
END;
GO

IF EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260922172428_AddUserProfileFields'
)
BEGIN
    DECLARE @var1 sysname;
    SELECT @var1 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[MST_User]') AND [c].[name] = N'AddressLine2');
    IF @var1 IS NOT NULL EXEC(N'ALTER TABLE [MST_User] DROP CONSTRAINT [' + @var1 + '];');
    ALTER TABLE [MST_User] DROP COLUMN [AddressLine2];
END;
GO

IF EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260922172428_AddUserProfileFields'
)
BEGIN
    DECLARE @var2 sysname;
    SELECT @var2 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[MST_User]') AND [c].[name] = N'City');
    IF @var2 IS NOT NULL EXEC(N'ALTER TABLE [MST_User] DROP CONSTRAINT [' + @var2 + '];');
    ALTER TABLE [MST_User] DROP COLUMN [City];
END;
GO

IF EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260922172428_AddUserProfileFields'
)
BEGIN
    DECLARE @var3 sysname;
    SELECT @var3 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[MST_User]') AND [c].[name] = N'Country');
    IF @var3 IS NOT NULL EXEC(N'ALTER TABLE [MST_User] DROP CONSTRAINT [' + @var3 + '];');
    ALTER TABLE [MST_User] DROP COLUMN [Country];
END;
GO

IF EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260922172428_AddUserProfileFields'
)
BEGIN
    DECLARE @var4 sysname;
    SELECT @var4 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[MST_User]') AND [c].[name] = N'DateOfBirth');
    IF @var4 IS NOT NULL EXEC(N'ALTER TABLE [MST_User] DROP CONSTRAINT [' + @var4 + '];');
    ALTER TABLE [MST_User] DROP COLUMN [DateOfBirth];
END;
GO

IF EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260922172428_AddUserProfileFields'
)
BEGIN
    DECLARE @var5 sysname;
    SELECT @var5 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[MST_User]') AND [c].[name] = N'Gender');
    IF @var5 IS NOT NULL EXEC(N'ALTER TABLE [MST_User] DROP CONSTRAINT [' + @var5 + '];');
    ALTER TABLE [MST_User] DROP COLUMN [Gender];
END;
GO

IF EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260922172428_AddUserProfileFields'
)
BEGIN
    DECLARE @var6 sysname;
    SELECT @var6 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[MST_User]') AND [c].[name] = N'Gstin');
    IF @var6 IS NOT NULL EXEC(N'ALTER TABLE [MST_User] DROP CONSTRAINT [' + @var6 + '];');
    ALTER TABLE [MST_User] DROP COLUMN [Gstin];
END;
GO

IF EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260922172428_AddUserProfileFields'
)
BEGIN
    DECLARE @var7 sysname;
    SELECT @var7 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[MST_User]') AND [c].[name] = N'PostalCode');
    IF @var7 IS NOT NULL EXEC(N'ALTER TABLE [MST_User] DROP CONSTRAINT [' + @var7 + '];');
    ALTER TABLE [MST_User] DROP COLUMN [PostalCode];
END;
GO

IF EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260922172428_AddUserProfileFields'
)
BEGIN
    DECLARE @var8 sysname;
    SELECT @var8 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[MST_User]') AND [c].[name] = N'State');
    IF @var8 IS NOT NULL EXEC(N'ALTER TABLE [MST_User] DROP CONSTRAINT [' + @var8 + '];');
    ALTER TABLE [MST_User] DROP COLUMN [State];
END;
GO

IF EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260922172428_AddUserProfileFields'
)
BEGIN
    DELETE FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260922172428_AddUserProfileFields';
END;
GO

COMMIT;
GO

