-- CRM - Leads and Web Enquiries (Regalis_WorkSpace_CRM_Build_Document, Nov 2026; kit deploy/crm).
-- Tables, master lists, settings, the CrmLead number sequence (L-000001), ten CRM permissions,
-- the new "Sales / Front desk" role, and the sidebar: a SALES band between OPERATIONS and
-- FINANCE with CRM (Leads, Follow-ups, Website Visitors, CRM Reports), plus CRM Settings under
-- Administration. Nothing existing is renamed, moved or changed.
--
-- Tables holding a person's details (lead, activity, follow-up, consent, visitor, page view,
-- notification, WhatsApp ref) have no IsDeleted column: "delete on request" is a real delete
-- (document section 17), done by the API in one transaction.
--
-- Idempotent; one transaction. ASCII only.
--   sqlcmd -S localhost -d RegalisWorkSpace_UAT -E -C -I -b -i 20261119120000_Crm.sql
-- Rollback: 20261119120000_Crm_Rollback.sql
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
SET NOCOUNT ON;
GO
SET XACT_ABORT ON;
BEGIN TRANSACTION;

DECLARE @now datetime2(3) = SYSUTCDATETIME();

-- 1. Master lists ------------------------------------------------------------------------------
IF OBJECT_ID(N'dbo.MST_CrmSource', N'U') IS NULL
    CREATE TABLE dbo.MST_CrmSource
    (
        SourceId       int IDENTITY(1,1) NOT NULL CONSTRAINT PK_MST_CrmSource PRIMARY KEY,
        Code           nvarchar(30)  NOT NULL CONSTRAINT UX_CrmSource_Code UNIQUE,
        Name           nvarchar(100) NOT NULL,
        IsWebsite      bit           NOT NULL CONSTRAINT DF_CrmSource_IsWebsite DEFAULT (0),
        UtmSource      nvarchar(200) NULL,
        UtmMedium      nvarchar(100) NULL,
        ReferrerMatch  nvarchar(200) NULL,
        SortOrder      int           NOT NULL CONSTRAINT DF_CrmSource_Sort DEFAULT (0),
        CreatedBy bigint NULL, CreatedDate datetime2(3) NOT NULL CONSTRAINT DF_CrmSource_Created DEFAULT (SYSUTCDATETIME()),
        ModifiedBy bigint NULL, ModifiedDate datetime2(3) NULL,
        IsActive bit NOT NULL CONSTRAINT DF_CrmSource_IsActive DEFAULT (1), IsDeleted bit NOT NULL CONSTRAINT DF_CrmSource_IsDeleted DEFAULT (0),
        DeletedBy bigint NULL, DeletedDate datetime2(3) NULL
    );

IF OBJECT_ID(N'dbo.MST_CrmStatus', N'U') IS NULL
    CREATE TABLE dbo.MST_CrmStatus
    (
        StatusId     int IDENTITY(1,1) NOT NULL CONSTRAINT PK_MST_CrmStatus PRIMARY KEY,
        Code         nvarchar(30)  NOT NULL CONSTRAINT UX_CrmStatus_Code UNIQUE,
        Name         nvarchar(60)  NOT NULL,
        Description  nvarchar(200) NULL,
        IsClosed     bit           NOT NULL CONSTRAINT DF_CrmStatus_IsClosed DEFAULT (0),
        IsWon        bit           NOT NULL CONSTRAINT DF_CrmStatus_IsWon DEFAULT (0),
        Requires     tinyint       NOT NULL CONSTRAINT DF_CrmStatus_Requires DEFAULT (0),
        Colour       nvarchar(20)  NOT NULL CONSTRAINT DF_CrmStatus_Colour DEFAULT (N'neutral'),
        SortOrder    int           NOT NULL CONSTRAINT DF_CrmStatus_Sort DEFAULT (0),
        CreatedBy bigint NULL, CreatedDate datetime2(3) NOT NULL CONSTRAINT DF_CrmStatus_Created DEFAULT (SYSUTCDATETIME()),
        ModifiedBy bigint NULL, ModifiedDate datetime2(3) NULL,
        IsActive bit NOT NULL CONSTRAINT DF_CrmStatus_IsActive DEFAULT (1), IsDeleted bit NOT NULL CONSTRAINT DF_CrmStatus_IsDeleted DEFAULT (0),
        DeletedBy bigint NULL, DeletedDate datetime2(3) NULL,
        CONSTRAINT CK_CrmStatus_Requires CHECK (Requires BETWEEN 0 AND 5)
    );

IF OBJECT_ID(N'dbo.MST_CrmLostReason', N'U') IS NULL
    CREATE TABLE dbo.MST_CrmLostReason
    (
        LostReasonId int IDENTITY(1,1) NOT NULL CONSTRAINT PK_MST_CrmLostReason PRIMARY KEY,
        Name         nvarchar(100) NOT NULL,
        Hint         nvarchar(200) NULL,
        SortOrder    int           NOT NULL CONSTRAINT DF_CrmLostReason_Sort DEFAULT (0),
        CreatedBy bigint NULL, CreatedDate datetime2(3) NOT NULL CONSTRAINT DF_CrmLostReason_Created DEFAULT (SYSUTCDATETIME()),
        ModifiedBy bigint NULL, ModifiedDate datetime2(3) NULL,
        IsActive bit NOT NULL CONSTRAINT DF_CrmLostReason_IsActive DEFAULT (1), IsDeleted bit NOT NULL CONSTRAINT DF_CrmLostReason_IsDeleted DEFAULT (0),
        DeletedBy bigint NULL, DeletedDate datetime2(3) NULL
    );

IF OBJECT_ID(N'dbo.MST_CrmLookingFor', N'U') IS NULL
    CREATE TABLE dbo.MST_CrmLookingFor
    (
        LookingForId    int IDENTITY(1,1) NOT NULL CONSTRAINT PK_MST_CrmLookingFor PRIMARY KEY,
        Name            nvarchar(100) NOT NULL,
        WorkspaceTypeId int           NULL CONSTRAINT FK_CrmLookingFor_WorkspaceType REFERENCES dbo.MST_WorkspaceType (WorkspaceTypeId),
        IsLarge         bit           NOT NULL CONSTRAINT DF_CrmLookingFor_IsLarge DEFAULT (0),
        SortOrder       int           NOT NULL CONSTRAINT DF_CrmLookingFor_Sort DEFAULT (0),
        CreatedBy bigint NULL, CreatedDate datetime2(3) NOT NULL CONSTRAINT DF_CrmLookingFor_Created DEFAULT (SYSUTCDATETIME()),
        ModifiedBy bigint NULL, ModifiedDate datetime2(3) NULL,
        IsActive bit NOT NULL CONSTRAINT DF_CrmLookingFor_IsActive DEFAULT (1), IsDeleted bit NOT NULL CONSTRAINT DF_CrmLookingFor_IsDeleted DEFAULT (0),
        DeletedBy bigint NULL, DeletedDate datetime2(3) NULL
    );

IF OBJECT_ID(N'dbo.MST_CrmAssignmentRule', N'U') IS NULL
    CREATE TABLE dbo.MST_CrmAssignmentRule
    (
        RuleId         int IDENTITY(1,1) NOT NULL CONSTRAINT PK_MST_CrmAssignmentRule PRIMARY KEY,
        Label          nvarchar(200) NOT NULL,
        ConditionType  tinyint       NOT NULL,
        LocationId     int           NULL CONSTRAINT FK_CrmRule_Location REFERENCES dbo.MST_Location (LocationId),
        MinPeople      int           NULL,
        LookingForId   int           NULL CONSTRAINT FK_CrmRule_LookingFor REFERENCES dbo.MST_CrmLookingFor (LookingForId),
        TargetType     tinyint       NOT NULL,
        TargetUserId   bigint        NULL CONSTRAINT FK_CrmRule_User REFERENCES dbo.MST_User (UserId),
        SortOrder      int           NOT NULL CONSTRAINT DF_CrmRule_Sort DEFAULT (0),
        CreatedBy bigint NULL, CreatedDate datetime2(3) NOT NULL CONSTRAINT DF_CrmRule_Created DEFAULT (SYSUTCDATETIME()),
        ModifiedBy bigint NULL, ModifiedDate datetime2(3) NULL,
        IsActive bit NOT NULL CONSTRAINT DF_CrmRule_IsActive DEFAULT (1), IsDeleted bit NOT NULL CONSTRAINT DF_CrmRule_IsDeleted DEFAULT (0),
        DeletedBy bigint NULL, DeletedDate datetime2(3) NULL,
        CONSTRAINT CK_CrmRule_Condition CHECK (ConditionType BETWEEN 1 AND 5),
        CONSTRAINT CK_CrmRule_Target CHECK (TargetType BETWEEN 1 AND 6)
    );

IF OBJECT_ID(N'dbo.MST_CrmTeamMember', N'U') IS NULL
    CREATE TABLE dbo.MST_CrmTeamMember
    (
        UserId           bigint NOT NULL CONSTRAINT PK_MST_CrmTeamMember PRIMARY KEY CONSTRAINT FK_CrmTeam_User REFERENCES dbo.MST_User (UserId),
        IsOnDuty         bit    NOT NULL CONSTRAINT DF_CrmTeam_OnDuty DEFAULT (0),
        IsManager        bit    NOT NULL CONSTRAINT DF_CrmTeam_Manager DEFAULT (0),
        LocationId       int    NULL CONSTRAINT FK_CrmTeam_Location REFERENCES dbo.MST_Location (LocationId),
        LastAssignedUtc  datetime2(3) NULL,
        CreatedBy bigint NULL, CreatedDate datetime2(3) NOT NULL CONSTRAINT DF_CrmTeam_Created DEFAULT (SYSUTCDATETIME()),
        ModifiedBy bigint NULL, ModifiedDate datetime2(3) NULL,
        IsActive bit NOT NULL CONSTRAINT DF_CrmTeam_IsActive DEFAULT (1), IsDeleted bit NOT NULL CONSTRAINT DF_CrmTeam_IsDeleted DEFAULT (0),
        DeletedBy bigint NULL, DeletedDate datetime2(3) NULL
    );

IF OBJECT_ID(N'dbo.MST_CrmSetting', N'U') IS NULL
    CREATE TABLE dbo.MST_CrmSetting
    (
        SettingId     int IDENTITY(1,1) NOT NULL CONSTRAINT PK_MST_CrmSetting PRIMARY KEY,
        SettingKey    nvarchar(60)   NOT NULL CONSTRAINT UX_CrmSetting_Key UNIQUE,
        SettingValue  nvarchar(2000) NULL,
        UpdatedBy     bigint         NULL,
        UpdatedUtc    datetime2(3)   NOT NULL CONSTRAINT DF_CrmSetting_Updated DEFAULT (SYSUTCDATETIME())
    );

-- 2. Leads and their history ----------------------------------------------------------------------
IF OBJECT_ID(N'dbo.TRN_CrmLead', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.TRN_CrmLead
    (
        LeadId               bigint IDENTITY(1,1) NOT NULL CONSTRAINT PK_TRN_CrmLead PRIMARY KEY,
        LeadNumber           nvarchar(30)   NOT NULL,
        Name                 nvarchar(150)  NOT NULL,
        Company              nvarchar(150)  NULL,
        Mobile               nvarchar(20)   NOT NULL,
        MobileNormalised     nvarchar(10)   NOT NULL,
        Email                nvarchar(256)  NULL,
        EmailNormalised      nvarchar(256)  NULL,
        LookingForId         int            NULL CONSTRAINT FK_CrmLead_LookingFor REFERENCES dbo.MST_CrmLookingFor (LookingForId),
        PeopleCount          int            NULL,
        BudgetText           nvarchar(100)  NULL,
        LocationId           int            NULL CONSTRAINT FK_CrmLead_Location REFERENCES dbo.MST_Location (LocationId),
        NeededFrom           date           NULL,
        StatusId             int            NOT NULL CONSTRAINT FK_CrmLead_Status REFERENCES dbo.MST_CrmStatus (StatusId),
        SourceId             int            NOT NULL CONSTRAINT FK_CrmLead_Source REFERENCES dbo.MST_CrmSource (SourceId),
        SourceDetail         nvarchar(300)  NULL,
        Channel              tinyint        NOT NULL CONSTRAINT DF_CrmLead_Channel DEFAULT (1),
        OwnerUserId          bigint         NULL CONSTRAINT FK_CrmLead_Owner REFERENCES dbo.MST_User (UserId),
        ReferredByCustomerId bigint         NULL CONSTRAINT FK_CrmLead_ReferredBy REFERENCES dbo.MST_User (UserId),
        WalkInUtc            datetime2(3)   NULL,
        ShownByUserId        bigint         NULL CONSTRAINT FK_CrmLead_ShownBy REFERENCES dbo.MST_User (UserId),
        VisitorId            bigint         NULL,
        FirstMessage         nvarchar(2000) NULL,
        LostReasonId         int            NULL CONSTRAINT FK_CrmLead_LostReason REFERENCES dbo.MST_CrmLostReason (LostReasonId),
        LostNote             nvarchar(500)  NULL,
        CustomerId           bigint         NULL CONSTRAINT FK_CrmLead_Customer REFERENCES dbo.MST_User (UserId),
        ConvertedUtc         datetime2(3)   NULL,
        QuotationId          bigint         NULL,
        TourUtc              datetime2(3)   NULL,
        DealValue            decimal(18,2)  NULL,
        FirstContactUtc      datetime2(3)   NULL,
        ResponseDueUtc       datetime2(3)   NULL,
        OwnerNudgedUtc       datetime2(3)   NULL,
        ManagerEscalatedUtc  datetime2(3)   NULL,
        ClosedUtc            datetime2(3)   NULL,
        CreatedBy            bigint         NULL,
        CreatedDate          datetime2(3)   NOT NULL CONSTRAINT DF_CrmLead_Created DEFAULT (SYSUTCDATETIME()),
        ModifiedBy           bigint         NULL,
        ModifiedDate         datetime2(3)   NULL,
        CONSTRAINT CK_CrmLead_Channel CHECK (Channel BETWEEN 1 AND 5)
    );
    CREATE UNIQUE INDEX UX_CrmLead_Number ON dbo.TRN_CrmLead (LeadNumber);
    CREATE INDEX IX_CrmLead_Mobile ON dbo.TRN_CrmLead (MobileNormalised);
    CREATE INDEX IX_CrmLead_Email ON dbo.TRN_CrmLead (EmailNormalised);
    CREATE INDEX IX_CrmLead_Status_Owner ON dbo.TRN_CrmLead (StatusId, OwnerUserId);
    CREATE INDEX IX_CrmLead_Customer ON dbo.TRN_CrmLead (CustomerId);
    CREATE INDEX IX_CrmLead_Created ON dbo.TRN_CrmLead (CreatedDate);
END;

IF OBJECT_ID(N'dbo.TRN_CrmLeadActivity', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.TRN_CrmLeadActivity
    (
        ActivityId   bigint IDENTITY(1,1) NOT NULL CONSTRAINT PK_TRN_CrmLeadActivity PRIMARY KEY,
        LeadId       bigint         NOT NULL CONSTRAINT FK_CrmActivity_Lead REFERENCES dbo.TRN_CrmLead (LeadId),
        Kind         tinyint        NOT NULL,
        Detail       nvarchar(2000) NOT NULL,
        HappenedUtc  datetime2(3)   NOT NULL,
        ByUserId     bigint         NULL,
        IsSystem     bit            NOT NULL CONSTRAINT DF_CrmActivity_IsSystem DEFAULT (0),
        CreatedBy bigint NULL, CreatedDate datetime2(3) NOT NULL CONSTRAINT DF_CrmActivity_Created DEFAULT (SYSUTCDATETIME()),
        ModifiedBy bigint NULL, ModifiedDate datetime2(3) NULL
    );
    CREATE INDEX IX_CrmLeadActivity_Lead ON dbo.TRN_CrmLeadActivity (LeadId, HappenedUtc);
END;

IF OBJECT_ID(N'dbo.TRN_CrmFollowUp', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.TRN_CrmFollowUp
    (
        FollowUpId    bigint IDENTITY(1,1) NOT NULL CONSTRAINT PK_TRN_CrmFollowUp PRIMARY KEY,
        LeadId        bigint        NOT NULL CONSTRAINT FK_CrmFollowUp_Lead REFERENCES dbo.TRN_CrmLead (LeadId),
        DueUtc        datetime2(3)  NOT NULL,
        WhatToDo      nvarchar(500) NOT NULL,
        Kind          nvarchar(10)  NOT NULL CONSTRAINT DF_CrmFollowUp_Kind DEFAULT (N'call'),
        OwnerUserId   bigint        NULL CONSTRAINT FK_CrmFollowUp_Owner REFERENCES dbo.MST_User (UserId),
        IsDone        bit           NOT NULL CONSTRAINT DF_CrmFollowUp_IsDone DEFAULT (0),
        Outcome       nvarchar(500) NULL,
        DoneUtc       datetime2(3)  NULL,
        DoneByUserId  bigint        NULL,
        RemindedUtc   datetime2(3)  NULL,
        CreatedBy bigint NULL, CreatedDate datetime2(3) NOT NULL CONSTRAINT DF_CrmFollowUp_Created DEFAULT (SYSUTCDATETIME()),
        ModifiedBy bigint NULL, ModifiedDate datetime2(3) NULL
    );
    CREATE INDEX IX_CrmFollowUp_Due ON dbo.TRN_CrmFollowUp (IsDone, DueUtc);
    CREATE INDEX IX_CrmFollowUp_Lead ON dbo.TRN_CrmFollowUp (LeadId);
END;

IF OBJECT_ID(N'dbo.TRN_CrmLeadConsent', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.TRN_CrmLeadConsent
    (
        ConsentId         bigint IDENTITY(1,1) NOT NULL CONSTRAINT PK_TRN_CrmLeadConsent PRIMARY KEY,
        LeadId            bigint         NOT NULL CONSTRAINT FK_CrmConsent_Lead REFERENCES dbo.TRN_CrmLead (LeadId),
        HowObtained       nvarchar(200)  NOT NULL,
        ConsentTextShown  nvarchar(1000) NULL,
        ContactConsent    bit            NOT NULL CONSTRAINT DF_CrmConsent_Contact DEFAULT (0),
        MarketingConsent  bit            NOT NULL CONSTRAINT DF_CrmConsent_Marketing DEFAULT (0),
        ConsentedUtc      datetime2(3)   NULL,
        SourceIpHash      nvarchar(64)   NULL,
        CreatedBy bigint NULL, CreatedDate datetime2(3) NOT NULL CONSTRAINT DF_CrmConsent_Created DEFAULT (SYSUTCDATETIME()),
        ModifiedBy bigint NULL, ModifiedDate datetime2(3) NULL
    );
    CREATE INDEX IX_CrmLeadConsent_Lead ON dbo.TRN_CrmLeadConsent (LeadId);
END;

-- 3. Anonymous website sessions --------------------------------------------------------------------
IF OBJECT_ID(N'dbo.TRN_CrmVisitor', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.TRN_CrmVisitor
    (
        VisitorId        bigint IDENTITY(1,1) NOT NULL CONSTRAINT PK_TRN_CrmVisitor PRIMARY KEY,
        VisitorKey       nvarchar(64)  NOT NULL,
        FirstSeenUtc     datetime2(3)  NOT NULL,
        LastSeenUtc      datetime2(3)  NOT NULL,
        SourceId         int           NULL CONSTRAINT FK_CrmVisitor_Source REFERENCES dbo.MST_CrmSource (SourceId),
        UtmSource        nvarchar(100) NULL,
        UtmMedium        nvarchar(100) NULL,
        UtmCampaign      nvarchar(100) NULL,
        CampaignSeenUtc  datetime2(3)  NULL,
        Referrer         nvarchar(300) NULL,
        LandingPage      nvarchar(300) NULL,
        City             nvarchar(100) NULL,
        Country          nvarchar(100) NULL,
        DeviceType       nvarchar(30)  NULL,
        Browser          nvarchar(50)  NULL,
        VisitCount       int           NOT NULL CONSTRAINT DF_CrmVisitor_Visits DEFAULT (1),
        PageViewCount    int           NOT NULL CONSTRAINT DF_CrmVisitor_Views DEFAULT (0),
        LeadId           bigint        NULL,
        DeleteAfterUtc   datetime2(3)  NULL,
        CreatedBy bigint NULL, CreatedDate datetime2(3) NOT NULL CONSTRAINT DF_CrmVisitor_Created DEFAULT (SYSUTCDATETIME()),
        ModifiedBy bigint NULL, ModifiedDate datetime2(3) NULL
    );
    CREATE UNIQUE INDEX UX_CrmVisitor_Key ON dbo.TRN_CrmVisitor (VisitorKey);
    CREATE INDEX IX_CrmVisitor_FirstSeen ON dbo.TRN_CrmVisitor (FirstSeenUtc);
    CREATE INDEX IX_CrmVisitor_LastSeen ON dbo.TRN_CrmVisitor (LastSeenUtc);
END;

IF OBJECT_ID(N'dbo.TRN_CrmPageView', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.TRN_CrmPageView
    (
        PageViewId     bigint IDENTITY(1,1) NOT NULL CONSTRAINT PK_TRN_CrmPageView PRIMARY KEY,
        VisitorId      bigint        NOT NULL CONSTRAINT FK_CrmPageView_Visitor REFERENCES dbo.TRN_CrmVisitor (VisitorId),
        Path           nvarchar(300) NOT NULL,
        ViewedUtc      datetime2(3)  NOT NULL,
        SecondsOnPage  int           NULL,
        VisitNumber    int           NOT NULL CONSTRAINT DF_CrmPageView_Visit DEFAULT (1)
    );
    CREATE INDEX IX_CrmPageView_Visitor ON dbo.TRN_CrmPageView (VisitorId, ViewedUtc);
    CREATE INDEX IX_CrmPageView_Viewed ON dbo.TRN_CrmPageView (ViewedUtc);
END;

IF OBJECT_ID(N'dbo.TRN_CrmWhatsAppRef', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.TRN_CrmWhatsAppRef
    (
        WhatsAppRefId  bigint IDENTITY(1,1) NOT NULL CONSTRAINT PK_TRN_CrmWhatsAppRef PRIMARY KEY,
        Code           nvarchar(12)  NOT NULL,
        VisitorId      bigint        NOT NULL,
        PagePath       nvarchar(300) NULL,
        CreatedUtc     datetime2(3)  NOT NULL,
        LeadId         bigint        NULL
    );
    CREATE UNIQUE INDEX UX_CrmWhatsAppRef_Code ON dbo.TRN_CrmWhatsAppRef (Code);
END;

IF OBJECT_ID(N'dbo.TRN_CrmNotification', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.TRN_CrmNotification
    (
        NotificationId    bigint IDENTITY(1,1) NOT NULL CONSTRAINT PK_TRN_CrmNotification PRIMARY KEY,
        LeadId            bigint         NULL,
        Kind              nvarchar(40)   NOT NULL,
        RecipientUserId   bigint         NULL,
        RecipientAddress  nvarchar(256)  NULL,
        Channel           nvarchar(10)   NOT NULL,
        Title             nvarchar(200)  NOT NULL,
        Body              nvarchar(2000) NULL,
        CreatedUtc        datetime2(3)   NOT NULL,
        SentUtc           datetime2(3)   NULL,
        Delivered         bit            NOT NULL CONSTRAINT DF_CrmNotification_Delivered DEFAULT (0),
        Error             nvarchar(500)  NULL,
        ReadUtc           datetime2(3)   NULL
    );
    CREATE INDEX IX_CrmNotification_Recipient ON dbo.TRN_CrmNotification (RecipientUserId, ReadUtc);
    CREATE INDEX IX_CrmNotification_Lead ON dbo.TRN_CrmNotification (LeadId);
END;

-- 4. Seed the editable lists (document sections 9.4, 11.1-11.5) -------------------------------------
INSERT INTO dbo.MST_CrmSource (Code, Name, IsWebsite, UtmSource, UtmMedium, ReferrerMatch, SortOrder)
SELECT v.Code, v.Name, v.IsWebsite, v.UtmSource, v.UtmMedium, v.ReferrerMatch, v.SortOrder
FROM (VALUES
    (N'GOOGLE',     N'Google search',            1, N'google',       NULL,    N'google.',            10),
    (N'GOOGLE_ADS', N'Google Ads',               1, N'google',       N'cpc',  NULL,                  15),
    (N'GBP',        N'Google Business listing',  1, N'gbp,gmb',      NULL,    N'business.google.',   20),
    (N'INSTAGRAM',  N'Instagram',                1, N'instagram,ig', NULL,    N'instagram.com',      30),
    (N'JUSTDIAL',   N'JustDial',                 1, N'justdial',     NULL,    N'justdial.com',       40),
    (N'FLYER',      N'Flyer / QR code',          1, N'flyer,qr',     NULL,    NULL,                  45),
    (N'OTHER_WEB',  N'Other website',            1, NULL,            NULL,    NULL,                  50),
    (N'DIRECT',     N'Direct',                   1, NULL,            NULL,    NULL,                  60),
    (N'PHONE',      N'Phone call',               0, NULL,            NULL,    NULL,                 100),
    (N'WALKIN',     N'Walk-in',                  0, NULL,            NULL,    NULL,                 110),
    (N'EMAIL',      N'Email',                    0, NULL,            NULL,    NULL,                 120),
    (N'WHATSAPP',   N'WhatsApp',                 0, NULL,            NULL,    NULL,                 130),
    (N'REFERRAL',   N'Referral',                 0, NULL,            NULL,    NULL,                 140),
    (N'EVENT',      N'Event / expo',             0, NULL,            NULL,    NULL,                 150),
    (N'OTHER',      N'Other',                    0, NULL,            NULL,    NULL,                 160)
) v (Code, Name, IsWebsite, UtmSource, UtmMedium, ReferrerMatch, SortOrder)
WHERE NOT EXISTS (SELECT 1 FROM dbo.MST_CrmSource x WHERE x.Code = v.Code);

INSERT INTO dbo.MST_CrmStatus (Code, Name, Description, IsClosed, IsWon, Requires, Colour, SortOrder)
SELECT v.Code, v.Name, v.Description, v.IsClosed, v.IsWon, v.Requires, v.Colour, v.SortOrder
FROM (VALUES
    (N'NEW',       N'New',            N'Nobody has called yet',     0, 0, 0, N'info',    10),
    (N'CONTACTED', N'Contacted',      N'Spoken to at least once',   0, 0, 1, N'neutral', 20),
    (N'VISIT',     N'Visit booked',   N'Coming to see the space',   0, 0, 2, N'warning', 30),
    (N'QUOTE',     N'Quotation sent', N'A price is with them',      0, 0, 3, N'gold',    40),
    (N'WON',       N'Won',            N'Converted to a customer',   1, 1, 4, N'success', 910),
    (N'LOST',      N'Lost',           N'Closed with a reason',      1, 0, 5, N'danger',  920)
) v (Code, Name, Description, IsClosed, IsWon, Requires, Colour, SortOrder)
WHERE NOT EXISTS (SELECT 1 FROM dbo.MST_CrmStatus x WHERE x.Code = v.Code);

IF NOT EXISTS (SELECT 1 FROM dbo.MST_CrmLostReason)
    INSERT INTO dbo.MST_CrmLostReason (Name, Hint, SortOrder) VALUES
        (N'Price too high',        N'Look at the cabin rate', 10),
        (N'No space of that size', N'Demand you cannot serve yet - the case for the next floor', 20),
        (N'Went somewhere else',   N'Ask where, and record it', 30),
        (N'Never replied',         N'Usually means we replied too slowly - check the response report', 40),
        (N'Wrong location',        N'Where the next branch should be', 50),
        (N'Not a real enquiry',    N'Spam or a wrong number', 60);

IF NOT EXISTS (SELECT 1 FROM dbo.MST_CrmLookingFor)
    INSERT INTO dbo.MST_CrmLookingFor (Name, WorkspaceTypeId, IsLarge, SortOrder)
    SELECT v.Name, (SELECT TOP 1 WorkspaceTypeId FROM dbo.MST_WorkspaceType WHERE TypeCode = v.TypeCode), v.IsLarge, v.SortOrder
    FROM (VALUES
        (N'Cabin',           N'CABIN',             1, 10),
        (N'Hot desk',        N'DESK',              0, 20),
        (N'Dedicated desk',  N'DESK',              0, 30),
        (N'Meeting room',    N'MEETING_ROOM',      0, 40),
        (N'Virtual office',  N'VIRTUAL_WORKSPACE', 0, 50),
        (N'Day pass',        N'DESK',              0, 60),
        (N'Event space',     NULL,                 0, 70),
        (N'Not sure yet',    NULL,                 0, 80)
    ) v (Name, TypeCode, IsLarge, SortOrder);

IF NOT EXISTS (SELECT 1 FROM dbo.MST_CrmAssignmentRule)
    INSERT INTO dbo.MST_CrmAssignmentRule (Label, ConditionType, MinPeople, TargetType, SortOrder) VALUES
        (N'They are an existing customer - the person who already looks after them', 1, NULL, 1, 10),
        (N'They asked for a specific location - whoever is on duty at that location', 2, NULL, 3, 20),
        (N'It is a cabin, or more than 5 desks - the manager', 3, 5, 2, 30),
        (N'Anything else - round robin among whoever is on duty', 4, NULL, 4, 40),
        (N'Nobody on duty - Unassigned, and the manager is told at once', 5, NULL, 5, 50);

INSERT INTO dbo.MST_CrmSetting (SettingKey, SettingValue)
SELECT v.k, v.v
FROM (VALUES
    (N'target.web', N'30'), (N'target.whatsapp', N'30'), (N'target.callback', N'15'), (N'target.email', N'240'), (N'target.escalate', N'60'),
    (N'hours.start', N'09:00'), (N'hours.end', N'19:00'), (N'hours.days', N'1,2,3,4,5,6'),
    (N'consent.text', N'I agree Regalis WorkSpace may contact me about workspace availability. We will not pass your details to anyone else.'),
    (N'consent.marketing', N'Also send me occasional news and offers from Regalis WorkSpace. (Optional)'),
    (N'retention.visitDays', N'90'), (N'retention.lostMonths', N'24'),
    -- Email stays OFF until a test email from production has been seen to arrive (section 8.5).
    (N'email.enabled', N'0'), (N'email.ack', N'0'), (N'email.digest', N'0')
) v (k, v)
WHERE NOT EXISTS (SELECT 1 FROM dbo.MST_CrmSetting x WHERE x.SettingKey = v.k);

IF NOT EXISTS (SELECT 1 FROM dbo.MST_NumberSequence WHERE SequenceKey = N'CrmLead')
    INSERT INTO dbo.MST_NumberSequence (SequenceKey, Prefix, CurrentValue, FinancialYear) VALUES (N'CrmLead', N'L-', 0, NULL);

-- 5. Permissions and the Sales / Front desk role (section 15) -------------------------------------
DECLARE @perm TABLE (Code nvarchar(100), ActionName nvarchar(50), Description nvarchar(200), DisplayOrder int);
INSERT INTO @perm VALUES
    (N'CRM.VIEW',          N'VIEW',          N'Open the CRM pages at all', 1),
    (N'CRM.LEAD.CREATE',   N'LEAD.CREATE',   N'Add a lead by hand', 2),
    (N'CRM.LEAD.EDIT',     N'LEAD.EDIT',     N'Change a lead''s details, log activity, set follow-ups', 3),
    (N'CRM.LEAD.VIEWALL',  N'LEAD.VIEWALL',  N'See every lead, not only their own and the unassigned ones', 4),
    (N'CRM.LEAD.ASSIGN',   N'LEAD.ASSIGN',   N'Change who owns a lead', 5),
    (N'CRM.LEAD.CONVERT',  N'LEAD.CONVERT',  N'Convert a lead to a customer', 6),
    (N'CRM.LEAD.DELETE',   N'LEAD.DELETE',   N'Delete a lead on request (the only destructive action in CRM)', 7),
    (N'CRM.VISITORS.VIEW', N'VISITORS.VIEW', N'See the anonymous website session list', 8),
    (N'CRM.REPORTS.VIEW',  N'REPORTS.VIEW',  N'See the CRM reports', 9),
    (N'CRM.SETTINGS',      N'SETTINGS',      N'Change sources, statuses, rules, targets and consent text', 10);

INSERT INTO dbo.MST_Permission (PermissionCode, ModuleName, ActionName, Description, DisplayOrder, CreatedDate, IsActive, IsDeleted)
SELECT p.Code, N'CRM', p.ActionName, p.Description, p.DisplayOrder, @now, 1, 0
FROM @perm p
WHERE NOT EXISTS (SELECT 1 FROM dbo.MST_Permission x WHERE x.PermissionCode = p.Code);

INSERT INTO dbo.MST_Role (RoleCode, RoleName, Description, IsSystemRole, PortalType, CreatedDate, IsActive, IsDeleted)
SELECT N'SALES', N'Sales / Front desk', N'Adds and works leads: sees their own leads plus anything unassigned. Cannot delete leads, change owners or convert.', 0, 2, @now, 1, 0
WHERE NOT EXISTS (SELECT 1 FROM dbo.MST_Role WHERE RoleCode = N'SALES');

DECLARE @grant TABLE (RoleCode nvarchar(50), Code nvarchar(100));
-- Owner (Administrator): everything.
INSERT INTO @grant SELECT N'ADMIN', Code FROM @perm;
-- Manager: everything except CRM Settings.
INSERT INTO @grant SELECT N'MANAGER', Code FROM @perm WHERE Code <> N'CRM.SETTINGS';
-- Sales / front desk: view, add, work their own and unassigned leads; and the dashboard (its + Add lead button).
INSERT INTO @grant VALUES (N'SALES', N'CRM.VIEW'), (N'SALES', N'CRM.LEAD.CREATE'), (N'SALES', N'CRM.LEAD.EDIT'), (N'SALES', N'DASHBOARD.VIEW');
-- Finance, Accountant, Auditor, Support, Housekeeping: nothing (CRM holds personal data of people who are not customers).

INSERT INTO dbo.MAP_RolePermission (RoleId, PermissionId)
SELECT DISTINCT r.RoleId, p.PermissionId
FROM @grant g
JOIN dbo.MST_Role r ON r.RoleCode = g.RoleCode
JOIN dbo.MST_Permission p ON p.PermissionCode = g.Code
WHERE NOT EXISTS (SELECT 1 FROM dbo.MAP_RolePermission x WHERE x.RoleId = r.RoleId AND x.PermissionId = p.PermissionId);

-- 6. Sidebar (section 2): SALES band at 250, between OPERATIONS (200) and FINANCE (300) ---------
INSERT INTO dbo.MST_Menu (ParentMenuId, MenuCode, MenuName, DisplayName, Route, Icon, DisplayOrder, PermissionId, PortalType, MenuLevel, IsVisible, CreatedDate, IsActive, IsDeleted)
SELECT NULL, N'ADM_SEC_SALES', N'Sales', N'Sales', NULL, NULL, 250, NULL, 2, 1, 1, @now, 1, 0
WHERE NOT EXISTS (SELECT 1 FROM dbo.MST_Menu WHERE MenuCode = N'ADM_SEC_SALES' AND IsDeleted = 0);

DECLARE @sales int = (SELECT MenuId FROM dbo.MST_Menu WHERE MenuCode = N'ADM_SEC_SALES' AND IsDeleted = 0);

INSERT INTO dbo.MST_Menu (ParentMenuId, MenuCode, MenuName, DisplayName, Route, Icon, DisplayOrder, PermissionId, PortalType, MenuLevel, IsVisible, CreatedDate, IsActive, IsDeleted)
SELECT @sales, N'ADM_CRM', N'CRM', N'CRM', N'/admin/crm/leads', N'Target', 10,
       (SELECT PermissionId FROM dbo.MST_Permission WHERE PermissionCode = N'CRM.VIEW'), 2, 1, 1, @now, 1, 0
WHERE NOT EXISTS (SELECT 1 FROM dbo.MST_Menu WHERE MenuCode = N'ADM_CRM' AND IsDeleted = 0);

DECLARE @crm int = (SELECT MenuId FROM dbo.MST_Menu WHERE MenuCode = N'ADM_CRM' AND IsDeleted = 0);
DECLARE @admin int = (SELECT MenuId FROM dbo.MST_Menu WHERE MenuCode = N'ADM_ADMINISTRATION' AND IsDeleted = 0);
IF @crm IS NULL OR @admin IS NULL
BEGIN
    RAISERROR('CRM or Administration menu row not found - nothing changed.', 16, 1);
    ROLLBACK TRANSACTION;
    RETURN;
END;

DECLARE @menu TABLE (Code nvarchar(100), Parent int, Display nvarchar(100), Route nvarchar(200), Icon nvarchar(50), Perm nvarchar(100), DisplayOrder int);
INSERT INTO @menu VALUES
    (N'ADM_CRM_LEADS',     @crm,   N'Leads',            N'/admin/crm/leads',                   N'UserPlus',      N'CRM.VIEW',          10),
    (N'ADM_CRM_FOLLOWUPS', @crm,   N'Follow-ups',       N'/admin/crm/follow-ups',              N'CalendarCheck', N'CRM.VIEW',          20),
    (N'ADM_CRM_VISITORS',  @crm,   N'Website Visitors', N'/admin/crm/visitors',                N'Globe',         N'CRM.VISITORS.VIEW', 30),
    (N'ADM_CRM_REPORTS',   @crm,   N'CRM Reports',      N'/admin/crm/reports',                 N'BarChart3',     N'CRM.REPORTS.VIEW',  40),
    (N'ADM_CRM_SETTINGS',  @admin, N'CRM Settings',     N'/admin/administration/crm-settings', N'Settings',      N'CRM.SETTINGS',      495);

INSERT INTO dbo.MST_Menu (ParentMenuId, MenuCode, MenuName, DisplayName, Route, Icon, DisplayOrder, PermissionId, PortalType, MenuLevel, IsVisible, CreatedDate, IsActive, IsDeleted)
SELECT m.Parent, m.Code, m.Display, m.Display, m.Route, m.Icon, m.DisplayOrder,
       (SELECT PermissionId FROM dbo.MST_Permission WHERE PermissionCode = m.Perm), 2, 2, 1, @now, 1, 0
FROM @menu m
WHERE NOT EXISTS (SELECT 1 FROM dbo.MST_Menu x WHERE x.MenuCode = m.Code AND x.IsDeleted = 0);

-- Who sees which row: each page row goes to every role holding its permission; the band and the
-- CRM parent go to every role that holds CRM.VIEW.
INSERT INTO dbo.MAP_RoleMenu (RoleId, MenuId)
SELECT DISTINCT rp.RoleId, m.MenuId
FROM dbo.MST_Menu m
JOIN dbo.MAP_RolePermission rp ON rp.PermissionId = m.PermissionId
JOIN dbo.MST_Role r ON r.RoleId = rp.RoleId AND r.RoleCode <> N'CUSTOMER'
WHERE m.MenuCode IN (N'ADM_CRM', N'ADM_CRM_LEADS', N'ADM_CRM_FOLLOWUPS', N'ADM_CRM_VISITORS', N'ADM_CRM_REPORTS', N'ADM_CRM_SETTINGS') AND m.IsDeleted = 0
  AND NOT EXISTS (SELECT 1 FROM dbo.MAP_RoleMenu x WHERE x.RoleId = rp.RoleId AND x.MenuId = m.MenuId);

INSERT INTO dbo.MAP_RoleMenu (RoleId, MenuId)
SELECT DISTINCT rp.RoleId, @sales
FROM dbo.MAP_RolePermission rp
JOIN dbo.MST_Permission p ON p.PermissionId = rp.PermissionId AND p.PermissionCode = N'CRM.VIEW'
JOIN dbo.MST_Role r ON r.RoleId = rp.RoleId AND r.RoleCode <> N'CUSTOMER'
WHERE NOT EXISTS (SELECT 1 FROM dbo.MAP_RoleMenu x WHERE x.RoleId = rp.RoleId AND x.MenuId = @sales);

-- The Sales role also gets Overview > Dashboard and System > Admin Profile, like every staff role.
INSERT INTO dbo.MAP_RoleMenu (RoleId, MenuId)
SELECT r.RoleId, m.MenuId
FROM dbo.MST_Role r
JOIN dbo.MST_Menu m ON m.MenuCode IN (N'ADM_SEC_OVERVIEW', N'ADM_DASHBOARD', N'ADM_SEC_SYSTEM', N'ADM_PROFILE') AND m.IsDeleted = 0
WHERE r.RoleCode = N'SALES'
  AND NOT EXISTS (SELECT 1 FROM dbo.MAP_RoleMenu x WHERE x.RoleId = r.RoleId AND x.MenuId = m.MenuId);

COMMIT TRANSACTION;
PRINT 'CRM migration applied.';
