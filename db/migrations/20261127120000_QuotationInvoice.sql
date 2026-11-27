-- Quotation -> invoice -> payment fixes (27 Nov 2026; kit deploy/quotation-invoice).
-- Two database guarantees behind the code changes. No table or column is added or changed, and no
-- row is written, updated or deleted.
--   B2  one invoice per quotation: UX_Invoice_Quotation, unique on TRN_Invoice.QuotationId where it is
--       set. It already exists wherever the EF model was migrated (Dev has it); created here only if
--       missing, and only if no quotation has two invoices.
--   B4  the same manual reference cannot be recorded twice on one invoice and mode while the first
--       receipt stands: UX_InvoicePayment_ManualReference on TRN_InvoicePayment (InvoiceId, Mode,
--       Reference), filtered to receipts that are not reversals, not reversed, not deleted and carry a
--       reference. The API refuses it first with a clear 409; this index is the backstop.
-- Needs the Billing & Finance migration (TRN_InvoicePayment) first. Stops without changing anything if
-- existing rows would break either index (then reverse the duplicate receipt first).
--
-- Idempotent; one transaction. ASCII only.
--   sqlcmd -S localhost -d RegalisWorkSpace_UAT -E -C -I -b -i 20261127120000_QuotationInvoice.sql
-- Rollback: 20261127120000_QuotationInvoice_Rollback.sql
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
SET ANSI_PADDING ON;
SET ANSI_WARNINGS ON;
SET ARITHABORT ON;
SET CONCAT_NULL_YIELDS_NULL ON;
SET NUMERIC_ROUNDABORT OFF;
SET NOCOUNT ON;
GO
SET XACT_ABORT ON;
BEGIN TRANSACTION;

IF OBJECT_ID(N'dbo.TRN_InvoicePayment', N'U') IS NULL
BEGIN
    RAISERROR('TRN_InvoicePayment not found - run the Billing & Finance migration (20261101165945_BillingFinance.sql) first. Nothing changed.', 16, 1);
    ROLLBACK TRANSACTION;
    RETURN;
END;

-- B2: one invoice per quotation.
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UX_Invoice_Quotation' AND object_id = OBJECT_ID(N'dbo.TRN_Invoice'))
BEGIN
    IF EXISTS (SELECT QuotationId FROM dbo.TRN_Invoice WHERE QuotationId IS NOT NULL GROUP BY QuotationId HAVING COUNT(*) > 1)
    BEGIN
        RAISERROR('A quotation has more than one invoice (SELECT QuotationId FROM TRN_Invoice GROUP BY QuotationId HAVING COUNT(*) > 1). Resolve it first. Nothing changed.', 16, 1);
        ROLLBACK TRANSACTION;
        RETURN;
    END;
    CREATE UNIQUE INDEX UX_Invoice_Quotation ON dbo.TRN_Invoice (QuotationId) WHERE QuotationId IS NOT NULL;
    PRINT 'UX_Invoice_Quotation created.';
END
ELSE
    PRINT 'UX_Invoice_Quotation already there.';

-- B4: no duplicate manual reference on a standing receipt.
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UX_InvoicePayment_ManualReference' AND object_id = OBJECT_ID(N'dbo.TRN_InvoicePayment'))
BEGIN
    IF EXISTS (SELECT 1 FROM dbo.TRN_InvoicePayment
               WHERE ReversalOfId IS NULL AND IsReversed = 0 AND IsDeleted = 0 AND Reference IS NOT NULL
               GROUP BY InvoiceId, Mode, Reference HAVING COUNT(*) > 1)
    BEGIN
        RAISERROR('Some invoices already have two standing receipts with the same mode and reference (see the B4 query in deploy/quotation-invoice/README.md). Reverse the duplicate first. Nothing changed.', 16, 1);
        ROLLBACK TRANSACTION;
        RETURN;
    END;
    CREATE UNIQUE INDEX UX_InvoicePayment_ManualReference ON dbo.TRN_InvoicePayment (InvoiceId, Mode, Reference)
        WHERE ReversalOfId IS NULL AND IsReversed = 0 AND IsDeleted = 0 AND Reference IS NOT NULL;
    PRINT 'UX_InvoicePayment_ManualReference created.';
END
ELSE
    PRINT 'UX_InvoicePayment_ManualReference already there.';

COMMIT TRANSACTION;
PRINT 'QuotationInvoice migration applied.';
