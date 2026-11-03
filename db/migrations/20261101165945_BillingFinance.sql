BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101165945_BillingFinance'
)
BEGIN
    CREATE TABLE [MST_BankColumnMapping] (
        [BankAccountId] int NOT NULL,
        [MappingJson] nvarchar(2000) NOT NULL,
        [DateFormat] nvarchar(20) NOT NULL,
        [CreatedBy] bigint NULL,
        [CreatedDate] datetime2(3) NOT NULL,
        [ModifiedBy] bigint NULL,
        [ModifiedDate] datetime2(3) NULL,
        [IsActive] bit NOT NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedBy] bigint NULL,
        [DeletedDate] datetime2(3) NULL,
        CONSTRAINT [PK_MST_BankColumnMapping] PRIMARY KEY ([BankAccountId]),
        CONSTRAINT [FK_MST_BankColumnMapping_MST_BankAccount_BankAccountId] FOREIGN KEY ([BankAccountId]) REFERENCES [MST_BankAccount] ([BankAccountId]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101165945_BillingFinance'
)
BEGIN
    CREATE TABLE [MST_ComplianceObligation] (
        [ComplianceObligationId] int NOT NULL IDENTITY,
        [Code] nvarchar(30) NOT NULL,
        [Name] nvarchar(150) NOT NULL,
        [Frequency] nvarchar(20) NOT NULL,
        [DueDay] int NOT NULL,
        [DueMonths] nvarchar(40) NULL,
        [AmountSource] nvarchar(100) NULL,
        [OwnerRole] nvarchar(50) NULL,
        [DisplayOrder] int NOT NULL,
        [CreatedBy] bigint NULL,
        [CreatedDate] datetime2(3) NOT NULL,
        [ModifiedBy] bigint NULL,
        [ModifiedDate] datetime2(3) NULL,
        [IsActive] bit NOT NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedBy] bigint NULL,
        [DeletedDate] datetime2(3) NULL,
        CONSTRAINT [PK_MST_ComplianceObligation] PRIMARY KEY ([ComplianceObligationId])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101165945_BillingFinance'
)
BEGIN
    CREATE TABLE [MST_CustomerTaxProfile] (
        [CustomerId] bigint NOT NULL,
        [Tan] nvarchar(10) NULL,
        [DoNotChase] bit NOT NULL,
        [DoNotChaseReason] nvarchar(300) NULL,
        [CreatedBy] bigint NULL,
        [CreatedDate] datetime2(3) NOT NULL,
        [ModifiedBy] bigint NULL,
        [ModifiedDate] datetime2(3) NULL,
        [IsActive] bit NOT NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedBy] bigint NULL,
        [DeletedDate] datetime2(3) NULL,
        CONSTRAINT [PK_MST_CustomerTaxProfile] PRIMARY KEY ([CustomerId]),
        CONSTRAINT [CK_CustomerTaxProfile_Tan] CHECK ([Tan] IS NULL OR [Tan] LIKE '[A-Z][A-Z][A-Z][A-Z][0-9][0-9][0-9][0-9][0-9][A-Z]'),
        CONSTRAINT [FK_MST_CustomerTaxProfile_MST_User_CustomerId] FOREIGN KEY ([CustomerId]) REFERENCES [MST_User] ([UserId]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101165945_BillingFinance'
)
BEGIN
    CREATE TABLE [MST_Department] (
        [DepartmentId] int NOT NULL IDENTITY,
        [Name] nvarchar(60) NOT NULL,
        [DisplayOrder] int NOT NULL,
        [CreatedBy] bigint NULL,
        [CreatedDate] datetime2(3) NOT NULL,
        [ModifiedBy] bigint NULL,
        [ModifiedDate] datetime2(3) NULL,
        [IsActive] bit NOT NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedBy] bigint NULL,
        [DeletedDate] datetime2(3) NULL,
        CONSTRAINT [PK_MST_Department] PRIMARY KEY ([DepartmentId])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101165945_BillingFinance'
)
BEGIN
    CREATE TABLE [MST_DifferenceReason] (
        [DifferenceReasonId] tinyint NOT NULL,
        [Code] nvarchar(40) NOT NULL,
        [Label] nvarchar(100) NOT NULL,
        [Help] nvarchar(300) NULL,
        [RequiredPermission] nvarchar(100) NULL,
        [IsActive] bit NOT NULL,
        [DisplayOrder] int NOT NULL,
        CONSTRAINT [PK_MST_DifferenceReason] PRIMARY KEY ([DifferenceReasonId])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101165945_BillingFinance'
)
BEGIN
    CREATE TABLE [MST_GstRate] (
        [GstRateId] int NOT NULL IDENTITY,
        [Rate] decimal(5,2) NOT NULL,
        [Label] nvarchar(50) NOT NULL,
        [EffectiveFrom] date NOT NULL,
        [EffectiveTo] date NULL,
        [IsActive] bit NOT NULL,
        CONSTRAINT [PK_MST_GstRate] PRIMARY KEY ([GstRateId])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101165945_BillingFinance'
)
BEGIN
    CREATE TABLE [MST_SupplierTaxProfile] (
        [SupplierId] int NOT NULL,
        [Pan] nvarchar(10) NULL,
        [DefaultTdsSection] nvarchar(10) NULL,
        [PaymentTermsDays] int NULL,
        [StateCode] nvarchar(2) NULL,
        [IsIndividual] bit NOT NULL,
        [CreatedBy] bigint NULL,
        [CreatedDate] datetime2(3) NOT NULL,
        [ModifiedBy] bigint NULL,
        [ModifiedDate] datetime2(3) NULL,
        [IsActive] bit NOT NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedBy] bigint NULL,
        [DeletedDate] datetime2(3) NULL,
        CONSTRAINT [PK_MST_SupplierTaxProfile] PRIMARY KEY ([SupplierId]),
        CONSTRAINT [CK_SupplierTaxProfile_Pan] CHECK ([Pan] IS NULL OR [Pan] LIKE '[A-Z][A-Z][A-Z][A-Z][A-Z][0-9][0-9][0-9][0-9][A-Z]'),
        CONSTRAINT [FK_MST_SupplierTaxProfile_MST_Supplier_SupplierId] FOREIGN KEY ([SupplierId]) REFERENCES [MST_Supplier] ([SupplierId]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101165945_BillingFinance'
)
BEGIN
    CREATE TABLE [MST_TdsSection] (
        [TdsSectionId] int NOT NULL IDENTITY,
        [Section] nvarchar(10) NOT NULL,
        [Description] nvarchar(200) NOT NULL,
        [Rate] decimal(5,2) NOT NULL,
        [IndividualRate] decimal(5,2) NULL,
        [NoPanRate] decimal(5,2) NOT NULL,
        [EffectiveFrom] date NOT NULL,
        [EffectiveTo] date NULL,
        [IsActive] bit NOT NULL,
        CONSTRAINT [PK_MST_TdsSection] PRIMARY KEY ([TdsSectionId])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101165945_BillingFinance'
)
BEGIN
    CREATE TABLE [TRN_BankImportBatch] (
        [BankImportBatchId] bigint NOT NULL IDENTITY,
        [BatchCode] nvarchar(50) NOT NULL,
        [BankAccountId] int NOT NULL,
        [FileName] nvarchar(260) NOT NULL,
        [RowCount] int NOT NULL,
        [ImportedCount] int NOT NULL,
        [SkippedCount] int NOT NULL,
        [ErrorCount] int NOT NULL,
        [FirstDate] date NULL,
        [LastDate] date NULL,
        [FileClosingBalance] decimal(18,2) NULL,
        [ColumnMappingJson] nvarchar(2000) NOT NULL,
        [ImportedByUserId] bigint NOT NULL,
        [ImportedUtc] datetime2(3) NOT NULL,
        [IsUndone] bit NOT NULL,
        [UndoneByUserId] bigint NULL,
        [UndoneUtc] datetime2(3) NULL,
        [CreatedBy] bigint NULL,
        [CreatedDate] datetime2(3) NOT NULL,
        [ModifiedBy] bigint NULL,
        [ModifiedDate] datetime2(3) NULL,
        [IsActive] bit NOT NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedBy] bigint NULL,
        [DeletedDate] datetime2(3) NULL,
        CONSTRAINT [PK_TRN_BankImportBatch] PRIMARY KEY ([BankImportBatchId]),
        CONSTRAINT [FK_TRN_BankImportBatch_MST_BankAccount_BankAccountId] FOREIGN KEY ([BankAccountId]) REFERENCES [MST_BankAccount] ([BankAccountId]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101165945_BillingFinance'
)
BEGIN
    CREATE TABLE [TRN_BankMatchAllocation] (
        [BankMatchAllocationId] bigint NOT NULL IDENTITY,
        [BankTransactionId] bigint NOT NULL,
        [DocType] tinyint NOT NULL,
        [DocId] bigint NOT NULL,
        [DocNumber] nvarchar(40) NOT NULL,
        [Amount] decimal(18,2) NOT NULL,
        [CreatedRefs] nvarchar(500) NULL,
        [UnmatchReason] nvarchar(300) NULL,
        [CreatedBy] bigint NULL,
        [CreatedDate] datetime2(3) NOT NULL,
        [ModifiedBy] bigint NULL,
        [ModifiedDate] datetime2(3) NULL,
        [IsActive] bit NOT NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedBy] bigint NULL,
        [DeletedDate] datetime2(3) NULL,
        CONSTRAINT [PK_TRN_BankMatchAllocation] PRIMARY KEY ([BankMatchAllocationId]),
        CONSTRAINT [CK_BankMatchAllocation_Amount] CHECK ([Amount] > 0),
        CONSTRAINT [FK_TRN_BankMatchAllocation_TRN_BankTransaction_BankTransactionId] FOREIGN KEY ([BankTransactionId]) REFERENCES [TRN_BankTransaction] ([BankTransactionId]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101165945_BillingFinance'
)
BEGIN
    CREATE TABLE [TRN_BankMatchDifference] (
        [BankMatchDifferenceId] bigint NOT NULL IDENTITY,
        [BankTransactionId] bigint NOT NULL,
        [Reason] tinyint NOT NULL,
        [SignedAmount] decimal(18,2) NOT NULL,
        [TdsSection] nvarchar(10) NULL,
        [TargetDocType] tinyint NULL,
        [TargetDocId] bigint NULL,
        [CreatedExpenseId] bigint NULL,
        [CreatedTdsEntryId] bigint NULL,
        [CreatedWalletTransactionId] bigint NULL,
        [CreatedVendorPaymentId] bigint NULL,
        [Note] nvarchar(300) NULL,
        [CreatedBy] bigint NULL,
        [CreatedDate] datetime2(3) NOT NULL,
        [ModifiedBy] bigint NULL,
        [ModifiedDate] datetime2(3) NULL,
        [IsActive] bit NOT NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedBy] bigint NULL,
        [DeletedDate] datetime2(3) NULL,
        CONSTRAINT [PK_TRN_BankMatchDifference] PRIMARY KEY ([BankMatchDifferenceId]),
        CONSTRAINT [CK_BankMatchDifference_Amount] CHECK ([SignedAmount] <> 0),
        CONSTRAINT [FK_TRN_BankMatchDifference_TRN_BankTransaction_BankTransactionId] FOREIGN KEY ([BankTransactionId]) REFERENCES [TRN_BankTransaction] ([BankTransactionId]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101165945_BillingFinance'
)
BEGIN
    CREATE TABLE [TRN_ExpenseReceipt] (
        [ExpenseReceiptId] bigint NOT NULL IDENTITY,
        [ExpenseId] bigint NOT NULL,
        [FileName] nvarchar(260) NOT NULL,
        [StoredPath] nvarchar(500) NOT NULL,
        [ContentType] nvarchar(100) NOT NULL,
        [SizeBytes] bigint NOT NULL,
        [UploadedByUserId] bigint NOT NULL,
        [UploadedUtc] datetime2(3) NOT NULL,
        [IsDeleted] bit NOT NULL,
        CONSTRAINT [PK_TRN_ExpenseReceipt] PRIMARY KEY ([ExpenseReceiptId]),
        CONSTRAINT [FK_TRN_ExpenseReceipt_TRN_Expense_ExpenseId] FOREIGN KEY ([ExpenseId]) REFERENCES [TRN_Expense] ([ExpenseId]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101165945_BillingFinance'
)
BEGIN
    CREATE TABLE [TRN_Gstr2bLine] (
        [Gstr2bLineId] bigint NOT NULL IDENTITY,
        [Period] nvarchar(7) NOT NULL,
        [SupplierGstin] nvarchar(15) NOT NULL,
        [SupplierName] nvarchar(200) NULL,
        [BillNo] nvarchar(60) NOT NULL,
        [BillDate] date NULL,
        [TaxableValue] decimal(18,2) NOT NULL,
        [Cgst] decimal(18,2) NOT NULL,
        [Sgst] decimal(18,2) NOT NULL,
        [Igst] decimal(18,2) NOT NULL,
        [MatchedBillId] bigint NULL,
        [Result] tinyint NOT NULL,
        [UploadedUtc] datetime2(3) NOT NULL,
        [UploadedByUserId] bigint NOT NULL,
        CONSTRAINT [PK_TRN_Gstr2bLine] PRIMARY KEY ([Gstr2bLineId])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101165945_BillingFinance'
)
BEGIN
    CREATE TABLE [TRN_GstReturn] (
        [GstReturnId] bigint NOT NULL IDENTITY,
        [Period] nvarchar(7) NOT NULL,
        [ReturnType] nvarchar(10) NOT NULL,
        [Status] tinyint NOT NULL,
        [Arn] nvarchar(20) NULL,
        [FiledOn] date NULL,
        [FiledByUserId] bigint NULL,
        [CreatedBy] bigint NULL,
        [CreatedDate] datetime2(3) NOT NULL,
        [ModifiedBy] bigint NULL,
        [ModifiedDate] datetime2(3) NULL,
        [IsActive] bit NOT NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedBy] bigint NULL,
        [DeletedDate] datetime2(3) NULL,
        CONSTRAINT [PK_TRN_GstReturn] PRIMARY KEY ([GstReturnId]),
        CONSTRAINT [CK_GstReturn_Filed] CHECK ([Status] <> 3 OR ([Arn] IS NOT NULL AND [FiledOn] IS NOT NULL)),
        CONSTRAINT [CK_GstReturn_Type] CHECK ([ReturnType] IN ('GSTR1','GSTR3B'))
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101165945_BillingFinance'
)
BEGIN
    CREATE TABLE [TRN_InvoicePayment] (
        [InvoicePaymentId] bigint NOT NULL IDENTITY,
        [ReceiptNumber] nvarchar(30) NOT NULL,
        [InvoiceId] bigint NOT NULL,
        [CustomerId] bigint NOT NULL,
        [PaymentId] bigint NULL,
        [Amount] decimal(18,2) NOT NULL,
        [PaidOn] date NOT NULL,
        [Mode] tinyint NOT NULL,
        [Reference] nvarchar(60) NULL,
        [BankAccountId] int NULL,
        [Note] nvarchar(300) NULL,
        [WalletTransactionId] bigint NULL,
        [CashCollectionId] bigint NULL,
        [BankTransactionId] bigint NULL,
        [IsReversed] bit NOT NULL,
        [ReversedByUserId] bigint NULL,
        [ReversedUtc] datetime2(3) NULL,
        [ReverseReason] nvarchar(300) NULL,
        [ReversalOfId] bigint NULL,
        [RecordedByUserId] bigint NOT NULL,
        [RecordedUtc] datetime2(3) NOT NULL,
        [CreatedBy] bigint NULL,
        [CreatedDate] datetime2(3) NOT NULL,
        [ModifiedBy] bigint NULL,
        [ModifiedDate] datetime2(3) NULL,
        [IsActive] bit NOT NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedBy] bigint NULL,
        [DeletedDate] datetime2(3) NULL,
        CONSTRAINT [PK_TRN_InvoicePayment] PRIMARY KEY ([InvoicePaymentId]),
        CONSTRAINT [CK_InvoicePayment_Amount] CHECK ([Amount] <> 0),
        CONSTRAINT [CK_InvoicePayment_Mode] CHECK ([Mode] BETWEEN 1 AND 8),
        CONSTRAINT [CK_InvoicePayment_Reference] CHECK ([Mode] IN (1,6,7,8) OR [ReversalOfId] IS NOT NULL OR [Reference] IS NOT NULL),
        CONSTRAINT [CK_InvoicePayment_Reversal] CHECK (([ReversalOfId] IS NULL AND [Amount] > 0) OR ([ReversalOfId] IS NOT NULL AND [Amount] < 0)),
        CONSTRAINT [FK_TRN_InvoicePayment_MST_BankAccount_BankAccountId] FOREIGN KEY ([BankAccountId]) REFERENCES [MST_BankAccount] ([BankAccountId]) ON DELETE NO ACTION,
        CONSTRAINT [FK_TRN_InvoicePayment_MST_User_CustomerId] FOREIGN KEY ([CustomerId]) REFERENCES [MST_User] ([UserId]) ON DELETE NO ACTION,
        CONSTRAINT [FK_TRN_InvoicePayment_TRN_InvoicePayment_ReversalOfId] FOREIGN KEY ([ReversalOfId]) REFERENCES [TRN_InvoicePayment] ([InvoicePaymentId]) ON DELETE NO ACTION,
        CONSTRAINT [FK_TRN_InvoicePayment_TRN_Invoice_InvoiceId] FOREIGN KEY ([InvoiceId]) REFERENCES [TRN_Invoice] ([InvoiceId]) ON DELETE NO ACTION,
        CONSTRAINT [FK_TRN_InvoicePayment_TRN_Payment_PaymentId] FOREIGN KEY ([PaymentId]) REFERENCES [TRN_Payment] ([PaymentId]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101165945_BillingFinance'
)
BEGIN
    CREATE TABLE [TRN_PeriodLock] (
        [PeriodLockId] int NOT NULL IDENTITY,
        [Period] nvarchar(7) NOT NULL,
        [IsLocked] bit NOT NULL,
        [LockedByUserId] bigint NULL,
        [LockedUtc] datetime2(3) NULL,
        [UnlockedByUserId] bigint NULL,
        [UnlockedUtc] datetime2(3) NULL,
        [UnlockReason] nvarchar(300) NULL,
        [CreatedBy] bigint NULL,
        [CreatedDate] datetime2(3) NOT NULL,
        [ModifiedBy] bigint NULL,
        [ModifiedDate] datetime2(3) NULL,
        [IsActive] bit NOT NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedBy] bigint NULL,
        [DeletedDate] datetime2(3) NULL,
        CONSTRAINT [PK_TRN_PeriodLock] PRIMARY KEY ([PeriodLockId]),
        CONSTRAINT [CK_PeriodLock_Period] CHECK ([Period] LIKE '[12][0-9][0-9][0-9]-[01][0-9]')
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101165945_BillingFinance'
)
BEGIN
    CREATE TABLE [TRN_PettyCashEntry] (
        [PettyCashEntryId] bigint NOT NULL IDENTITY,
        [EntryType] tinyint NOT NULL,
        [Amount] decimal(18,2) NOT NULL,
        [HappenedOn] date NOT NULL,
        [Description] nvarchar(300) NOT NULL,
        [ExpenseId] bigint NULL,
        [VendorPaymentId] bigint NULL,
        [BankAccountId] int NULL,
        [BalanceAfter] decimal(18,2) NOT NULL,
        [Reason] nvarchar(300) NULL,
        [CountedAmount] decimal(18,2) NULL,
        [EnteredByUserId] bigint NOT NULL,
        [CreatedBy] bigint NULL,
        [CreatedDate] datetime2(3) NOT NULL,
        [ModifiedBy] bigint NULL,
        [ModifiedDate] datetime2(3) NULL,
        [IsActive] bit NOT NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedBy] bigint NULL,
        [DeletedDate] datetime2(3) NULL,
        CONSTRAINT [PK_TRN_PettyCashEntry] PRIMARY KEY ([PettyCashEntryId]),
        CONSTRAINT [CK_PettyCash_NotNegative] CHECK ([BalanceAfter] >= 0),
        CONSTRAINT [CK_PettyCash_Sign] CHECK (([EntryType] = 1 AND [Amount] > 0) OR ([EntryType] = 2 AND [Amount] < 0) OR [EntryType] = 3)
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101165945_BillingFinance'
)
BEGIN
    CREATE TABLE [TRN_PurchaseBill] (
        [PurchaseBillId] bigint NOT NULL IDENTITY,
        [BillNumber] nvarchar(30) NOT NULL,
        [SupplierId] int NOT NULL,
        [SupplierBillNo] nvarchar(60) NOT NULL,
        [SupplierBillNoKey] nvarchar(60) NOT NULL,
        [BillDate] date NOT NULL,
        [DueDate] date NOT NULL,
        [PaymentTermsDays] int NOT NULL,
        [PurchaseOrderId] bigint NULL,
        [LocationId] int NOT NULL,
        [SupplierStateCode] nvarchar(2) NULL,
        [PlaceOfSupplyCode] nvarchar(2) NULL,
        [IsInterState] bit NOT NULL,
        [SupplierGstin] nvarchar(15) NULL,
        [TaxableValue] decimal(18,2) NOT NULL,
        [CgstAmount] decimal(18,2) NOT NULL,
        [SgstAmount] decimal(18,2) NOT NULL,
        [IgstAmount] decimal(18,2) NOT NULL,
        [BillTotal] decimal(18,2) NOT NULL,
        [TdsSection] nvarchar(10) NULL,
        [TdsRate] decimal(5,2) NOT NULL,
        [TdsAmount] decimal(18,2) NOT NULL,
        [Status] tinyint NOT NULL,
        [EnteredByUserId] bigint NOT NULL,
        [SubmittedUtc] datetime2(3) NULL,
        [ApprovedByUserId] bigint NULL,
        [ApprovedUtc] datetime2(3) NULL,
        [ApprovalComment] nvarchar(500) NULL,
        [RejectReason] nvarchar(500) NULL,
        [HoldReason] nvarchar(500) NULL,
        [CancelReason] nvarchar(500) NULL,
        [Notes] nvarchar(500) NULL,
        [CreatedBy] bigint NULL,
        [CreatedDate] datetime2(3) NOT NULL,
        [ModifiedBy] bigint NULL,
        [ModifiedDate] datetime2(3) NULL,
        [IsActive] bit NOT NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedBy] bigint NULL,
        [DeletedDate] datetime2(3) NULL,
        CONSTRAINT [PK_TRN_PurchaseBill] PRIMARY KEY ([PurchaseBillId]),
        CONSTRAINT [CK_PurchaseBill_Dates] CHECK ([DueDate] >= [BillDate]),
        CONSTRAINT [CK_PurchaseBill_Status] CHECK ([Status] BETWEEN 1 AND 8),
        CONSTRAINT [CK_PurchaseBill_Total] CHECK ([BillTotal] >= 0 AND [TaxableValue] >= 0 AND [TdsAmount] >= 0),
        CONSTRAINT [FK_TRN_PurchaseBill_MST_Location_LocationId] FOREIGN KEY ([LocationId]) REFERENCES [MST_Location] ([LocationId]) ON DELETE NO ACTION,
        CONSTRAINT [FK_TRN_PurchaseBill_MST_Supplier_SupplierId] FOREIGN KEY ([SupplierId]) REFERENCES [MST_Supplier] ([SupplierId]) ON DELETE NO ACTION,
        CONSTRAINT [FK_TRN_PurchaseBill_TRN_PurchaseOrder_PurchaseOrderId] FOREIGN KEY ([PurchaseOrderId]) REFERENCES [TRN_PurchaseOrder] ([PurchaseOrderId]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101165945_BillingFinance'
)
BEGIN
    CREATE TABLE [TRN_RecurringExpense] (
        [RecurringExpenseId] bigint NOT NULL IDENTITY,
        [Category] nvarchar(100) NOT NULL,
        [Description] nvarchar(500) NOT NULL,
        [DepartmentId] int NULL,
        [SupplierId] int NULL,
        [LocationId] int NOT NULL,
        [Amount] decimal(18,2) NOT NULL,
        [PaymentMethod] tinyint NOT NULL,
        [DayOfMonth] int NOT NULL,
        [StartOn] date NOT NULL,
        [EndOn] date NULL,
        [LastRaisedFor] date NULL,
        [CreatedBy] bigint NULL,
        [CreatedDate] datetime2(3) NOT NULL,
        [ModifiedBy] bigint NULL,
        [ModifiedDate] datetime2(3) NULL,
        [IsActive] bit NOT NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedBy] bigint NULL,
        [DeletedDate] datetime2(3) NULL,
        CONSTRAINT [PK_TRN_RecurringExpense] PRIMARY KEY ([RecurringExpenseId]),
        CONSTRAINT [CK_RecurringExpense_Amount] CHECK ([Amount] > 0),
        CONSTRAINT [CK_RecurringExpense_Day] CHECK ([DayOfMonth] BETWEEN 1 AND 28)
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101165945_BillingFinance'
)
BEGIN
    CREATE TABLE [TRN_ReminderLog] (
        [ReminderLogId] bigint NOT NULL IDENTITY,
        [ReminderType] nvarchar(40) NOT NULL,
        [DocumentType] nvarchar(30) NOT NULL,
        [DocumentId] bigint NOT NULL,
        [DocumentNumber] nvarchar(40) NULL,
        [Recipient] nvarchar(200) NOT NULL,
        [Channel] nvarchar(20) NOT NULL,
        [SentUtc] datetime2(3) NOT NULL,
        [Delivered] bit NOT NULL,
        [Error] nvarchar(500) NULL,
        CONSTRAINT [PK_TRN_ReminderLog] PRIMARY KEY ([ReminderLogId])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101165945_BillingFinance'
)
BEGIN
    CREATE TABLE [TRN_TdsChallan] (
        [TdsChallanId] bigint NOT NULL IDENTITY,
        [BsrCode] nvarchar(7) NOT NULL,
        [ChallanNo] nvarchar(10) NOT NULL,
        [PaidOn] date NOT NULL,
        [Amount] decimal(18,2) NOT NULL,
        [Quarter] tinyint NOT NULL,
        [FinancialYear] nvarchar(7) NOT NULL,
        [CreatedBy] bigint NULL,
        [CreatedDate] datetime2(3) NOT NULL,
        [ModifiedBy] bigint NULL,
        [ModifiedDate] datetime2(3) NULL,
        [IsActive] bit NOT NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedBy] bigint NULL,
        [DeletedDate] datetime2(3) NULL,
        CONSTRAINT [PK_TRN_TdsChallan] PRIMARY KEY ([TdsChallanId]),
        CONSTRAINT [CK_TdsChallan_Amount] CHECK ([Amount] > 0),
        CONSTRAINT [CK_TdsChallan_Bsr] CHECK (LEN([BsrCode]) = 7)
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101165945_BillingFinance'
)
BEGIN
    CREATE TABLE [TRN_VendorPayment] (
        [VendorPaymentId] bigint NOT NULL IDENTITY,
        [PaymentNumber] nvarchar(30) NOT NULL,
        [SupplierId] int NOT NULL,
        [PaidOn] date NOT NULL,
        [Amount] decimal(18,2) NOT NULL,
        [TdsAmount] decimal(18,2) NOT NULL,
        [AdvanceAdjusted] decimal(18,2) NOT NULL,
        [AdvanceAmount] decimal(18,2) NOT NULL,
        [Mode] tinyint NOT NULL,
        [Reference] nvarchar(60) NULL,
        [BankAccountId] int NULL,
        [FromPettyCash] bit NOT NULL,
        [Note] nvarchar(300) NULL,
        [IsReversed] bit NOT NULL,
        [ReversedByUserId] bigint NULL,
        [ReversedUtc] datetime2(3) NULL,
        [ReverseReason] nvarchar(300) NULL,
        [PaidByUserId] bigint NOT NULL,
        [CreatedBy] bigint NULL,
        [CreatedDate] datetime2(3) NOT NULL,
        [ModifiedBy] bigint NULL,
        [ModifiedDate] datetime2(3) NULL,
        [IsActive] bit NOT NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedBy] bigint NULL,
        [DeletedDate] datetime2(3) NULL,
        CONSTRAINT [PK_TRN_VendorPayment] PRIMARY KEY ([VendorPaymentId]),
        CONSTRAINT [CK_VendorPayment_Amounts] CHECK ([Amount] >= 0 AND [TdsAmount] >= 0 AND [AdvanceAdjusted] >= 0 AND [AdvanceAmount] >= 0 AND [AdvanceAmount] <= [Amount]),
        CONSTRAINT [CK_VendorPayment_Reference] CHECK ([Mode] = 6 OR [Reference] IS NOT NULL),
        CONSTRAINT [CK_VendorPayment_Source] CHECK (([FromPettyCash] = 1 AND [BankAccountId] IS NULL) OR ([FromPettyCash] = 0 AND [BankAccountId] IS NOT NULL)),
        CONSTRAINT [FK_TRN_VendorPayment_MST_BankAccount_BankAccountId] FOREIGN KEY ([BankAccountId]) REFERENCES [MST_BankAccount] ([BankAccountId]) ON DELETE NO ACTION,
        CONSTRAINT [FK_TRN_VendorPayment_MST_Supplier_SupplierId] FOREIGN KEY ([SupplierId]) REFERENCES [MST_Supplier] ([SupplierId]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101165945_BillingFinance'
)
BEGIN
    CREATE TABLE [TRN_ComplianceStatus] (
        [ComplianceStatusId] bigint NOT NULL IDENTITY,
        [ComplianceObligationId] int NOT NULL,
        [Period] nvarchar(7) NOT NULL,
        [Status] tinyint NOT NULL,
        [CompletedOn] date NULL,
        [CompletedByUserId] bigint NULL,
        [Reference] nvarchar(60) NULL,
        [CreatedBy] bigint NULL,
        [CreatedDate] datetime2(3) NOT NULL,
        [ModifiedBy] bigint NULL,
        [ModifiedDate] datetime2(3) NULL,
        [IsActive] bit NOT NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedBy] bigint NULL,
        [DeletedDate] datetime2(3) NULL,
        CONSTRAINT [PK_TRN_ComplianceStatus] PRIMARY KEY ([ComplianceStatusId]),
        CONSTRAINT [FK_TRN_ComplianceStatus_MST_ComplianceObligation_ComplianceObligationId] FOREIGN KEY ([ComplianceObligationId]) REFERENCES [MST_ComplianceObligation] ([ComplianceObligationId]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101165945_BillingFinance'
)
BEGIN
    CREATE TABLE [TRN_ExpenseControl] (
        [ExpenseId] bigint NOT NULL,
        [DepartmentId] int NULL,
        [ApprovalStatus] tinyint NOT NULL,
        [ApprovalLevel] tinyint NOT NULL,
        [DecidedByUserId] bigint NULL,
        [DecidedUtc] datetime2(3) NULL,
        [DecisionComment] nvarchar(500) NULL,
        [PaidFrom] tinyint NOT NULL,
        [IsReimbursement] bit NOT NULL,
        [ReimburseToUserId] bigint NULL,
        [ReimbursedUtc] datetime2(3) NULL,
        [ReimbursedByUserId] bigint NULL,
        [ReimbursedVia] nvarchar(20) NULL,
        [ReimbursementReference] nvarchar(60) NULL,
        [RecurringExpenseId] bigint NULL,
        [IsRecurringDraft] bit NOT NULL,
        [PettyCashEntryId] bigint NULL,
        [CreatedBy] bigint NULL,
        [CreatedDate] datetime2(3) NOT NULL,
        [ModifiedBy] bigint NULL,
        [ModifiedDate] datetime2(3) NULL,
        [IsActive] bit NOT NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedBy] bigint NULL,
        [DeletedDate] datetime2(3) NULL,
        CONSTRAINT [PK_TRN_ExpenseControl] PRIMARY KEY ([ExpenseId]),
        CONSTRAINT [CK_ExpenseControl_Decision] CHECK ([ApprovalStatus] <> 3 OR [DecisionComment] IS NOT NULL),
        CONSTRAINT [CK_ExpenseControl_Reimburse] CHECK ([IsReimbursement] = 0 OR [ReimburseToUserId] IS NOT NULL),
        CONSTRAINT [FK_TRN_ExpenseControl_MST_Department_DepartmentId] FOREIGN KEY ([DepartmentId]) REFERENCES [MST_Department] ([DepartmentId]) ON DELETE NO ACTION,
        CONSTRAINT [FK_TRN_ExpenseControl_TRN_Expense_ExpenseId] FOREIGN KEY ([ExpenseId]) REFERENCES [TRN_Expense] ([ExpenseId]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101165945_BillingFinance'
)
BEGIN
    CREATE TABLE [TRN_PurchaseBillAttachment] (
        [PurchaseBillAttachmentId] bigint NOT NULL IDENTITY,
        [PurchaseBillId] bigint NOT NULL,
        [FileName] nvarchar(260) NOT NULL,
        [StoredPath] nvarchar(500) NOT NULL,
        [ContentType] nvarchar(100) NOT NULL,
        [SizeBytes] bigint NOT NULL,
        [UploadedByUserId] bigint NOT NULL,
        [UploadedUtc] datetime2(3) NOT NULL,
        CONSTRAINT [PK_TRN_PurchaseBillAttachment] PRIMARY KEY ([PurchaseBillAttachmentId]),
        CONSTRAINT [FK_TRN_PurchaseBillAttachment_TRN_PurchaseBill_PurchaseBillId] FOREIGN KEY ([PurchaseBillId]) REFERENCES [TRN_PurchaseBill] ([PurchaseBillId]) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101165945_BillingFinance'
)
BEGIN
    CREATE TABLE [TRN_PurchaseBillEvent] (
        [PurchaseBillEventId] bigint NOT NULL IDENTITY,
        [PurchaseBillId] bigint NOT NULL,
        [EventType] nvarchar(40) NOT NULL,
        [Comment] nvarchar(500) NULL,
        [UserId] bigint NULL,
        [EventUtc] datetime2(3) NOT NULL,
        CONSTRAINT [PK_TRN_PurchaseBillEvent] PRIMARY KEY ([PurchaseBillEventId]),
        CONSTRAINT [FK_TRN_PurchaseBillEvent_TRN_PurchaseBill_PurchaseBillId] FOREIGN KEY ([PurchaseBillId]) REFERENCES [TRN_PurchaseBill] ([PurchaseBillId]) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101165945_BillingFinance'
)
BEGIN
    CREATE TABLE [TRN_PurchaseBillLine] (
        [PurchaseBillLineId] bigint NOT NULL IDENTITY,
        [PurchaseBillId] bigint NOT NULL,
        [LineNumber] int NOT NULL,
        [Description] nvarchar(300) NOT NULL,
        [HsnSac] nvarchar(10) NULL,
        [Quantity] decimal(18,3) NOT NULL,
        [Rate] decimal(18,2) NOT NULL,
        [TaxableValue] decimal(18,2) NOT NULL,
        [GstRate] decimal(5,2) NOT NULL,
        [CgstAmount] decimal(18,2) NOT NULL,
        [SgstAmount] decimal(18,2) NOT NULL,
        [IgstAmount] decimal(18,2) NOT NULL,
        CONSTRAINT [PK_TRN_PurchaseBillLine] PRIMARY KEY ([PurchaseBillLineId]),
        CONSTRAINT [CK_PurchaseBillLine_Qty] CHECK ([Quantity] > 0 AND [Rate] >= 0),
        CONSTRAINT [FK_TRN_PurchaseBillLine_TRN_PurchaseBill_PurchaseBillId] FOREIGN KEY ([PurchaseBillId]) REFERENCES [TRN_PurchaseBill] ([PurchaseBillId]) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101165945_BillingFinance'
)
BEGIN
    CREATE TABLE [TRN_TdsEntry] (
        [TdsEntryId] bigint NOT NULL IDENTITY,
        [Direction] tinyint NOT NULL,
        [PartyType] nvarchar(20) NOT NULL,
        [PartyId] bigint NOT NULL,
        [PartyName] nvarchar(200) NOT NULL,
        [PartyTaxId] nvarchar(10) NULL,
        [Section] nvarchar(10) NOT NULL,
        [Rate] decimal(5,2) NOT NULL,
        [BaseAmount] decimal(18,2) NOT NULL,
        [TdsAmount] decimal(18,2) NOT NULL,
        [DeductedOn] date NOT NULL,
        [Quarter] tinyint NOT NULL,
        [FinancialYear] nvarchar(7) NOT NULL,
        [SourceType] nvarchar(20) NOT NULL,
        [SourceId] bigint NOT NULL,
        [DocumentNumber] nvarchar(40) NULL,
        [TdsChallanId] bigint NULL,
        [CertificateNo] nvarchar(40) NULL,
        [CertificateReceivedOn] date NULL,
        [CertificatePath] nvarchar(500) NULL,
        [CertificateFileName] nvarchar(260) NULL,
        [CreatedBy] bigint NULL,
        [CreatedDate] datetime2(3) NOT NULL,
        [ModifiedBy] bigint NULL,
        [ModifiedDate] datetime2(3) NULL,
        [IsActive] bit NOT NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedBy] bigint NULL,
        [DeletedDate] datetime2(3) NULL,
        CONSTRAINT [PK_TRN_TdsEntry] PRIMARY KEY ([TdsEntryId]),
        CONSTRAINT [CK_TdsEntry_Amount] CHECK ([TdsAmount] > 0 AND [BaseAmount] >= 0),
        CONSTRAINT [CK_TdsEntry_Quarter] CHECK ([Quarter] BETWEEN 1 AND 4),
        CONSTRAINT [FK_TRN_TdsEntry_TRN_TdsChallan_TdsChallanId] FOREIGN KEY ([TdsChallanId]) REFERENCES [TRN_TdsChallan] ([TdsChallanId]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101165945_BillingFinance'
)
BEGIN
    CREATE TABLE [TRN_VendorPaymentAllocation] (
        [VendorPaymentAllocationId] bigint NOT NULL IDENTITY,
        [VendorPaymentId] bigint NOT NULL,
        [PurchaseBillId] bigint NOT NULL,
        [Amount] decimal(18,2) NOT NULL,
        [TdsAmount] decimal(18,2) NOT NULL,
        CONSTRAINT [PK_TRN_VendorPaymentAllocation] PRIMARY KEY ([VendorPaymentAllocationId]),
        CONSTRAINT [CK_VendorPaymentAllocation_Amount] CHECK ([Amount] > 0 AND [TdsAmount] >= 0 AND [TdsAmount] <= [Amount]),
        CONSTRAINT [FK_TRN_VendorPaymentAllocation_TRN_PurchaseBill_PurchaseBillId] FOREIGN KEY ([PurchaseBillId]) REFERENCES [TRN_PurchaseBill] ([PurchaseBillId]) ON DELETE NO ACTION,
        CONSTRAINT [FK_TRN_VendorPaymentAllocation_TRN_VendorPayment_VendorPaymentId] FOREIGN KEY ([VendorPaymentId]) REFERENCES [TRN_VendorPayment] ([VendorPaymentId]) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101165945_BillingFinance'
)
BEGIN
    CREATE UNIQUE INDEX [UX_ComplianceObligation_Code] ON [MST_ComplianceObligation] ([Code]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101165945_BillingFinance'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [UX_Department_Name] ON [MST_Department] ([Name]) WHERE [IsDeleted] = 0');
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101165945_BillingFinance'
)
BEGIN
    CREATE UNIQUE INDEX [UX_DifferenceReason_Code] ON [MST_DifferenceReason] ([Code]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101165945_BillingFinance'
)
BEGIN
    CREATE UNIQUE INDEX [UX_GstRate_Rate_From] ON [MST_GstRate] ([Rate], [EffectiveFrom]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101165945_BillingFinance'
)
BEGIN
    CREATE UNIQUE INDEX [UX_TdsSection_Section_From] ON [MST_TdsSection] ([Section], [EffectiveFrom]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101165945_BillingFinance'
)
BEGIN
    CREATE INDEX [IX_TRN_BankImportBatch_BankAccountId] ON [TRN_BankImportBatch] ([BankAccountId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101165945_BillingFinance'
)
BEGIN
    CREATE UNIQUE INDEX [UX_BankImportBatch_Code] ON [TRN_BankImportBatch] ([BatchCode]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101165945_BillingFinance'
)
BEGIN
    CREATE INDEX [IX_BankMatchAllocation_Doc] ON [TRN_BankMatchAllocation] ([DocType], [DocId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101165945_BillingFinance'
)
BEGIN
    CREATE INDEX [IX_BankMatchAllocation_Line] ON [TRN_BankMatchAllocation] ([BankTransactionId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101165945_BillingFinance'
)
BEGIN
    CREATE INDEX [IX_BankMatchDifference_Line] ON [TRN_BankMatchDifference] ([BankTransactionId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101165945_BillingFinance'
)
BEGIN
    CREATE UNIQUE INDEX [UX_ComplianceStatus_Obligation_Period] ON [TRN_ComplianceStatus] ([ComplianceObligationId], [Period]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101165945_BillingFinance'
)
BEGIN
    CREATE INDEX [IX_ExpenseControl_Status] ON [TRN_ExpenseControl] ([ApprovalStatus]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101165945_BillingFinance'
)
BEGIN
    CREATE INDEX [IX_TRN_ExpenseControl_DepartmentId] ON [TRN_ExpenseControl] ([DepartmentId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101165945_BillingFinance'
)
BEGIN
    CREATE INDEX [IX_ExpenseReceipt_Expense] ON [TRN_ExpenseReceipt] ([ExpenseId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101165945_BillingFinance'
)
BEGIN
    CREATE INDEX [IX_Gstr2bLine_Period] ON [TRN_Gstr2bLine] ([Period]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101165945_BillingFinance'
)
BEGIN
    CREATE UNIQUE INDEX [UX_GstReturn_Period_Type] ON [TRN_GstReturn] ([Period], [ReturnType]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101165945_BillingFinance'
)
BEGIN
    CREATE INDEX [IX_InvoicePayment_Customer_Date] ON [TRN_InvoicePayment] ([CustomerId], [PaidOn]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101165945_BillingFinance'
)
BEGIN
    CREATE INDEX [IX_InvoicePayment_Invoice] ON [TRN_InvoicePayment] ([InvoiceId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101165945_BillingFinance'
)
BEGIN
    CREATE INDEX [IX_InvoicePayment_Payment] ON [TRN_InvoicePayment] ([PaymentId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101165945_BillingFinance'
)
BEGIN
    CREATE INDEX [IX_TRN_InvoicePayment_BankAccountId] ON [TRN_InvoicePayment] ([BankAccountId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101165945_BillingFinance'
)
BEGIN
    CREATE INDEX [IX_TRN_InvoicePayment_ReversalOfId] ON [TRN_InvoicePayment] ([ReversalOfId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101165945_BillingFinance'
)
BEGIN
    CREATE UNIQUE INDEX [UX_InvoicePayment_Receipt] ON [TRN_InvoicePayment] ([ReceiptNumber]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101165945_BillingFinance'
)
BEGIN
    CREATE UNIQUE INDEX [UX_PeriodLock_Period] ON [TRN_PeriodLock] ([Period]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101165945_BillingFinance'
)
BEGIN
    CREATE INDEX [IX_PettyCash_Date] ON [TRN_PettyCashEntry] ([HappenedOn]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101165945_BillingFinance'
)
BEGIN
    CREATE INDEX [IX_PurchaseBill_BillDate] ON [TRN_PurchaseBill] ([BillDate]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101165945_BillingFinance'
)
BEGIN
    CREATE INDEX [IX_PurchaseBill_Status_Due] ON [TRN_PurchaseBill] ([Status], [DueDate]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101165945_BillingFinance'
)
BEGIN
    CREATE INDEX [IX_TRN_PurchaseBill_LocationId] ON [TRN_PurchaseBill] ([LocationId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101165945_BillingFinance'
)
BEGIN
    CREATE INDEX [IX_TRN_PurchaseBill_PurchaseOrderId] ON [TRN_PurchaseBill] ([PurchaseOrderId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101165945_BillingFinance'
)
BEGIN
    CREATE UNIQUE INDEX [UX_PurchaseBill_Number] ON [TRN_PurchaseBill] ([BillNumber]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101165945_BillingFinance'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [UX_PurchaseBill_Supplier_BillNo] ON [TRN_PurchaseBill] ([SupplierId], [SupplierBillNoKey]) WHERE [IsDeleted] = 0 AND [Status] <> 8');
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101165945_BillingFinance'
)
BEGIN
    CREATE INDEX [IX_TRN_PurchaseBillAttachment_PurchaseBillId] ON [TRN_PurchaseBillAttachment] ([PurchaseBillId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101165945_BillingFinance'
)
BEGIN
    CREATE INDEX [IX_TRN_PurchaseBillEvent_PurchaseBillId] ON [TRN_PurchaseBillEvent] ([PurchaseBillId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101165945_BillingFinance'
)
BEGIN
    CREATE INDEX [IX_TRN_PurchaseBillLine_PurchaseBillId] ON [TRN_PurchaseBillLine] ([PurchaseBillId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101165945_BillingFinance'
)
BEGIN
    CREATE INDEX [IX_ReminderLog_Document] ON [TRN_ReminderLog] ([DocumentType], [DocumentId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101165945_BillingFinance'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [UX_TdsChallan_Bsr_No_Date] ON [TRN_TdsChallan] ([BsrCode], [ChallanNo], [PaidOn]) WHERE [IsDeleted] = 0');
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101165945_BillingFinance'
)
BEGIN
    CREATE INDEX [IX_TdsEntry_Challan] ON [TRN_TdsEntry] ([TdsChallanId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101165945_BillingFinance'
)
BEGIN
    CREATE INDEX [IX_TdsEntry_Direction_Period] ON [TRN_TdsEntry] ([Direction], [FinancialYear], [Quarter]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101165945_BillingFinance'
)
BEGIN
    CREATE INDEX [IX_TdsEntry_Source] ON [TRN_TdsEntry] ([SourceType], [SourceId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101165945_BillingFinance'
)
BEGIN
    CREATE INDEX [IX_TRN_VendorPayment_BankAccountId] ON [TRN_VendorPayment] ([BankAccountId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101165945_BillingFinance'
)
BEGIN
    CREATE INDEX [IX_VendorPayment_Supplier_Date] ON [TRN_VendorPayment] ([SupplierId], [PaidOn]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101165945_BillingFinance'
)
BEGIN
    CREATE UNIQUE INDEX [UX_VendorPayment_Number] ON [TRN_VendorPayment] ([PaymentNumber]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101165945_BillingFinance'
)
BEGIN
    CREATE INDEX [IX_VendorPaymentAllocation_Bill] ON [TRN_VendorPaymentAllocation] ([PurchaseBillId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101165945_BillingFinance'
)
BEGIN
    CREATE UNIQUE INDEX [UX_VendorPaymentAllocation_Payment_Bill] ON [TRN_VendorPaymentAllocation] ([VendorPaymentId], [PurchaseBillId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101165945_BillingFinance'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261101165945_BillingFinance', N'8.0.19');
END;
GO

COMMIT;
GO

