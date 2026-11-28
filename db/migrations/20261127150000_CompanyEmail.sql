/* 20261127150000_CompanyEmail.sql
   Company email on letterheads (27 Nov 2026).

   The PDF letterheads (purchase orders, invoices, quotations) print the
   Company.Email setting under the company name. Changed from enquiry@amstag.in
   to info@regalis.co.in. Note: finance reminder emails also fall back to
   Company.Email when Reminders.FinanceEmail is empty.

   Idempotent: re-running changes nothing.
   Rollback: 20261127150000_CompanyEmail_Rollback.sql */
SET NOCOUNT ON;
SET XACT_ABORT ON;
BEGIN TRAN;

UPDATE MST_AppSetting
   SET SettingValue = N'info@regalis.co.in', ModifiedDate = GETUTCDATE()
 WHERE SettingKey = N'Company.Email'
   AND ISNULL(SettingValue, N'') <> N'info@regalis.co.in';
PRINT CONCAT('Company.Email changed: ', @@ROWCOUNT);

IF NOT EXISTS (SELECT 1 FROM MST_AppSetting WHERE SettingKey = N'Company.Email')
    PRINT 'WARNING: Company.Email setting not found; nothing changed.';

COMMIT;
SELECT SettingKey, SettingValue FROM MST_AppSetting WHERE SettingKey = N'Company.Email';
