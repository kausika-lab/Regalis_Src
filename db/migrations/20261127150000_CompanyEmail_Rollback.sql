/* 20261127150000_CompanyEmail_Rollback.sql
   Undoes 20261127150000_CompanyEmail.sql: Company.Email goes back to enquiry@amstag.in. */
SET NOCOUNT ON;
SET XACT_ABORT ON;
BEGIN TRAN;

UPDATE MST_AppSetting
   SET SettingValue = N'enquiry@amstag.in', ModifiedDate = GETUTCDATE()
 WHERE SettingKey = N'Company.Email'
   AND ISNULL(SettingValue, N'') <> N'enquiry@amstag.in';
PRINT CONCAT('Company.Email restored: ', @@ROWCOUNT);

COMMIT;
