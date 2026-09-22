/* ---------------------------------------------------------------------------
   Rollback: take PASS.EDIT back off the Customer role.

   Run this as soon as the API carries a dedicated PASS.CANCEL permission, or
   before promoting this database to production. Afterwards a customer cancel
   returns 403 again and cancelling becomes a front-desk action -- Administrator
   and Operations Manager both already hold PASS.EDIT.

   Takes effect on the customer's next login (perm claims live in the JWT).

   Idempotent -- re-running changes nothing.
   Forward: 20260916000000_GrantPassEditToCustomer.sql
   --------------------------------------------------------------------------- */
SET NOCOUNT ON;
SET XACT_ABORT ON;

DECLARE @CustomerRoleId int = (SELECT RoleId       FROM MST_Role       WHERE RoleName = 'Customer');
DECLARE @PassEditId     int = (SELECT PermissionId FROM MST_Permission WHERE PermissionCode = 'PASS.EDIT' AND IsDeleted = 0);

DELETE FROM MAP_RolePermission
WHERE  RoleId       = @CustomerRoleId
  AND  PermissionId = @PassEditId;

/* Verification -- what the Customer role is left holding for PASS. */
SELECT r.RoleName, p.PermissionCode
FROM   MAP_RolePermission AS rp
JOIN   MST_Role           AS r ON r.RoleId       = rp.RoleId
JOIN   MST_Permission     AS p ON p.PermissionId = rp.PermissionId
WHERE  rp.RoleId = @CustomerRoleId
  AND  p.ModuleName = 'PASS'
ORDER  BY p.PermissionCode;
