BEGIN TRANSACTION;
GO

IF EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261014075619_WorkspaceTypeManagement'
)
BEGIN

    DECLARE @desk int = (SELECT [WorkspaceTypeId] FROM [MST_WorkspaceType] WHERE [TypeCode] = N'DESK');
    DECLARE @hot int = (SELECT [WorkspaceTypeId] FROM [MST_WorkspaceType] WHERE [TypeCode] = N'HOT_DESK');
    IF @hot IS NOT NULL AND @desk IS NOT NULL
    BEGIN
        UPDATE [TRN_Booking] SET [WorkspaceTypeId] = @desk WHERE [WorkspaceTypeId] = @hot;
        UPDATE [TRN_QuotationLine] SET [WorkspaceTypeId] = @desk WHERE [WorkspaceTypeId] = @hot;
        UPDATE [MST_Workspace] SET [WorkspaceTypeId] = @desk WHERE [WorkspaceTypeId] = @hot;
        DELETE FROM [MST_WorkspaceType] WHERE [WorkspaceTypeId] = @hot;
        UPDATE [MST_WorkspaceType] SET [DisplayOrder] = 2 WHERE [TypeCode] = N'CABIN';
        UPDATE [MST_WorkspaceType] SET [DisplayOrder] = 3 WHERE [TypeCode] = N'MEETING_ROOM';
        UPDATE [MST_WorkspaceType] SET [DisplayOrder] = 4 WHERE [TypeCode] = N'VIRTUAL_WORKSPACE';
    END;
END;
GO

IF EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261014075619_WorkspaceTypeManagement'
)
BEGIN
    DROP INDEX [UX_WorkspaceType_Name] ON [MST_WorkspaceType];
END;
GO

IF EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261014075619_WorkspaceTypeManagement'
)
BEGIN
    ALTER TABLE [MST_WorkspaceType] DROP CONSTRAINT [CK_WorkspaceType_BookingModes];
END;
GO

IF EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261014075619_WorkspaceTypeManagement'
)
BEGIN
    ALTER TABLE [MST_WorkspaceType] DROP CONSTRAINT [CK_WorkspaceType_CodePrefix];
END;
GO

IF EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261014075619_WorkspaceTypeManagement'
)
BEGIN
    DECLARE @var0 sysname;
    SELECT @var0 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[MST_WorkspaceType]') AND [c].[name] = N'NameKey');
    IF @var0 IS NOT NULL EXEC(N'ALTER TABLE [MST_WorkspaceType] DROP CONSTRAINT [' + @var0 + '];');
    ALTER TABLE [MST_WorkspaceType] DROP COLUMN [NameKey];
END;
GO

IF EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261014075619_WorkspaceTypeManagement'
)
BEGIN
    DECLARE @var1 sysname;
    SELECT @var1 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[MST_WorkspaceType]') AND [c].[name] = N'BookingModes');
    IF @var1 IS NOT NULL EXEC(N'ALTER TABLE [MST_WorkspaceType] DROP CONSTRAINT [' + @var1 + '];');
    ALTER TABLE [MST_WorkspaceType] DROP COLUMN [BookingModes];
END;
GO

IF EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261014075619_WorkspaceTypeManagement'
)
BEGIN
    DECLARE @var2 sysname;
    SELECT @var2 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[MST_WorkspaceType]') AND [c].[name] = N'CodePrefix');
    IF @var2 IS NOT NULL EXEC(N'ALTER TABLE [MST_WorkspaceType] DROP CONSTRAINT [' + @var2 + '];');
    ALTER TABLE [MST_WorkspaceType] DROP COLUMN [CodePrefix];
END;
GO

IF EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261014075619_WorkspaceTypeManagement'
)
BEGIN
    DECLARE @var3 sysname;
    SELECT @var3 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[MST_WorkspaceType]') AND [c].[name] = N'CustomerBookable');
    IF @var3 IS NOT NULL EXEC(N'ALTER TABLE [MST_WorkspaceType] DROP CONSTRAINT [' + @var3 + '];');
    ALTER TABLE [MST_WorkspaceType] DROP COLUMN [CustomerBookable];
END;
GO

IF EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261014075619_WorkspaceTypeManagement'
)
BEGIN
    DECLARE @var4 sysname;
    SELECT @var4 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[MST_WorkspaceType]') AND [c].[name] = N'IsSystem');
    IF @var4 IS NOT NULL EXEC(N'ALTER TABLE [MST_WorkspaceType] DROP CONSTRAINT [' + @var4 + '];');
    ALTER TABLE [MST_WorkspaceType] DROP COLUMN [IsSystem];
END;
GO

IF EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261014075619_WorkspaceTypeManagement'
)
BEGIN
    DELETE FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261014075619_WorkspaceTypeManagement';
END;
GO

COMMIT;
GO

