/* ---------------------------------------------------------------------------
   Rollback for 20260908000000_AddUserProfileFields.

   Drops ONLY the nine columns that migration added. It never touches the
   pre-existing Address column or any other column, index or row.

   WARNING: dropping a column destroys the data in it. Run this only if the
   columns are still empty, or after exporting whatever they hold.
--------------------------------------------------------------------------- */

SET NOCOUNT ON;
SET XACT_ABORT ON;
GO

BEGIN TRANSACTION;
GO

IF COL_LENGTH('dbo.MST_User', 'Gstin')        IS NOT NULL ALTER TABLE dbo.MST_User DROP COLUMN Gstin;
GO
IF COL_LENGTH('dbo.MST_User', 'Gender')       IS NOT NULL ALTER TABLE dbo.MST_User DROP COLUMN Gender;
GO
IF COL_LENGTH('dbo.MST_User', 'DateOfBirth')  IS NOT NULL ALTER TABLE dbo.MST_User DROP COLUMN DateOfBirth;
GO
IF COL_LENGTH('dbo.MST_User', 'PostalCode')   IS NOT NULL ALTER TABLE dbo.MST_User DROP COLUMN PostalCode;
GO
IF COL_LENGTH('dbo.MST_User', 'Country')      IS NOT NULL ALTER TABLE dbo.MST_User DROP COLUMN Country;
GO
IF COL_LENGTH('dbo.MST_User', 'State')        IS NOT NULL ALTER TABLE dbo.MST_User DROP COLUMN [State];
GO
IF COL_LENGTH('dbo.MST_User', 'City')         IS NOT NULL ALTER TABLE dbo.MST_User DROP COLUMN City;
GO
IF COL_LENGTH('dbo.MST_User', 'AddressLine2') IS NOT NULL ALTER TABLE dbo.MST_User DROP COLUMN AddressLine2;
GO
IF COL_LENGTH('dbo.MST_User', 'AddressLine1') IS NOT NULL ALTER TABLE dbo.MST_User DROP COLUMN AddressLine1;
GO

COMMIT TRANSACTION;
GO
