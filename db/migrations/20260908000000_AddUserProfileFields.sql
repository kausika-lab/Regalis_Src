/* ---------------------------------------------------------------------------
   Regalis WorkSpace — MST_User structured address + DOB / gender / GSTIN
   Equivalent of EF Core migration 20260908000000_AddUserProfileFields.

   Additive and idempotent:
     * every column is NULLable, so existing rows are untouched
     * the existing Address nvarchar(500) column is NOT modified or dropped
     * no column is renamed or retyped, no index or constraint is changed
     * safe to re-run — each ALTER is guarded by COL_LENGTH

   Column types mirror conventions already in this schema:
     AddressLine1/2  MST_Location.AddressLine1/2          nvarchar(200)
     City / State    MST_Location.City / State            nvarchar(100)
     PostalCode      MST_Location.PostalCode              nvarchar(20)
     Gstin           MST_Supplier.Gstin / TRN_Invoice.CustomerGstin nvarchar(20)
     DateOfBirth     TRN_CustomerPass.ValidFrom           date
     Gender          MST_User.UserType / Status           tinyint (byte enum)

   NOTE: this script deliberately does NOT insert a __EFMigrationsHistory row.
   Read README.md in this folder first — applying DDL out of band from EF needs
   the reconciliation step described there, or `dotnet ef database update` will
   later fail trying to add columns that already exist.
--------------------------------------------------------------------------- */

SET NOCOUNT ON;
SET XACT_ABORT ON;
GO

BEGIN TRANSACTION;
GO

IF COL_LENGTH('dbo.MST_User', 'AddressLine1') IS NULL
    ALTER TABLE dbo.MST_User ADD AddressLine1 nvarchar(200) NULL;
GO
IF COL_LENGTH('dbo.MST_User', 'AddressLine2') IS NULL
    ALTER TABLE dbo.MST_User ADD AddressLine2 nvarchar(200) NULL;
GO
IF COL_LENGTH('dbo.MST_User', 'City') IS NULL
    ALTER TABLE dbo.MST_User ADD City nvarchar(100) NULL;
GO
IF COL_LENGTH('dbo.MST_User', 'State') IS NULL
    ALTER TABLE dbo.MST_User ADD [State] nvarchar(100) NULL;
GO
IF COL_LENGTH('dbo.MST_User', 'Country') IS NULL
    ALTER TABLE dbo.MST_User ADD Country nvarchar(100) NULL;
GO
IF COL_LENGTH('dbo.MST_User', 'PostalCode') IS NULL
    ALTER TABLE dbo.MST_User ADD PostalCode nvarchar(20) NULL;
GO
IF COL_LENGTH('dbo.MST_User', 'DateOfBirth') IS NULL
    ALTER TABLE dbo.MST_User ADD DateOfBirth date NULL;
GO
IF COL_LENGTH('dbo.MST_User', 'Gender') IS NULL
    ALTER TABLE dbo.MST_User ADD Gender tinyint NULL;
GO
IF COL_LENGTH('dbo.MST_User', 'Gstin') IS NULL
    ALTER TABLE dbo.MST_User ADD Gstin nvarchar(20) NULL;
GO

COMMIT TRANSACTION;
GO

/* Verification — expect 9 rows */
SELECT c.name AS ColumnName, t.name AS DataType, c.max_length, c.is_nullable
FROM sys.columns c
JOIN sys.types  t ON t.user_type_id = c.user_type_id
WHERE c.object_id = OBJECT_ID('dbo.MST_User')
  AND c.name IN ('AddressLine1','AddressLine2','City','State','Country',
                 'PostalCode','DateOfBirth','Gender','Gstin')
ORDER BY c.name;
GO
