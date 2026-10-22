/* 20261023060000_SupportContactDetails_Rollback.sql
   Undoes 20261023060000_SupportContactDetails.sql: the Support & Contact card
   goes back to the values it showed before (UAT and production were the same). */
SET NOCOUNT ON;
SET XACT_ABORT ON;
BEGIN TRAN;

DECLARE @v TABLE (K NVARCHAR(100) NOT NULL, V NVARCHAR(500) NOT NULL);
INSERT INTO @v (K, V) VALUES
    (N'Support.ContactNumber', N'7483337555'),
    (N'Support.Email',         N'enquiry@amstag.in'),
    (N'Support.Hours',         N'Mon-Sat 09:00-19:00 IST');

UPDATE s
   SET SettingValue = v.V, ModifiedDate = GETUTCDATE()
  FROM MST_AppSetting s
  JOIN @v v ON v.K = s.SettingKey
 WHERE s.SettingValue <> v.V;
PRINT CONCAT('Support settings restored: ', @@ROWCOUNT);

COMMIT;
