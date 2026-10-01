-- Undo the GSTIN backfill of 02 Oct 2026 (the settings rows are harmless and stay).
UPDATE i SET CustomerGstin = b.OldGstin
FROM TRN_Invoice i JOIN dbo.BAK_InvoiceGstinBackfill_20261002 b ON b.InvoiceId = i.InvoiceId;
