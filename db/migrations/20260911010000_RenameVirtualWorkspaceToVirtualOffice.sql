/* ---------------------------------------------------------------------------
   Rename the workspace type "Virtual Workspace" -> "Virtual Office"

   Data script, not DDL. The booking screen renders
   MST_WorkspaceType.TypeName verbatim on its type cards, so the display name
   lives here, not in the SPA bundle. The two virtual workspaces were already
   named "Virtual Office 01/02" -- only their description still said
   "Virtual workspace", which is corrected for consistency.

   TypeCode stays VIRTUAL_WORKSPACE on purpose: it is the stable key the API
   and any integration match on, and renaming it is a breaking change with no
   user-visible benefit.

   TRN_AuditLog is deliberately left alone -- it is a record of what was
   changed and when, and must not be rewritten.

   Idempotent -- re-running changes nothing.
   Rollback: 20260911010000_RenameVirtualWorkspaceToVirtualOffice_Rollback.sql
   --------------------------------------------------------------------------- */
SET NOCOUNT ON;
SET XACT_ABORT ON;

UPDATE MST_WorkspaceType
SET    TypeName     = N'Virtual Office',
       ModifiedDate = SYSUTCDATETIME()
WHERE  TypeCode = N'VIRTUAL_WORKSPACE'
  AND  TypeName <> N'Virtual Office';

UPDATE MST_Workspace
SET    Description  = N'Virtual office with meeting-room access on demand.',
       ModifiedDate = SYSUTCDATETIME()
WHERE  Description = N'Virtual workspace with meeting-room access on demand.';

/* Verification */
SELECT WorkspaceTypeId, TypeCode, TypeName, IsActive, IsDeleted
FROM   MST_WorkspaceType
ORDER  BY WorkspaceTypeId;

SELECT WorkspaceId, WorkspaceCode, WorkspaceName, Description
FROM   MST_Workspace
WHERE  WorkspaceCode LIKE 'RRN-V-%'
ORDER  BY WorkspaceId;
