/* ===========================================================================
   Production database access: only the production API's app pool.

   The restore brought UAT's database users along (IIS APPPOOL\regalis-api,
   regalis_uat). They are dropped so nothing UAT runs can touch production.

   Run: sqlcmd -S localhost -d RegalisWorkSpace_PROD -E -C -I -b -i 03-database-access.sql
   =========================================================================== */
SET NOCOUNT ON;

IF DB_NAME() <> N'RegalisWorkSpace_PROD'
BEGIN
    RAISERROR(N'This script only runs against RegalisWorkSpace_PROD.', 16, 1);
    RETURN;
END

IF USER_ID(N'IIS APPPOOL\regalis-api') IS NOT NULL DROP USER [IIS APPPOOL\regalis-api];
IF USER_ID(N'regalis_uat')             IS NOT NULL DROP USER [regalis_uat];

IF SUSER_ID(N'IIS APPPOOL\regalis-prod-api') IS NULL
    CREATE LOGIN [IIS APPPOOL\regalis-prod-api] FROM WINDOWS WITH DEFAULT_DATABASE = [RegalisWorkSpace_PROD];
IF USER_ID(N'IIS APPPOOL\regalis-prod-api') IS NULL
    CREATE USER [IIS APPPOOL\regalis-prod-api] FOR LOGIN [IIS APPPOOL\regalis-prod-api];

ALTER ROLE db_datareader ADD MEMBER [IIS APPPOOL\regalis-prod-api];
ALTER ROLE db_datawriter ADD MEMBER [IIS APPPOOL\regalis-prod-api];

PRINT N'Access granted to IIS APPPOOL\regalis-prod-api.';
