/* ---------------------------------------------------------------------------
   Let a customer cancel their own pass  (MAP_RolePermission: Customer + PASS.EDIT)

   *** UAT STOPGAP. DO NOT PROMOTE THIS TO PRODUCTION AS-IS. ***

   Why it is needed
   ----------------
   POST /api/passes/{id}/cancel is guarded with PASS.EDIT (endpoints.md line
   178, and the [HasPermission] blob in Regalis.API.dll next to
   PassesController+<Cancel>d__6). The Customer role holds only PASS.VIEW and
   PASS.CREATE, so every customer cancel returns 403:

       [WRN] Authorization failed: user 4 lacks permission PASS.EDIT
       [INF] HTTP POST /api/passes/3/cancel responded 403 (user: 4)

   The cancel itself is already implemented server-side, refund included --
   IPassService.CancelAsync is documented as "Cancel before the pass starts:
   full refund to the wallet; once started it can only be cancelled without a
   refund." Only the authorization is missing.

   What it costs
   -------------
   PASS.EDIT is not specific to cancelling. The same permission guards

       PUT /api/pass-plans/{id}    PassPlansController.Update    (endpoints.md:170)

   Pass plans are master data. Row-level scoping via ICurrentUser covers rows a
   customer owns -- bookings, invoices, tickets, passes, wallet (architecture.md
   s4) -- and a pass plan is owned by nobody, so that scoping does not apply
   here. After this grant a customer can very likely PUT a pass plan and rewrite
   its price, then buy at the price they just set. That has NOT been proven by
   test; it is the reading of the docs and the compiled attributes.

   The real fix is in the API, not in this table: give Cancel its own
   PASS.CANCEL permission (constant + MST_Permission seed + MAP_RolePermission
   grant, per catalogues.md s1) with the ownership check in PassService, and
   revert this migration.

   Takes effect on the customer's NEXT LOGIN. PermissionAuthorizationHandler
   reads the perm claims carried in the JWT (architecture.md s4), so an access
   token issued before this runs keeps the old claim set for up to
   Jwt.AccessTokenMinutes = 15.

   Idempotent -- re-running changes nothing.
   Rollback: 20260916000000_GrantPassEditToCustomer_Rollback.sql
   --------------------------------------------------------------------------- */
SET NOCOUNT ON;
SET XACT_ABORT ON;

DECLARE @CustomerRoleId int = (SELECT RoleId       FROM MST_Role       WHERE RoleName = 'Customer');
DECLARE @PassEditId     int = (SELECT PermissionId FROM MST_Permission WHERE PermissionCode = 'PASS.EDIT' AND IsDeleted = 0);

IF @CustomerRoleId IS NULL OR @PassEditId IS NULL
    THROW 50000, 'Customer role or PASS.EDIT permission not found - nothing granted.', 1;

INSERT INTO MAP_RolePermission (RoleId, PermissionId)
SELECT @CustomerRoleId, @PassEditId
WHERE NOT EXISTS (SELECT 1
                  FROM   MAP_RolePermission
                  WHERE  RoleId = @CustomerRoleId
                    AND  PermissionId = @PassEditId);

/* Verification -- every PASS.* permission the Customer role now carries. */
SELECT r.RoleName, p.PermissionCode
FROM   MAP_RolePermission AS rp
JOIN   MST_Role           AS r ON r.RoleId       = rp.RoleId
JOIN   MST_Permission     AS p ON p.PermissionId = rp.PermissionId
WHERE  rp.RoleId = @CustomerRoleId
  AND  p.ModuleName = 'PASS'
ORDER  BY p.PermissionCode;
