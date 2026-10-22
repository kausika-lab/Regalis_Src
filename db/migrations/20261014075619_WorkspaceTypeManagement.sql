BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261014075619_WorkspaceTypeManagement'
)
BEGIN
    ALTER TABLE [MST_WorkspaceType] ADD [BookingModes] tinyint NOT NULL DEFAULT CAST(1 AS tinyint);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261014075619_WorkspaceTypeManagement'
)
BEGIN
    ALTER TABLE [MST_WorkspaceType] ADD [CodePrefix] nchar(1) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261014075619_WorkspaceTypeManagement'
)
BEGIN
    ALTER TABLE [MST_WorkspaceType] ADD [CustomerBookable] bit NOT NULL DEFAULT CAST(0 AS bit);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261014075619_WorkspaceTypeManagement'
)
BEGIN
    ALTER TABLE [MST_WorkspaceType] ADD [IsSystem] bit NOT NULL DEFAULT CAST(0 AS bit);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261014075619_WorkspaceTypeManagement'
)
BEGIN

    UPDATE [MST_WorkspaceType] SET [IsSystem] = 1, [CodePrefix] = N'D', [BookingModes] = 3, [CustomerBookable] = 1, [DisplayOrder] = 1 WHERE [TypeCode] = N'DESK';
    UPDATE [MST_WorkspaceType] SET [IsSystem] = 1, [CodePrefix] = N'C', [BookingModes] = 3, [CustomerBookable] = 1, [DisplayOrder] = 3 WHERE [TypeCode] = N'CABIN';
    UPDATE [MST_WorkspaceType] SET [IsSystem] = 1, [CodePrefix] = N'M', [BookingModes] = 1, [CustomerBookable] = 1, [DisplayOrder] = 4 WHERE [TypeCode] = N'MEETING_ROOM';
    UPDATE [MST_WorkspaceType] SET [IsSystem] = 1, [CodePrefix] = N'V', [BookingModes] = 2, [CustomerBookable] = 1, [DisplayOrder] = 5 WHERE [TypeCode] = N'VIRTUAL_WORKSPACE';
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261014075619_WorkspaceTypeManagement'
)
BEGIN

    IF EXISTS (SELECT 1 FROM [MST_WorkspaceType] WHERE [TypeCode] = N'DESK')
       AND NOT EXISTS (SELECT 1 FROM [MST_WorkspaceType] WHERE [TypeCode] = N'HOT_DESK')
    BEGIN
        INSERT INTO [MST_WorkspaceType] ([TypeCode], [TypeName], [Description], [IconName], [DisplayOrder],
            [RequiresCapacity], [CodePrefix], [IsSystem], [BookingModes], [CustomerBookable],
            [CreatedBy], [CreatedDate], [IsActive], [IsDeleted])
        VALUES (N'HOT_DESK', N'Hot Desk', NULL, N'Armchair', 2, 0, N'D', 1, 1, 0, NULL, SYSUTCDATETIME(), 1, 0);

        DECLARE @desk int = (SELECT [WorkspaceTypeId] FROM [MST_WorkspaceType] WHERE [TypeCode] = N'DESK');
        DECLARE @hot int = (SELECT [WorkspaceTypeId] FROM [MST_WorkspaceType] WHERE [TypeCode] = N'HOT_DESK');

        UPDATE [MST_Workspace] SET [WorkspaceTypeId] = @hot
        WHERE [WorkspaceTypeId] = @desk AND [WorkspaceCode] LIKE N'DEK-D-%';

        UPDATE b SET [WorkspaceTypeId] = @hot
        FROM [TRN_Booking] b JOIN [MST_Workspace] w ON w.[WorkspaceId] = b.[WorkspaceId]
        WHERE w.[WorkspaceTypeId] = @hot AND b.[WorkspaceTypeId] = @desk;
    END;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261014075619_WorkspaceTypeManagement'
)
BEGIN

    UPDATE [MST_WorkspaceType] SET [TypeName] = LTRIM(RTRIM([TypeName]));
    WHILE EXISTS (SELECT 1 FROM [MST_WorkspaceType] WHERE [TypeName] LIKE N'%  %')
        UPDATE [MST_WorkspaceType] SET [TypeName] = REPLACE([TypeName], N'  ', N' ') WHERE [TypeName] LIKE N'%  %';
    IF EXISTS (SELECT LOWER([TypeName]) FROM [MST_WorkspaceType] WHERE [IsDeleted] = 0
               GROUP BY LOWER([TypeName]) HAVING COUNT(*) > 1)
        THROW 50001, N'WorkspaceTypeManagement: two workspace types have the same name. Rename one, then run again.', 1;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261014075619_WorkspaceTypeManagement'
)
BEGIN
    EXEC(N'ALTER TABLE [MST_WorkspaceType] ADD [NameKey] AS LOWER(LTRIM(RTRIM([TypeName]))) PERSISTED');
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261014075619_WorkspaceTypeManagement'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [UX_WorkspaceType_Name] ON [MST_WorkspaceType] ([NameKey]) WHERE [IsDeleted] = 0');
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261014075619_WorkspaceTypeManagement'
)
BEGIN
    EXEC(N'ALTER TABLE [MST_WorkspaceType] ADD CONSTRAINT [CK_WorkspaceType_BookingModes] CHECK ([BookingModes] BETWEEN 1 AND 7)');
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261014075619_WorkspaceTypeManagement'
)
BEGIN
    EXEC(N'ALTER TABLE [MST_WorkspaceType] ADD CONSTRAINT [CK_WorkspaceType_CodePrefix] CHECK ([CodePrefix] IS NULL OR [CodePrefix] COLLATE Latin1_General_BIN LIKE ''[A-Z]'')');
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261014075619_WorkspaceTypeManagement'
)
BEGIN

    IF EXISTS (SELECT 1 FROM [MST_Workspace] w LEFT JOIN [MST_WorkspaceType] t ON t.[WorkspaceTypeId] = w.[WorkspaceTypeId]
               WHERE t.[WorkspaceTypeId] IS NULL)
        THROW 50002, N'WorkspaceTypeManagement: a workspace has no valid type.', 1;
    IF EXISTS (SELECT 1 FROM [TRN_Booking] b JOIN [MST_Workspace] w ON w.[WorkspaceId] = b.[WorkspaceId]
               WHERE b.[WorkspaceTypeId] <> w.[WorkspaceTypeId])
        THROW 50003, N'WorkspaceTypeManagement: a booking type differs from its workspace type.', 1;
    IF EXISTS (SELECT 1 FROM [MST_Workspace] w JOIN [MST_WorkspaceType] t ON t.[WorkspaceTypeId] = w.[WorkspaceTypeId]
               WHERE w.[IsDeleted] = 0 AND t.[CodePrefix] IS NOT NULL
                 AND (w.[WorkspaceCode] COLLATE Latin1_General_BIN NOT LIKE '[A-Z][A-Z][A-Z]-[A-Z]-[0-9][0-9]'
                      OR SUBSTRING(w.[WorkspaceCode], 5, 1) <> t.[CodePrefix]))
        THROW 50004, N'WorkspaceTypeManagement: a live workspace code is not XXX-P-NN with its type letter.', 1;
    IF EXISTS (SELECT 1 FROM [MST_Workspace] w JOIN [MST_WorkspaceType] t ON t.[WorkspaceTypeId] = w.[WorkspaceTypeId]
               WHERE w.[IsDeleted] = 0
                 AND ((t.[BookingModes] & 1 = 1 AND NOT w.[PricePerHour] > 0)
                   OR (t.[BookingModes] & 1 = 0 AND t.[BookingModes] & 2 = 2 AND NOT ISNULL(w.[PricePerMonth], 0) > 0)))
        THROW 50005, N'WorkspaceTypeManagement: a live workspace is missing the price its type requires.', 1;
    -- The workspaces customers can book online must not change: hot desks were kept off the
    -- monthly list by their name, and are now kept off by their type.
    IF EXISTS (SELECT 1 FROM [MST_Workspace] w JOIN [MST_WorkspaceType] t ON t.[WorkspaceTypeId] = w.[WorkspaceTypeId]
               WHERE w.[IsDeleted] = 0 AND LTRIM(w.[WorkspaceName]) LIKE N'Hot Desk%' AND t.[CustomerBookable] = 1)
        THROW 50006, N'WorkspaceTypeManagement: a hot desk would become bookable online.', 1;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261014075619_WorkspaceTypeManagement'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261014075619_WorkspaceTypeManagement', N'8.0.19');
END;
GO

COMMIT;
GO

