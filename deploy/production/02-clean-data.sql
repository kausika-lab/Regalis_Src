/* ===========================================================================
   Production database: keep master data, remove UAT test data.

   KEEPS   staff accounts (admins + housekeeping) and their roles, profiles,
           notification settings; locations, workspaces, workspace types,
           pass/membership plans, menus, roles, permissions, app settings,
           ticket categories/issue types, active banners.
   REMOVES every customer account and wallet; every TRN_ row (bookings,
           invoices, payments, passes, quotations, POs, expenses, tickets,
           audit/login/notification logs, tokens); inventory items and
           suppliers (UAT test stock); soft-deleted workspaces and banners;
           the BAK_ tables left by UAT fixes.
   RESETS  document numbers (booking, invoice, payment, ...) to start at 1,
           workspace status Occupied/Maintenance back to Available, and the
           two settings that pointed at UAT.

   Soft-deleted menus, permissions, settings and ticket categories are kept on
   purpose: they are retirement markers from earlier migrations.

   One transaction: any error rolls everything back.
   Run: sqlcmd -S localhost -d RegalisWorkSpace_PROD -E -C -I -b -i 02-clean-data.sql
   =========================================================================== */
SET NOCOUNT ON;
SET XACT_ABORT ON;

IF DB_NAME() <> N'RegalisWorkSpace_PROD'
BEGIN
    DECLARE @db sysname = DB_NAME();
    RAISERROR(N'This script only runs against RegalisWorkSpace_PROD (current: %s).', 16, 1, @db);
    RETURN;
END

BEGIN TRANSACTION;

-- 1. UAT fix-up backup tables -------------------------------------------------
DECLARE @sql nvarchar(max) = N'';
SELECT @sql += N'DROP TABLE ' + QUOTENAME(s.name) + N'.' + QUOTENAME(t.name) + N';'
FROM sys.tables t JOIN sys.schemas s ON s.schema_id = t.schema_id
WHERE t.name LIKE N'BAK[_]%';
EXEC (@sql);

-- 2. Constraints off while rows go in any order; re-checked in step 9 --------
EXEC sp_MSforeachtable N'ALTER TABLE ? NOCHECK CONSTRAINT ALL';

-- Tables that end up empty get their identity reseeded in step 8.
DECLARE @emptied TABLE (name sysname PRIMARY KEY);

-- 3. Every transaction table ---------------------------------------------------
SET @sql = N'';
SELECT @sql += N'DELETE FROM ' + QUOTENAME(s.name) + N'.' + QUOTENAME(t.name) + N';'
FROM sys.tables t JOIN sys.schemas s ON s.schema_id = t.schema_id
WHERE t.name LIKE N'TRN[_]%';
EXEC (@sql);
INSERT @emptied SELECT name FROM sys.tables WHERE name LIKE N'TRN[_]%';

-- 4. Customers and everything hanging off them ------------------------------
DECLARE @customers TABLE (UserId bigint PRIMARY KEY);
INSERT @customers SELECT UserId FROM dbo.MST_User WHERE UserType = 1;

DELETE FROM dbo.MST_Wallet;
DELETE FROM dbo.MAP_UserRole                 WHERE UserId IN (SELECT UserId FROM @customers);
DELETE FROM dbo.MST_AdminNotificationSetting WHERE UserId IN (SELECT UserId FROM @customers);
DELETE FROM dbo.MST_HousekeeperProfile       WHERE UserId IN (SELECT UserId FROM @customers);
DELETE FROM dbo.MST_User                     WHERE UserId IN (SELECT UserId FROM @customers);
INSERT @emptied VALUES (N'MST_Wallet');

-- Staff keep their passwords, but must sign in afresh.
UPDATE dbo.MST_User SET FailedLoginCount = 0, LockoutEndUtc = NULL, LastLoginUtc = NULL;

-- 5. UAT test stock -----------------------------------------------------------
DELETE FROM dbo.MST_InventoryItem;
DELETE FROM dbo.MST_Supplier;
INSERT @emptied VALUES (N'MST_InventoryItem'), (N'MST_Supplier');

-- 6. Soft-deleted rows nothing needs any more -------------------------------
DELETE FROM dbo.MAP_WorkspaceAmenity WHERE WorkspaceId IN (SELECT WorkspaceId FROM dbo.MST_Workspace WHERE IsDeleted = 1);
DELETE FROM dbo.MST_Workspace WHERE IsDeleted = 1;
DELETE FROM dbo.MST_Banner    WHERE IsDeleted = 1;

-- Workspaces held by UAT bookings / maintenance start out free.
UPDATE dbo.MST_Workspace SET Status = 1 WHERE Status IN (2, 3);

-- 7. Numbers and settings ----------------------------------------------------
-- Housekeeper codes (HK-1, HK-2) belong to the kept staff profiles.
UPDATE dbo.MST_NumberSequence SET CurrentValue = 0 WHERE SequenceKey <> N'Housekeeper';

UPDATE dbo.MST_AppSetting SET SettingValue = N'C:\inetpub\wwwroot\Regalis_Prod\api\brand\regalis-logo.png'
WHERE SettingKey = N'Company.LogoPath';
UPDATE dbo.MST_AppSetting SET SettingValue = N'https://myregalis.com'
WHERE SettingKey = N'Quotation.PortalBaseUrl';

-- 8. Emptied tables number from 1 again ---------------------------------------
DECLARE @t sysname;
DECLARE c CURSOR LOCAL FAST_FORWARD FOR
    SELECT e.name FROM @emptied e
    JOIN sys.identity_columns ic ON ic.object_id = OBJECT_ID(N'dbo.' + e.name)
    WHERE ic.last_value IS NOT NULL;
OPEN c;
FETCH NEXT FROM c INTO @t;
WHILE @@FETCH_STATUS = 0
BEGIN
    DBCC CHECKIDENT (@t, RESEED, 0) WITH NO_INFOMSGS;
    FETCH NEXT FROM c INTO @t;
END
CLOSE c; DEALLOCATE c;

-- 9. Constraints back on, and every remaining row re-validated -------------
EXEC sp_MSforeachtable N'ALTER TABLE ? WITH CHECK CHECK CONSTRAINT ALL';

COMMIT TRANSACTION;
PRINT N'Production data cleaned.';
