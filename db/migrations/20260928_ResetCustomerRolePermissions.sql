-- SECURITY FIX: the CUSTOMER role (RoleId 1) had been granted almost every admin
-- permission (USER.*, ROLE.*, WALLET.EDIT, SETTING.EDIT, PAYROLL.VIEW, AUDIT.VIEW ...).
-- Permission checks read only the "perm" claims in the JWT, so any signed-in customer
-- could call the admin APIs. Found and confirmed 2026-09-28 (a customer token got 200 on
-- /api/users, /api/roles, /api/settings).
--
-- This keeps exactly the customer-portal permissions the seeder grants the Customer role
-- (SeedCatalogue.RolePermissions[Customer]) and removes everything else.
-- A full copy of the role's mapping is saved first, so it can be restored exactly:
--   INSERT INTO MAP_RolePermission (RoleId, PermissionId)
--   SELECT RoleId, PermissionId FROM BAK_CustomerRolePermission_20260928 b
--   WHERE NOT EXISTS (SELECT 1 FROM MAP_RolePermission m WHERE m.RoleId = b.RoleId AND m.PermissionId = b.PermissionId);
-- Safe to run more than once. Existing customer tokens keep their old claims until they
-- expire (access tokens last 15 minutes) or the customer signs in again.
--   sqlcmd -S localhost -d RegalisWorkSpace_UAT -E -C -I -b -i 20260928_ResetCustomerRolePermissions.sql

SET XACT_ABORT ON;
BEGIN TRANSACTION;

IF OBJECT_ID('BAK_CustomerRolePermission_20260928') IS NULL
    SELECT rp.RolePermissionId, rp.RoleId, rp.PermissionId, p.PermissionCode, SYSUTCDATETIME() AS BackedUpUtc
    INTO BAK_CustomerRolePermission_20260928
    FROM MAP_RolePermission rp
    JOIN MST_Permission p ON p.PermissionId = rp.PermissionId
    WHERE rp.RoleId = (SELECT RoleId FROM MST_Role WHERE RoleCode = N'CUSTOMER');

DECLARE @Keep TABLE (PermissionCode nvarchar(100) PRIMARY KEY);
INSERT INTO @Keep VALUES
 (N'DASHBOARD.VIEW'), (N'LOCATION.VIEW'), (N'WORKSPACE.VIEW'), (N'CALENDAR.VIEW'),
 (N'BOOKING.VIEW'), (N'BOOKING.CREATE'), (N'BOOKING.EDIT'),
 (N'PAYMENT.VIEW'), (N'PAYMENT.CREATE'), (N'WALLET.VIEW'), (N'WALLET.CREATE'),
 (N'INVOICE.VIEW'), (N'INVOICE.PRINT'), (N'PASS.VIEW'), (N'PASS.CREATE'),
 (N'QUOTATION.VIEW'), (N'QUOTATION.APPROVE'), (N'QUOTATION.PRINT'),
 (N'ACCESS.VIEW'), (N'ATTENDANCE.VIEW'), (N'SERVICE.VIEW'), (N'SERVICE.CREATE'),
 (N'BANNER.VIEW'), (N'NOTIFICATION.VIEW'), (N'NOTIFICATION.EDIT'),
 (N'TICKET.VIEW'), (N'TICKET.CREATE'), (N'TICKET.EDIT');

DELETE rp
FROM MAP_RolePermission rp
JOIN MST_Permission p ON p.PermissionId = rp.PermissionId
WHERE rp.RoleId = (SELECT RoleId FROM MST_Role WHERE RoleCode = N'CUSTOMER')
  AND p.PermissionCode NOT IN (SELECT PermissionCode FROM @Keep);

COMMIT;

SELECT COUNT(*) AS CustomerPermissionsNow FROM MAP_RolePermission
WHERE RoleId = (SELECT RoleId FROM MST_Role WHERE RoleCode = N'CUSTOMER');
SELECT COUNT(*) AS BackedUpRows FROM BAK_CustomerRolePermission_20260928;
