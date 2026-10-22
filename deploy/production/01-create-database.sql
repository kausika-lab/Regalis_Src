/* ===========================================================================
   Production database: a copy of UAT's schema and master data.

   Takes a COPY_ONLY backup of RegalisWorkSpace_UAT (does not disturb UAT's
   backup chain) and restores it as RegalisWorkSpace_PROD. 02-clean-data.sql
   then strips the test customers and every transaction.

   Restoring (instead of re-running every migration) gives production exactly
   the schema and __EFMigrationsHistory that UAT has, so later UAT migrations
   apply to production unchanged.

   Run: sqlcmd -S localhost -E -C -I -b -i 01-create-database.sql
   Stops if RegalisWorkSpace_PROD already exists - it never overwrites.
   =========================================================================== */
SET NOCOUNT ON;

IF DB_ID(N'RegalisWorkSpace_PROD') IS NOT NULL
BEGIN
    RAISERROR(N'RegalisWorkSpace_PROD already exists. Nothing was changed.', 16, 1);
    RETURN;
END

DECLARE @data nvarchar(260) = CAST(SERVERPROPERTY('InstanceDefaultDataPath') AS nvarchar(260));
DECLARE @log  nvarchar(260) = CAST(SERVERPROPERTY('InstanceDefaultLogPath')  AS nvarchar(260));
DECLARE @bak  nvarchar(260) = CAST(SERVERPROPERTY('InstanceDefaultBackupPath') AS nvarchar(260))
                              + N'\RegalisWorkSpace_UAT_to_PROD_' + CONVERT(nvarchar(8), GETDATE(), 112) + N'.bak';

BACKUP DATABASE [RegalisWorkSpace_UAT] TO DISK = @bak WITH COPY_ONLY, INIT, CHECKSUM;

DECLARE @mdf nvarchar(400) = @data + N'RegalisWorkSpace_PROD.mdf';
DECLARE @ldf nvarchar(400) = @log  + N'RegalisWorkSpace_PROD_log.ldf';

RESTORE DATABASE [RegalisWorkSpace_PROD] FROM DISK = @bak
WITH MOVE N'RegalisWorkSpace_UAT'     TO @mdf,
     MOVE N'RegalisWorkSpace_UAT_log' TO @ldf,
     CHECKSUM, RECOVERY;

-- Nightly full backups (05-backup-job.sql) are the recovery plan, so the log
-- does not need to grow between log backups.
ALTER DATABASE [RegalisWorkSpace_PROD] SET RECOVERY SIMPLE;

PRINT N'RegalisWorkSpace_PROD restored from ' + @bak;
