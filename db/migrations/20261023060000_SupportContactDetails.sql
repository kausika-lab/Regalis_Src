/* 20261023060000_SupportContactDetails.sql
   Support & Contact details (23 Oct 2026).

   The customer Support & Contact card reads MST_AppSetting (group Support). New
   values as given on 23 Oct 2026:
     Support.ContactNumber  7483375555
     Support.Email          info@regalis.co.in
     Support.Hours          Monday - Saturday, 9 AM - 6 PM
   Company.Email (invoices, letterheads) is not touched.

   Idempotent: re-running changes nothing.
   Rollback: 20261023060000_SupportContactDetails_Rollback.sql */
SET NOCOUNT ON;
SET XACT_ABORT ON;
BEGIN TRAN;

DECLARE @v TABLE (K NVARCHAR(100) NOT NULL, V NVARCHAR(500) NOT NULL);
INSERT INTO @v (K, V) VALUES
    (N'Support.ContactNumber', N'7483375555'),
    (N'Support.Email',         N'info@regalis.co.in'),
    (N'Support.Hours',         N'Monday - Saturday, 9 AM - 6 PM');

UPDATE s
   SET SettingValue = v.V, ModifiedDate = GETUTCDATE()
  FROM MST_AppSetting s
  JOIN @v v ON v.K = s.SettingKey
 WHERE s.SettingValue <> v.V;
PRINT CONCAT('Support settings changed: ', @@ROWCOUNT);

IF EXISTS (SELECT 1 FROM @v v WHERE NOT EXISTS (SELECT 1 FROM MST_AppSetting s WHERE s.SettingKey = v.K))
    THROW 50000, 'A Support.* setting row is missing; the API seeds them, nothing was changed.', 1;

COMMIT;
