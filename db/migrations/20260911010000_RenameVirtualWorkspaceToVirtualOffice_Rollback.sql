/* Rollback for 20260911010000_RenameVirtualWorkspaceToVirtualOffice.sql
   Restores the "Virtual Workspace" wording. Also revert the hard-coded empty
   state sentence in frontend/assets/BookWorkspacePage-*.js if you run this. */
SET NOCOUNT ON;
SET XACT_ABORT ON;

UPDATE MST_WorkspaceType
SET    TypeName     = N'Virtual Workspace',
       ModifiedDate = SYSUTCDATETIME()
WHERE  TypeCode = N'VIRTUAL_WORKSPACE'
  AND  TypeName <> N'Virtual Workspace';

UPDATE MST_Workspace
SET    Description  = N'Virtual workspace with meeting-room access on demand.',
       ModifiedDate = SYSUTCDATETIME()
WHERE  Description = N'Virtual office with meeting-room access on demand.';

SELECT WorkspaceTypeId, TypeCode, TypeName FROM MST_WorkspaceType ORDER BY WorkspaceTypeId;
