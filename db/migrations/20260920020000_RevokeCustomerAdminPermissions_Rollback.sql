/* ---------------------------------------------------------------------------
   Rollback for 20260920020000_RevokeCustomerAdminPermissions.sql.

   Restores exactly the grants that script removed, by replaying its backup
   table SYS_CustomerPermissionBackup. It does not re-grant "everything" -- if
   the forward script was never run, or the backup is empty, this does nothing.

   Restoring puts three live customer accounts back to holding USER.DELETE,
   ROLE.EDIT, SETTING.EDIT and the rest. Only run it if the revocation actually
   broke something, and note what broke -- that is the signal for which
   permission a customer legitimately needs, and it should then be granted
   individually rather than by restoring the whole set.

   Takes effect on each customer's next login (JWT perm claims, up to
   Jwt.AccessTokenMinutes = 15).

   Idempotent -- safe to re-run.
   --------------------------------------------------------------------------- */
SET NOCOUNT ON;
SET XACT_ABORT ON;

IF OBJECT_ID('dbo.SYS_CustomerPermissionBackup') IS NULL
BEGIN
    PRINT 'No backup table - the revocation was never applied. Nothing to restore.';
    RETURN;
END

DECLARE @CustomerRoleId int = (SELECT RoleId FROM MST_Role WHERE RoleCode = 'CUSTOMER');
IF @CustomerRoleId IS NULL
    THROW 50000, 'Customer role not found - nothing restored.', 1;

INSERT INTO MAP_RolePermission (RoleId, PermissionId)
SELECT b.RoleId, b.PermissionId
FROM   dbo.SYS_CustomerPermissionBackup b
WHERE  b.RoleId = @CustomerRoleId
  AND  EXISTS (SELECT 1 FROM MST_Permission p WHERE p.PermissionId = b.PermissionId)
  AND  NOT EXISTS (SELECT 1 FROM MAP_RolePermission rp
                   WHERE rp.RoleId = b.RoleId AND rp.PermissionId = b.PermissionId);

DELETE FROM dbo.SYS_CustomerPermissionBackup WHERE RoleId = @CustomerRoleId;

/* Drop the backup table once it is empty, so a later forward run starts clean. */
IF NOT EXISTS (SELECT 1 FROM dbo.SYS_CustomerPermissionBackup)
    DROP TABLE dbo.SYS_CustomerPermissionBackup;

/* --- verification --------------------------------------------------------- */
SELECT COUNT(*) AS CustomerGrantsNow
FROM   MAP_RolePermission
WHERE  RoleId = @CustomerRoleId;

SELECT CASE WHEN OBJECT_ID('dbo.SYS_CustomerPermissionBackup') IS NULL
            THEN 'backup table dropped' ELSE 'backup table still has rows' END AS BackupState;
