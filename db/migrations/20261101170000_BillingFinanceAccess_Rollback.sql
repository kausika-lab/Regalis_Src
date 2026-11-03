-- Rollback of 20261101170000_BillingFinanceAccess.sql. Run BEFORE the EF rollback
-- (20261101165945_BillingFinance_Rollback.sql), which drops the tables.
-- Removes the menus, role grants, the two roles (only if no user holds them),
-- permissions, settings and sequences it added; restores the old sidebar order and hides
-- Reconciliation again. Master data lives in the new tables and goes with them.
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
SET NOCOUNT ON;
GO
SET XACT_ABORT ON;
BEGIN TRANSACTION;
DECLARE @now datetime2(3) = SYSUTCDATETIME();

DECLARE @codes TABLE (Code nvarchar(100));
INSERT INTO @codes VALUES (N'ADM_FINANCE_BAND_IN'), (N'ADM_FINANCE_BAND_DESK'), (N'ADM_FINANCE_BAND_OUT'), (N'ADM_FINANCE_BAND_BANK'),
    (N'ADM_FINANCE_BAND_COMPLIANCE'), (N'ADM_FINANCE_PETTY_CASH'), (N'ADM_FINANCE_BILLS'), (N'ADM_FINANCE_VENDOR_PAYMENTS'), (N'ADM_FINANCE_GST'),
    (N'ADM_FINANCE_TDS'), (N'ADM_FINANCE_COMPLIANCE');
DELETE rm FROM MAP_RoleMenu rm JOIN MST_Menu m ON m.MenuId = rm.MenuId WHERE m.MenuCode IN (SELECT Code FROM @codes);
DELETE FROM MST_Menu WHERE MenuCode IN (SELECT Code FROM @codes);

-- Reconciliation back to hidden; sidebar order back to 20260915/20261027.
DELETE rm FROM MAP_RoleMenu rm JOIN MST_Menu m ON m.MenuId = rm.MenuId WHERE m.MenuCode = N'ADM_FINANCE_BANK_RECON';
UPDATE MST_Menu SET IsActive = 0, IsDeleted = 1, DeletedDate = @now, ModifiedDate = @now WHERE MenuCode = N'ADM_FINANCE_BANK_RECON';
UPDATE m SET m.DisplayOrder = o.DisplayOrder, m.ModifiedDate = @now
FROM MST_Menu m
JOIN (VALUES (N'ADM_FINANCE_INVOICES', 10), (N'ADM_FINANCE_PAYMENTS', 20), (N'ADM_FINANCE_CASH', 25), (N'ADM_FINANCE_CASH_REGISTER', 26),
    (N'ADM_FINANCE_WALLET', 30), (N'ADM_FINANCE_QUOTATIONS', 40), (N'ADM_FINANCE_EXPENSES', 50), (N'ADM_FINANCE_PO', 60), (N'ADM_FINANCE_SUPPLIERS', 70),
    (N'ADM_FINANCE_BANK_ACCOUNTS', 80), (N'ADM_FINANCE_BANK_TXNS', 90)) o (Code, DisplayOrder) ON o.Code = m.MenuCode;

DECLARE @perms TABLE (Code nvarchar(100));
INSERT INTO @perms VALUES (N'PAYMENT.RECORD'), (N'PAYMENT.REVERSE'), (N'BILL.VIEW'), (N'BILL.CREATE'), (N'BILL.APPROVE'), (N'BILL.PAY'), (N'BANK.IMPORT'),
    (N'BANK.MATCH'), (N'BANK.WRITEOFF'), (N'TDS.VIEW'), (N'TDS.MANAGE'), (N'GST.VIEW'), (N'GST.MANAGE'), (N'PETTYCASH.MANAGE'), (N'EXPENSE.APPROVE'),
    (N'EXPENSE.REIMBURSE'), (N'COMPLIANCE.VIEW'), (N'COMPLIANCE.MANAGE'), (N'FINANCE.LOCK'), (N'FINANCE.UNLOCK'), (N'FINANCE.EXPORT');

-- The two roles: removed only when nobody holds them.
DECLARE @roles TABLE (RoleId int);
INSERT INTO @roles SELECT r.RoleId FROM MST_Role r WHERE r.RoleCode IN (N'ACCOUNTANT', N'AUDITOR')
    AND NOT EXISTS (SELECT 1 FROM MAP_UserRole ur WHERE ur.RoleId = r.RoleId);
DELETE FROM MAP_RoleMenu WHERE RoleId IN (SELECT RoleId FROM @roles);
DELETE FROM MAP_RolePermission WHERE RoleId IN (SELECT RoleId FROM @roles);
DELETE FROM MST_Role WHERE RoleId IN (SELECT RoleId FROM @roles);

DELETE rp FROM MAP_RolePermission rp JOIN MST_Permission p ON p.PermissionId = rp.PermissionId WHERE p.PermissionCode IN (SELECT Code FROM @perms);
UPDATE MST_Menu SET PermissionId = NULL WHERE PermissionId IN (SELECT PermissionId FROM MST_Permission WHERE PermissionCode IN (SELECT Code FROM @perms));
DELETE FROM MST_Permission WHERE PermissionCode IN (SELECT Code FROM @perms);

DELETE FROM MST_AppSetting WHERE SettingKey IN (N'Expense.ApprovalLimitNone', N'Expense.ApprovalLimitManager', N'Expense.ReceiptRequiredAbove',
    N'Bill.ApprovalLimit', N'Bill.DefaultPaymentTermsDays', N'Bank.RoundingCap', N'Bank.ChargeCategory', N'PettyCash.Float', N'Reminders.Enabled',
    N'Reminders.InvoiceDueSoon.Enabled', N'Reminders.InvoiceDaysBefore', N'Reminders.InvoiceOverdue.Enabled', N'Reminders.InvoiceOverdueDays',
    N'Reminders.BillDue.Enabled', N'Reminders.ApprovalDigest.Enabled', N'Reminders.Compliance.Enabled', N'Reminders.ComplianceDaysBefore',
    N'Reminders.FinanceEmail', N'Reminders.AccountantEmail', N'Compliance.ProfessionalTaxAmount');

DELETE FROM MST_NumberSequence WHERE SequenceKey IN (N'InvoiceReceipt', N'PurchaseBill', N'VendorPayment');
COMMIT TRANSACTION;
