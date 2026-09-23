BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260922191326_AddQuotationBillingFields'
)
BEGIN
    ALTER TABLE [TRN_QuotationLine] ADD [CgstAmount] decimal(18,2) NOT NULL DEFAULT 0.0;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260922191326_AddQuotationBillingFields'
)
BEGIN
    ALTER TABLE [TRN_QuotationLine] ADD [DiscountShare] decimal(18,2) NOT NULL DEFAULT 0.0;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260922191326_AddQuotationBillingFields'
)
BEGIN
    ALTER TABLE [TRN_QuotationLine] ADD [EndUtc] datetime2(3) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260922191326_AddQuotationBillingFields'
)
BEGIN
    ALTER TABLE [TRN_QuotationLine] ADD [IgstAmount] decimal(18,2) NOT NULL DEFAULT 0.0;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260922191326_AddQuotationBillingFields'
)
BEGIN
    ALTER TABLE [TRN_QuotationLine] ADD [LineNotes] nvarchar(500) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260922191326_AddQuotationBillingFields'
)
BEGIN
    ALTER TABLE [TRN_QuotationLine] ADD [LineType] tinyint NOT NULL DEFAULT CAST(0 AS tinyint);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260922191326_AddQuotationBillingFields'
)
BEGIN
    ALTER TABLE [TRN_QuotationLine] ADD [PassPlanId] int NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260922191326_AddQuotationBillingFields'
)
BEGIN
    ALTER TABLE [TRN_QuotationLine] ADD [ServiceType] tinyint NOT NULL DEFAULT CAST(0 AS tinyint);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260922191326_AddQuotationBillingFields'
)
BEGIN
    ALTER TABLE [TRN_QuotationLine] ADD [SgstAmount] decimal(18,2) NOT NULL DEFAULT 0.0;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260922191326_AddQuotationBillingFields'
)
BEGIN
    ALTER TABLE [TRN_QuotationLine] ADD [StandardRate] decimal(14,2) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260922191326_AddQuotationBillingFields'
)
BEGIN
    ALTER TABLE [TRN_QuotationLine] ADD [StartUtc] datetime2(3) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260922191326_AddQuotationBillingFields'
)
BEGIN
    ALTER TABLE [TRN_QuotationLine] ADD [TaxableAmount] decimal(18,2) NOT NULL DEFAULT 0.0;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260922191326_AddQuotationBillingFields'
)
BEGIN
    ALTER TABLE [TRN_QuotationLine] ADD [WorkspaceId] int NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260922191326_AddQuotationBillingFields'
)
BEGIN
    ALTER TABLE [TRN_Quotation] ADD [AdjustmentAmount] decimal(18,2) NOT NULL DEFAULT 0.0;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260922191326_AddQuotationBillingFields'
)
BEGIN
    ALTER TABLE [TRN_Quotation] ADD [AdjustmentLabel] nvarchar(50) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260922191326_AddQuotationBillingFields'
)
BEGIN
    ALTER TABLE [TRN_Quotation] ADD [AdvanceInvoiceEnabled] bit NOT NULL DEFAULT CAST(0 AS bit);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260922191326_AddQuotationBillingFields'
)
BEGIN
    ALTER TABLE [TRN_Quotation] ADD [AdvanceType] tinyint NOT NULL DEFAULT CAST(0 AS tinyint);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260922191326_AddQuotationBillingFields'
)
BEGIN
    ALTER TABLE [TRN_Quotation] ADD [AdvanceValue] decimal(14,2) NOT NULL DEFAULT 0.0;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260922191326_AddQuotationBillingFields'
)
BEGIN
    ALTER TABLE [TRN_Quotation] ADD [CustomerCompanyName] nvarchar(200) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260922191326_AddQuotationBillingFields'
)
BEGIN
    ALTER TABLE [TRN_Quotation] ADD [DecisionConfirmedBy] nvarchar(150) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260922191326_AddQuotationBillingFields'
)
BEGIN
    ALTER TABLE [TRN_Quotation] ADD [DecisionIpAddress] nvarchar(45) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260922191326_AddQuotationBillingFields'
)
BEGIN
    ALTER TABLE [TRN_Quotation] ADD [DecisionMode] tinyint NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260922191326_AddQuotationBillingFields'
)
BEGIN
    ALTER TABLE [TRN_Quotation] ADD [DecisionNote] nvarchar(500) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260922191326_AddQuotationBillingFields'
)
BEGIN
    ALTER TABLE [TRN_Quotation] ADD [DecisionOnBehalf] bit NOT NULL DEFAULT CAST(0 AS bit);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260922191326_AddQuotationBillingFields'
)
BEGIN
    ALTER TABLE [TRN_Quotation] ADD [DiscountType] tinyint NOT NULL DEFAULT CAST(0 AS tinyint);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260922191326_AddQuotationBillingFields'
)
BEGIN
    ALTER TABLE [TRN_Quotation] ADD [DiscountValue] decimal(14,2) NOT NULL DEFAULT 0.0;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260922191326_AddQuotationBillingFields'
)
BEGIN
    ALTER TABLE [TRN_Quotation] ADD [InternalNote] nvarchar(1000) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260922191326_AddQuotationBillingFields'
)
BEGIN
    ALTER TABLE [TRN_Quotation] ADD [PlaceOfSupplyStateCode] nvarchar(2) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260922191326_AddQuotationBillingFields'
)
BEGIN
    ALTER TABLE [TRN_Quotation] ADD [ReferenceNumber] nvarchar(50) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260922191326_AddQuotationBillingFields'
)
BEGIN
    ALTER TABLE [TRN_Quotation] ADD [RejectionCode] nvarchar(40) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260922191326_AddQuotationBillingFields'
)
BEGIN
    ALTER TABLE [TRN_Quotation] ADD [RevisionNumber] int NOT NULL DEFAULT 0;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260922191326_AddQuotationBillingFields'
)
BEGIN
    ALTER TABLE [TRN_Quotation] ADD [RevisionOfQuotationId] bigint NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260922191326_AddQuotationBillingFields'
)
BEGIN
    ALTER TABLE [TRN_Quotation] ADD [RoundOffAmount] decimal(18,2) NOT NULL DEFAULT 0.0;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260922191326_AddQuotationBillingFields'
)
BEGIN
    ALTER TABLE [TRN_Quotation] ADD [SalespersonId] bigint NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260922191326_AddQuotationBillingFields'
)
BEGIN
    ALTER TABLE [TRN_Quotation] ADD [Subject] nvarchar(250) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260922191326_AddQuotationBillingFields'
)
BEGIN
    ALTER TABLE [TRN_Quotation] ADD [TaxMode] tinyint NOT NULL DEFAULT CAST(0 AS tinyint);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260922191326_AddQuotationBillingFields'
)
BEGIN
    ALTER TABLE [TRN_Quotation] ADD [TdsAmount] decimal(18,2) NOT NULL DEFAULT 0.0;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260922191326_AddQuotationBillingFields'
)
BEGIN
    ALTER TABLE [TRN_Quotation] ADD [TdsCode] nvarchar(20) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260922191326_AddQuotationBillingFields'
)
BEGIN
    ALTER TABLE [TRN_Quotation] ADD [TdsLabel] nvarchar(80) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260922191326_AddQuotationBillingFields'
)
BEGIN
    ALTER TABLE [TRN_Quotation] ADD [TdsPercent] decimal(5,2) NOT NULL DEFAULT 0.0;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260922191326_AddQuotationBillingFields'
)
BEGIN
    ALTER TABLE [TRN_Invoice] ADD [AdjustmentAmount] decimal(18,2) NOT NULL DEFAULT 0.0;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260922191326_AddQuotationBillingFields'
)
BEGIN
    ALTER TABLE [TRN_Invoice] ADD [AdjustmentLabel] nvarchar(50) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260922191326_AddQuotationBillingFields'
)
BEGIN
    ALTER TABLE [TRN_Invoice] ADD [RoundOffAmount] decimal(18,2) NOT NULL DEFAULT 0.0;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260922191326_AddQuotationBillingFields'
)
BEGIN
    ALTER TABLE [TRN_Invoice] ADD [TdsAmount] decimal(18,2) NOT NULL DEFAULT 0.0;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260922191326_AddQuotationBillingFields'
)
BEGIN
    ALTER TABLE [TRN_Invoice] ADD [TdsLabel] nvarchar(80) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260922191326_AddQuotationBillingFields'
)
BEGIN
    ALTER TABLE [TRN_Invoice] ADD [TdsPercent] decimal(5,2) NOT NULL DEFAULT 0.0;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260922191326_AddQuotationBillingFields'
)
BEGIN
    ALTER TABLE [MST_Workspace] ADD [PricePerMonth] decimal(18,2) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260922191326_AddQuotationBillingFields'
)
BEGIN
    CREATE TABLE [TRN_QuotationAttachment] (
        [QuotationAttachmentId] bigint NOT NULL IDENTITY,
        [QuotationId] bigint NOT NULL,
        [FileName] nvarchar(260) NOT NULL,
        [StoredFileName] nvarchar(100) NOT NULL,
        [FilePath] nvarchar(500) NOT NULL,
        [ContentType] nvarchar(150) NOT NULL,
        [FileSizeBytes] bigint NOT NULL,
        [SendWithEmail] bit NOT NULL,
        [UploadedByUserId] bigint NOT NULL,
        [UploadedUtc] datetime2(3) NOT NULL,
        CONSTRAINT [PK_TRN_QuotationAttachment] PRIMARY KEY ([QuotationAttachmentId]),
        CONSTRAINT [FK_TRN_QuotationAttachment_TRN_Quotation_QuotationId] FOREIGN KEY ([QuotationId]) REFERENCES [TRN_Quotation] ([QuotationId]) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260922191326_AddQuotationBillingFields'
)
BEGIN
    CREATE TABLE [TRN_QuotationEvent] (
        [QuotationEventId] bigint NOT NULL IDENTITY,
        [QuotationId] bigint NOT NULL,
        [EventType] tinyint NOT NULL,
        [ActorType] tinyint NOT NULL,
        [ActorUserId] bigint NULL,
        [Summary] nvarchar(400) NOT NULL,
        [Channel] nvarchar(20) NULL,
        [Destination] nvarchar(256) NULL,
        [DeliveryStatus] tinyint NULL,
        [FailureReason] nvarchar(500) NULL,
        [CreatedUtc] datetime2(3) NOT NULL,
        CONSTRAINT [PK_TRN_QuotationEvent] PRIMARY KEY ([QuotationEventId]),
        CONSTRAINT [FK_TRN_QuotationEvent_TRN_Quotation_QuotationId] FOREIGN KEY ([QuotationId]) REFERENCES [TRN_Quotation] ([QuotationId]) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260922191326_AddQuotationBillingFields'
)
BEGIN
    EXEC(N'CREATE INDEX [IX_Quotation_RevisionOf] ON [TRN_Quotation] ([RevisionOfQuotationId]) WHERE [RevisionOfQuotationId] IS NOT NULL');
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260922191326_AddQuotationBillingFields'
)
BEGIN
    CREATE INDEX [IX_QuotationAttachment_Quotation] ON [TRN_QuotationAttachment] ([QuotationId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260922191326_AddQuotationBillingFields'
)
BEGIN
    CREATE INDEX [IX_QuotationEvent_Quotation_Created] ON [TRN_QuotationEvent] ([QuotationId], [CreatedUtc]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260922191326_AddQuotationBillingFields'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260922191326_AddQuotationBillingFields', N'8.0.19');
END;
GO

COMMIT;
GO


-- ---------------------------------------------------------------------------
-- MST_AppSetting rows for the quotation feature (document SS2.3, SS3.2-SS3.5).
--
-- These are part of the migration rather than the startup seeder because UAT runs
-- with SeedOnStartup=false; the seeder also carries the same rows so a fresh clone
-- still comes up configured. Inserted only when missing, so re-running the script
-- never overwrites a value an admin has since edited.
-- ---------------------------------------------------------------------------
MERGE dbo.MST_AppSetting AS target
USING (VALUES
    ('Quotation.DefaultGstPercent', '18', 'Quotation', 'decimal', 'Default GST rate on a quotation line'),
    ('Quotation.GstRateOptions', '0,5,12,18,28', 'Quotation', 'string', 'GST rates the line dropdown offers'),
    ('Quotation.HomeStateCode', '29', 'Quotation', 'string', 'GST state Regalis supplies from; a match is intra-state (CGST+SGST)'),
    ('Quotation.RoundOffEnabled', 'true', 'Quotation', 'bool', 'Round the quotation total to the nearest rupee'),
    ('Quotation.CustomerNotes', 'Thank you for considering Regalis WorkSpace.', 'Quotation', 'string', 'Default customer notes printed on a quotation'),
    ('Quotation.TdsOptions', '194I|194-I Rent 10%|10;194J|194-J Professional 10%|10', 'Quotation', 'string', 'TDS sections offered, as code|label|percent rows'),
    ('Quotation.EmailSubjectTemplate', 'Quotation {quote_number} from Regalis WorkSpace', 'Quotation', 'string', 'Subject of the quotation email'),
    ('Quotation.EmailBodyTemplate', '', 'Quotation', 'string', 'Body of the quotation email; {customer_name} {quote_number} {total} {valid_until} {portal_link}'),
    ('Quotation.WhatsAppTemplate', '', 'Quotation', 'string', 'WhatsApp message for a quotation; same placeholders as the email'),
    ('Quotation.PortalBaseUrl', '', 'Quotation', 'string', 'Base URL the customer portal approval link is built on'),
    ('Quotation.ExpiryReminderDays', '2', 'Quotation', 'int', 'Days before expiry the reminder goes out'),
    ('Quotation.AdvanceMonths', '1', 'Quotation', 'decimal', 'Default months of rent for the advance (security deposit) invoice')
) AS source (SettingKey, SettingValue, SettingGroup, DataType, Description)
ON target.SettingKey = source.SettingKey
WHEN NOT MATCHED BY TARGET THEN
    INSERT (SettingKey, SettingValue, SettingGroup, DataType, Description,
            IsEditableByAdmin, IsSecret, CreatedDate, IsActive, IsDeleted)
    VALUES (source.SettingKey, source.SettingValue, source.SettingGroup, source.DataType,
            source.Description, 1, 0, SYSUTCDATETIME(), 1, 0);
GO

-- Verification: 12 new rows plus the 2 that already existed.
SELECT SettingKey, SettingValue FROM dbo.MST_AppSetting
 WHERE SettingGroup = 'Quotation' ORDER BY SettingKey;
GO

-- ---------------------------------------------------------------------------
-- Backfill the new byte-enum columns on rows that existed before this migration.
--
-- EF gives a new non-nullable byte column a default of 0, which is not a member
-- of any of these enums. Left alone, an old quotation reads as tax mode 0 and the
-- screen shows "IGST @ 0%" in place of the CGST and SGST it actually charged.
-- The totals were always right; only their labelling was wrong.
-- ---------------------------------------------------------------------------
UPDATE dbo.TRN_Quotation
   SET TaxMode = CASE WHEN IgstAmount > 0 THEN 2 ELSE 1 END
 WHERE TaxMode = 0;

UPDATE dbo.TRN_Quotation SET DiscountType = 1 WHERE DiscountType = 0;
UPDATE dbo.TRN_Quotation SET AdvanceType = 1 WHERE AdvanceType = 0;

-- Older lines predate the line/service classification: they are ordinary service
-- lines of no particular type, which is exactly Service + Other.
UPDATE dbo.TRN_QuotationLine SET LineType = 1 WHERE LineType = 0;
UPDATE dbo.TRN_QuotationLine SET ServiceType = 5 WHERE ServiceType = 0;

-- Per-line tax figures the old engine only kept in aggregate. Splitting the line's
-- own TaxAmount keeps CGST + SGST equal to the tax that was actually charged.
UPDATE dbo.TRN_QuotationLine
   SET TaxableAmount = LineAmount
 WHERE TaxableAmount = 0 AND LineAmount <> 0;

UPDATE l
   SET l.CgstAmount = CASE WHEN q.TaxMode = 1 THEN ROUND(l.TaxAmount / 2.0, 2) ELSE 0 END,
       l.SgstAmount = CASE WHEN q.TaxMode = 1 THEN l.TaxAmount - ROUND(l.TaxAmount / 2.0, 2) ELSE 0 END,
       l.IgstAmount = CASE WHEN q.TaxMode = 2 THEN l.TaxAmount ELSE 0 END
  FROM dbo.TRN_QuotationLine l
  JOIN dbo.TRN_Quotation q ON q.QuotationId = l.QuotationId
 WHERE l.CgstAmount = 0 AND l.SgstAmount = 0 AND l.IgstAmount = 0 AND l.TaxAmount <> 0;
GO

-- Verification: no row should be left on an out-of-range enum value.
SELECT 'Quotations with TaxMode 0'  AS Check_, COUNT(*) AS Rows_ FROM dbo.TRN_Quotation WHERE TaxMode = 0
UNION ALL SELECT 'Lines with LineType 0', COUNT(*) FROM dbo.TRN_QuotationLine WHERE LineType = 0;
GO
