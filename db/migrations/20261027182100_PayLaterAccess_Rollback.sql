-- Rollback of 20261027182100_PayLaterAccess.sql: removes the Pay Later sidebar entries,
-- permissions and their grants, settings and number sequences. A sequence that has issued
-- numbers (CurrentValue > 0) is kept, so receipt numbers are never reused.
-- Safe to run more than once. ASCII only.
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO
SET XACT_ABORT ON;
BEGIN TRANSACTION;

DELETE rm FROM MAP_RoleMenu rm
JOIN MST_Menu m ON m.MenuId = rm.MenuId
WHERE m.MenuCode IN (N'ADM_FINANCE_CASH', N'ADM_FINANCE_CASH_REGISTER');
DELETE FROM MST_Menu WHERE MenuCode IN (N'ADM_FINANCE_CASH', N'ADM_FINANCE_CASH_REGISTER');

DELETE rp FROM MAP_RolePermission rp
JOIN MST_Permission p ON p.PermissionId = rp.PermissionId
WHERE p.PermissionCode IN (N'PAYMENT.CASH_COLLECT', N'PAYMENT.CASH_CLOSE', N'PAYMENT.CASH_VOID', N'PAYMENT.CASH_APPROVE');
DELETE FROM MST_Permission
WHERE PermissionCode IN (N'PAYMENT.CASH_COLLECT', N'PAYMENT.CASH_CLOSE', N'PAYMENT.CASH_VOID', N'PAYMENT.CASH_APPROVE');

DELETE FROM MST_AppSetting WHERE SettingKey LIKE N'PayLater.%' OR SettingKey = N'Wallet.DeskAddMoneyEnabled';

DELETE FROM MST_NumberSequence WHERE SequenceKey IN (N'CashReceipt', N'CashSession') AND CurrentValue = 0;

COMMIT TRANSACTION;

SELECT
    (SELECT COUNT(*) FROM MST_Permission WHERE PermissionCode LIKE N'PAYMENT.CASH%') AS Permissions,
    (SELECT COUNT(*) FROM MST_Menu WHERE MenuCode LIKE N'ADM_FINANCE_CASH%') AS Menus,
    (SELECT COUNT(*) FROM MST_AppSetting WHERE SettingKey LIKE N'PayLater.%') AS Settings,
    (SELECT COUNT(*) FROM MST_NumberSequence WHERE SequenceKey IN (N'CashReceipt', N'CashSession')) AS Sequences;
