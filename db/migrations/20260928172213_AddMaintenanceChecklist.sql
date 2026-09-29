BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260928172213_AddMaintenanceChecklist'
)
BEGIN
    CREATE TABLE [MST_HousekeeperProfile] (
        [HousekeeperProfileId] bigint NOT NULL IDENTITY,
        [UserId] bigint NOT NULL,
        [HousekeeperCode] nvarchar(30) NOT NULL,
        [Shift] tinyint NOT NULL,
        [AssignedAreas] nvarchar(300) NULL,
        [JoiningDate] date NOT NULL,
        [CreatedBy] bigint NULL,
        [CreatedDate] datetime2(3) NOT NULL,
        [ModifiedBy] bigint NULL,
        [ModifiedDate] datetime2(3) NULL,
        [IsActive] bit NOT NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedBy] bigint NULL,
        [DeletedDate] datetime2(3) NULL,
        CONSTRAINT [PK_MST_HousekeeperProfile] PRIMARY KEY ([HousekeeperProfileId]),
        CONSTRAINT [FK_MST_HousekeeperProfile_MST_User_UserId] FOREIGN KEY ([UserId]) REFERENCES [MST_User] ([UserId]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260928172213_AddMaintenanceChecklist'
)
BEGIN
    CREATE TABLE [TRN_MaintenanceWorkItem] (
        [MaintenanceWorkItemId] bigint NOT NULL IDENTITY,
        [WorkNumber] nvarchar(30) NOT NULL,
        [Title] nvarchar(200) NOT NULL,
        [LocationId] int NOT NULL,
        [WorkspaceId] int NULL,
        [AreaName] nvarchar(100) NOT NULL,
        [Category] nvarchar(60) NOT NULL,
        [Priority] tinyint NOT NULL,
        [AssignedToUserId] bigint NOT NULL,
        [AssignedUtc] datetime2(3) NOT NULL,
        [DueUtc] datetime2(3) NOT NULL,
        [Status] tinyint NOT NULL,
        [Instructions] nvarchar(1000) NULL,
        [StartedUtc] datetime2(3) NULL,
        [CompletedUtc] datetime2(3) NULL,
        [CompletedByUserId] bigint NULL,
        [CompletionNote] nvarchar(1000) NULL,
        [PhotoPath] nvarchar(500) NULL,
        [PhotoFileName] nvarchar(260) NULL,
        [PhotoContentType] nvarchar(100) NULL,
        [VerifiedUtc] datetime2(3) NULL,
        [VerifiedByUserId] bigint NULL,
        [ReworkReason] nvarchar(500) NULL,
        [ReworkCount] int NOT NULL,
        [OverdueSinceUtc] datetime2(3) NULL,
        [CancelledUtc] datetime2(3) NULL,
        [CancelReason] nvarchar(500) NULL,
        [LinkedScheduleId] bigint NULL,
        [CreatedBy] bigint NULL,
        [CreatedDate] datetime2(3) NOT NULL,
        [ModifiedBy] bigint NULL,
        [ModifiedDate] datetime2(3) NULL,
        [IsActive] bit NOT NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedBy] bigint NULL,
        [DeletedDate] datetime2(3) NULL,
        CONSTRAINT [PK_TRN_MaintenanceWorkItem] PRIMARY KEY ([MaintenanceWorkItemId]),
        CONSTRAINT [FK_TRN_MaintenanceWorkItem_MST_Location_LocationId] FOREIGN KEY ([LocationId]) REFERENCES [MST_Location] ([LocationId]) ON DELETE NO ACTION,
        CONSTRAINT [FK_TRN_MaintenanceWorkItem_MST_User_AssignedToUserId] FOREIGN KEY ([AssignedToUserId]) REFERENCES [MST_User] ([UserId]) ON DELETE NO ACTION,
        CONSTRAINT [FK_TRN_MaintenanceWorkItem_TRN_HousekeepingSchedule_LinkedScheduleId] FOREIGN KEY ([LinkedScheduleId]) REFERENCES [TRN_HousekeepingSchedule] ([HousekeepingScheduleId]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260928172213_AddMaintenanceChecklist'
)
BEGIN
    CREATE UNIQUE INDEX [UX_HousekeeperProfile_Code] ON [MST_HousekeeperProfile] ([HousekeeperCode]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260928172213_AddMaintenanceChecklist'
)
BEGIN
    CREATE UNIQUE INDEX [UX_HousekeeperProfile_User] ON [MST_HousekeeperProfile] ([UserId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260928172213_AddMaintenanceChecklist'
)
BEGIN
    CREATE INDEX [IX_MaintenanceWork_Assignee_Status] ON [TRN_MaintenanceWorkItem] ([AssignedToUserId], [Status]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260928172213_AddMaintenanceChecklist'
)
BEGIN
    CREATE INDEX [IX_MaintenanceWork_Due_Status] ON [TRN_MaintenanceWorkItem] ([DueUtc], [Status]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260928172213_AddMaintenanceChecklist'
)
BEGIN
    CREATE INDEX [IX_TRN_MaintenanceWorkItem_LinkedScheduleId] ON [TRN_MaintenanceWorkItem] ([LinkedScheduleId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260928172213_AddMaintenanceChecklist'
)
BEGIN
    CREATE INDEX [IX_TRN_MaintenanceWorkItem_LocationId] ON [TRN_MaintenanceWorkItem] ([LocationId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260928172213_AddMaintenanceChecklist'
)
BEGIN
    CREATE UNIQUE INDEX [UX_MaintenanceWork_Number] ON [TRN_MaintenanceWorkItem] ([WorkNumber]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260928172213_AddMaintenanceChecklist'
)
BEGIN
    IF NOT EXISTS (SELECT 1 FROM MST_NumberSequence WHERE SequenceKey = N'MaintenanceWork' AND FinancialYear IS NULL)
        INSERT INTO MST_NumberSequence (SequenceKey, Prefix, CurrentValue, FinancialYear) VALUES (N'MaintenanceWork', N'MW-', 0, NULL);
    IF NOT EXISTS (SELECT 1 FROM MST_NumberSequence WHERE SequenceKey = N'Housekeeper' AND FinancialYear IS NULL)
        INSERT INTO MST_NumberSequence (SequenceKey, Prefix, CurrentValue, FinancialYear) VALUES (N'Housekeeper', N'HK-', 0, NULL);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260928172213_AddMaintenanceChecklist'
)
BEGIN
    DECLARE @start bigint = (SELECT CurrentValue FROM MST_NumberSequence WHERE SequenceKey = N'Housekeeper' AND FinancialYear IS NULL);
    ;WITH hk AS (
        SELECT DISTINCT u.UserId, u.CreatedDate
        FROM MST_User u
        JOIN MAP_UserRole ur ON ur.UserId = u.UserId
        JOIN MST_Role r ON r.RoleId = ur.RoleId
        WHERE r.RoleCode = N'HOUSEKEEPING' AND u.IsDeleted = 0
          AND NOT EXISTS (SELECT 1 FROM MST_HousekeeperProfile p WHERE p.UserId = u.UserId)
    )
    INSERT INTO MST_HousekeeperProfile (UserId, HousekeeperCode, Shift, AssignedAreas, JoiningDate, CreatedDate, IsActive, IsDeleted)
    SELECT UserId,
           N'HK-' + RIGHT(N'000000' + CAST(@start + ROW_NUMBER() OVER (ORDER BY UserId) AS nvarchar(20)), 6),
           1, NULL, CAST(CreatedDate AS date), SYSUTCDATETIME(), 1, 0
    FROM hk;
    UPDATE MST_NumberSequence SET CurrentValue = CurrentValue + @@ROWCOUNT
    WHERE SequenceKey = N'Housekeeper' AND FinancialYear IS NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260928172213_AddMaintenanceChecklist'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260928172213_AddMaintenanceChecklist', N'8.0.19');
END;
GO

COMMIT;
GO

