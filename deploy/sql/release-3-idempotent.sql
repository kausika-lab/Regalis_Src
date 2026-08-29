IF OBJECT_ID(N'[__EFMigrationsHistory]') IS NULL
BEGIN
    CREATE TABLE [__EFMigrationsHistory] (
        [MigrationId] nvarchar(150) NOT NULL,
        [ProductVersion] nvarchar(32) NOT NULL,
        CONSTRAINT [PK___EFMigrationsHistory] PRIMARY KEY ([MigrationId])
    );
END;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260828171612_InitialCreate'
)
BEGIN
    CREATE TABLE [MST_Amenity] (
        [AmenityId] int NOT NULL IDENTITY,
        [AmenityName] nvarchar(100) NOT NULL,
        [IconName] nvarchar(60) NULL,
        [DisplayOrder] int NOT NULL,
        [CreatedBy] bigint NULL,
        [CreatedDate] datetime2(3) NOT NULL,
        [ModifiedBy] bigint NULL,
        [ModifiedDate] datetime2(3) NULL,
        [IsActive] bit NOT NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedBy] bigint NULL,
        [DeletedDate] datetime2(3) NULL,
        CONSTRAINT [PK_MST_Amenity] PRIMARY KEY ([AmenityId])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260828171612_InitialCreate'
)
BEGIN
    CREATE TABLE [MST_AppSetting] (
        [AppSettingId] int NOT NULL IDENTITY,
        [SettingKey] nvarchar(120) NOT NULL,
        [SettingValue] nvarchar(max) NOT NULL,
        [SettingGroup] nvarchar(60) NOT NULL,
        [DataType] nvarchar(20) NOT NULL,
        [Description] nvarchar(300) NULL,
        [IsEditableByAdmin] bit NOT NULL,
        [IsSecret] bit NOT NULL,
        [CreatedBy] bigint NULL,
        [CreatedDate] datetime2(3) NOT NULL,
        [ModifiedBy] bigint NULL,
        [ModifiedDate] datetime2(3) NULL,
        [IsActive] bit NOT NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedBy] bigint NULL,
        [DeletedDate] datetime2(3) NULL,
        CONSTRAINT [PK_MST_AppSetting] PRIMARY KEY ([AppSettingId])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260828171612_InitialCreate'
)
BEGIN
    CREATE TABLE [MST_Location] (
        [LocationId] int NOT NULL IDENTITY,
        [LocationCode] nvarchar(30) NOT NULL,
        [LocationName] nvarchar(150) NOT NULL,
        [AddressLine1] nvarchar(200) NULL,
        [AddressLine2] nvarchar(200) NULL,
        [City] nvarchar(100) NULL,
        [State] nvarchar(100) NULL,
        [PostalCode] nvarchar(20) NULL,
        [ContactNumber] nvarchar(20) NULL,
        [Email] nvarchar(256) NULL,
        [WorkingHoursStart] time(0) NOT NULL,
        [WorkingHoursEnd] time(0) NOT NULL,
        [WorkingDays] nvarchar(30) NOT NULL,
        [Latitude] decimal(9,6) NULL,
        [Longitude] decimal(9,6) NULL,
        [ImagePath] nvarchar(500) NULL,
        [Description] nvarchar(max) NULL,
        [IsDefault] bit NOT NULL,
        [CreatedBy] bigint NULL,
        [CreatedDate] datetime2(3) NOT NULL,
        [ModifiedBy] bigint NULL,
        [ModifiedDate] datetime2(3) NULL,
        [IsActive] bit NOT NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedBy] bigint NULL,
        [DeletedDate] datetime2(3) NULL,
        CONSTRAINT [PK_MST_Location] PRIMARY KEY ([LocationId])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260828171612_InitialCreate'
)
BEGIN
    CREATE TABLE [MST_NumberSequence] (
        [NumberSequenceId] int NOT NULL IDENTITY,
        [SequenceKey] nvarchar(60) NOT NULL,
        [Prefix] nvarchar(30) NOT NULL,
        [CurrentValue] bigint NOT NULL,
        [FinancialYear] nvarchar(10) NULL,
        CONSTRAINT [PK_MST_NumberSequence] PRIMARY KEY ([NumberSequenceId])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260828171612_InitialCreate'
)
BEGIN
    CREATE TABLE [MST_Permission] (
        [PermissionId] int NOT NULL IDENTITY,
        [PermissionCode] nvarchar(80) NOT NULL,
        [ModuleName] nvarchar(60) NOT NULL,
        [ActionName] nvarchar(30) NOT NULL,
        [Description] nvarchar(200) NULL,
        [DisplayOrder] int NOT NULL,
        [CreatedBy] bigint NULL,
        [CreatedDate] datetime2(3) NOT NULL,
        [ModifiedBy] bigint NULL,
        [ModifiedDate] datetime2(3) NULL,
        [IsActive] bit NOT NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedBy] bigint NULL,
        [DeletedDate] datetime2(3) NULL,
        CONSTRAINT [PK_MST_Permission] PRIMARY KEY ([PermissionId])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260828171612_InitialCreate'
)
BEGIN
    CREATE TABLE [MST_Role] (
        [RoleId] int NOT NULL IDENTITY,
        [RoleCode] nvarchar(50) NOT NULL,
        [RoleName] nvarchar(100) NOT NULL,
        [Description] nvarchar(300) NULL,
        [IsSystemRole] bit NOT NULL,
        [PortalType] tinyint NOT NULL,
        [CreatedBy] bigint NULL,
        [CreatedDate] datetime2(3) NOT NULL,
        [ModifiedBy] bigint NULL,
        [ModifiedDate] datetime2(3) NULL,
        [IsActive] bit NOT NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedBy] bigint NULL,
        [DeletedDate] datetime2(3) NULL,
        CONSTRAINT [PK_MST_Role] PRIMARY KEY ([RoleId])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260828171612_InitialCreate'
)
BEGIN
    CREATE TABLE [MST_TicketCategory] (
        [TicketCategoryId] int NOT NULL IDENTITY,
        [CategoryName] nvarchar(120) NOT NULL,
        [DisplayOrder] int NOT NULL,
        [CreatedBy] bigint NULL,
        [CreatedDate] datetime2(3) NOT NULL,
        [ModifiedBy] bigint NULL,
        [ModifiedDate] datetime2(3) NULL,
        [IsActive] bit NOT NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedBy] bigint NULL,
        [DeletedDate] datetime2(3) NULL,
        CONSTRAINT [PK_MST_TicketCategory] PRIMARY KEY ([TicketCategoryId])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260828171612_InitialCreate'
)
BEGIN
    CREATE TABLE [MST_WorkspaceType] (
        [WorkspaceTypeId] int NOT NULL IDENTITY,
        [TypeCode] nvarchar(30) NOT NULL,
        [TypeName] nvarchar(100) NOT NULL,
        [Description] nvarchar(max) NULL,
        [IconName] nvarchar(60) NULL,
        [DisplayOrder] int NOT NULL,
        [RequiresCapacity] bit NOT NULL,
        [CreatedBy] bigint NULL,
        [CreatedDate] datetime2(3) NOT NULL,
        [ModifiedBy] bigint NULL,
        [ModifiedDate] datetime2(3) NULL,
        [IsActive] bit NOT NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedBy] bigint NULL,
        [DeletedDate] datetime2(3) NULL,
        CONSTRAINT [PK_MST_WorkspaceType] PRIMARY KEY ([WorkspaceTypeId])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260828171612_InitialCreate'
)
BEGIN
    CREATE TABLE [TRN_AccessLog] (
        [AccessLogId] bigint NOT NULL IDENTITY,
        [AccessTokenId] bigint NULL,
        [UserId] bigint NULL,
        [ScannedTokenValue] nvarchar(64) NULL,
        [WorkspaceId] int NULL,
        [LocationId] int NOT NULL,
        [Direction] tinyint NOT NULL,
        [AccessMethod] tinyint NOT NULL,
        [Result] tinyint NOT NULL,
        [DenyReason] nvarchar(200) NULL,
        [DeviceId] nvarchar(80) NULL,
        [DeviceStatus] nvarchar(60) NULL,
        [IsManualOverride] bit NOT NULL,
        [OverrideByUserId] bigint NULL,
        [OverrideReason] nvarchar(500) NULL,
        [IpAddress] nvarchar(64) NULL,
        [LoggedUtc] datetime2(3) NOT NULL,
        CONSTRAINT [PK_TRN_AccessLog] PRIMARY KEY ([AccessLogId])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260828171612_InitialCreate'
)
BEGIN
    CREATE TABLE [TRN_AuditLog] (
        [AuditLogId] bigint NOT NULL IDENTITY,
        [UserId] bigint NULL,
        [UserName] nvarchar(150) NULL,
        [Action] nvarchar(40) NOT NULL,
        [ModuleName] nvarchar(60) NOT NULL,
        [EntityName] nvarchar(100) NULL,
        [EntityId] nvarchar(60) NULL,
        [OldValue] nvarchar(max) NULL,
        [NewValue] nvarchar(max) NULL,
        [IpAddress] nvarchar(64) NULL,
        [UserAgent] nvarchar(300) NULL,
        [CorrelationId] nvarchar(64) NULL,
        [TimestampUtc] datetime2(3) NOT NULL,
        CONSTRAINT [PK_TRN_AuditLog] PRIMARY KEY ([AuditLogId])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260828171612_InitialCreate'
)
BEGIN
    CREATE TABLE [TRN_LoginHistory] (
        [LoginHistoryId] bigint NOT NULL IDENTITY,
        [UserId] bigint NULL,
        [AttemptedEmail] nvarchar(256) NOT NULL,
        [IsSuccess] bit NOT NULL,
        [FailureReason] nvarchar(200) NULL,
        [IpAddress] nvarchar(64) NULL,
        [UserAgent] nvarchar(300) NULL,
        [AttemptedUtc] datetime2(3) NOT NULL,
        CONSTRAINT [PK_TRN_LoginHistory] PRIMARY KEY ([LoginHistoryId])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260828171612_InitialCreate'
)
BEGIN
    CREATE TABLE [TRN_Notification] (
        [NotificationId] bigint NOT NULL IDENTITY,
        [UserId] bigint NULL,
        [Audience] tinyint NOT NULL,
        [AudienceRefId] bigint NULL,
        [Title] nvarchar(200) NOT NULL,
        [Message] nvarchar(max) NOT NULL,
        [Category] tinyint NOT NULL,
        [Priority] tinyint NOT NULL,
        [ReferenceType] nvarchar(40) NULL,
        [ReferenceId] bigint NULL,
        [ActionLabel] nvarchar(60) NULL,
        [ActionRoute] nvarchar(200) NULL,
        [IsRead] bit NOT NULL,
        [ReadUtc] datetime2(3) NULL,
        [ScheduledForUtc] datetime2(3) NULL,
        [SentUtc] datetime2(3) NULL,
        [CreatedByUserId] bigint NULL,
        [CreatedBy] bigint NULL,
        [CreatedDate] datetime2(3) NOT NULL,
        [ModifiedBy] bigint NULL,
        [ModifiedDate] datetime2(3) NULL,
        [IsActive] bit NOT NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedBy] bigint NULL,
        [DeletedDate] datetime2(3) NULL,
        CONSTRAINT [PK_TRN_Notification] PRIMARY KEY ([NotificationId])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260828171612_InitialCreate'
)
BEGIN
    CREATE TABLE [TRN_Otp] (
        [OtpId] bigint NOT NULL IDENTITY,
        [UserId] bigint NULL,
        [Destination] nvarchar(256) NOT NULL,
        [Channel] tinyint NOT NULL,
        [Purpose] tinyint NOT NULL,
        [CodeHash] nvarchar(128) NOT NULL,
        [ExpiresUtc] datetime2(3) NOT NULL,
        [AttemptCount] int NOT NULL,
        [MaxAttempts] int NOT NULL,
        [ConsumedUtc] datetime2(3) NULL,
        [CreatedUtc] datetime2(3) NOT NULL,
        CONSTRAINT [PK_TRN_Otp] PRIMARY KEY ([OtpId])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260828171612_InitialCreate'
)
BEGIN
    CREATE TABLE [MST_Banner] (
        [BannerId] int NOT NULL IDENTITY,
        [Title] nvarchar(150) NOT NULL,
        [Subtitle] nvarchar(300) NULL,
        [ImagePath] nvarchar(500) NOT NULL,
        [LinkUrl] nvarchar(500) NULL,
        [PromotionalPrice] decimal(18,2) NULL,
        [DisplayOrder] int NOT NULL,
        [ValidFromUtc] datetime2(3) NOT NULL,
        [ValidToUtc] datetime2(3) NOT NULL,
        [LocationId] int NULL,
        [PortalType] tinyint NOT NULL,
        [CreatedBy] bigint NULL,
        [CreatedDate] datetime2(3) NOT NULL,
        [ModifiedBy] bigint NULL,
        [ModifiedDate] datetime2(3) NULL,
        [IsActive] bit NOT NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedBy] bigint NULL,
        [DeletedDate] datetime2(3) NULL,
        CONSTRAINT [PK_MST_Banner] PRIMARY KEY ([BannerId]),
        CONSTRAINT [FK_MST_Banner_MST_Location_LocationId] FOREIGN KEY ([LocationId]) REFERENCES [MST_Location] ([LocationId]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260828171612_InitialCreate'
)
BEGIN
    CREATE TABLE [MST_InventoryItem] (
        [InventoryItemId] int NOT NULL IDENTITY,
        [ItemCode] nvarchar(40) NOT NULL,
        [ItemName] nvarchar(150) NOT NULL,
        [Category] nvarchar(60) NULL,
        [Unit] nvarchar(20) NOT NULL,
        [OpeningQuantity] decimal(18,3) NOT NULL,
        [ReorderLevel] decimal(18,3) NOT NULL,
        [UnitCost] decimal(18,2) NULL,
        [LocationId] int NOT NULL,
        [Description] nvarchar(max) NULL,
        [ImagePath] nvarchar(500) NULL,
        [CreatedBy] bigint NULL,
        [CreatedDate] datetime2(3) NOT NULL,
        [ModifiedBy] bigint NULL,
        [ModifiedDate] datetime2(3) NULL,
        [IsActive] bit NOT NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedBy] bigint NULL,
        [DeletedDate] datetime2(3) NULL,
        CONSTRAINT [PK_MST_InventoryItem] PRIMARY KEY ([InventoryItemId]),
        CONSTRAINT [FK_MST_InventoryItem_MST_Location_LocationId] FOREIGN KEY ([LocationId]) REFERENCES [MST_Location] ([LocationId]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260828171612_InitialCreate'
)
BEGIN
    CREATE TABLE [MST_PassPlan] (
        [PassPlanId] int NOT NULL IDENTITY,
        [PlanCode] nvarchar(40) NOT NULL,
        [PlanName] nvarchar(120) NOT NULL,
        [PassType] tinyint NOT NULL,
        [LocationId] int NULL,
        [Price] decimal(18,2) NOT NULL,
        [ValidityDays] int NOT NULL,
        [MaxGuests] int NULL,
        [Description] nvarchar(max) NULL,
        [TermsAndConditions] nvarchar(max) NULL,
        [ImagePath] nvarchar(500) NULL,
        [DisplayOrder] int NOT NULL,
        [CreatedBy] bigint NULL,
        [CreatedDate] datetime2(3) NOT NULL,
        [ModifiedBy] bigint NULL,
        [ModifiedDate] datetime2(3) NULL,
        [IsActive] bit NOT NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedBy] bigint NULL,
        [DeletedDate] datetime2(3) NULL,
        CONSTRAINT [PK_MST_PassPlan] PRIMARY KEY ([PassPlanId]),
        CONSTRAINT [FK_MST_PassPlan_MST_Location_LocationId] FOREIGN KEY ([LocationId]) REFERENCES [MST_Location] ([LocationId]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260828171612_InitialCreate'
)
BEGIN
    CREATE TABLE [MST_ServiceItem] (
        [ServiceItemId] int NOT NULL IDENTITY,
        [ItemCode] nvarchar(40) NOT NULL,
        [ItemName] nvarchar(120) NOT NULL,
        [Category] nvarchar(60) NOT NULL,
        [Price] decimal(18,2) NOT NULL,
        [Unit] nvarchar(20) NULL,
        [ImagePath] nvarchar(500) NULL,
        [Description] nvarchar(max) NULL,
        [IsAvailable] bit NOT NULL,
        [DisplayOrder] int NOT NULL,
        [LocationId] int NULL,
        [CreatedBy] bigint NULL,
        [CreatedDate] datetime2(3) NOT NULL,
        [ModifiedBy] bigint NULL,
        [ModifiedDate] datetime2(3) NULL,
        [IsActive] bit NOT NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedBy] bigint NULL,
        [DeletedDate] datetime2(3) NULL,
        CONSTRAINT [PK_MST_ServiceItem] PRIMARY KEY ([ServiceItemId]),
        CONSTRAINT [FK_MST_ServiceItem_MST_Location_LocationId] FOREIGN KEY ([LocationId]) REFERENCES [MST_Location] ([LocationId]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260828171612_InitialCreate'
)
BEGIN
    CREATE TABLE [MST_User] (
        [UserId] bigint NOT NULL IDENTITY,
        [UserType] tinyint NOT NULL,
        [FullName] nvarchar(150) NOT NULL,
        [Email] nvarchar(256) NOT NULL,
        [MobileNumber] nvarchar(20) NOT NULL,
        [PasswordHash] nvarchar(400) NOT NULL,
        [CompanyName] nvarchar(200) NULL,
        [Address] nvarchar(500) NULL,
        [PhotoPath] nvarchar(500) NULL,
        [DefaultLocationId] int NULL,
        [IsEmailVerified] bit NOT NULL,
        [IsMobileVerified] bit NOT NULL,
        [Status] tinyint NOT NULL,
        [FailedLoginCount] int NOT NULL,
        [LockoutEndUtc] datetime2(3) NULL,
        [LastLoginUtc] datetime2(3) NULL,
        [LastPasswordChangeUtc] datetime2(3) NULL,
        [MustChangePassword] bit NOT NULL,
        [CreatedBy] bigint NULL,
        [CreatedDate] datetime2(3) NOT NULL,
        [ModifiedBy] bigint NULL,
        [ModifiedDate] datetime2(3) NULL,
        [IsActive] bit NOT NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedBy] bigint NULL,
        [DeletedDate] datetime2(3) NULL,
        CONSTRAINT [PK_MST_User] PRIMARY KEY ([UserId]),
        CONSTRAINT [CK_User_UserType] CHECK ([UserType] IN (1,2,3)),
        CONSTRAINT [FK_MST_User_MST_Location_DefaultLocationId] FOREIGN KEY ([DefaultLocationId]) REFERENCES [MST_Location] ([LocationId]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260828171612_InitialCreate'
)
BEGIN
    CREATE TABLE [MST_Menu] (
        [MenuId] int NOT NULL IDENTITY,
        [ParentMenuId] int NULL,
        [MenuCode] nvarchar(60) NOT NULL,
        [MenuName] nvarchar(100) NOT NULL,
        [DisplayName] nvarchar(100) NOT NULL,
        [Route] nvarchar(200) NULL,
        [Icon] nvarchar(60) NULL,
        [DisplayOrder] int NOT NULL,
        [PermissionId] int NULL,
        [PortalType] tinyint NOT NULL,
        [MenuLevel] tinyint NOT NULL,
        [IsVisible] bit NOT NULL,
        [CreatedBy] bigint NULL,
        [CreatedDate] datetime2(3) NOT NULL,
        [ModifiedBy] bigint NULL,
        [ModifiedDate] datetime2(3) NULL,
        [IsActive] bit NOT NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedBy] bigint NULL,
        [DeletedDate] datetime2(3) NULL,
        CONSTRAINT [PK_MST_Menu] PRIMARY KEY ([MenuId]),
        CONSTRAINT [FK_MST_Menu_MST_Menu_ParentMenuId] FOREIGN KEY ([ParentMenuId]) REFERENCES [MST_Menu] ([MenuId]) ON DELETE NO ACTION,
        CONSTRAINT [FK_MST_Menu_MST_Permission_PermissionId] FOREIGN KEY ([PermissionId]) REFERENCES [MST_Permission] ([PermissionId]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260828171612_InitialCreate'
)
BEGIN
    CREATE TABLE [MAP_RolePermission] (
        [RolePermissionId] bigint NOT NULL IDENTITY,
        [RoleId] int NOT NULL,
        [PermissionId] int NOT NULL,
        CONSTRAINT [PK_MAP_RolePermission] PRIMARY KEY ([RolePermissionId]),
        CONSTRAINT [FK_MAP_RolePermission_MST_Permission_PermissionId] FOREIGN KEY ([PermissionId]) REFERENCES [MST_Permission] ([PermissionId]) ON DELETE CASCADE,
        CONSTRAINT [FK_MAP_RolePermission_MST_Role_RoleId] FOREIGN KEY ([RoleId]) REFERENCES [MST_Role] ([RoleId]) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260828171612_InitialCreate'
)
BEGIN
    CREATE TABLE [MST_TicketIssueType] (
        [TicketIssueTypeId] int NOT NULL IDENTITY,
        [TicketCategoryId] int NOT NULL,
        [IssueTypeName] nvarchar(150) NOT NULL,
        [DisplayOrder] int NOT NULL,
        [CreatedBy] bigint NULL,
        [CreatedDate] datetime2(3) NOT NULL,
        [ModifiedBy] bigint NULL,
        [ModifiedDate] datetime2(3) NULL,
        [IsActive] bit NOT NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedBy] bigint NULL,
        [DeletedDate] datetime2(3) NULL,
        CONSTRAINT [PK_MST_TicketIssueType] PRIMARY KEY ([TicketIssueTypeId]),
        CONSTRAINT [FK_MST_TicketIssueType_MST_TicketCategory_TicketCategoryId] FOREIGN KEY ([TicketCategoryId]) REFERENCES [MST_TicketCategory] ([TicketCategoryId]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260828171612_InitialCreate'
)
BEGIN
    CREATE TABLE [MST_Workspace] (
        [WorkspaceId] int NOT NULL IDENTITY,
        [LocationId] int NOT NULL,
        [WorkspaceTypeId] int NOT NULL,
        [WorkspaceCode] nvarchar(40) NOT NULL,
        [WorkspaceName] nvarchar(120) NOT NULL,
        [FloorNumber] nvarchar(20) NULL,
        [Capacity] int NOT NULL,
        [PricePerHour] decimal(18,2) NOT NULL,
        [PricePerDay] decimal(18,2) NULL,
        [Equipment] nvarchar(500) NULL,
        [Description] nvarchar(max) NULL,
        [ImagePath] nvarchar(500) NULL,
        [Status] tinyint NOT NULL,
        [IsBookable] bit NOT NULL,
        [RowVersion] rowversion NOT NULL,
        [CreatedBy] bigint NULL,
        [CreatedDate] datetime2(3) NOT NULL,
        [ModifiedBy] bigint NULL,
        [ModifiedDate] datetime2(3) NULL,
        [IsActive] bit NOT NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedBy] bigint NULL,
        [DeletedDate] datetime2(3) NULL,
        CONSTRAINT [PK_MST_Workspace] PRIMARY KEY ([WorkspaceId]),
        CONSTRAINT [CK_Workspace_Capacity] CHECK ([Capacity] >= 1),
        CONSTRAINT [CK_Workspace_Price] CHECK ([PricePerHour] >= 0),
        CONSTRAINT [FK_MST_Workspace_MST_Location_LocationId] FOREIGN KEY ([LocationId]) REFERENCES [MST_Location] ([LocationId]) ON DELETE NO ACTION,
        CONSTRAINT [FK_MST_Workspace_MST_WorkspaceType_WorkspaceTypeId] FOREIGN KEY ([WorkspaceTypeId]) REFERENCES [MST_WorkspaceType] ([WorkspaceTypeId]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260828171612_InitialCreate'
)
BEGIN
    CREATE TABLE [TRN_InventoryTransaction] (
        [InventoryTransactionId] bigint NOT NULL IDENTITY,
        [InventoryItemId] int NOT NULL,
        [TransactionNumber] nvarchar(30) NOT NULL,
        [TransactionType] tinyint NOT NULL,
        [Quantity] decimal(18,3) NOT NULL,
        [TransactionDate] date NOT NULL,
        [SourceOrDestination] nvarchar(200) NULL,
        [ReferenceNumber] nvarchar(80) NULL,
        [UnitCost] decimal(18,2) NULL,
        [DamageDescription] nvarchar(500) NULL,
        [DamageCause] nvarchar(300) NULL,
        [PhotoPath] nvarchar(500) NULL,
        [LocationId] int NOT NULL,
        [EnteredByUserId] bigint NOT NULL,
        [Notes] nvarchar(500) NULL,
        [CreatedBy] bigint NULL,
        [CreatedDate] datetime2(3) NOT NULL,
        [ModifiedBy] bigint NULL,
        [ModifiedDate] datetime2(3) NULL,
        [IsActive] bit NOT NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedBy] bigint NULL,
        [DeletedDate] datetime2(3) NULL,
        CONSTRAINT [PK_TRN_InventoryTransaction] PRIMARY KEY ([InventoryTransactionId]),
        CONSTRAINT [CK_InvTxn_Quantity] CHECK ([Quantity] > 0),
        CONSTRAINT [FK_TRN_InventoryTransaction_MST_InventoryItem_InventoryItemId] FOREIGN KEY ([InventoryItemId]) REFERENCES [MST_InventoryItem] ([InventoryItemId]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260828171612_InitialCreate'
)
BEGIN
    CREATE TABLE [MAP_UserRole] (
        [UserRoleId] bigint NOT NULL IDENTITY,
        [UserId] bigint NOT NULL,
        [RoleId] int NOT NULL,
        [AssignedDateUtc] datetime2(3) NOT NULL,
        [AssignedBy] bigint NULL,
        CONSTRAINT [PK_MAP_UserRole] PRIMARY KEY ([UserRoleId]),
        CONSTRAINT [FK_MAP_UserRole_MST_Role_RoleId] FOREIGN KEY ([RoleId]) REFERENCES [MST_Role] ([RoleId]) ON DELETE CASCADE,
        CONSTRAINT [FK_MAP_UserRole_MST_User_UserId] FOREIGN KEY ([UserId]) REFERENCES [MST_User] ([UserId]) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260828171612_InitialCreate'
)
BEGIN
    CREATE TABLE [MST_Wallet] (
        [WalletId] bigint NOT NULL IDENTITY,
        [CustomerId] bigint NOT NULL,
        [Balance] decimal(18,2) NOT NULL,
        [TotalCredited] decimal(18,2) NOT NULL,
        [TotalDebited] decimal(18,2) NOT NULL,
        [LastTransactionUtc] datetime2(3) NULL,
        [RowVersion] rowversion NOT NULL,
        [CreatedBy] bigint NULL,
        [CreatedDate] datetime2(3) NOT NULL,
        [ModifiedBy] bigint NULL,
        [ModifiedDate] datetime2(3) NULL,
        [IsActive] bit NOT NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedBy] bigint NULL,
        [DeletedDate] datetime2(3) NULL,
        CONSTRAINT [PK_MST_Wallet] PRIMARY KEY ([WalletId]),
        CONSTRAINT [CK_Wallet_Balance] CHECK ([Balance] >= 0),
        CONSTRAINT [FK_MST_Wallet_MST_User_CustomerId] FOREIGN KEY ([CustomerId]) REFERENCES [MST_User] ([UserId]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260828171612_InitialCreate'
)
BEGIN
    CREATE TABLE [TRN_AccessToken] (
        [AccessTokenId] bigint NOT NULL IDENTITY,
        [TokenValue] nvarchar(64) NOT NULL,
        [QrPayload] nvarchar(500) NOT NULL,
        [UserId] bigint NOT NULL,
        [BookingId] bigint NULL,
        [CustomerPassId] bigint NULL,
        [HousekeepingScheduleId] bigint NULL,
        [WorkspaceId] int NULL,
        [LocationId] int NOT NULL,
        [TokenPurpose] tinyint NOT NULL,
        [ValidFromUtc] datetime2(3) NOT NULL,
        [ValidToUtc] datetime2(3) NOT NULL,
        [Status] tinyint NOT NULL,
        [UsageCount] int NOT NULL,
        [MaxUsageCount] int NULL,
        [FirstUsedUtc] datetime2(3) NULL,
        [LastUsedUtc] datetime2(3) NULL,
        [RevokedReason] nvarchar(300) NULL,
        [CreatedBy] bigint NULL,
        [CreatedDate] datetime2(3) NOT NULL,
        [ModifiedBy] bigint NULL,
        [ModifiedDate] datetime2(3) NULL,
        [IsActive] bit NOT NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedBy] bigint NULL,
        [DeletedDate] datetime2(3) NULL,
        CONSTRAINT [PK_TRN_AccessToken] PRIMARY KEY ([AccessTokenId]),
        CONSTRAINT [FK_TRN_AccessToken_MST_User_UserId] FOREIGN KEY ([UserId]) REFERENCES [MST_User] ([UserId]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260828171612_InitialCreate'
)
BEGIN
    CREATE TABLE [TRN_Attendance] (
        [AttendanceId] bigint NOT NULL IDENTITY,
        [UserId] bigint NOT NULL,
        [SubjectType] tinyint NOT NULL,
        [LocationId] int NOT NULL,
        [WorkspaceId] int NULL,
        [BookingId] bigint NULL,
        [HousekeepingScheduleId] bigint NULL,
        [AttendanceDate] date NOT NULL,
        [CheckInUtc] datetime2(3) NOT NULL,
        [CheckOutUtc] datetime2(3) NULL,
        [TotalMinutes] int NULL,
        [Status] tinyint NOT NULL,
        [CheckInMethod] tinyint NOT NULL,
        [CheckOutMethod] tinyint NULL,
        [CheckInAccessLogId] bigint NULL,
        [CheckOutAccessLogId] bigint NULL,
        [IsException] bit NOT NULL,
        [ExceptionNotes] nvarchar(500) NULL,
        [IsCorrected] bit NOT NULL,
        [CorrectedByUserId] bigint NULL,
        [CorrectionReason] nvarchar(500) NULL,
        [OriginalCheckInUtc] datetime2(3) NULL,
        [OriginalCheckOutUtc] datetime2(3) NULL,
        [CreatedBy] bigint NULL,
        [CreatedDate] datetime2(3) NOT NULL,
        [ModifiedBy] bigint NULL,
        [ModifiedDate] datetime2(3) NULL,
        [IsActive] bit NOT NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedBy] bigint NULL,
        [DeletedDate] datetime2(3) NULL,
        CONSTRAINT [PK_TRN_Attendance] PRIMARY KEY ([AttendanceId]),
        CONSTRAINT [FK_TRN_Attendance_MST_User_UserId] FOREIGN KEY ([UserId]) REFERENCES [MST_User] ([UserId]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260828171612_InitialCreate'
)
BEGIN
    CREATE TABLE [TRN_CustomerPass] (
        [CustomerPassId] bigint NOT NULL IDENTITY,
        [PassNumber] nvarchar(30) NOT NULL,
        [CustomerId] bigint NOT NULL,
        [PassPlanId] int NOT NULL,
        [LocationId] int NOT NULL,
        [PassType] tinyint NOT NULL,
        [PurchaseUtc] datetime2(3) NOT NULL,
        [ValidFrom] date NOT NULL,
        [ValidTo] date NOT NULL,
        [GuestCount] int NOT NULL,
        [Price] decimal(18,2) NOT NULL,
        [TaxAmount] decimal(18,2) NOT NULL,
        [TotalAmount] decimal(18,2) NOT NULL,
        [Status] tinyint NOT NULL,
        [UsedUtc] datetime2(3) NULL,
        [InvoiceId] bigint NULL,
        [PaymentId] bigint NULL,
        [CreatedBy] bigint NULL,
        [CreatedDate] datetime2(3) NOT NULL,
        [ModifiedBy] bigint NULL,
        [ModifiedDate] datetime2(3) NULL,
        [IsActive] bit NOT NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedBy] bigint NULL,
        [DeletedDate] datetime2(3) NULL,
        CONSTRAINT [PK_TRN_CustomerPass] PRIMARY KEY ([CustomerPassId]),
        CONSTRAINT [FK_TRN_CustomerPass_MST_PassPlan_PassPlanId] FOREIGN KEY ([PassPlanId]) REFERENCES [MST_PassPlan] ([PassPlanId]) ON DELETE NO ACTION,
        CONSTRAINT [FK_TRN_CustomerPass_MST_User_CustomerId] FOREIGN KEY ([CustomerId]) REFERENCES [MST_User] ([UserId]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260828171612_InitialCreate'
)
BEGIN
    CREATE TABLE [TRN_HousekeepingSchedule] (
        [HousekeepingScheduleId] bigint NOT NULL IDENTITY,
        [ScheduleNumber] nvarchar(30) NOT NULL,
        [StaffUserId] bigint NOT NULL,
        [LocationId] int NOT NULL,
        [WorkspaceId] int NULL,
        [AreaName] nvarchar(150) NULL,
        [ScheduledDate] date NOT NULL,
        [StartUtc] datetime2(3) NOT NULL,
        [EndUtc] datetime2(3) NOT NULL,
        [TaskType] tinyint NOT NULL,
        [TaskDescription] nvarchar(500) NOT NULL,
        [Notes] nvarchar(500) NULL,
        [Status] tinyint NOT NULL,
        [BlocksWorkspace] bit NOT NULL,
        [WorkspaceBlockId] bigint NULL,
        [CheckInUtc] datetime2(3) NULL,
        [CheckOutUtc] datetime2(3) NULL,
        [CompletionNotes] nvarchar(500) NULL,
        [AssignedByUserId] bigint NOT NULL,
        [CreatedBy] bigint NULL,
        [CreatedDate] datetime2(3) NOT NULL,
        [ModifiedBy] bigint NULL,
        [ModifiedDate] datetime2(3) NULL,
        [IsActive] bit NOT NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedBy] bigint NULL,
        [DeletedDate] datetime2(3) NULL,
        CONSTRAINT [PK_TRN_HousekeepingSchedule] PRIMARY KEY ([HousekeepingScheduleId]),
        CONSTRAINT [FK_TRN_HousekeepingSchedule_MST_User_StaffUserId] FOREIGN KEY ([StaffUserId]) REFERENCES [MST_User] ([UserId]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260828171612_InitialCreate'
)
BEGIN
    CREATE TABLE [TRN_Invoice] (
        [InvoiceId] bigint NOT NULL IDENTITY,
        [InvoiceNumber] nvarchar(40) NOT NULL,
        [CustomerId] bigint NOT NULL,
        [BookingId] bigint NULL,
        [CustomerPassId] bigint NULL,
        [ServiceOrderId] bigint NULL,
        [LocationId] int NOT NULL,
        [InvoiceDate] date NOT NULL,
        [DueDate] date NOT NULL,
        [SubTotal] decimal(18,2) NOT NULL,
        [DiscountAmount] decimal(18,2) NOT NULL,
        [TaxableAmount] decimal(18,2) NOT NULL,
        [CgstPercent] decimal(5,2) NOT NULL,
        [SgstPercent] decimal(5,2) NOT NULL,
        [IgstPercent] decimal(5,2) NOT NULL,
        [CgstAmount] decimal(18,2) NOT NULL,
        [SgstAmount] decimal(18,2) NOT NULL,
        [IgstAmount] decimal(18,2) NOT NULL,
        [TotalAmount] decimal(18,2) NOT NULL,
        [PaidAmount] decimal(18,2) NOT NULL,
        [OutstandingAmount] AS [TotalAmount] - [PaidAmount] PERSISTED,
        [Status] tinyint NOT NULL,
        [PaymentId] bigint NULL,
        [PaidUtc] datetime2(3) NULL,
        [CustomerName] nvarchar(150) NOT NULL,
        [CustomerEmail] nvarchar(256) NOT NULL,
        [CustomerMobile] nvarchar(20) NULL,
        [CustomerGstin] nvarchar(20) NULL,
        [BillingAddress] nvarchar(500) NULL,
        [CompanyName] nvarchar(200) NOT NULL,
        [CompanyGstin] nvarchar(20) NULL,
        [CompanyAddress] nvarchar(500) NULL,
        [PdfPath] nvarchar(500) NULL,
        [Notes] nvarchar(500) NULL,
        [CancellationReason] nvarchar(500) NULL,
        [CreatedBy] bigint NULL,
        [CreatedDate] datetime2(3) NOT NULL,
        [ModifiedBy] bigint NULL,
        [ModifiedDate] datetime2(3) NULL,
        [IsActive] bit NOT NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedBy] bigint NULL,
        [DeletedDate] datetime2(3) NULL,
        CONSTRAINT [PK_TRN_Invoice] PRIMARY KEY ([InvoiceId]),
        CONSTRAINT [FK_TRN_Invoice_MST_User_CustomerId] FOREIGN KEY ([CustomerId]) REFERENCES [MST_User] ([UserId]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260828171612_InitialCreate'
)
BEGIN
    CREATE TABLE [TRN_Payment] (
        [PaymentId] bigint NOT NULL IDENTITY,
        [PaymentNumber] nvarchar(30) NOT NULL,
        [CustomerId] bigint NOT NULL,
        [BookingId] bigint NULL,
        [InvoiceId] bigint NULL,
        [CustomerPassId] bigint NULL,
        [ServiceOrderId] bigint NULL,
        [Amount] decimal(18,2) NOT NULL,
        [PaymentMethod] tinyint NOT NULL,
        [Gateway] nvarchar(40) NULL,
        [GatewayOrderId] nvarchar(120) NULL,
        [GatewayPaymentId] nvarchar(120) NULL,
        [GatewaySignature] nvarchar(256) NULL,
        [IsSignatureVerified] bit NOT NULL,
        [Status] tinyint NOT NULL,
        [PaidUtc] datetime2(3) NULL,
        [FailureReason] nvarchar(500) NULL,
        [RefundedAmount] decimal(18,2) NULL,
        [RefundReference] nvarchar(120) NULL,
        [RawGatewayResponse] nvarchar(max) NULL,
        [CreatedBy] bigint NULL,
        [CreatedDate] datetime2(3) NOT NULL,
        [ModifiedBy] bigint NULL,
        [ModifiedDate] datetime2(3) NULL,
        [IsActive] bit NOT NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedBy] bigint NULL,
        [DeletedDate] datetime2(3) NULL,
        CONSTRAINT [PK_TRN_Payment] PRIMARY KEY ([PaymentId]),
        CONSTRAINT [FK_TRN_Payment_MST_User_CustomerId] FOREIGN KEY ([CustomerId]) REFERENCES [MST_User] ([UserId]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260828171612_InitialCreate'
)
BEGIN
    CREATE TABLE [TRN_RefreshToken] (
        [RefreshTokenId] bigint NOT NULL IDENTITY,
        [UserId] bigint NOT NULL,
        [TokenHash] nvarchar(128) NOT NULL,
        [FamilyId] uniqueidentifier NOT NULL,
        [ExpiresUtc] datetime2(3) NOT NULL,
        [CreatedUtc] datetime2(3) NOT NULL,
        [CreatedByIp] nvarchar(64) NULL,
        [ConsumedUtc] datetime2(3) NULL,
        [RevokedUtc] datetime2(3) NULL,
        [RevokedReason] nvarchar(200) NULL,
        [ReplacedByTokenHash] nvarchar(128) NULL,
        CONSTRAINT [PK_TRN_RefreshToken] PRIMARY KEY ([RefreshTokenId]),
        CONSTRAINT [FK_TRN_RefreshToken_MST_User_UserId] FOREIGN KEY ([UserId]) REFERENCES [MST_User] ([UserId]) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260828171612_InitialCreate'
)
BEGIN
    CREATE TABLE [TRN_ServiceOrder] (
        [ServiceOrderId] bigint NOT NULL IDENTITY,
        [OrderNumber] nvarchar(30) NOT NULL,
        [CustomerId] bigint NOT NULL,
        [LocationId] int NOT NULL,
        [OrderUtc] datetime2(3) NOT NULL,
        [SubTotal] decimal(18,2) NOT NULL,
        [TaxAmount] decimal(18,2) NOT NULL,
        [TotalAmount] decimal(18,2) NOT NULL,
        [Status] tinyint NOT NULL,
        [PaymentId] bigint NULL,
        [InvoiceId] bigint NULL,
        [Notes] nvarchar(500) NULL,
        [CreatedBy] bigint NULL,
        [CreatedDate] datetime2(3) NOT NULL,
        [ModifiedBy] bigint NULL,
        [ModifiedDate] datetime2(3) NULL,
        [IsActive] bit NOT NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedBy] bigint NULL,
        [DeletedDate] datetime2(3) NULL,
        CONSTRAINT [PK_TRN_ServiceOrder] PRIMARY KEY ([ServiceOrderId]),
        CONSTRAINT [FK_TRN_ServiceOrder_MST_User_CustomerId] FOREIGN KEY ([CustomerId]) REFERENCES [MST_User] ([UserId]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260828171612_InitialCreate'
)
BEGIN
    CREATE TABLE [MAP_RoleMenu] (
        [RoleMenuId] bigint NOT NULL IDENTITY,
        [RoleId] int NOT NULL,
        [MenuId] int NOT NULL,
        CONSTRAINT [PK_MAP_RoleMenu] PRIMARY KEY ([RoleMenuId]),
        CONSTRAINT [FK_MAP_RoleMenu_MST_Menu_MenuId] FOREIGN KEY ([MenuId]) REFERENCES [MST_Menu] ([MenuId]) ON DELETE CASCADE,
        CONSTRAINT [FK_MAP_RoleMenu_MST_Role_RoleId] FOREIGN KEY ([RoleId]) REFERENCES [MST_Role] ([RoleId]) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260828171612_InitialCreate'
)
BEGIN
    CREATE TABLE [TRN_Ticket] (
        [TicketId] bigint NOT NULL IDENTITY,
        [TicketNumber] nvarchar(30) NOT NULL,
        [CustomerId] bigint NOT NULL,
        [TicketCategoryId] int NOT NULL,
        [TicketIssueTypeId] int NOT NULL,
        [LocationId] int NOT NULL,
        [Subject] nvarchar(200) NOT NULL,
        [Description] nvarchar(max) NOT NULL,
        [Priority] tinyint NOT NULL,
        [Status] tinyint NOT NULL,
        [AssignedToUserId] bigint NULL,
        [AssignedUtc] datetime2(3) NULL,
        [FirstResponseUtc] datetime2(3) NULL,
        [ResolvedUtc] datetime2(3) NULL,
        [ClosedUtc] datetime2(3) NULL,
        [ResolutionNotes] nvarchar(max) NULL,
        [ReopenCount] int NOT NULL,
        [CreatedBy] bigint NULL,
        [CreatedDate] datetime2(3) NOT NULL,
        [ModifiedBy] bigint NULL,
        [ModifiedDate] datetime2(3) NULL,
        [IsActive] bit NOT NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedBy] bigint NULL,
        [DeletedDate] datetime2(3) NULL,
        CONSTRAINT [PK_TRN_Ticket] PRIMARY KEY ([TicketId]),
        CONSTRAINT [FK_TRN_Ticket_MST_TicketCategory_TicketCategoryId] FOREIGN KEY ([TicketCategoryId]) REFERENCES [MST_TicketCategory] ([TicketCategoryId]) ON DELETE NO ACTION,
        CONSTRAINT [FK_TRN_Ticket_MST_TicketIssueType_TicketIssueTypeId] FOREIGN KEY ([TicketIssueTypeId]) REFERENCES [MST_TicketIssueType] ([TicketIssueTypeId]) ON DELETE NO ACTION,
        CONSTRAINT [FK_TRN_Ticket_MST_User_CustomerId] FOREIGN KEY ([CustomerId]) REFERENCES [MST_User] ([UserId]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260828171612_InitialCreate'
)
BEGIN
    CREATE TABLE [MAP_WorkspaceAmenity] (
        [WorkspaceAmenityId] bigint NOT NULL IDENTITY,
        [WorkspaceId] int NOT NULL,
        [AmenityId] int NOT NULL,
        CONSTRAINT [PK_MAP_WorkspaceAmenity] PRIMARY KEY ([WorkspaceAmenityId]),
        CONSTRAINT [FK_MAP_WorkspaceAmenity_MST_Amenity_AmenityId] FOREIGN KEY ([AmenityId]) REFERENCES [MST_Amenity] ([AmenityId]) ON DELETE CASCADE,
        CONSTRAINT [FK_MAP_WorkspaceAmenity_MST_Workspace_WorkspaceId] FOREIGN KEY ([WorkspaceId]) REFERENCES [MST_Workspace] ([WorkspaceId]) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260828171612_InitialCreate'
)
BEGIN
    CREATE TABLE [TRN_WorkspaceBlock] (
        [WorkspaceBlockId] bigint NOT NULL IDENTITY,
        [WorkspaceId] int NOT NULL,
        [LocationId] int NOT NULL,
        [StartUtc] datetime2(3) NOT NULL,
        [EndUtc] datetime2(3) NOT NULL,
        [BlockReason] tinyint NOT NULL,
        [ReasonNotes] nvarchar(500) NULL,
        [HousekeepingScheduleId] bigint NULL,
        [BlockedByUserId] bigint NOT NULL,
        [ReleasedUtc] datetime2(3) NULL,
        [ReleasedByUserId] bigint NULL,
        [CreatedBy] bigint NULL,
        [CreatedDate] datetime2(3) NOT NULL,
        [ModifiedBy] bigint NULL,
        [ModifiedDate] datetime2(3) NULL,
        [IsActive] bit NOT NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedBy] bigint NULL,
        [DeletedDate] datetime2(3) NULL,
        CONSTRAINT [PK_TRN_WorkspaceBlock] PRIMARY KEY ([WorkspaceBlockId]),
        CONSTRAINT [CK_Block_Period] CHECK ([EndUtc] > [StartUtc]),
        CONSTRAINT [FK_TRN_WorkspaceBlock_MST_Location_LocationId] FOREIGN KEY ([LocationId]) REFERENCES [MST_Location] ([LocationId]) ON DELETE NO ACTION,
        CONSTRAINT [FK_TRN_WorkspaceBlock_MST_Workspace_WorkspaceId] FOREIGN KEY ([WorkspaceId]) REFERENCES [MST_Workspace] ([WorkspaceId]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260828171612_InitialCreate'
)
BEGIN
    CREATE TABLE [TRN_WalletTransaction] (
        [WalletTransactionId] bigint NOT NULL IDENTITY,
        [WalletId] bigint NOT NULL,
        [CustomerId] bigint NOT NULL,
        [TransactionNumber] nvarchar(30) NOT NULL,
        [Direction] tinyint NOT NULL,
        [Category] tinyint NOT NULL,
        [Amount] decimal(18,2) NOT NULL,
        [BalanceAfter] decimal(18,2) NOT NULL,
        [ReferenceType] nvarchar(40) NULL,
        [ReferenceId] bigint NULL,
        [PaymentId] bigint NULL,
        [Narration] nvarchar(300) NOT NULL,
        [TransactionUtc] datetime2(3) NOT NULL,
        [PerformedByUserId] bigint NOT NULL,
        [CreatedBy] bigint NULL,
        [CreatedDate] datetime2(3) NOT NULL,
        [ModifiedBy] bigint NULL,
        [ModifiedDate] datetime2(3) NULL,
        [IsActive] bit NOT NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedBy] bigint NULL,
        [DeletedDate] datetime2(3) NULL,
        CONSTRAINT [PK_TRN_WalletTransaction] PRIMARY KEY ([WalletTransactionId]),
        CONSTRAINT [FK_TRN_WalletTransaction_MST_Wallet_WalletId] FOREIGN KEY ([WalletId]) REFERENCES [MST_Wallet] ([WalletId]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260828171612_InitialCreate'
)
BEGIN
    CREATE TABLE [TRN_Booking] (
        [BookingId] bigint NOT NULL IDENTITY,
        [BookingNumber] nvarchar(30) NOT NULL,
        [CustomerId] bigint NOT NULL,
        [LocationId] int NOT NULL,
        [WorkspaceId] int NOT NULL,
        [WorkspaceTypeId] int NOT NULL,
        [BookingDate] date NOT NULL,
        [StartUtc] datetime2(3) NOT NULL,
        [EndUtc] datetime2(3) NOT NULL,
        [DurationMinutes] int NOT NULL,
        [BaseAmount] decimal(18,2) NOT NULL,
        [DiscountAmount] decimal(18,2) NOT NULL,
        [TaxAmount] decimal(18,2) NOT NULL,
        [TotalAmount] decimal(18,2) NOT NULL,
        [Status] tinyint NOT NULL,
        [PaymentStatus] tinyint NOT NULL,
        [BookingSource] tinyint NOT NULL,
        [CustomerPassId] bigint NULL,
        [CreatedByUserId] bigint NOT NULL,
        [CheckInUtc] datetime2(3) NULL,
        [CheckOutUtc] datetime2(3) NULL,
        [CancelledUtc] datetime2(3) NULL,
        [CancelledByUserId] bigint NULL,
        [CancellationReason] nvarchar(500) NULL,
        [RefundAmount] decimal(18,2) NULL,
        [Notes] nvarchar(500) NULL,
        [RowVersion] rowversion NOT NULL,
        [CreatedBy] bigint NULL,
        [CreatedDate] datetime2(3) NOT NULL,
        [ModifiedBy] bigint NULL,
        [ModifiedDate] datetime2(3) NULL,
        [IsActive] bit NOT NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedBy] bigint NULL,
        [DeletedDate] datetime2(3) NULL,
        CONSTRAINT [PK_TRN_Booking] PRIMARY KEY ([BookingId]),
        CONSTRAINT [CK_Booking_Amounts] CHECK ([BaseAmount] >= 0 AND [TaxAmount] >= 0 AND [TotalAmount] >= 0),
        CONSTRAINT [CK_Booking_Period] CHECK ([EndUtc] > [StartUtc]),
        CONSTRAINT [FK_TRN_Booking_MST_Location_LocationId] FOREIGN KEY ([LocationId]) REFERENCES [MST_Location] ([LocationId]) ON DELETE NO ACTION,
        CONSTRAINT [FK_TRN_Booking_MST_User_CustomerId] FOREIGN KEY ([CustomerId]) REFERENCES [MST_User] ([UserId]) ON DELETE NO ACTION,
        CONSTRAINT [FK_TRN_Booking_MST_WorkspaceType_WorkspaceTypeId] FOREIGN KEY ([WorkspaceTypeId]) REFERENCES [MST_WorkspaceType] ([WorkspaceTypeId]) ON DELETE NO ACTION,
        CONSTRAINT [FK_TRN_Booking_MST_Workspace_WorkspaceId] FOREIGN KEY ([WorkspaceId]) REFERENCES [MST_Workspace] ([WorkspaceId]) ON DELETE NO ACTION,
        CONSTRAINT [FK_TRN_Booking_TRN_CustomerPass_CustomerPassId] FOREIGN KEY ([CustomerPassId]) REFERENCES [TRN_CustomerPass] ([CustomerPassId]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260828171612_InitialCreate'
)
BEGIN
    CREATE TABLE [TRN_InvoiceLine] (
        [InvoiceLineId] bigint NOT NULL IDENTITY,
        [InvoiceId] bigint NOT NULL,
        [LineNumber] int NOT NULL,
        [Description] nvarchar(300) NOT NULL,
        [HsnSacCode] nvarchar(20) NULL,
        [Quantity] decimal(18,3) NOT NULL,
        [UnitPrice] decimal(18,2) NOT NULL,
        [LineAmount] decimal(18,2) NOT NULL,
        [TaxPercent] decimal(5,2) NOT NULL,
        [TaxAmount] decimal(18,2) NOT NULL,
        [TotalAmount] decimal(18,2) NOT NULL,
        CONSTRAINT [PK_TRN_InvoiceLine] PRIMARY KEY ([InvoiceLineId]),
        CONSTRAINT [FK_TRN_InvoiceLine_TRN_Invoice_InvoiceId] FOREIGN KEY ([InvoiceId]) REFERENCES [TRN_Invoice] ([InvoiceId]) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260828171612_InitialCreate'
)
BEGIN
    CREATE TABLE [TRN_ServiceOrderLine] (
        [ServiceOrderLineId] bigint NOT NULL IDENTITY,
        [ServiceOrderId] bigint NOT NULL,
        [ServiceItemId] int NOT NULL,
        [ItemName] nvarchar(120) NOT NULL,
        [Quantity] decimal(18,3) NOT NULL,
        [UnitPrice] decimal(18,2) NOT NULL,
        [LineAmount] decimal(18,2) NOT NULL,
        CONSTRAINT [PK_TRN_ServiceOrderLine] PRIMARY KEY ([ServiceOrderLineId]),
        CONSTRAINT [FK_TRN_ServiceOrderLine_MST_ServiceItem_ServiceItemId] FOREIGN KEY ([ServiceItemId]) REFERENCES [MST_ServiceItem] ([ServiceItemId]) ON DELETE NO ACTION,
        CONSTRAINT [FK_TRN_ServiceOrderLine_TRN_ServiceOrder_ServiceOrderId] FOREIGN KEY ([ServiceOrderId]) REFERENCES [TRN_ServiceOrder] ([ServiceOrderId]) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260828171612_InitialCreate'
)
BEGIN
    CREATE TABLE [TRN_TicketAttachment] (
        [TicketAttachmentId] bigint NOT NULL IDENTITY,
        [TicketId] bigint NOT NULL,
        [TicketMessageId] bigint NULL,
        [FileName] nvarchar(255) NOT NULL,
        [StoredFileName] nvarchar(255) NOT NULL,
        [FilePath] nvarchar(500) NOT NULL,
        [ContentType] nvarchar(120) NOT NULL,
        [FileSizeBytes] bigint NOT NULL,
        [UploadedByUserId] bigint NOT NULL,
        [UploadedUtc] datetime2(3) NOT NULL,
        CONSTRAINT [PK_TRN_TicketAttachment] PRIMARY KEY ([TicketAttachmentId]),
        CONSTRAINT [FK_TRN_TicketAttachment_TRN_Ticket_TicketId] FOREIGN KEY ([TicketId]) REFERENCES [TRN_Ticket] ([TicketId]) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260828171612_InitialCreate'
)
BEGIN
    CREATE TABLE [TRN_TicketMessage] (
        [TicketMessageId] bigint NOT NULL IDENTITY,
        [TicketId] bigint NOT NULL,
        [SenderUserId] bigint NOT NULL,
        [SenderType] tinyint NOT NULL,
        [Message] nvarchar(max) NOT NULL,
        [IsInternalNote] bit NOT NULL,
        [SentUtc] datetime2(3) NOT NULL,
        [CreatedBy] bigint NULL,
        [CreatedDate] datetime2(3) NOT NULL,
        [ModifiedBy] bigint NULL,
        [ModifiedDate] datetime2(3) NULL,
        [IsActive] bit NOT NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedBy] bigint NULL,
        [DeletedDate] datetime2(3) NULL,
        CONSTRAINT [PK_TRN_TicketMessage] PRIMARY KEY ([TicketMessageId]),
        CONSTRAINT [FK_TRN_TicketMessage_TRN_Ticket_TicketId] FOREIGN KEY ([TicketId]) REFERENCES [TRN_Ticket] ([TicketId]) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260828171612_InitialCreate'
)
BEGIN
    CREATE TABLE [TRN_BookingStatusHistory] (
        [BookingStatusHistoryId] bigint NOT NULL IDENTITY,
        [BookingId] bigint NOT NULL,
        [FromStatus] tinyint NULL,
        [ToStatus] tinyint NOT NULL,
        [ChangedByUserId] bigint NOT NULL,
        [ChangedUtc] datetime2(3) NOT NULL,
        [Reason] nvarchar(500) NULL,
        CONSTRAINT [PK_TRN_BookingStatusHistory] PRIMARY KEY ([BookingStatusHistoryId]),
        CONSTRAINT [FK_TRN_BookingStatusHistory_TRN_Booking_BookingId] FOREIGN KEY ([BookingId]) REFERENCES [TRN_Booking] ([BookingId]) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260828171612_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_MAP_RoleMenu_MenuId] ON [MAP_RoleMenu] ([MenuId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260828171612_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [UX_RoleMenu] ON [MAP_RoleMenu] ([RoleId], [MenuId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260828171612_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_MAP_RolePermission_PermissionId] ON [MAP_RolePermission] ([PermissionId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260828171612_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [UX_RolePermission] ON [MAP_RolePermission] ([RoleId], [PermissionId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260828171612_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_MAP_UserRole_RoleId] ON [MAP_UserRole] ([RoleId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260828171612_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [UX_UserRole] ON [MAP_UserRole] ([UserId], [RoleId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260828171612_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_MAP_WorkspaceAmenity_AmenityId] ON [MAP_WorkspaceAmenity] ([AmenityId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260828171612_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [UX_WorkspaceAmenity] ON [MAP_WorkspaceAmenity] ([WorkspaceId], [AmenityId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260828171612_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [UX_AppSetting_Key] ON [MST_AppSetting] ([SettingKey]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260828171612_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_MST_Banner_LocationId] ON [MST_Banner] ([LocationId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260828171612_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_MST_InventoryItem_LocationId] ON [MST_InventoryItem] ([LocationId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260828171612_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [UX_InventoryItem_Code] ON [MST_InventoryItem] ([ItemCode]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260828171612_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [UX_Location_Code] ON [MST_Location] ([LocationCode]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260828171612_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_MST_Menu_ParentMenuId] ON [MST_Menu] ([ParentMenuId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260828171612_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_MST_Menu_PermissionId] ON [MST_Menu] ([PermissionId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260828171612_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [UX_Menu_Code] ON [MST_Menu] ([MenuCode]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260828171612_InitialCreate'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [UX_NumberSequence_Key_Year] ON [MST_NumberSequence] ([SequenceKey], [FinancialYear]) WHERE [FinancialYear] IS NOT NULL');
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260828171612_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_MST_PassPlan_LocationId] ON [MST_PassPlan] ([LocationId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260828171612_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [UX_PassPlan_Code] ON [MST_PassPlan] ([PlanCode]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260828171612_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [UX_Permission_Code] ON [MST_Permission] ([PermissionCode]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260828171612_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [UX_Role_Code] ON [MST_Role] ([RoleCode]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260828171612_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_MST_ServiceItem_LocationId] ON [MST_ServiceItem] ([LocationId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260828171612_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [UX_ServiceItem_Code] ON [MST_ServiceItem] ([ItemCode]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260828171612_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_MST_TicketIssueType_TicketCategoryId] ON [MST_TicketIssueType] ([TicketCategoryId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260828171612_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_MST_User_DefaultLocationId] ON [MST_User] ([DefaultLocationId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260828171612_InitialCreate'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [UX_User_Email] ON [MST_User] ([Email]) WHERE [IsDeleted] = 0');
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260828171612_InitialCreate'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [UX_User_Mobile] ON [MST_User] ([MobileNumber]) WHERE [IsDeleted] = 0');
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260828171612_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [UX_Wallet_Customer] ON [MST_Wallet] ([CustomerId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260828171612_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_MST_Workspace_WorkspaceTypeId] ON [MST_Workspace] ([WorkspaceTypeId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260828171612_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [UX_Workspace_Location_Code] ON [MST_Workspace] ([LocationId], [WorkspaceCode]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260828171612_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [UX_WorkspaceType_Code] ON [MST_WorkspaceType] ([TypeCode]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260828171612_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_AccessLog_Logged] ON [TRN_AccessLog] ([LoggedUtc]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260828171612_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_AccessLog_Result_Logged] ON [TRN_AccessLog] ([Result], [LoggedUtc]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260828171612_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_AccessLog_User_Logged] ON [TRN_AccessLog] ([UserId], [LoggedUtc]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260828171612_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_AccessToken_Booking] ON [TRN_AccessToken] ([BookingId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260828171612_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_AccessToken_User_Status] ON [TRN_AccessToken] ([UserId], [Status]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260828171612_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [UX_AccessToken_Value] ON [TRN_AccessToken] ([TokenValue]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260828171612_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Attendance_Date_Location] ON [TRN_Attendance] ([AttendanceDate], [LocationId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260828171612_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Attendance_User_Date] ON [TRN_Attendance] ([UserId], [AttendanceDate]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260828171612_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_AuditLog_Entity] ON [TRN_AuditLog] ([EntityName], [EntityId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260828171612_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_AuditLog_Timestamp] ON [TRN_AuditLog] ([TimestampUtc]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260828171612_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_AuditLog_User_Timestamp] ON [TRN_AuditLog] ([UserId], [TimestampUtc]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260828171612_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Booking_Customer_Start] ON [TRN_Booking] ([CustomerId], [StartUtc] DESC);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260828171612_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Booking_Date_Status] ON [TRN_Booking] ([BookingDate], [Status]) INCLUDE ([LocationId], [TotalAmount]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260828171612_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Booking_Location_Start] ON [TRN_Booking] ([LocationId], [StartUtc]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260828171612_InitialCreate'
)
BEGIN
    EXEC(N'CREATE INDEX [IX_Booking_Workspace_Period] ON [TRN_Booking] ([WorkspaceId], [StartUtc], [EndUtc]) INCLUDE ([Status], [BookingId]) WHERE [IsDeleted] = 0');
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260828171612_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_TRN_Booking_CustomerPassId] ON [TRN_Booking] ([CustomerPassId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260828171612_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_TRN_Booking_WorkspaceTypeId] ON [TRN_Booking] ([WorkspaceTypeId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260828171612_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [UX_Booking_Number] ON [TRN_Booking] ([BookingNumber]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260828171612_InitialCreate'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [UX_Booking_Workspace_Start] ON [TRN_Booking] ([WorkspaceId], [StartUtc]) WHERE [Status] IN (1,2,3) AND [IsDeleted] = 0');
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260828171612_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_BookingStatusHistory_Booking] ON [TRN_BookingStatusHistory] ([BookingId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260828171612_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_CustomerPass_Customer_Status] ON [TRN_CustomerPass] ([CustomerId], [Status]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260828171612_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_CustomerPass_ValidTo_Status] ON [TRN_CustomerPass] ([ValidTo], [Status]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260828171612_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_TRN_CustomerPass_PassPlanId] ON [TRN_CustomerPass] ([PassPlanId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260828171612_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [UX_CustomerPass_Number] ON [TRN_CustomerPass] ([PassNumber]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260828171612_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_HkSchedule_Date_Status] ON [TRN_HousekeepingSchedule] ([ScheduledDate], [Status]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260828171612_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_HkSchedule_Staff_Date] ON [TRN_HousekeepingSchedule] ([StaffUserId], [ScheduledDate]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260828171612_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_HkSchedule_Workspace_Period] ON [TRN_HousekeepingSchedule] ([WorkspaceId], [StartUtc], [EndUtc]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260828171612_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [UX_HkSchedule_Number] ON [TRN_HousekeepingSchedule] ([ScheduleNumber]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260828171612_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_InvTxn_Item_Date] ON [TRN_InventoryTransaction] ([InventoryItemId], [TransactionDate]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260828171612_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_InvTxn_Type_Date] ON [TRN_InventoryTransaction] ([TransactionType], [TransactionDate]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260828171612_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [UX_InvTxn_Number] ON [TRN_InventoryTransaction] ([TransactionNumber]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260828171612_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Invoice_Customer_Date] ON [TRN_Invoice] ([CustomerId], [InvoiceDate]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260828171612_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Invoice_Status_Date] ON [TRN_Invoice] ([Status], [InvoiceDate]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260828171612_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [UX_Invoice_Number] ON [TRN_Invoice] ([InvoiceNumber]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260828171612_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_TRN_InvoiceLine_InvoiceId] ON [TRN_InvoiceLine] ([InvoiceId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260828171612_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_LoginHistory_Attempted] ON [TRN_LoginHistory] ([AttemptedUtc]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260828171612_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Notification_Scheduled] ON [TRN_Notification] ([ScheduledForUtc]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260828171612_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Notification_User_Created] ON [TRN_Notification] ([UserId], [CreatedDate]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260828171612_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Notification_User_IsRead] ON [TRN_Notification] ([UserId], [IsRead]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260828171612_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Otp_Destination_Purpose] ON [TRN_Otp] ([Destination], [Purpose]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260828171612_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Payment_Customer_Date] ON [TRN_Payment] ([CustomerId], [CreatedDate]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260828171612_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Payment_Status_Date] ON [TRN_Payment] ([Status], [PaidUtc]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260828171612_InitialCreate'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [UX_Payment_GatewayPaymentId] ON [TRN_Payment] ([GatewayPaymentId]) WHERE [GatewayPaymentId] IS NOT NULL');
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260828171612_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [UX_Payment_Number] ON [TRN_Payment] ([PaymentNumber]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260828171612_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_RefreshToken_User_Family] ON [TRN_RefreshToken] ([UserId], [FamilyId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260828171612_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [UX_RefreshToken_Hash] ON [TRN_RefreshToken] ([TokenHash]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260828171612_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_ServiceOrder_Customer_Date] ON [TRN_ServiceOrder] ([CustomerId], [OrderUtc]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260828171612_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [UX_ServiceOrder_Number] ON [TRN_ServiceOrder] ([OrderNumber]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260828171612_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_TRN_ServiceOrderLine_ServiceItemId] ON [TRN_ServiceOrderLine] ([ServiceItemId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260828171612_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_TRN_ServiceOrderLine_ServiceOrderId] ON [TRN_ServiceOrderLine] ([ServiceOrderId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260828171612_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Ticket_Assigned_Status] ON [TRN_Ticket] ([AssignedToUserId], [Status]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260828171612_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Ticket_Customer_Created] ON [TRN_Ticket] ([CustomerId], [CreatedDate]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260828171612_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Ticket_Status_Priority] ON [TRN_Ticket] ([Status], [Priority]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260828171612_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_TRN_Ticket_TicketCategoryId] ON [TRN_Ticket] ([TicketCategoryId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260828171612_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_TRN_Ticket_TicketIssueTypeId] ON [TRN_Ticket] ([TicketIssueTypeId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260828171612_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [UX_Ticket_Number] ON [TRN_Ticket] ([TicketNumber]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260828171612_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_TRN_TicketAttachment_TicketId] ON [TRN_TicketAttachment] ([TicketId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260828171612_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_TRN_TicketMessage_TicketId] ON [TRN_TicketMessage] ([TicketId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260828171612_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_TRN_WalletTransaction_WalletId] ON [TRN_WalletTransaction] ([WalletId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260828171612_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_WalletTxn_Category] ON [TRN_WalletTransaction] ([Category]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260828171612_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_WalletTxn_Customer_Date] ON [TRN_WalletTransaction] ([CustomerId], [TransactionUtc] DESC);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260828171612_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [UX_WalletTxn_Number] ON [TRN_WalletTransaction] ([TransactionNumber]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260828171612_InitialCreate'
)
BEGIN
    EXEC(N'CREATE INDEX [IX_Block_Workspace_Period] ON [TRN_WorkspaceBlock] ([WorkspaceId], [StartUtc], [EndUtc]) WHERE [IsActive] = 1 AND [IsDeleted] = 0');
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260828171612_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_TRN_WorkspaceBlock_LocationId] ON [TRN_WorkspaceBlock] ([LocationId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260828171612_InitialCreate'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260828171612_InitialCreate', N'8.0.19');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260828210450_AddPasswordHistory'
)
BEGIN
    CREATE TABLE [TRN_PasswordHistory] (
        [PasswordHistoryId] bigint NOT NULL IDENTITY,
        [UserId] bigint NOT NULL,
        [PasswordHash] nvarchar(400) NOT NULL,
        [ChangedUtc] datetime2(3) NOT NULL,
        CONSTRAINT [PK_TRN_PasswordHistory] PRIMARY KEY ([PasswordHistoryId]),
        CONSTRAINT [FK_TRN_PasswordHistory_MST_User_UserId] FOREIGN KEY ([UserId]) REFERENCES [MST_User] ([UserId]) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260828210450_AddPasswordHistory'
)
BEGIN
    CREATE INDEX [IX_PasswordHistory_User_Changed] ON [TRN_PasswordHistory] ([UserId], [ChangedUtc]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260828210450_AddPasswordHistory'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260828210450_AddPasswordHistory', N'8.0.19');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260829180229_SignedAdjustmentQuantity'
)
BEGIN
    ALTER TABLE [TRN_InventoryTransaction] DROP CONSTRAINT [CK_InvTxn_Quantity];
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260829180229_SignedAdjustmentQuantity'
)
BEGIN
    EXEC(N'ALTER TABLE [TRN_InventoryTransaction] ADD CONSTRAINT [CK_InvTxn_Quantity] CHECK ([Quantity] > 0 OR ([TransactionType] = 5 AND [Quantity] <> 0))');
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260829180229_SignedAdjustmentQuantity'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260829180229_SignedAdjustmentQuantity', N'8.0.19');
END;
GO

COMMIT;
GO

