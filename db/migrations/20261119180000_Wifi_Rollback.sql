-- Rollback of 20261119180000_Wifi.sql. Run it together with going back to an API without the Wi-Fi kit.
-- WARNING: drops the Wi-Fi tables, so every access issued, device and session record is lost, and the
-- passwords in the Wi-Fi controller are NOT removed by this script (take them out on the controller).
-- Idempotent; one transaction. ASCII only.
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
SET NOCOUNT ON;
GO
SET XACT_ABORT ON;
BEGIN TRANSACTION;

DELETE rm FROM dbo.MAP_RoleMenu rm JOIN dbo.MST_Menu m ON m.MenuId = rm.MenuId WHERE m.MenuCode IN (N'ADM_ACCESS_WIFI', N'CUST_WIFI');
DELETE FROM dbo.MST_Menu WHERE MenuCode IN (N'ADM_ACCESS_WIFI', N'CUST_WIFI');
-- The Device / Access Control parent was given to Support only for Wi-Fi: take it back from every role
-- that holds no ACCESS.* permission (before the kit only Administrator and Operations Manager had it,
-- both through ACCESS.VIEW). The System band is left: every staff role already had it.
DELETE rm FROM dbo.MAP_RoleMenu rm
JOIN dbo.MST_Menu m ON m.MenuId = rm.MenuId AND m.MenuCode = N'ADM_ACCESS'
WHERE NOT EXISTS (SELECT 1 FROM dbo.MAP_RolePermission rp JOIN dbo.MST_Permission p ON p.PermissionId = rp.PermissionId
                  WHERE rp.RoleId = rm.RoleId AND p.PermissionCode LIKE N'ACCESS.%');

DELETE rp FROM dbo.MAP_RolePermission rp JOIN dbo.MST_Permission p ON p.PermissionId = rp.PermissionId WHERE p.PermissionCode LIKE N'WIFI.%';
DELETE FROM dbo.MST_Permission WHERE PermissionCode LIKE N'WIFI.%';
DELETE FROM dbo.MST_NumberSequence WHERE SequenceKey = N'WifiAccess';

IF OBJECT_ID(N'dbo.TRN_WifiSimEvent', N'U') IS NOT NULL DROP TABLE dbo.TRN_WifiSimEvent;
IF OBJECT_ID(N'dbo.TRN_WifiSimClient', N'U') IS NOT NULL DROP TABLE dbo.TRN_WifiSimClient;
IF OBJECT_ID(N'dbo.TRN_WifiSimKey', N'U') IS NOT NULL DROP TABLE dbo.TRN_WifiSimKey;
IF OBJECT_ID(N'dbo.MST_WifiSetting', N'U') IS NOT NULL DROP TABLE dbo.MST_WifiSetting;
IF OBJECT_ID(N'dbo.TRN_WifiBlock', N'U') IS NOT NULL DROP TABLE dbo.TRN_WifiBlock;
IF OBJECT_ID(N'dbo.TRN_WifiEvent', N'U') IS NOT NULL DROP TABLE dbo.TRN_WifiEvent;
IF OBJECT_ID(N'dbo.TRN_WifiSession', N'U') IS NOT NULL DROP TABLE dbo.TRN_WifiSession;
IF OBJECT_ID(N'dbo.TRN_WifiDevice', N'U') IS NOT NULL DROP TABLE dbo.TRN_WifiDevice;
IF OBJECT_ID(N'dbo.TRN_WifiAccessSource', N'U') IS NOT NULL DROP TABLE dbo.TRN_WifiAccessSource;
IF OBJECT_ID(N'dbo.TRN_WifiAccess', N'U') IS NOT NULL DROP TABLE dbo.TRN_WifiAccess;

COMMIT TRANSACTION;
PRINT 'Wi-Fi rolled back.';
