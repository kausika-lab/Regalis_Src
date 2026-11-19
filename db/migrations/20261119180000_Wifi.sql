-- Wi-Fi Access (Regalis_WorkSpace_WiFi_Feature_Specification v1.0; kit deploy/wifi).
-- Tables, the WifiAccess number sequence (WIFI-000001), permissions WIFI.VIEW / WIFI.MANAGE, the
-- admin menu row System > Device / Access Control > Wi-Fi (/admin/wifi), the customer menu row
-- My Wi-Fi (/wifi), and the settings defaults. Nothing existing is renamed, moved or changed.
--
-- Access rows are never deleted (spec 6.3). Session rows are deleted by the API after the
-- retention period (12 months by default). The TRN_WifiSim* tables are the simulated Wi-Fi
-- controller used until the real hardware is confirmed (open item 1).
--
-- issue.sinceUtc is set to the moment this script runs: only bookings and passes paid from now
-- on get Wi-Fi automatically (Settings can back-date it).
--
-- Idempotent; one transaction. ASCII only.
--   sqlcmd -S localhost -d RegalisWorkSpace_UAT -E -C -I -b -i 20261119180000_Wifi.sql
-- Rollback: 20261119180000_Wifi_Rollback.sql
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
SET NOCOUNT ON;
GO
SET XACT_ABORT ON;
BEGIN TRANSACTION;

DECLARE @now datetime2(3) = SYSUTCDATETIME();

-- 1. Tables -----------------------------------------------------------------------------------------
IF OBJECT_ID(N'dbo.TRN_WifiAccess', N'U') IS NULL
    CREATE TABLE dbo.TRN_WifiAccess
    (
        AccessId             bigint IDENTITY(1,1) NOT NULL CONSTRAINT PK_TRN_WifiAccess PRIMARY KEY,
        AccessNo             nvarchar(30)  NOT NULL,
        Kind                 tinyint       NOT NULL,   -- 1 booking, 2 pass, 3 team member, 4 guest
        Profile              nvarchar(20)  NOT NULL,
        CustomerId           bigint        NULL,
        BookingId            bigint        NULL,
        CustomerPassId       bigint        NULL,
        ParentAccessId       bigint        NULL,
        HolderName           nvarchar(150) NOT NULL,
        HolderEmail          nvarchar(256) NULL,
        HolderMobile         nvarchar(20)  NULL,
        Ssid                 nvarchar(64)  NOT NULL,
        Username             nvarchar(64)  NOT NULL,
        PasswordEnc          nvarchar(400) NOT NULL,   -- AES-GCM; the key is not in the database
        ValidFromUtc         datetime2(3)  NOT NULL,
        ValidUntilUtc        datetime2(3)  NOT NULL,
        DeviceLimit          int           NOT NULL,
        Status               tinyint       NOT NULL CONSTRAINT DF_WifiAccess_Status DEFAULT (1),  -- 1 issued 2 active 3 paused 4 expired 5 revoked
        ControllerRef        nvarchar(100) NULL,
        ControllerState      tinyint       NOT NULL CONSTRAINT DF_WifiAccess_CtrlState DEFAULT (0),  -- 0 pending 1 live 2 failed 3 removed
        ControllerAttempts   int           NOT NULL CONSTRAINT DF_WifiAccess_CtrlAttempts DEFAULT (0),
        ControllerNextTryUtc datetime2(3)  NULL,
        ControllerError      nvarchar(500) NULL,
        IssuedBy             bigint        NULL,
        IssuedUtc            datetime2(3)  NOT NULL,
        DeliveredUtc         datetime2(3)  NULL,
        EmailStatus          nvarchar(10)  NULL,
        EmailSentUtc         datetime2(3)  NULL,
        EmailError           nvarchar(500) NULL,
        WhatsAppUtc          datetime2(3)  NULL,
        SeenInAppUtc         datetime2(3)  NULL,
        ReminderSentUtc      datetime2(3)  NULL,
        PausedUtc            datetime2(3)  NULL,
        PauseReason          nvarchar(300) NULL,
        RevokedReason        nvarchar(500) NULL,
        RevokedBy            bigint        NULL,
        RevokedUtc           datetime2(3)  NULL,
        ExpiredUtc           datetime2(3)  NULL,
        Note                 nvarchar(500) NULL,
        CreatedUtc           datetime2(3)  NOT NULL,
        UpdatedUtc           datetime2(3)  NOT NULL,
        CONSTRAINT UX_WifiAccess_No UNIQUE (AccessNo),
        CONSTRAINT UX_WifiAccess_Username UNIQUE (Username),
        CONSTRAINT CK_WifiAccess_Kind CHECK (Kind BETWEEN 1 AND 4),
        CONSTRAINT CK_WifiAccess_Status CHECK (Status BETWEEN 1 AND 5)
    );
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_WifiAccess_Customer')
    CREATE INDEX IX_WifiAccess_Customer ON dbo.TRN_WifiAccess (CustomerId);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_WifiAccess_Status')
    CREATE INDEX IX_WifiAccess_Status ON dbo.TRN_WifiAccess (Status, ValidUntilUtc);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_WifiAccess_Ref')
    CREATE INDEX IX_WifiAccess_Ref ON dbo.TRN_WifiAccess (ControllerRef);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_WifiAccess_Parent')
    CREATE INDEX IX_WifiAccess_Parent ON dbo.TRN_WifiAccess (ParentAccessId);

IF OBJECT_ID(N'dbo.TRN_WifiAccessSource', N'U') IS NULL
    CREATE TABLE dbo.TRN_WifiAccessSource
    (
        SourceId    bigint IDENTITY(1,1) NOT NULL CONSTRAINT PK_TRN_WifiAccessSource PRIMARY KEY,
        AccessId    bigint       NOT NULL,   -- 0 = this kind of booking gets no Wi-Fi (e.g. virtual office)
        SourceType  tinyint      NOT NULL,   -- 1 booking, 2 pass
        RefId       bigint       NOT NULL,
        RefNumber   nvarchar(40) NOT NULL,
        StartUtc    datetime2(3) NOT NULL,
        EndUtc      datetime2(3) NOT NULL,
        IsCancelled bit          NOT NULL CONSTRAINT DF_WifiAccessSource_Cancelled DEFAULT (0),
        CreatedUtc  datetime2(3) NOT NULL,
        CONSTRAINT UX_WifiAccessSource_Ref UNIQUE (SourceType, RefId)
    );
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_WifiAccessSource_Access')
    CREATE INDEX IX_WifiAccessSource_Access ON dbo.TRN_WifiAccessSource (AccessId);

IF OBJECT_ID(N'dbo.TRN_WifiDevice', N'U') IS NULL
    CREATE TABLE dbo.TRN_WifiDevice
    (
        DeviceId     bigint IDENTITY(1,1) NOT NULL CONSTRAINT PK_TRN_WifiDevice PRIMARY KEY,
        AccessId     bigint        NOT NULL,
        MacAddress   nvarchar(17)  NOT NULL,
        DeviceName   nvarchar(120) NULL,
        FirstSeenUtc datetime2(3)  NOT NULL,
        LastSeenUtc  datetime2(3)  NOT NULL,
        RemovedUtc   datetime2(3)  NULL,
        RemovedBy    nvarchar(20)  NULL
    );
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_WifiDevice_Access_Mac')
    CREATE INDEX IX_WifiDevice_Access_Mac ON dbo.TRN_WifiDevice (AccessId, MacAddress);

IF OBJECT_ID(N'dbo.TRN_WifiSession', N'U') IS NULL
    CREATE TABLE dbo.TRN_WifiSession
    (
        SessionId   bigint IDENTITY(1,1) NOT NULL CONSTRAINT PK_TRN_WifiSession PRIMARY KEY,
        AccessId    bigint        NULL,      -- NULL = matched no customer
        MacAddress  nvarchar(17)  NOT NULL,
        DeviceName  nvarchar(120) NULL,
        Ssid        nvarchar(64)  NULL,
        AccessPoint nvarchar(120) NULL,
        IpAddress   nvarchar(45)  NULL,
        StartedUtc  datetime2(3)  NOT NULL,
        EndedUtc    datetime2(3)  NULL,
        LastSeenUtc datetime2(3)  NOT NULL,
        BytesUp     bigint        NOT NULL CONSTRAINT DF_WifiSession_Up DEFAULT (0),
        BytesDown   bigint        NOT NULL CONSTRAINT DF_WifiSession_Down DEFAULT (0),
        Result      tinyint       NOT NULL CONSTRAINT DF_WifiSession_Result DEFAULT (1)  -- 1 connected 2 wrong password 3 expired 4 device limit
    );
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_WifiSession_Open')
    CREATE INDEX IX_WifiSession_Open ON dbo.TRN_WifiSession (EndedUtc, MacAddress);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_WifiSession_Started')
    CREATE INDEX IX_WifiSession_Started ON dbo.TRN_WifiSession (StartedUtc);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_WifiSession_Access')
    CREATE INDEX IX_WifiSession_Access ON dbo.TRN_WifiSession (AccessId);

IF OBJECT_ID(N'dbo.TRN_WifiEvent', N'U') IS NULL
    CREATE TABLE dbo.TRN_WifiEvent
    (
        EventId     bigint IDENTITY(1,1) NOT NULL CONSTRAINT PK_TRN_WifiEvent PRIMARY KEY,
        AccessId    bigint         NULL,
        Event       nvarchar(30)   NOT NULL,
        ActorUserId bigint         NULL,     -- NULL = the system
        Detail      nvarchar(1000) NOT NULL,
        MetaJson    nvarchar(max)  NULL,
        CreatedUtc  datetime2(3)   NOT NULL
    );
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_WifiEvent_Access')
    CREATE INDEX IX_WifiEvent_Access ON dbo.TRN_WifiEvent (AccessId, CreatedUtc);

IF OBJECT_ID(N'dbo.TRN_WifiBlock', N'U') IS NULL
    CREATE TABLE dbo.TRN_WifiBlock
    (
        BlockId      bigint IDENTITY(1,1) NOT NULL CONSTRAINT PK_TRN_WifiBlock PRIMARY KEY,
        MacAddress   nvarchar(17)  NOT NULL,
        Reason       nvarchar(300) NULL,
        BlockedBy    bigint        NULL,
        BlockedUtc   datetime2(3)  NOT NULL,
        UnblockedUtc datetime2(3)  NULL,
        UnblockedBy  bigint        NULL
    );
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_WifiBlock_Mac')
    CREATE INDEX IX_WifiBlock_Mac ON dbo.TRN_WifiBlock (MacAddress);

IF OBJECT_ID(N'dbo.MST_WifiSetting', N'U') IS NULL
    CREATE TABLE dbo.MST_WifiSetting
    (
        SettingKey   nvarchar(60)  NOT NULL CONSTRAINT PK_MST_WifiSetting PRIMARY KEY,
        SettingValue nvarchar(max) NULL,
        UpdatedBy    bigint        NULL,
        UpdatedUtc   datetime2(3)  NULL
    );

IF OBJECT_ID(N'dbo.TRN_WifiSimKey', N'U') IS NULL
    CREATE TABLE dbo.TRN_WifiSimKey
    (
        ControllerRef nvarchar(100) NOT NULL CONSTRAINT PK_TRN_WifiSimKey PRIMARY KEY,
        Ssid          nvarchar(64)  NOT NULL,
        Username      nvarchar(64)  NOT NULL,
        PasswordEnc   nvarchar(400) NOT NULL,
        CreatedUtc    datetime2(3)  NOT NULL
    );

IF OBJECT_ID(N'dbo.TRN_WifiSimClient', N'U') IS NULL
    CREATE TABLE dbo.TRN_WifiSimClient
    (
        MacAddress    nvarchar(17)  NOT NULL CONSTRAINT PK_TRN_WifiSimClient PRIMARY KEY,
        ControllerRef nvarchar(100) NULL,
        DeviceName    nvarchar(120) NULL,
        Ssid          nvarchar(64)  NULL,
        AccessPoint   nvarchar(120) NULL,
        IpAddress     nvarchar(45)  NULL,
        ConnectedUtc  datetime2(3)  NULL,
        BytesUp       bigint        NOT NULL CONSTRAINT DF_WifiSimClient_Up DEFAULT (0),
        BytesDown     bigint        NOT NULL CONSTRAINT DF_WifiSimClient_Down DEFAULT (0),
        IsBlocked     bit           NOT NULL CONSTRAINT DF_WifiSimClient_Blocked DEFAULT (0)
    );

IF OBJECT_ID(N'dbo.TRN_WifiSimEvent', N'U') IS NULL
    CREATE TABLE dbo.TRN_WifiSimEvent
    (
        SimEventId    bigint IDENTITY(1,1) NOT NULL CONSTRAINT PK_TRN_WifiSimEvent PRIMARY KEY,
        MacAddress    nvarchar(17)  NOT NULL,
        DeviceName    nvarchar(120) NULL,
        Ssid          nvarchar(64)  NULL,
        AccessPoint   nvarchar(120) NULL,
        ControllerRef nvarchar(100) NULL,
        Result        tinyint       NOT NULL,
        AtUtc         datetime2(3)  NOT NULL
    );

-- 2. Settings defaults (spec 5.3; open items 2-6 as written in the spec) ---------------------------------
INSERT INTO dbo.MST_WifiSetting (SettingKey, SettingValue, UpdatedUtc)
SELECT v.k, v.v, @now
FROM (VALUES
    (N'controller.type',       N'simulated'),
    (N'mode',                  N'per_customer'),
    (N'issue.auto',            N'1'),
    (N'issue.sinceUtc',        CONVERT(nvarchar(40), @now, 127) + N'Z'),
    (N'issue.payLater',        N'0'),
    (N'delivery.email',        N'1'),
    (N'delivery.whatsapp',     N'1'),
    (N'delivery.qr',           N'1'),
    (N'delivery.reminder',     N'0'),
    (N'rules.dayPassUntil',    N'closing'),
    (N'rules.idleMinutes',     N'30'),
    (N'rules.cancel',          N'1'),
    (N'rules.overdue',         N'1'),
    (N'rules.overdueDays',     N'7'),
    (N'rules.unknownDevice',   N'1'),
    (N'retention.months',      N'12')
) v (k, v)
WHERE NOT EXISTS (SELECT 1 FROM dbo.MST_WifiSetting x WHERE x.SettingKey = v.k);

IF NOT EXISTS (SELECT 1 FROM dbo.MST_NumberSequence WHERE SequenceKey = N'WifiAccess')
    INSERT INTO dbo.MST_NumberSequence (SequenceKey, Prefix, CurrentValue, FinancialYear) VALUES (N'WifiAccess', N'WIFI-', 0, NULL);

-- 3. Permissions (spec 8) ----------------------------------------------------------------------------
DECLARE @perm TABLE (Code nvarchar(100), ActionName nvarchar(50), Description nvarchar(200), DisplayOrder int);
INSERT INTO @perm VALUES
    (N'WIFI.VIEW',   N'VIEW',   N'See who is on the Wi-Fi, the access issued, the usage log and the settings', 1),
    (N'WIFI.MANAGE', N'MANAGE', N'Issue guest access, resend, reset, extend, revoke, disconnect or block devices, change Wi-Fi settings', 2);

INSERT INTO dbo.MST_Permission (PermissionCode, ModuleName, ActionName, Description, DisplayOrder, CreatedDate, IsActive, IsDeleted)
SELECT p.Code, N'WIFI', p.ActionName, p.Description, p.DisplayOrder, @now, 1, 0
FROM @perm p
WHERE NOT EXISTS (SELECT 1 FROM dbo.MST_Permission x WHERE x.PermissionCode = p.Code);

DECLARE @grant TABLE (RoleCode nvarchar(50), Code nvarchar(100));
INSERT INTO @grant VALUES
    (N'ADMIN', N'WIFI.VIEW'), (N'ADMIN', N'WIFI.MANAGE'),
    (N'MANAGER', N'WIFI.VIEW'), (N'MANAGER', N'WIFI.MANAGE'),
    (N'SUPPORT', N'WIFI.VIEW');   -- support answers "Having trouble connecting?" tickets

INSERT INTO dbo.MAP_RolePermission (RoleId, PermissionId)
SELECT DISTINCT r.RoleId, p.PermissionId
FROM @grant g
JOIN dbo.MST_Role r ON r.RoleCode = g.RoleCode
JOIN dbo.MST_Permission p ON p.PermissionCode = g.Code
WHERE NOT EXISTS (SELECT 1 FROM dbo.MAP_RolePermission x WHERE x.RoleId = r.RoleId AND x.PermissionId = p.PermissionId);

-- 4. Menus (spec 1.2) ---------------------------------------------------------------------------------
DECLARE @access int = (SELECT MenuId FROM dbo.MST_Menu WHERE MenuCode = N'ADM_ACCESS' AND IsDeleted = 0);
DECLARE @system int = (SELECT MenuId FROM dbo.MST_Menu WHERE MenuCode = N'ADM_SEC_SYSTEM' AND IsDeleted = 0);
IF @access IS NULL OR @system IS NULL
BEGIN
    RAISERROR('Device / Access Control or System menu row not found - nothing changed.', 16, 1);
    ROLLBACK TRANSACTION;
    RETURN;
END;

INSERT INTO dbo.MST_Menu (ParentMenuId, MenuCode, MenuName, DisplayName, Route, Icon, DisplayOrder, PermissionId, PortalType, MenuLevel, IsVisible, CreatedDate, IsActive, IsDeleted)
SELECT @access, N'ADM_ACCESS_WIFI', N'Wi-Fi', N'Wi-Fi', N'/admin/wifi', N'Wifi', 280,
       (SELECT PermissionId FROM dbo.MST_Permission WHERE PermissionCode = N'WIFI.VIEW'), 2, 2, 1, @now, 1, 0
WHERE NOT EXISTS (SELECT 1 FROM dbo.MST_Menu WHERE MenuCode = N'ADM_ACCESS_WIFI' AND IsDeleted = 0);

-- The Wi-Fi row, its Device / Access Control parent and the System band go to every staff role holding WIFI.VIEW.
INSERT INTO dbo.MAP_RoleMenu (RoleId, MenuId)
SELECT DISTINCT rp.RoleId, m.MenuId
FROM dbo.MAP_RolePermission rp
JOIN dbo.MST_Permission p ON p.PermissionId = rp.PermissionId AND p.PermissionCode = N'WIFI.VIEW'
JOIN dbo.MST_Role r ON r.RoleId = rp.RoleId AND r.RoleCode <> N'CUSTOMER'
JOIN dbo.MST_Menu m ON m.MenuCode IN (N'ADM_ACCESS_WIFI', N'ADM_ACCESS', N'ADM_SEC_SYSTEM') AND m.IsDeleted = 0
WHERE NOT EXISTS (SELECT 1 FROM dbo.MAP_RoleMenu x WHERE x.RoleId = rp.RoleId AND x.MenuId = m.MenuId);

-- Customer portal: My Wi-Fi, after Payment History. No permission row: the API serves only the
-- signed-in customer's own access, and every customer may have some.
INSERT INTO dbo.MST_Menu (ParentMenuId, MenuCode, MenuName, DisplayName, Route, Icon, DisplayOrder, PermissionId, PortalType, MenuLevel, IsVisible, CreatedDate, IsActive, IsDeleted)
SELECT NULL, N'CUST_WIFI', N'My Wi-Fi', N'My Wi-Fi', N'/wifi', N'Wifi', 78, NULL, 1, 1, 1, @now, 1, 0
WHERE NOT EXISTS (SELECT 1 FROM dbo.MST_Menu WHERE MenuCode = N'CUST_WIFI' AND IsDeleted = 0);

INSERT INTO dbo.MAP_RoleMenu (RoleId, MenuId)
SELECT r.RoleId, m.MenuId
FROM dbo.MST_Role r
JOIN dbo.MST_Menu m ON m.MenuCode = N'CUST_WIFI' AND m.IsDeleted = 0
WHERE r.RoleCode = N'CUSTOMER'
  AND NOT EXISTS (SELECT 1 FROM dbo.MAP_RoleMenu x WHERE x.RoleId = r.RoleId AND x.MenuId = m.MenuId);

COMMIT TRANSACTION;
PRINT 'Wi-Fi migration applied.';
