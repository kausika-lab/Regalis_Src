BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260929175125_AdminNotificationCentre'
)
BEGIN
    CREATE TABLE [MST_AdminNotificationSetting] (
        [UserId] bigint NOT NULL,
        [Type] tinyint NOT NULL,
        [Email] bit NOT NULL,
        [WhatsApp] bit NOT NULL,
        [ModifiedUtc] datetime2(3) NOT NULL,
        CONSTRAINT [PK_MST_AdminNotificationSetting] PRIMARY KEY ([UserId], [Type]),
        CONSTRAINT [FK_MST_AdminNotificationSetting_MST_User_UserId] FOREIGN KEY ([UserId]) REFERENCES [MST_User] ([UserId]) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260929175125_AdminNotificationCentre'
)
BEGIN
    CREATE TABLE [TRN_AdminNotification] (
        [AdminNotificationId] bigint NOT NULL IDENTITY,
        [Type] tinyint NOT NULL,
        [EventCode] nvarchar(60) NOT NULL,
        [Severity] tinyint NOT NULL,
        [Title] nvarchar(200) NOT NULL,
        [Body] nvarchar(1000) NOT NULL,
        [CustomerId] bigint NULL,
        [ReferenceType] nvarchar(40) NULL,
        [ReferenceId] bigint NULL,
        [ReferenceLabel] nvarchar(120) NULL,
        [ActionLabel] nvarchar(60) NULL,
        [ActionRoute] nvarchar(300) NULL,
        [CreatedUtc] datetime2(3) NOT NULL,
        [DeliveredUtc] datetime2(3) NULL,
        [DeliveryNote] nvarchar(300) NULL,
        CONSTRAINT [PK_TRN_AdminNotification] PRIMARY KEY ([AdminNotificationId]),
        CONSTRAINT [FK_TRN_AdminNotification_MST_User_CustomerId] FOREIGN KEY ([CustomerId]) REFERENCES [MST_User] ([UserId]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260929175125_AdminNotificationCentre'
)
BEGIN
    CREATE TABLE [TRN_Announcement] (
        [AnnouncementId] bigint NOT NULL IDENTITY,
        [Title] nvarchar(200) NOT NULL,
        [Message] nvarchar(500) NOT NULL,
        [Audience] tinyint NOT NULL,
        [TargetCustomerId] bigint NULL,
        [SendEmail] bit NOT NULL,
        [SendWhatsApp] bit NOT NULL,
        [Status] tinyint NOT NULL,
        [ScheduledForUtc] datetime2(3) NULL,
        [SentUtc] datetime2(3) NULL,
        [SentByUserId] bigint NULL,
        [RecipientCount] int NOT NULL,
        [EmailSentCount] int NOT NULL,
        [WhatsAppSentCount] int NOT NULL,
        [WhatsAppManualShare] bit NOT NULL,
        [CreatedBy] bigint NULL,
        [CreatedDate] datetime2(3) NOT NULL,
        [ModifiedBy] bigint NULL,
        [ModifiedDate] datetime2(3) NULL,
        [IsActive] bit NOT NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedBy] bigint NULL,
        [DeletedDate] datetime2(3) NULL,
        CONSTRAINT [PK_TRN_Announcement] PRIMARY KEY ([AnnouncementId]),
        CONSTRAINT [FK_TRN_Announcement_MST_User_TargetCustomerId] FOREIGN KEY ([TargetCustomerId]) REFERENCES [MST_User] ([UserId]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260929175125_AdminNotificationCentre'
)
BEGIN
    CREATE TABLE [TRN_AdminNotificationRead] (
        [AdminNotificationId] bigint NOT NULL,
        [UserId] bigint NOT NULL,
        [ReadUtc] datetime2(3) NOT NULL,
        CONSTRAINT [PK_TRN_AdminNotificationRead] PRIMARY KEY ([AdminNotificationId], [UserId]),
        CONSTRAINT [FK_TRN_AdminNotificationRead_MST_User_UserId] FOREIGN KEY ([UserId]) REFERENCES [MST_User] ([UserId]) ON DELETE NO ACTION,
        CONSTRAINT [FK_TRN_AdminNotificationRead_TRN_AdminNotification_AdminNotificationId] FOREIGN KEY ([AdminNotificationId]) REFERENCES [TRN_AdminNotification] ([AdminNotificationId]) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260929175125_AdminNotificationCentre'
)
BEGIN
    CREATE INDEX [IX_AdminNotification_Created] ON [TRN_AdminNotification] ([CreatedUtc]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260929175125_AdminNotificationCentre'
)
BEGIN
    CREATE INDEX [IX_AdminNotification_Reference] ON [TRN_AdminNotification] ([ReferenceType], [ReferenceId], [EventCode]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260929175125_AdminNotificationCentre'
)
BEGIN
    CREATE INDEX [IX_AdminNotification_Type_Created] ON [TRN_AdminNotification] ([Type], [CreatedUtc]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260929175125_AdminNotificationCentre'
)
BEGIN
    EXEC(N'CREATE INDEX [IX_AdminNotification_Undelivered] ON [TRN_AdminNotification] ([DeliveredUtc]) WHERE [DeliveredUtc] IS NULL');
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260929175125_AdminNotificationCentre'
)
BEGIN
    CREATE INDEX [IX_TRN_AdminNotification_CustomerId] ON [TRN_AdminNotification] ([CustomerId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260929175125_AdminNotificationCentre'
)
BEGIN
    CREATE INDEX [IX_AdminNotificationRead_User] ON [TRN_AdminNotificationRead] ([UserId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260929175125_AdminNotificationCentre'
)
BEGIN
    CREATE INDEX [IX_Announcement_Status_Scheduled] ON [TRN_Announcement] ([Status], [ScheduledForUtc]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260929175125_AdminNotificationCentre'
)
BEGIN
    CREATE INDEX [IX_TRN_Announcement_TargetCustomerId] ON [TRN_Announcement] ([TargetCustomerId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260929175125_AdminNotificationCentre'
)
BEGIN
    INSERT INTO TRN_AdminNotification
        (Type, EventCode, Severity, Title, Body, CustomerId, ReferenceType, ReferenceId,
         ReferenceLabel, ActionLabel, ActionRoute, CreatedUtc, DeliveredUtc, DeliveryNote)
    SELECT CASE n.Category WHEN 1 THEN 2 WHEN 4 THEN 3 WHEN 8 THEN 5 WHEN 12 THEN 7 ELSE 6 END,
           'legacy', 1, LEFT(n.Title, 200), LEFT(n.Message, 1000), NULL, n.ReferenceType,
           n.ReferenceId, NULL, n.ActionLabel, n.ActionRoute,
           MIN(n.CreatedDate), MIN(n.CreatedDate), 'Moved from the old per-admin feed'
    FROM TRN_Notification n
    WHERE n.Audience = 3 AND n.UserId IS NOT NULL AND n.IsDeleted = 0
    GROUP BY n.Category, n.Title, n.Message, n.ReferenceType, n.ReferenceId,
             n.ActionLabel, n.ActionRoute, n.SentUtc;

    INSERT INTO TRN_AdminNotificationRead (AdminNotificationId, UserId, ReadUtc)
    SELECT a.AdminNotificationId, n.UserId, MIN(ISNULL(n.ReadUtc, n.CreatedDate))
    FROM TRN_Notification n
    JOIN TRN_AdminNotification a
      ON a.EventCode = 'legacy'
     AND a.Title = LEFT(n.Title, 200) AND a.Body = LEFT(n.Message, 1000)
     AND ISNULL(a.ReferenceType, '') = ISNULL(n.ReferenceType, '')
     AND ISNULL(a.ReferenceId, -1) = ISNULL(n.ReferenceId, -1)
    JOIN MST_User u ON u.UserId = n.UserId
    WHERE n.Audience = 3 AND n.UserId IS NOT NULL AND n.IsDeleted = 0 AND n.IsRead = 1
    GROUP BY a.AdminNotificationId, n.UserId;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260929175125_AdminNotificationCentre'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260929175125_AdminNotificationCentre', N'8.0.19');
END;
GO

COMMIT;
GO

