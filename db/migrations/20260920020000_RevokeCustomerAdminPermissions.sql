/* ---------------------------------------------------------------------------
   Revoke administrative permissions from the Customer role (RoleId 1).

   *** NOT APPLIED. PROPOSAL FOR REVIEW. ***
   This changes what three live customer accounts can do. Read the whole header,
   run the DRY RUN block at the bottom, and confirm against
   ROLE_PERMISSION_MATRIX.md -- which is NOT in this repo -- before running it.

   THE FINDING
   -----------
   MST_Role RoleId 1 (CUSTOMER, PortalType 1, IsSystemRole 1) holds ALL 131
   permissions -- byte-for-byte the same grant count as ADMIN. That includes
   USER.DELETE, ROLE.EDIT, PERMISSION.EDIT, SETTING.EDIT, PAYROLL.VIEW,
   MENU.EDIT and AUDIT.VIEW.

   Three real accounts hold this role: UserId 4 (customer@regalis.in),
   5 (dhina007@gmail.com) and 6 (bavanthiravi@gmail.com).

   Endpoints are guarded by permission CODE, not by portal. The role's
   PortalType 1 does not gate anything at the API. Row scoping via ICurrentUser
   protects rows a customer OWNS (bookings, invoices, tickets, passes, wallet),
   but master data -- pass plans, workspaces, settings, roles -- is owned by
   nobody, so that scoping does not apply to it.

   HOW IT LIKELY HAPPENED  (inference, not proof)
   ----------------------------------------------
   MAP_RolePermission is a bare mapping table (RolePermissionId, RoleId,
   PermissionId) with no audit columns, and TRN_AuditLog has no rows for these
   grants -- only the original role CREATE rows from the 2026-09-01 seed. So
   this cannot be dated directly. The IDENTITY sequence is the evidence:

     RoleId 1 legitimate customer grants   low ids   (e.g. BOOKING.VIEW = 5)
     RoleId 1 administrative grants        342-444   contiguous, much later

   Id 342 is PASS.EDIT, which is exactly what
   20260916000000_GrantPassEditToCustomer.sql grants -- and that script is
   clean: it inserts one row, idempotently. So the ~102 rows at 343-444 came
   from something else immediately afterwards. The most likely cause is the
   /admin/roles permission-matrix screen: PUT /api/roles/{id}/permissions
   replaces a role's grant set wholesale, so one "select all" on the Customer
   role would produce exactly this pattern.

   WHAT THIS SCRIPT DOES
   ---------------------
   Two tiers, deliberately conservative -- it revokes what is clear-cut and
   leaves the arguable cases alone rather than guessing and breaking the
   customer portal.

   Tier 1, revoke the whole module -- no customer surface exists for any of it:
       USER ROLE PERMISSION MENU SETTING AUDIT PAYROLL EXPENSE
       SUPPLIER PURCHASE BANK INVENTORY BANNER REPORT HOUSEKEEPING CALENDAR

   Tier 2, revoke every non-VIEW verb, keep VIEW -- the customer portal reads
   these to render pickers and listings:
       WORKSPACE LOCATION SERVICE CUSTOMER ACCESS NOTIFICATION

   Untouched, customer-functional:
       BOOKING PASS WALLET TICKET INVOICE QUOTATION DASHBOARD
       ATTENDANCE PAYMENT

   Net effect: 131 grants -> 49. PASS.EDIT is deliberately KEPT; it is the
   documented stopgap from 20260916000000_GrantPassEditToCustomer.sql, and that
   script's own header explains the real fix (a dedicated PASS.CANCEL
   permission) and should be actioned separately.

   STILL NEEDS A SECOND PASS
   -------------------------
   These survive Tier 1 and 2 but are questionable for a customer. They are NOT
   revoked here because each may back a real customer action, and row scoping
   may already contain them. Decide these deliberately:

       BOOKING.APPROVE  BOOKING.DELETE   TICKET.APPROVE   TICKET.DELETE
       QUOTATION.APPROVE QUOTATION.DELETE INVOICE.CREATE  INVOICE.EDIT
       WALLET.EDIT      WALLET.CREATE    ATTENDANCE.APPROVE
       CUSTOMER.VIEW    -- lets a customer list the customer directory

   WHEN IT TAKES EFFECT
   --------------------
   On each customer's NEXT LOGIN. PermissionAuthorizationHandler reads the perm
   claims baked into the JWT, so an access token issued before this runs keeps
   the old claim set for up to Jwt.AccessTokenMinutes = 15.

   Idempotent -- re-running changes nothing.
   Rollback: 20260920020000_RevokeCustomerAdminPermissions_Rollback.sql
            (restores the exact grants this script removed, from the backup
             table it writes -- so run this script, not a hand-written undo)
   --------------------------------------------------------------------------- */
SET NOCOUNT ON;
SET XACT_ABORT ON;

DECLARE @CustomerRoleId int = (SELECT RoleId FROM MST_Role WHERE RoleCode = 'CUSTOMER');
IF @CustomerRoleId IS NULL
    THROW 50000, 'Customer role not found - nothing revoked.', 1;

DECLARE @FullRevoke TABLE (ModuleName nvarchar(50) PRIMARY KEY);
INSERT INTO @FullRevoke (ModuleName) VALUES
    ('USER'),('ROLE'),('PERMISSION'),('MENU'),('SETTING'),('AUDIT'),
    ('PAYROLL'),('EXPENSE'),('SUPPLIER'),('PURCHASE'),('BANK'),
    ('INVENTORY'),('BANNER'),('REPORT'),('HOUSEKEEPING'),('CALENDAR');

DECLARE @WriteRevoke TABLE (ModuleName nvarchar(50) PRIMARY KEY);
INSERT INTO @WriteRevoke (ModuleName) VALUES
    ('WORKSPACE'),('LOCATION'),('SERVICE'),('CUSTOMER'),('ACCESS'),('NOTIFICATION');

/* The grants this script is about to remove. */
DECLARE @Doomed TABLE (PermissionId int PRIMARY KEY, PermissionCode nvarchar(100));

INSERT INTO @Doomed (PermissionId, PermissionCode)
SELECT p.PermissionId, p.PermissionCode
FROM   MAP_RolePermission rp
JOIN   MST_Permission     p ON p.PermissionId = rp.PermissionId
WHERE  rp.RoleId = @CustomerRoleId
  AND  (p.ModuleName IN (SELECT ModuleName FROM @FullRevoke)
        OR (p.ModuleName IN (SELECT ModuleName FROM @WriteRevoke)
            AND p.PermissionCode NOT LIKE '%.VIEW'));

/* --- backup, so the rollback restores exactly what was taken -------------- */
IF OBJECT_ID('dbo.SYS_CustomerPermissionBackup') IS NULL
    CREATE TABLE dbo.SYS_CustomerPermissionBackup
    (
        RoleId       int       NOT NULL,
        PermissionId int       NOT NULL,
        RevokedUtc   datetime2 NOT NULL
            CONSTRAINT DF_SYS_CustomerPermissionBackup_RevokedUtc DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT PK_SYS_CustomerPermissionBackup PRIMARY KEY (RoleId, PermissionId)
    );

INSERT INTO dbo.SYS_CustomerPermissionBackup (RoleId, PermissionId)
SELECT @CustomerRoleId, d.PermissionId
FROM   @Doomed d
WHERE  NOT EXISTS (SELECT 1 FROM dbo.SYS_CustomerPermissionBackup b
                   WHERE b.RoleId = @CustomerRoleId AND b.PermissionId = d.PermissionId);

/* --- revoke --------------------------------------------------------------- */
DELETE rp
FROM   MAP_RolePermission rp
WHERE  rp.RoleId = @CustomerRoleId
  AND  rp.PermissionId IN (SELECT PermissionId FROM @Doomed);

/* --- verification --------------------------------------------------------- */
SELECT 'revoked' AS Bucket, COUNT(*) AS Cnt FROM @Doomed
UNION ALL
SELECT 'remaining', COUNT(*) FROM MAP_RolePermission WHERE RoleId = @CustomerRoleId;

SELECT p.PermissionCode AS StillGrantedToCustomer
FROM   MAP_RolePermission rp
JOIN   MST_Permission p ON p.PermissionId = rp.PermissionId
WHERE  rp.RoleId = @CustomerRoleId
ORDER  BY p.PermissionCode;

/* ---------------------------------------------------------------------------
   DRY RUN -- run this on its own first. It changes nothing and prints exactly
   what the script above would remove.

   SELECT p.PermissionCode, p.ModuleName
   FROM   MAP_RolePermission rp
   JOIN   MST_Permission p ON p.PermissionId = rp.PermissionId
   WHERE  rp.RoleId = (SELECT RoleId FROM MST_Role WHERE RoleCode = 'CUSTOMER')
     AND  (p.ModuleName IN ('USER','ROLE','PERMISSION','MENU','SETTING','AUDIT',
                            'PAYROLL','EXPENSE','SUPPLIER','PURCHASE','BANK',
                            'INVENTORY','BANNER','REPORT','HOUSEKEEPING','CALENDAR')
           OR (p.ModuleName IN ('WORKSPACE','LOCATION','SERVICE','CUSTOMER',
                                'ACCESS','NOTIFICATION')
               AND p.PermissionCode NOT LIKE '%.VIEW'))
   ORDER  BY p.ModuleName, p.PermissionCode;
   --------------------------------------------------------------------------- */
