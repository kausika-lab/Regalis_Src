BEGIN TRANSACTION;
GO

IF EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101165945_BillingFinance'
)
BEGIN
    DROP TABLE [MST_BankColumnMapping];
END;
GO

IF EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101165945_BillingFinance'
)
BEGIN
    DROP TABLE [MST_CustomerTaxProfile];
END;
GO

IF EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101165945_BillingFinance'
)
BEGIN
    DROP TABLE [MST_DifferenceReason];
END;
GO

IF EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101165945_BillingFinance'
)
BEGIN
    DROP TABLE [MST_GstRate];
END;
GO

IF EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101165945_BillingFinance'
)
BEGIN
    DROP TABLE [MST_SupplierTaxProfile];
END;
GO

IF EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101165945_BillingFinance'
)
BEGIN
    DROP TABLE [MST_TdsSection];
END;
GO

IF EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101165945_BillingFinance'
)
BEGIN
    DROP TABLE [TRN_BankImportBatch];
END;
GO

IF EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101165945_BillingFinance'
)
BEGIN
    DROP TABLE [TRN_BankMatchAllocation];
END;
GO

IF EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101165945_BillingFinance'
)
BEGIN
    DROP TABLE [TRN_BankMatchDifference];
END;
GO

IF EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101165945_BillingFinance'
)
BEGIN
    DROP TABLE [TRN_ComplianceStatus];
END;
GO

IF EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101165945_BillingFinance'
)
BEGIN
    DROP TABLE [TRN_ExpenseControl];
END;
GO

IF EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101165945_BillingFinance'
)
BEGIN
    DROP TABLE [TRN_ExpenseReceipt];
END;
GO

IF EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101165945_BillingFinance'
)
BEGIN
    DROP TABLE [TRN_Gstr2bLine];
END;
GO

IF EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101165945_BillingFinance'
)
BEGIN
    DROP TABLE [TRN_GstReturn];
END;
GO

IF EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101165945_BillingFinance'
)
BEGIN
    DROP TABLE [TRN_InvoicePayment];
END;
GO

IF EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101165945_BillingFinance'
)
BEGIN
    DROP TABLE [TRN_PeriodLock];
END;
GO

IF EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101165945_BillingFinance'
)
BEGIN
    DROP TABLE [TRN_PettyCashEntry];
END;
GO

IF EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101165945_BillingFinance'
)
BEGIN
    DROP TABLE [TRN_PurchaseBillAttachment];
END;
GO

IF EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101165945_BillingFinance'
)
BEGIN
    DROP TABLE [TRN_PurchaseBillEvent];
END;
GO

IF EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101165945_BillingFinance'
)
BEGIN
    DROP TABLE [TRN_PurchaseBillLine];
END;
GO

IF EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101165945_BillingFinance'
)
BEGIN
    DROP TABLE [TRN_RecurringExpense];
END;
GO

IF EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101165945_BillingFinance'
)
BEGIN
    DROP TABLE [TRN_ReminderLog];
END;
GO

IF EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101165945_BillingFinance'
)
BEGIN
    DROP TABLE [TRN_TdsEntry];
END;
GO

IF EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101165945_BillingFinance'
)
BEGIN
    DROP TABLE [TRN_VendorPaymentAllocation];
END;
GO

IF EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101165945_BillingFinance'
)
BEGIN
    DROP TABLE [MST_ComplianceObligation];
END;
GO

IF EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101165945_BillingFinance'
)
BEGIN
    DROP TABLE [MST_Department];
END;
GO

IF EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101165945_BillingFinance'
)
BEGIN
    DROP TABLE [TRN_TdsChallan];
END;
GO

IF EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101165945_BillingFinance'
)
BEGIN
    DROP TABLE [TRN_PurchaseBill];
END;
GO

IF EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101165945_BillingFinance'
)
BEGIN
    DROP TABLE [TRN_VendorPayment];
END;
GO

IF EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101165945_BillingFinance'
)
BEGIN
    DELETE FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101165945_BillingFinance';
END;
GO

COMMIT;
GO

