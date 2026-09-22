/* ---------------------------------------------------------------------------
   Rollback for 20260920000000_SyncWorkspaceMaintenanceStatus.sql.

   Removes the write-through sync and leaves MST_Workspace.Status back under the
   sole control of PATCH /api/workspaces/{id}/status, exactly as before.

   Order matters. Before dropping anything, every workspace this process put
   into maintenance is restored to the status it displaced -- otherwise those
   rows would be stranded at Status 3 with nothing left to release them, which
   is the same orphaned-value problem the migration set out to fix.

   Workspaces an admin set to Maintenance by hand are not in the provenance
   table and are correctly left alone.

   Idempotent -- safe to re-run, and safe to run if the migration was never
   applied.
   --------------------------------------------------------------------------- */
SET NOCOUNT ON;
SET XACT_ABORT ON;
GO

/* --- 1. drop the trigger first, so the restore below does not re-fire it --- */
IF OBJECT_ID('dbo.TR_HousekeepingSchedule_SyncWorkspaceStatus', 'TR') IS NOT NULL
    DROP TRIGGER dbo.TR_HousekeepingSchedule_SyncWorkspaceStatus;
GO

/* --- 2. release anything the sync is currently holding -------------------- */
IF OBJECT_ID('dbo.SYS_WorkspaceMaintenanceAuto') IS NOT NULL
BEGIN
    DECLARE @Now datetime2 = SYSUTCDATETIME();
    DECLARE @By  bigint    = (SELECT TOP 1 UserId FROM MST_User WHERE Email = 'admin@regalis.in');

    UPDATE w
    SET    w.Status       = s.PreviousStatus,
           w.ModifiedBy   = @By,
           w.ModifiedDate = @Now
    FROM   MST_Workspace w
    JOIN   dbo.SYS_WorkspaceMaintenanceAuto s ON s.WorkspaceId = w.WorkspaceId
    WHERE  w.IsDeleted = 0
      AND  w.Status    = 3;
END
GO

/* --- 3. drop the Agent job ------------------------------------------------ */
IF EXISTS (SELECT 1 FROM msdb.dbo.sysjobs
           WHERE name = N'Regalis - Sync workspace maintenance status')
    EXEC msdb.dbo.sp_delete_job
         @job_name = N'Regalis - Sync workspace maintenance status',
         @delete_unused_schedule = 1;
GO

/* --- 4. drop the reconciler and its provenance table ---------------------- */
IF OBJECT_ID('dbo.usp_SyncWorkspaceMaintenanceStatus', 'P') IS NOT NULL
    DROP PROCEDURE dbo.usp_SyncWorkspaceMaintenanceStatus;
GO

IF OBJECT_ID('dbo.SYS_WorkspaceMaintenanceAuto') IS NOT NULL
    DROP TABLE dbo.SYS_WorkspaceMaintenanceAuto;
GO

/* --- verification --------------------------------------------------------- */
SELECT 'trigger'   AS Object, CASE WHEN OBJECT_ID('dbo.TR_HousekeepingSchedule_SyncWorkspaceStatus','TR') IS NULL THEN 'dropped' ELSE 'STILL PRESENT' END AS State
UNION ALL
SELECT 'procedure', CASE WHEN OBJECT_ID('dbo.usp_SyncWorkspaceMaintenanceStatus','P')  IS NULL THEN 'dropped' ELSE 'STILL PRESENT' END
UNION ALL
SELECT 'table',     CASE WHEN OBJECT_ID('dbo.SYS_WorkspaceMaintenanceAuto')            IS NULL THEN 'dropped' ELSE 'STILL PRESENT' END
UNION ALL
SELECT 'agent job', CASE WHEN NOT EXISTS (SELECT 1 FROM msdb.dbo.sysjobs WHERE name = N'Regalis - Sync workspace maintenance status') THEN 'dropped' ELSE 'STILL PRESENT' END;

SELECT WorkspaceId, WorkspaceCode, WorkspaceName, Status
FROM   MST_Workspace
WHERE  IsDeleted = 0 AND Status <> 1
ORDER  BY WorkspaceId;
