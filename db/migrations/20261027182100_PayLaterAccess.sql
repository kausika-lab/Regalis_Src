-- Pay Later (cash, UPI or card at the front desk): permissions, sidebar, settings and
-- number sequences. Design: "Pay Later (Cash) - Feature Design & Implementation Plan",
-- decisions of 27 Oct 2026.
--
-- The tables and columns (TRN_CashCollection, TRN_CashRegisterSession, TRN_Booking.IsPayLater
-- ...) come from the EF migration in the backend source, not from this file. Run this one
-- after it, in the same deploy: the sidebar entries point at pages that ship with that build.
--
--   PAYMENT.CASH_COLLECT  record a desk collection against a pay-later booking
--   PAYMENT.CASH_CLOSE    open / close a register session (day close)
--   PAYMENT.CASH_VOID     void a receipt (reason required)          - ADMIN only
--   PAYMENT.CASH_APPROVE  approve a day close and its variance       - ADMIN only
--
-- Grants: ADMIN all four; FINANCE and MANAGER collect + close. Change who holds what in
-- Roles & Permissions, no deploy needed. Sidebar: two children under Finance > Billing &
-- Finance, for ADMIN, FINANCE and MANAGER (the sidebar is filtered by role).
--
-- Settings: PayLater.Enabled ships false and Wallet.DeskAddMoneyEnabled ships true, so
-- nothing changes on deploy. Go-live = flip both. No amount limit (0 = none), 2 open
-- pay-later bookings per customer, check-in blocked until paid.
--
-- Idempotent; one transaction. ASCII only (sqlcmd reads a BOM-less file as ANSI).
--   sqlcmd -S localhost -d RegalisWorkSpace_UAT -E -C -I -b -i 20261027182100_PayLaterAccess.sql
-- Rollback: 20261027182100_PayLaterAccess_Rollback.sql
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO
SET XACT_ABORT ON;
BEGIN TRANSACTION;

DECLARE @now datetime2(3) = SYSUTCDATETIME();

-- 1. Permissions
DECLARE @perm TABLE (Code nvarchar(100), ActionName nvarchar(50), Description nvarchar(200), DisplayOrder int);
INSERT INTO @perm VALUES
    (N'PAYMENT.CASH_COLLECT', N'CASH_COLLECT', N'Record a desk collection (cash, UPI or card) against a pay-later booking', 43),
    (N'PAYMENT.CASH_CLOSE', N'CASH_CLOSE', N'Open and close a cash register session', 44),
    (N'PAYMENT.CASH_VOID', N'CASH_VOID', N'Void a desk collection receipt', 45),
    (N'PAYMENT.CASH_APPROVE', N'CASH_APPROVE', N'Approve a day close and its variance', 46);

INSERT INTO MST_Permission (PermissionCode, ModuleName, ActionName, Description, DisplayOrder, CreatedDate, IsActive, IsDeleted)
SELECT p.Code, N'PAYMENT', p.ActionName, p.Description, p.DisplayOrder, @now, 1, 0
FROM @perm p
WHERE NOT EXISTS (SELECT 1 FROM MST_Permission x WHERE x.PermissionCode = p.Code);

-- 2. Role grants
DECLARE @grant TABLE (RoleCode nvarchar(50), Code nvarchar(100));
INSERT INTO @grant VALUES
    (N'ADMIN', N'PAYMENT.CASH_COLLECT'), (N'ADMIN', N'PAYMENT.CASH_CLOSE'),
    (N'ADMIN', N'PAYMENT.CASH_VOID'), (N'ADMIN', N'PAYMENT.CASH_APPROVE'),
    (N'FINANCE', N'PAYMENT.CASH_COLLECT'), (N'FINANCE', N'PAYMENT.CASH_CLOSE'),
    (N'MANAGER', N'PAYMENT.CASH_COLLECT'), (N'MANAGER', N'PAYMENT.CASH_CLOSE');

INSERT INTO MAP_RolePermission (RoleId, PermissionId)
SELECT r.RoleId, p.PermissionId
FROM @grant g
JOIN MST_Role r ON r.RoleCode = g.RoleCode
JOIN MST_Permission p ON p.PermissionCode = g.Code
WHERE NOT EXISTS (SELECT 1 FROM MAP_RolePermission x WHERE x.RoleId = r.RoleId AND x.PermissionId = p.PermissionId);

-- 3. Sidebar children under ADM_FINANCE (Billing & Finance)
DECLARE @parent int = (SELECT MenuId FROM MST_Menu WHERE MenuCode = N'ADM_FINANCE' AND IsDeleted = 0);
IF @parent IS NULL
BEGIN
    RAISERROR('ADM_FINANCE menu not found - nothing changed.', 16, 1);
    ROLLBACK TRANSACTION;
    RETURN;
END;

DECLARE @menu TABLE (Code nvarchar(100), Display nvarchar(100), Route nvarchar(200), Icon nvarchar(50), Perm nvarchar(100), DisplayOrder int);
INSERT INTO @menu VALUES
    (N'ADM_FINANCE_CASH', N'Cash Collections', N'/admin/billing/cash-collections', N'Banknote', N'PAYMENT.CASH_COLLECT', 25),
    (N'ADM_FINANCE_CASH_REGISTER', N'Cash Register', N'/admin/billing/cash-register', N'Calculator', N'PAYMENT.CASH_CLOSE', 26);

INSERT INTO MST_Menu (ParentMenuId, MenuCode, MenuName, DisplayName, Route, Icon, DisplayOrder, PermissionId, PortalType, MenuLevel, IsVisible, CreatedDate, IsActive, IsDeleted)
SELECT @parent, m.Code, m.Display, m.Display, m.Route, m.Icon, m.DisplayOrder,
       (SELECT PermissionId FROM MST_Permission WHERE PermissionCode = m.Perm), 2, 2, 1, @now, 1, 0
FROM @menu m
WHERE NOT EXISTS (SELECT 1 FROM MST_Menu x WHERE x.MenuCode = m.Code AND x.IsDeleted = 0);

INSERT INTO MAP_RoleMenu (RoleId, MenuId)
SELECT r.RoleId, child.MenuId
FROM MST_Role r
JOIN MST_Menu child ON child.ParentMenuId = @parent AND child.IsDeleted = 0
WHERE child.MenuCode IN (N'ADM_FINANCE_CASH', N'ADM_FINANCE_CASH_REGISTER')
  AND r.RoleCode IN (N'ADMIN', N'FINANCE', N'MANAGER')
  AND NOT EXISTS (SELECT 1 FROM MAP_RoleMenu x WHERE x.RoleId = r.RoleId AND x.MenuId = child.MenuId);

-- 4. Settings (UAT and production run with Database__SeedOnStartup=false)
DECLARE @set TABLE (SettingKey nvarchar(120), SettingValue nvarchar(200), DataType nvarchar(20), Description nvarchar(300));
INSERT INTO @set VALUES
    (N'PayLater.Enabled', N'false', N'bool', N'Offer "Pay later at the front desk" at checkout and in admin booking.'),
    (N'PayLater.AllowedBookingKinds', N'Hourly,MultiDay,Monthly', N'string', N'Booking kinds that may pay later: any of Hourly, MultiDay, Monthly, comma-separated.'),
    (N'PayLater.MaxAmount', N'0', N'decimal', N'Largest single pay-later booking, incl. GST. 0 = no limit.'),
    (N'PayLater.MaxOutstandingPerCustomer', N'0', N'decimal', N'Largest total a customer may owe on pay-later bookings. 0 = no limit.'),
    (N'PayLater.MaxOpenBookings', N'2', N'int', N'Unpaid pay-later bookings a customer may hold at once. 0 = no limit.'),
    (N'PayLater.DueBeforeStartMinutes', N'0', N'int', N'Payment is due this many minutes before the booking starts. 0 = at check-in.'),
    (N'PayLater.AutoCancelAfterStartMinutes', N'30', N'int', N'Release an unpaid pay-later booking this many minutes after its start. 0 = never.'),
    (N'PayLater.BlockCheckInUntilPaid', N'true', N'bool', N'Refuse check-in while a pay-later booking still has a balance.'),
    (N'PayLater.ReminderHoursBefore', N'24', N'int', N'Send the customer a payment reminder this many hours before the start. 0 = no reminder.'),
    (N'PayLater.AutoBlockAfterUnpaidNoShows', N'3', N'int', N'Switch pay later off for a customer after this many unpaid bookings were released. 0 = never.');

-- Wallet top-up at the desk (Admin > Wallet > Add money). Decision of 27 Oct 2026: wallets
-- are topped up online only. Ships ON so desk operations do not change on deploy; switch it
-- to false on the day PayLater.Enabled goes true (customers then pay at the desk through
-- pay later instead).
INSERT INTO @set VALUES
    (N'Wallet.DeskAddMoneyEnabled', N'true', N'bool', N'Allow staff to add money to a customer wallet at the desk. Wallets are topped up online only once pay later is live.');

INSERT INTO MST_AppSetting (SettingKey, SettingValue, SettingGroup, DataType, Description,
                            IsEditableByAdmin, IsSecret, CreatedDate, IsActive, IsDeleted)
SELECT s.SettingKey, s.SettingValue, LEFT(s.SettingKey, CHARINDEX(N'.', s.SettingKey) - 1), s.DataType, s.Description, 1, 0, @now, 1, 0
FROM @set s
WHERE NOT EXISTS (SELECT 1 FROM MST_AppSetting x WHERE x.SettingKey = s.SettingKey);

-- 5. Number sequences: receipts follow the invoice style (financial-year prefixed)
INSERT INTO MST_NumberSequence (SequenceKey, Prefix, CurrentValue, FinancialYear)
SELECT v.SequenceKey, v.Prefix, 0, v.FinancialYear
FROM (VALUES (N'CashReceipt', N'REG/RCPT', N'2026-27'), (N'CashSession', N'CRS-', NULL)) v (SequenceKey, Prefix, FinancialYear)
WHERE NOT EXISTS (SELECT 1 FROM MST_NumberSequence x WHERE x.SequenceKey = v.SequenceKey);

COMMIT TRANSACTION;

SELECT p.PermissionCode, COUNT(rp.RoleId) AS Roles
FROM MST_Permission p LEFT JOIN MAP_RolePermission rp ON rp.PermissionId = p.PermissionId
WHERE p.PermissionCode LIKE N'PAYMENT.CASH%' GROUP BY p.PermissionCode ORDER BY p.PermissionCode;
SELECT m.MenuCode, m.Route, COUNT(rm.RoleId) AS Roles
FROM MST_Menu m LEFT JOIN MAP_RoleMenu rm ON rm.MenuId = m.MenuId
WHERE m.MenuCode LIKE N'ADM_FINANCE_CASH%' AND m.IsDeleted = 0 GROUP BY m.MenuCode, m.Route ORDER BY m.MenuCode;
SELECT SettingKey, SettingValue FROM MST_AppSetting WHERE SettingKey LIKE N'PayLater.%' OR SettingKey = N'Wallet.DeskAddMoneyEnabled' ORDER BY SettingKey;
SELECT SequenceKey, Prefix, CurrentValue, FinancialYear FROM MST_NumberSequence WHERE SequenceKey IN (N'CashReceipt', N'CashSession');
