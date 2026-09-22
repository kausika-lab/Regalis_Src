/* ---------------------------------------------------------------------------
   Write-through sync: make MST_Workspace.Status follow the Maintenance screen.

   NOT YET APPLIED. Reviewed and held pending a decision -- see CAVEATS.

   WHY THIS IS A DATABASE SCRIPT AND NOT A SERVICE CHANGE
   -----------------------------------------------------
   MST_Workspace.Status is written by exactly one code path --
   PATCH /api/workspaces/{id}/status. Housekeeping never touches it; per Design
   Invariant 12 a blocking schedule writes TRN_WorkspaceBlock instead, and that
   is what drops the slot from customer availability. So the two screens have
   always described maintenance with two unrelated values.

   Reconciling them properly belongs in HousekeepingService. This deployment
   ships api/ as compiled DLLs with no C# source, so the only server-side lever
   is the database. That is a real trade-off -- read the CAVEATS before applying.

   WHAT IT DOES
   ------------
   dbo.usp_SyncWorkspaceMaintenanceStatus reconciles workspaces against the
   schedules that are blocking RIGHT NOW:

       BlocksWorkspace = 1
       Status IN (1 Scheduled, 2 InProgress)
       StartUtc <= now < EndUtc          -- half-open, per the calendar rule

   A workspace that matches is set to Status 3 (Maintenance). When the window
   closes, or the task is completed/cancelled, it is restored.

   MANUAL MAINTENANCE IS PRESERVED
   -------------------------------
   The reconciler must not undo an admin who set Maintenance by hand from the
   Workspaces dropdown. SYS_WorkspaceMaintenanceAuto records which workspaces
   THIS process put into maintenance, and the status it displaced. Only those
   rows are ever restored; a manually-set workspace is never touched. That table
   is provenance, not a second status value -- Status remains the single stored
   answer to "is this workspace under maintenance".

   It sits outside the MST_/TRN_/MAP_ scheme on purpose: those prefixes mean an
   entity in the EF model, and this table is database-side infrastructure that
   Regalis.Domain knows nothing about.

   TWO MECHANISMS, BECAUSE A CLOCK TICK IS NOT A WRITE
   ---------------------------------------------------
   TR_HousekeepingSchedule_SyncWorkspaceStatus fires on schedule writes, which
   covers create / edit / cancel / check-out. It does NOT cover the clock: a
   window opening or closing is not a write. That is what the Agent job at the
   bottom is for. Without the job, a window will not take effect until something
   writes to the row again.

   CAVEATS -- READ BEFORE APPLYING
   -------------------------------
   * MST_Workspace.RowVersion is a rowversion column used by EF for optimistic
     concurrency. Every status flip bumps it, so an admin holding an open
     workspace edit form while a maintenance window opens will get a concurrency
     failure on save. Acceptable, but it is a real behaviour change.
   * The C# SaveChanges interceptor does not run, so no TRN_AuditLog row is
     written for these status changes. The proc sets ModifiedBy/ModifiedDate so
     the change is at least attributable.
   * Business logic in a trigger is invisible to anyone reading the C#. If the
     API source is ever restored, move this into HousekeepingService and drop
     this migration -- the rollback script does that cleanly.
   * @Database_name in the Agent job step is hard-coded to RegalisWorkSpace_UAT.
     Change it before running this anywhere else.

   Idempotent -- safe to re-run.
   Rollback: 20260920000000_SyncWorkspaceMaintenanceStatus_Rollback.sql
   --------------------------------------------------------------------------- */
SET NOCOUNT ON;
SET XACT_ABORT ON;
GO

/* --- provenance table ---------------------------------------------------- */
IF OBJECT_ID('dbo.SYS_WorkspaceMaintenanceAuto') IS NULL
BEGIN
    CREATE TABLE dbo.SYS_WorkspaceMaintenanceAuto
    (
        WorkspaceId    int       NOT NULL
            CONSTRAINT PK_SYS_WorkspaceMaintenanceAuto PRIMARY KEY,
        PreviousStatus tinyint   NOT NULL,
        AppliedUtc     datetime2 NOT NULL
            CONSTRAINT DF_SYS_WorkspaceMaintenanceAuto_AppliedUtc DEFAULT (SYSUTCDATETIME())
    );
END
GO

/* --- reconciler ---------------------------------------------------------- */
CREATE OR ALTER PROCEDURE dbo.usp_SyncWorkspaceMaintenanceStatus
    @WorkspaceId int = NULL   -- NULL reconciles every workspace
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @Now datetime2 = SYSUTCDATETIME();
    DECLARE @By  bigint    = (SELECT TOP 1 UserId FROM MST_User WHERE Email = 'admin@regalis.in');

    /* Workspaces whose maintenance window is open right now. */
    DECLARE @Active TABLE (WorkspaceId int PRIMARY KEY);

    INSERT INTO @Active (WorkspaceId)
    SELECT DISTINCT h.WorkspaceId
    FROM   TRN_HousekeepingSchedule h
    WHERE  h.WorkspaceId    IS NOT NULL
      AND  h.IsDeleted       = 0
      AND  h.BlocksWorkspace = 1
      AND  h.Status         IN (1, 2)          -- Scheduled, InProgress
      AND  h.StartUtc       <= @Now
      AND  h.EndUtc          > @Now            -- half-open [StartUtc, EndUtc)
      AND  (@WorkspaceId IS NULL OR h.WorkspaceId = @WorkspaceId);

    /* 1. Entering maintenance -- remember the status being displaced. */
    INSERT INTO dbo.SYS_WorkspaceMaintenanceAuto (WorkspaceId, PreviousStatus, AppliedUtc)
    SELECT w.WorkspaceId, w.Status, @Now
    FROM   MST_Workspace w
    JOIN   @Active a ON a.WorkspaceId = w.WorkspaceId
    WHERE  w.IsDeleted = 0
      AND  w.Status   <> 3
      AND  NOT EXISTS (SELECT 1 FROM dbo.SYS_WorkspaceMaintenanceAuto s
                       WHERE  s.WorkspaceId = w.WorkspaceId);

    UPDATE w
    SET    w.Status       = 3,
           w.ModifiedBy   = @By,
           w.ModifiedDate = @Now
    FROM   MST_Workspace w
    JOIN   @Active a ON a.WorkspaceId = w.WorkspaceId
    WHERE  w.IsDeleted = 0
      AND  w.Status   <> 3;

    /* 2. Leaving maintenance -- restore only what we put there. */
    UPDATE w
    SET    w.Status       = s.PreviousStatus,
           w.ModifiedBy   = @By,
           w.ModifiedDate = @Now
    FROM   MST_Workspace w
    JOIN   dbo.SYS_WorkspaceMaintenanceAuto s ON s.WorkspaceId = w.WorkspaceId
    WHERE  w.IsDeleted = 0
      AND  w.Status    = 3
      AND  NOT EXISTS (SELECT 1 FROM @Active a WHERE a.WorkspaceId = w.WorkspaceId)
      AND  (@WorkspaceId IS NULL OR w.WorkspaceId = @WorkspaceId);

    DELETE s
    FROM   dbo.SYS_WorkspaceMaintenanceAuto s
    WHERE  NOT EXISTS (SELECT 1 FROM @Active a WHERE a.WorkspaceId = s.WorkspaceId)
      AND  (@WorkspaceId IS NULL OR s.WorkspaceId = @WorkspaceId);
END
GO

/* --- trigger: schedule writes -------------------------------------------- */
CREATE OR ALTER TRIGGER dbo.TR_HousekeepingSchedule_SyncWorkspaceStatus
ON dbo.TRN_HousekeepingSchedule
AFTER INSERT, UPDATE, DELETE
AS
BEGIN
    SET NOCOUNT ON;
    IF TRIGGER_NESTLEVEL() > 1 RETURN;

    /* Both sides of the statement: a schedule can be moved between workspaces,
       which leaves the old one to be released and the new one to be blocked. */
    DECLARE @Touched TABLE (WorkspaceId int PRIMARY KEY);

    INSERT INTO @Touched (WorkspaceId)
    SELECT DISTINCT WorkspaceId FROM (
        SELECT WorkspaceId FROM inserted WHERE WorkspaceId IS NOT NULL
        UNION
        SELECT WorkspaceId FROM deleted  WHERE WorkspaceId IS NOT NULL
    ) x;

    IF NOT EXISTS (SELECT 1 FROM @Touched) RETURN;

    DECLARE @Id int;
    DECLARE c CURSOR LOCAL FAST_FORWARD FOR SELECT WorkspaceId FROM @Touched;
    OPEN c;
    FETCH NEXT FROM c INTO @Id;
    WHILE @@FETCH_STATUS = 0
    BEGIN
        EXEC dbo.usp_SyncWorkspaceMaintenanceStatus @WorkspaceId = @Id;
        FETCH NEXT FROM c INTO @Id;
    END
    CLOSE c;
    DEALLOCATE c;
END
GO

/* --- first reconcile ----------------------------------------------------- */
EXEC dbo.usp_SyncWorkspaceMaintenanceStatus;
GO

/* --- Agent job: closes the clock gap --------------------------------------
   The trigger cannot see a window open or close, because neither is a write.
   This job reconciles every 5 minutes. Skipped automatically if SQL Agent is
   not running on this host (e.g. SQL Express) -- on such a host write-through
   only half works, and the derived view in the SPA is the better fit.
   --------------------------------------------------------------------------- */
IF EXISTS (SELECT 1 FROM sys.dm_server_services
           WHERE servicename LIKE 'SQL Server Agent%' AND status_desc = 'Running')
BEGIN
    IF NOT EXISTS (SELECT 1 FROM msdb.dbo.sysjobs
                   WHERE name = N'Regalis - Sync workspace maintenance status')
    BEGIN
        EXEC msdb.dbo.sp_add_job
             @job_name    = N'Regalis - Sync workspace maintenance status',
             @description = N'Reconciles MST_Workspace.Status against open housekeeping maintenance windows.';

        EXEC msdb.dbo.sp_add_jobstep
             @job_name      = N'Regalis - Sync workspace maintenance status',
             @step_name     = N'Reconcile',
             @subsystem     = N'TSQL',
             @database_name = N'RegalisWorkSpace_UAT',
             @command       = N'EXEC dbo.usp_SyncWorkspaceMaintenanceStatus;';

        EXEC msdb.dbo.sp_add_jobschedule
             @job_name          = N'Regalis - Sync workspace maintenance status',
             @name              = N'Every 5 minutes',
             @freq_type         = 4,
             @freq_interval     = 1,
             @freq_subday_type  = 4,
             @freq_subday_interval = 5;

        EXEC msdb.dbo.sp_add_jobserver
             @job_name = N'Regalis - Sync workspace maintenance status';
    END
END
ELSE
    PRINT 'SQL Server Agent is not running -- the 5-minute reconcile job was NOT created.';
GO

/* --- verification -------------------------------------------------------- */
SELECT w.WorkspaceId, w.WorkspaceCode, w.WorkspaceName, w.Status,
       CASE WHEN s.WorkspaceId IS NULL THEN 'manual/none' ELSE 'schedule-driven' END AS Source,
       s.PreviousStatus
FROM   MST_Workspace w
LEFT   JOIN dbo.SYS_WorkspaceMaintenanceAuto s ON s.WorkspaceId = w.WorkspaceId
WHERE  w.IsDeleted = 0
  AND  (w.Status <> 1 OR s.WorkspaceId IS NOT NULL)
ORDER  BY w.WorkspaceId;
