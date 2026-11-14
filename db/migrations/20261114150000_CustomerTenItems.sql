/* ---------------------------------------------------------------------------
   customer-10 kit (14 Nov 2026) - Customer Portal brief, 10 items.

   1. Customer sidebar: "Payment History" (CUST_PAYMENTS, /payments) after Invoices.
      Permission PAYMENT.VIEW (customers already hold it); granted to the CUSTOMER role.
   2. Pay-later passes and memberships that are not yet paid in full move from
      Active (1) to the new AwaitingPayment (5). They become Active again by themselves
      when the desk records the payment (PassActivationInterceptor in the API).
      Run this together with the new API; the old API does not know status 5.

   No table or column changes. Idempotent; ASCII only.
     sqlcmd -S localhost -d RegalisWorkSpace_UAT -E -C -I -b -i 20261114150000_CustomerTenItems.sql
   Rollback: 20261114150000_CustomerTenItems_Rollback.sql
   --------------------------------------------------------------------------- */
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
SET NOCOUNT ON;
GO
SET XACT_ABORT ON;
BEGIN TRANSACTION;

DECLARE @perm int = (SELECT PermissionId FROM dbo.MST_Permission WHERE PermissionCode = 'PAYMENT.VIEW');
IF @perm IS NULL THROW 50001, 'PAYMENT.VIEW permission not found', 1;

IF NOT EXISTS (SELECT 1 FROM dbo.MST_Menu WHERE MenuCode = 'CUST_PAYMENTS')
    INSERT dbo.MST_Menu (ParentMenuId, MenuCode, MenuName, DisplayName, Route, Icon, DisplayOrder, PermissionId, PortalType, MenuLevel, IsVisible, CreatedDate, IsActive, IsDeleted)
    VALUES (NULL, 'CUST_PAYMENTS', 'Payment History', 'Payment History', '/payments', 'Banknote', 75, @perm, 1, 1, 1, SYSUTCDATETIME(), 1, 0);
ELSE
    UPDATE dbo.MST_Menu SET IsVisible = 1, IsActive = 1, IsDeleted = 0, DeletedDate = NULL, DeletedBy = NULL
    WHERE MenuCode = 'CUST_PAYMENTS';

DECLARE @menu int = (SELECT MenuId FROM dbo.MST_Menu WHERE MenuCode = 'CUST_PAYMENTS');
DECLARE @role int = (SELECT RoleId FROM dbo.MST_Role WHERE RoleCode = 'CUSTOMER');
IF NOT EXISTS (SELECT 1 FROM dbo.MAP_RoleMenu WHERE RoleId = @role AND MenuId = @menu)
    INSERT dbo.MAP_RoleMenu (RoleId, MenuId) VALUES (@role, @menu);

UPDATE dbo.TRN_CustomerPass SET Status = 5
WHERE IsPayLater = 1 AND Status = 1 AND AmountPaid < TotalAmount AND IsDeleted = 0;
PRINT CONCAT('Pay-later passes now awaiting payment: ', @@ROWCOUNT);

COMMIT;
PRINT 'customer-10 migration applied.';
