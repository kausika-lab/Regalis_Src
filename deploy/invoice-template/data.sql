-- New invoice template (02 Oct 2026). ASCII only. Idempotent.
SET NOCOUNT ON;
SET XACT_ABORT ON;
BEGIN TRAN;

-- 1. Settings the new template reads (empty = that part does not print).
INSERT INTO MST_AppSetting (SettingKey, SettingValue, SettingGroup, DataType, Description, IsEditableByAdmin, IsSecret, CreatedDate, IsActive, IsDeleted)
SELECT v.k, N'', v.g, 'string', v.d, 1, 0, SYSUTCDATETIME(), 1, 0
FROM (VALUES
  ('Company.BankAccountName',   'Company', 'Bank account name printed on invoices'),
  ('Company.BankAccountNumber', 'Company', 'Bank account number printed on invoices'),
  ('Company.BankIfsc',          'Company', 'Bank IFSC code printed on invoices'),
  ('Company.BankBranch',        'Company', 'Bank and branch printed on invoices'),
  ('Invoice.TermsAndConditions','Invoice', 'Terms printed on invoices that have none of their own')
) v(k, g, d)
WHERE NOT EXISTS (SELECT 1 FROM MST_AppSetting s WHERE s.SettingKey = v.k);

-- 2. Booking and pass invoices never stored the customer's GSTIN; copy it from the
--    profile. The ids are kept so this can be undone.
IF OBJECT_ID('dbo.BAK_InvoiceGstinBackfill_20261002') IS NULL
  CREATE TABLE dbo.BAK_InvoiceGstinBackfill_20261002 (InvoiceId bigint PRIMARY KEY, OldGstin nvarchar(20) NULL);

INSERT INTO dbo.BAK_InvoiceGstinBackfill_20261002 (InvoiceId, OldGstin)
SELECT i.InvoiceId, i.CustomerGstin
FROM TRN_Invoice i JOIN MST_User u ON u.UserId = i.CustomerId
WHERE ISNULL(i.CustomerGstin, '') = '' AND i.QuotationId IS NULL AND ISNULL(u.Gstin, '') <> ''
  AND NOT EXISTS (SELECT 1 FROM dbo.BAK_InvoiceGstinBackfill_20261002 b WHERE b.InvoiceId = i.InvoiceId);

UPDATE i SET CustomerGstin = UPPER(LTRIM(RTRIM(u.Gstin)))
FROM TRN_Invoice i JOIN MST_User u ON u.UserId = i.CustomerId
JOIN dbo.BAK_InvoiceGstinBackfill_20261002 b ON b.InvoiceId = i.InvoiceId
WHERE ISNULL(i.CustomerGstin, '') = '';

COMMIT;
SELECT (SELECT COUNT(*) FROM dbo.BAK_InvoiceGstinBackfill_20261002) AS InvoicesGivenGstin,
       (SELECT COUNT(*) FROM MST_AppSetting WHERE SettingKey IN ('Company.BankAccountName','Company.BankAccountNumber','Company.BankIfsc','Company.BankBranch','Invoice.TermsAndConditions')) AS NewSettings;
