-- Billing & Finance build (Regalis_WorkSpace_Billing_Finance_Build_Document, Oct 2026):
-- permissions, the Accountant and Auditor roles, the sidebar (five bands, six new pages,
-- Reconciliation restored), settings, master data and number sequences. The period lock and
-- "reconciled means locked" are enforced in the API (BillingFinanceRulesInterceptor), not by
-- triggers: EF Core cannot save to a table that has triggers it was not told about.
--
-- The tables come from the EF migration 20261101165945_BillingFinance (run it first).
-- Idempotent; one transaction. ASCII only.
--   sqlcmd -S localhost -d RegalisWorkSpace_UAT -E -C -I -b -i 20261101170000_BillingFinanceAccess.sql
-- Rollback: 20261101170000_BillingFinanceAccess_Rollback.sql
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
SET NOCOUNT ON;
GO
SET XACT_ABORT ON;
BEGIN TRANSACTION;

DECLARE @now datetime2(3) = SYSUTCDATETIME();

-- 1. Permissions ---------------------------------------------------------------------
DECLARE @perm TABLE (Code nvarchar(100), ModuleName nvarchar(50), ActionName nvarchar(50), Description nvarchar(200), DisplayOrder int);
INSERT INTO @perm VALUES
    (N'PAYMENT.RECORD',   N'PAYMENT',    N'RECORD',    N'Record a customer payment against an invoice (cash, UPI, bank, cheque, card, wallet)', 47),
    (N'PAYMENT.REVERSE',  N'PAYMENT',    N'REVERSE',   N'Reverse a recorded customer or vendor payment', 48),
    (N'BILL.VIEW',        N'BILL',       N'VIEW',      N'See purchase bills, vendor payments and supplier ledgers', 1),
    (N'BILL.CREATE',      N'BILL',       N'CREATE',    N'Enter and edit a purchase bill', 2),
    (N'BILL.APPROVE',     N'BILL',       N'APPROVE',   N'Approve, reject or hold a purchase bill (never one you entered)', 3),
    (N'BILL.PAY',         N'BILL',       N'PAY',       N'Post a vendor payment', 4),
    (N'BANK.IMPORT',      N'BANK',       N'IMPORT',    N'Import a bank statement', 7),
    (N'BANK.MATCH',       N'BANK',       N'MATCH',     N'Match and reconcile a bank line, and unmatch it', 8),
    (N'BANK.WRITEOFF',    N'BANK',       N'WRITEOFF',  N'Accept a short payment as a difference', 9),
    (N'TDS.VIEW',         N'TDS',        N'VIEW',      N'See the TDS registers', 1),
    (N'TDS.MANAGE',       N'TDS',        N'MANAGE',    N'Record TDS challans and certificates', 2),
    (N'GST.VIEW',         N'GST',        N'VIEW',      N'See the GST page', 1),
    (N'GST.MANAGE',       N'GST',        N'MANAGE',    N'Upload GSTR-2B and update the return tracker', 2),
    (N'PETTYCASH.MANAGE', N'PETTYCASH',  N'MANAGE',    N'Top up and count the petty cash tin', 1),
    (N'EXPENSE.APPROVE',  N'EXPENSE',    N'APPROVE',   N'Approve or reject an expense (never one you raised)', 6),
    (N'EXPENSE.REIMBURSE',N'EXPENSE',    N'REIMBURSE', N'Clear what is owed to staff', 7),
    (N'COMPLIANCE.VIEW',  N'COMPLIANCE', N'VIEW',      N'See the compliance calendar, period locks and reminder log', 1),
    (N'COMPLIANCE.MANAGE',N'COMPLIANCE', N'MANAGE',    N'Mark obligations done and run reminders', 2),
    (N'FINANCE.LOCK',     N'FINANCE',    N'LOCK',      N'Lock a filed month', 1),
    (N'FINANCE.UNLOCK',   N'FINANCE',    N'UNLOCK',    N'Unlock a closed month (reason required)', 2),
    (N'FINANCE.EXPORT',   N'FINANCE',    N'EXPORT',    N'Download the accounting export', 3);

INSERT INTO MST_Permission (PermissionCode, ModuleName, ActionName, Description, DisplayOrder, CreatedDate, IsActive, IsDeleted)
SELECT p.Code, p.ModuleName, p.ActionName, p.Description, p.DisplayOrder, @now, 1, 0
FROM @perm p
WHERE NOT EXISTS (SELECT 1 FROM MST_Permission x WHERE x.PermissionCode = p.Code);

-- 2. Roles: Accountant (reads and exports, files returns) and Auditor (reads, exports, audit log)
INSERT INTO MST_Role (RoleCode, RoleName, Description, IsSystemRole, PortalType, CreatedDate, IsActive, IsDeleted)
SELECT v.RoleCode, v.RoleName, v.Description, 0, 2, @now, 1, 0
FROM (VALUES
    (N'ACCOUNTANT', N'Accountant', N'Reads everything in Billing & Finance, exports everything, files returns. Creates and edits nothing.'),
    (N'AUDITOR', N'Auditor', N'Reads everything including the audit log, and exports. Changes nothing at all.')) v (RoleCode, RoleName, Description)
WHERE NOT EXISTS (SELECT 1 FROM MST_Role r WHERE r.RoleCode = v.RoleCode);

DECLARE @views TABLE (Code nvarchar(100));
INSERT INTO @views VALUES (N'DASHBOARD.VIEW'), (N'INVOICE.VIEW'), (N'INVOICE.PRINT'), (N'PAYMENT.VIEW'), (N'WALLET.VIEW'), (N'QUOTATION.VIEW'),
    (N'QUOTATION.PRINT'), (N'EXPENSE.VIEW'), (N'PURCHASE.VIEW'), (N'SUPPLIER.VIEW'), (N'CUSTOMER.VIEW'), (N'BANK.VIEW'), (N'BILL.VIEW'), (N'TDS.VIEW'),
    (N'GST.VIEW'), (N'COMPLIANCE.VIEW'), (N'REPORT.VIEW'), (N'REPORT.FINANCE'), (N'REPORT.CUSTOMERS'),
    (N'INVOICE.EXPORT'), (N'PAYMENT.EXPORT'), (N'WALLET.EXPORT'), (N'QUOTATION.EXPORT'), (N'EXPENSE.EXPORT'), (N'PURCHASE.EXPORT'), (N'BANK.EXPORT'),
    (N'REPORT.EXPORT'), (N'REPORT.PRINT'), (N'FINANCE.EXPORT');

DECLARE @grant TABLE (RoleCode nvarchar(50), Code nvarchar(100));
-- Owner (Administrator): everything new.
INSERT INTO @grant SELECT N'ADMIN', Code FROM @perm;
-- Finance / office manager: everything except unlocking a month and writing off a short payment.
INSERT INTO @grant SELECT N'FINANCE', Code FROM @perm WHERE Code NOT IN (N'FINANCE.UNLOCK', N'BANK.WRITEOFF');
-- Front desk (Operations Manager runs the desk): record cash and UPI at the desk.
INSERT INTO @grant VALUES (N'MANAGER', N'PAYMENT.RECORD');
INSERT INTO @grant SELECT N'ACCOUNTANT', Code FROM @views;
INSERT INTO @grant VALUES (N'ACCOUNTANT', N'GST.MANAGE'), (N'ACCOUNTANT', N'TDS.MANAGE'), (N'ACCOUNTANT', N'COMPLIANCE.MANAGE');
INSERT INTO @grant SELECT N'AUDITOR', Code FROM @views;
INSERT INTO @grant VALUES (N'AUDITOR', N'AUDIT.VIEW'), (N'AUDITOR', N'AUDIT.EXPORT');

INSERT INTO MAP_RolePermission (RoleId, PermissionId)
SELECT DISTINCT r.RoleId, p.PermissionId
FROM @grant g
JOIN MST_Role r ON r.RoleCode = g.RoleCode
JOIN MST_Permission p ON p.PermissionCode = g.Code
WHERE NOT EXISTS (SELECT 1 FROM MAP_RolePermission x WHERE x.RoleId = r.RoleId AND x.PermissionId = p.PermissionId);

-- 3. Sidebar ---------------------------------------------------------------------------
DECLARE @parent int = (SELECT MenuId FROM MST_Menu WHERE MenuCode = N'ADM_FINANCE' AND IsDeleted = 0);
IF @parent IS NULL
BEGIN
    RAISERROR('ADM_FINANCE menu not found - nothing changed.', 16, 1);
    ROLLBACK TRANSACTION;
    RETURN;
END;

-- 3a. Reconciliation: restore the deleted row, under Billing & Finance, at the page's real route.
UPDATE MST_Menu
SET ParentMenuId = @parent, Route = N'/admin/billing/reconciliation', Icon = N'ListChecks', IsActive = 1, IsDeleted = 0, IsVisible = 1,
    MenuLevel = 2, DeletedBy = NULL, DeletedDate = NULL, ModifiedDate = @now
WHERE MenuCode = N'ADM_FINANCE_BANK_RECON';

-- 3b. New rows: five band labels (no route; drawn as headings) and six pages.
DECLARE @menu TABLE (Code nvarchar(100), Display nvarchar(100), Route nvarchar(200), Icon nvarchar(50), Perm nvarchar(100), DisplayOrder int);
INSERT INTO @menu VALUES
    (N'ADM_FINANCE_BAND_IN',         N'MONEY IN',            NULL, N'Band', NULL, 1),
    (N'ADM_FINANCE_BAND_DESK',       N'CASH DESK',           NULL, N'Band', NULL, 20),
    (N'ADM_FINANCE_BAND_OUT',        N'MONEY OUT',           NULL, N'Band', NULL, 40),
    (N'ADM_FINANCE_BAND_BANK',       N'BANK',                NULL, N'Band', NULL, 70),
    (N'ADM_FINANCE_BAND_COMPLIANCE', N'COMPLIANCE',          NULL, N'Band', NULL, 90),
    (N'ADM_FINANCE_PETTY_CASH',      N'Petty Cash',          N'/admin/billing/petty-cash',      N'Coins',        N'PETTYCASH.MANAGE', 27),
    (N'ADM_FINANCE_BILLS',           N'Purchase Bills',      N'/admin/billing/purchase-bills',  N'FileText',     N'BILL.VIEW',        52),
    (N'ADM_FINANCE_VENDOR_PAYMENTS', N'Vendor Payments',     N'/admin/billing/vendor-payments', N'Send',         N'BILL.VIEW',        54),
    (N'ADM_FINANCE_GST',             N'GST',                 N'/admin/billing/gst',             N'Percent',      N'GST.VIEW',         92),
    (N'ADM_FINANCE_TDS',             N'TDS',                 N'/admin/billing/tds',             N'Scale',        N'TDS.VIEW',         94),
    (N'ADM_FINANCE_COMPLIANCE',      N'Compliance Calendar', N'/admin/billing/compliance',      N'CalendarCheck',N'COMPLIANCE.VIEW',  96);

INSERT INTO MST_Menu (ParentMenuId, MenuCode, MenuName, DisplayName, Route, Icon, DisplayOrder, PermissionId, PortalType, MenuLevel, IsVisible, CreatedDate, IsActive, IsDeleted)
SELECT @parent, m.Code, m.Display, m.Display, m.Route, m.Icon, m.DisplayOrder,
       (SELECT PermissionId FROM MST_Permission WHERE PermissionCode = m.Perm), 2, 2, 1, @now, 1, 0
FROM @menu m
WHERE NOT EXISTS (SELECT 1 FROM MST_Menu x WHERE x.MenuCode = m.Code AND x.IsDeleted = 0);

-- 3c. Order the existing pages into their bands. Names and routes are not touched.
UPDATE m SET m.DisplayOrder = o.DisplayOrder, m.ModifiedDate = @now
FROM MST_Menu m
JOIN (VALUES
    (N'ADM_FINANCE_INVOICES', 10), (N'ADM_FINANCE_PAYMENTS', 12), (N'ADM_FINANCE_QUOTATIONS', 14), (N'ADM_FINANCE_WALLET', 16),
    (N'ADM_FINANCE_CASH', 23), (N'ADM_FINANCE_CASH_REGISTER', 25),
    (N'ADM_FINANCE_PO', 50), (N'ADM_FINANCE_EXPENSES', 56), (N'ADM_FINANCE_SUPPLIERS', 58),
    (N'ADM_FINANCE_BANK_ACCOUNTS', 72), (N'ADM_FINANCE_BANK_TXNS', 74), (N'ADM_FINANCE_BANK_RECON', 76)) o (Code, DisplayOrder)
  ON o.Code = m.MenuCode
WHERE m.IsDeleted = 0 AND m.ParentMenuId = @parent;

-- 3d. Who sees what in the sidebar. A page row goes to every role holding its permission;
-- bands and the parents go to every role that sees any finance page.
INSERT INTO MAP_RoleMenu (RoleId, MenuId)
SELECT DISTINCT rp.RoleId, m.MenuId
FROM MST_Menu m
JOIN MAP_RolePermission rp ON rp.PermissionId = m.PermissionId
JOIN MST_Role r ON r.RoleId = rp.RoleId AND r.RoleCode <> N'CUSTOMER'
WHERE m.ParentMenuId = @parent AND m.IsDeleted = 0 AND m.Route IS NOT NULL
  AND (m.MenuCode IN (SELECT Code FROM @menu) OR m.MenuCode = N'ADM_FINANCE_BANK_RECON' OR r.RoleCode IN (N'ACCOUNTANT', N'AUDITOR'))
  AND NOT EXISTS (SELECT 1 FROM MAP_RoleMenu x WHERE x.RoleId = rp.RoleId AND x.MenuId = m.MenuId);

INSERT INTO MAP_RoleMenu (RoleId, MenuId)
SELECT DISTINCT rm.RoleId, b.MenuId
FROM MAP_RoleMenu rm
JOIN MST_Menu page ON page.MenuId = rm.MenuId AND page.ParentMenuId = @parent AND page.Route IS NOT NULL
JOIN MST_Menu b ON (b.ParentMenuId = @parent AND b.Icon = N'Band' AND b.IsDeleted = 0)
                 OR b.MenuId = @parent
                 OR b.MenuId = (SELECT ParentMenuId FROM MST_Menu WHERE MenuId = @parent)
WHERE b.MenuId IS NOT NULL
  AND NOT EXISTS (SELECT 1 FROM MAP_RoleMenu x WHERE x.RoleId = rm.RoleId AND x.MenuId = b.MenuId);

-- 4. Settings (doc 19.1). Limits are settings, not code. Reminders ship OFF until email
-- from production is proven.
DECLARE @set TABLE (SettingKey nvarchar(120), SettingValue nvarchar(200), DataType nvarchar(20), Description nvarchar(300));
INSERT INTO @set VALUES
    (N'Expense.ApprovalLimitNone', N'2000', N'decimal', N'An expense up to this amount is approved automatically.'),
    (N'Expense.ApprovalLimitManager', N'25000', N'decimal', N'Up to this amount the office manager (EXPENSE.APPROVE) approves; above it, the owner.'),
    (N'Expense.ReceiptRequiredAbove', N'2000', N'decimal', N'A receipt must be attached before approving an expense above this amount.'),
    (N'Bill.ApprovalLimit', N'25000', N'decimal', N'A purchase bill above this total waits for BILL.APPROVE before it can be paid.'),
    (N'Bill.DefaultPaymentTermsDays', N'15', N'int', N'Payment terms (days) for a supplier with none of their own.'),
    (N'Bank.RoundingCap', N'10', N'decimal', N'Largest difference that bank matching may write off as rounding.'),
    (N'Bank.ChargeCategory', N'Bank charges', N'string', N'Expense category for bank charges found while matching.'),
    (N'PettyCash.Float', N'5000', N'decimal', N'The petty cash float.'),
    (N'Reminders.Enabled', N'false', N'bool', N'Master switch for every finance reminder. Turn on only after email from production is proven.'),
    (N'Reminders.InvoiceDueSoon.Enabled', N'true', N'bool', N'Email the customer before an invoice falls due.'),
    (N'Reminders.InvoiceDaysBefore', N'3', N'int', N'Days before the due date to remind the customer.'),
    (N'Reminders.InvoiceOverdue.Enabled', N'true', N'bool', N'Email the customer when an invoice is overdue.'),
    (N'Reminders.InvoiceOverdueDays', N'1,7,15', N'string', N'Days after the due date to chase; never more than three reminders per invoice.'),
    (N'Reminders.BillDue.Enabled', N'true', N'bool', N'Tell finance 3 days before and on the day a supplier bill falls due.'),
    (N'Reminders.ApprovalDigest.Enabled', N'true', N'bool', N'Daily 09:00 digest of bills and expenses waiting for approval.'),
    (N'Reminders.Compliance.Enabled', N'true', N'bool', N'Remind finance and the accountant of GST and TDS dates.'),
    (N'Reminders.ComplianceDaysBefore', N'7,2,0', N'string', N'Days before a compliance due date to send a reminder.'),
    (N'Reminders.FinanceEmail', N'', N'string', N'Where finance reminders go. Empty = Company.Email.'),
    (N'Reminders.AccountantEmail', N'', N'string', N'The accountant''s email for compliance reminders.'),
    (N'Compliance.ProfessionalTaxAmount', N'0', N'decimal', N'Monthly professional tax shown on the compliance calendar.');

INSERT INTO MST_AppSetting (SettingKey, SettingValue, SettingGroup, DataType, Description, IsEditableByAdmin, IsSecret, CreatedDate, IsActive, IsDeleted)
SELECT s.SettingKey, s.SettingValue, LEFT(s.SettingKey, CHARINDEX(N'.', s.SettingKey) - 1), s.DataType, s.Description, 1, 0, @now, 1, 0
FROM @set s
WHERE NOT EXISTS (SELECT 1 FROM MST_AppSetting x WHERE x.SettingKey = s.SettingKey);

-- 5. Number sequences: receipts, purchase bills (internal number), vendor payments.
INSERT INTO MST_NumberSequence (SequenceKey, Prefix, CurrentValue, FinancialYear)
SELECT v.SequenceKey, v.Prefix, 0, v.FinancialYear
FROM (VALUES (N'InvoiceReceipt', N'REG/RCP', N'2026-27'), (N'PurchaseBill', N'REG/PB', N'2026-27'), (N'VendorPayment', N'REG/VP', N'2026-27')) v (SequenceKey, Prefix, FinancialYear)
WHERE NOT EXISTS (SELECT 1 FROM MST_NumberSequence x WHERE x.SequenceKey = v.SequenceKey);

-- 6. Master data (doc 19.2) ------------------------------------------------------------
INSERT INTO MST_Department (Name, DisplayOrder, CreatedDate, IsActive, IsDeleted)
SELECT v.Name, v.DisplayOrder, @now, 1, 0
FROM (VALUES (N'Front desk', 10), (N'Facilities', 20), (N'Operations', 30), (N'Overheads', 40), (N'Marketing', 50), (N'IT', 60)) v (Name, DisplayOrder)
WHERE NOT EXISTS (SELECT 1 FROM MST_Department d WHERE d.Name = v.Name AND d.IsDeleted = 0);

INSERT INTO MST_TdsSection (Section, Description, Rate, IndividualRate, NoPanRate, EffectiveFrom, EffectiveTo, IsActive)
SELECT v.Section, v.Description, v.Rate, v.IndividualRate, 20, '2020-04-01', NULL, 1
FROM (VALUES
    (N'194C', N'Contractors - housekeeping, security, maintenance contracts', 2.00, 1.00),
    (N'194J', N'Professional and technical fees - CA, consultants, legal', 10.00, NULL),
    (N'194I(b)', N'Rent of land or building', 10.00, NULL),
    (N'194I(a)', N'Rent of plant or machinery', 2.00, NULL),
    (N'194H', N'Commission and brokerage', 5.00, NULL),
    (N'194Q', N'Purchase of goods', 0.10, NULL)) v (Section, Description, Rate, IndividualRate)
WHERE NOT EXISTS (SELECT 1 FROM MST_TdsSection s WHERE s.Section = v.Section);

INSERT INTO MST_GstRate (Rate, Label, EffectiveFrom, EffectiveTo, IsActive)
SELECT v.Rate, v.Label, '2017-07-01', NULL, 1
FROM (VALUES (28.00, N'28%'), (18.00, N'18%'), (12.00, N'12%'), (5.00, N'5%'), (0.00, N'Exempt / nil')) v (Rate, Label)
WHERE NOT EXISTS (SELECT 1 FROM MST_GstRate g WHERE g.Rate = v.Rate);

INSERT INTO MST_DifferenceReason (DifferenceReasonId, Code, Label, Help, RequiredPermission, IsActive, DisplayOrder)
SELECT v.Id, v.Code, v.Label, v.Help, v.Perm, 1, v.Id * 10
FROM (VALUES
    (1, N'TDS', N'TDS deducted by the customer', N'They paid less because they withheld tax. The invoice is settled in full and a TDS receivable waits for their Form 16A.', NULL),
    (2, N'BANK_CHARGE', N'Bank charge', N'NEFT fee, cheque return fee, card commission. Recorded as an expense in the bank charges category.', NULL),
    (3, N'INTEREST', N'Interest earned', N'Interest credited by the bank. Recorded as other income against this line.', NULL),
    (4, N'ROUNDING', N'Rounding', N'A few paise or rupees either way. Written off, up to the rounding cap.', NULL),
    (5, N'SHORT_PAYMENT', N'Short payment accepted', N'A discount agreed with the customer. The invoice is closed with this reason.', N'BANK.WRITEOFF'),
    (6, N'ADVANCE', N'Advance', N'Paid more than owed: the extra goes to the customer''s wallet, or is an advance to the supplier.', NULL)) v (Id, Code, Label, Help, Perm)
WHERE NOT EXISTS (SELECT 1 FROM MST_DifferenceReason d WHERE d.DifferenceReasonId = v.Id);

INSERT INTO MST_ComplianceObligation (Code, Name, Frequency, DueDay, DueMonths, AmountSource, OwnerRole, DisplayOrder, CreatedDate, IsActive, IsDeleted)
SELECT v.Code, v.Name, v.Frequency, v.DueDay, v.DueMonths, v.AmountSource, v.OwnerRole, v.DisplayOrder, @now, 1, 0
FROM (VALUES
    (N'TDS_CHALLAN', N'TDS payment - challan', N'MonthlyNext', 7, NULL, N'TdsUnpaid', N'Finance', 10),
    (N'GSTR1', N'GSTR-1', N'MonthlyNext', 11, NULL, NULL, N'Accountant', 20),
    (N'GSTR3B', N'GSTR-3B and the GST payment', N'MonthlyNext', 20, NULL, N'GstCash', N'Accountant', 30),
    (N'PROF_TAX', N'Professional tax', N'Monthly', 30, NULL, N'Setting:Compliance.ProfessionalTaxAmount', N'Finance', 40),
    (N'TDS_26Q', N'TDS return 26Q', N'Quarterly', 31, N'7,10,1,5', NULL, N'Accountant', 50)) v (Code, Name, Frequency, DueDay, DueMonths, AmountSource, OwnerRole, DisplayOrder)
WHERE NOT EXISTS (SELECT 1 FROM MST_ComplianceObligation o WHERE o.Code = v.Code);

-- Every expense recorded before approvals existed is treated as approved.
INSERT INTO TRN_ExpenseControl (ExpenseId, ApprovalStatus, ApprovalLevel, DecisionComment, PaidFrom, IsReimbursement, IsRecurringDraft, CreatedDate, IsActive, IsDeleted)
SELECT e.ExpenseId, 2, 0, N'Recorded before expense approval was introduced.', 1, 0, 0, @now, 1, 0
FROM TRN_Expense e
WHERE NOT EXISTS (SELECT 1 FROM TRN_ExpenseControl c WHERE c.ExpenseId = e.ExpenseId);

COMMIT TRANSACTION;
GO

SELECT p.PermissionCode, COUNT(rp.RoleId) AS Roles
FROM MST_Permission p LEFT JOIN MAP_RolePermission rp ON rp.PermissionId = p.PermissionId
WHERE p.PermissionCode IN (N'PAYMENT.RECORD', N'PAYMENT.REVERSE', N'BILL.VIEW', N'BILL.CREATE', N'BILL.APPROVE', N'BILL.PAY', N'BANK.IMPORT', N'BANK.MATCH',
    N'BANK.WRITEOFF', N'TDS.VIEW', N'TDS.MANAGE', N'GST.VIEW', N'GST.MANAGE', N'PETTYCASH.MANAGE', N'EXPENSE.APPROVE', N'EXPENSE.REIMBURSE', N'COMPLIANCE.VIEW',
    N'COMPLIANCE.MANAGE', N'FINANCE.LOCK', N'FINANCE.UNLOCK', N'FINANCE.EXPORT')
GROUP BY p.PermissionCode ORDER BY p.PermissionCode;
SELECT m.DisplayOrder, m.MenuCode, m.DisplayName, m.Route, COUNT(rm.RoleId) AS Roles
FROM MST_Menu m LEFT JOIN MAP_RoleMenu rm ON rm.MenuId = m.MenuId
WHERE m.ParentMenuId = (SELECT MenuId FROM MST_Menu WHERE MenuCode = N'ADM_FINANCE' AND IsDeleted = 0) AND m.IsDeleted = 0
GROUP BY m.DisplayOrder, m.MenuCode, m.DisplayName, m.Route ORDER BY m.DisplayOrder;
