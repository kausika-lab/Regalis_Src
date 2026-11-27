-- Rollback of 20261127120000_QuotationInvoice.sql. Drops UX_InvoicePayment_ManualReference only.
-- UX_Invoice_Quotation is part of the EF model (InvoiceConfiguration) and existed before this kit
-- wherever the model was migrated, so it is NOT dropped. No data is touched.
-- Idempotent; one transaction. ASCII only.
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
SET NOCOUNT ON;
GO
SET XACT_ABORT ON;
BEGIN TRANSACTION;

IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UX_InvoicePayment_ManualReference' AND object_id = OBJECT_ID(N'dbo.TRN_InvoicePayment'))
    DROP INDEX UX_InvoicePayment_ManualReference ON dbo.TRN_InvoicePayment;

COMMIT TRANSACTION;
PRINT 'QuotationInvoice rolled back.';
