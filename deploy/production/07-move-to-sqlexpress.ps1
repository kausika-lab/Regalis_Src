<#
  Moves RegalisWorkSpace_PROD from the Developer edition (not licensed for
  production) to a SQL Server 2025 Express instance, localhost\SQLEXPRESS.
  UAT stays on the Developer edition, which is licensed for test use.

  PREREQUISITE (this server is a domain controller): SQL Server setup needs the
  group its Browser service uses, which DC promotion deleted. Create it once,
  from an elevated PowerShell:

    New-ADGroup -Name 'SQLServer2005SQLBrowserUser$WIN-8S0SS6G8A2V' `
      -SamAccountName 'SQLServer2005SQLBrowserUser$WIN-8S0SS6G8A2V' `
      -GroupScope DomainLocal -GroupCategory Security

  Steps: install Express (setup in C:\Tools\sqlexpress2025, updated from
  Microsoft Update) -> stop the production API -> back up PROD -> restore on
  Express -> logins -> repoint the API and the nightly backup -> start and
  verify. The old copy on the Developer instance is taken OFFLINE, not dropped,
  as the fallback.

  Run elevated:  powershell -ExecutionPolicy Bypass -File 07-move-to-sqlexpress.ps1
#>
$ErrorActionPreference = 'Stop'
$setup   = 'C:\Tools\sqlexpress2025\setup\setup.exe'
$sqlcmd  = 'C:\Program Files\Microsoft SQL Server\Client SDK\ODBC\180\Tools\Binn\sqlcmd.exe'
$old     = 'localhost'
$new     = 'localhost\SQLEXPRESS'
$db      = 'RegalisWorkSpace_PROD'
$move    = 'C:\ProgramData\Regalis\dbmove'
$apiCfg  = 'C:\inetpub\wwwroot\Regalis_Prod\api\web.config'
$appcmd  = "$env:SystemRoot\System32\inetsrv\appcmd.exe"
$pool    = 'regalis-prod-api'

function Sql($server, $query, $database = 'master') {
    $out = & $sqlcmd -S $server -d $database -E -C -I -b -W -h -1 -Q "SET NOCOUNT ON; $query"
    if ($LASTEXITCODE -ne 0) { throw "sqlcmd on $server failed: $out" }
    return $out
}

# --- 1. Express instance ------------------------------------------------------------
if (-not (Get-Service 'MSSQL$SQLEXPRESS' -ErrorAction SilentlyContinue)) {
    if (-not (Get-ADGroup -Filter "Name -eq 'SQLServer2005SQLBrowserUser`$WIN-8S0SS6G8A2V'")) {
        throw 'The SQL Browser AD group is missing - create it first (see the top of this script). Nothing was changed.'
    }
    $p = Start-Process $setup -Wait -PassThru -NoNewWindow -ArgumentList '/Q', '/ACTION=Install', '/FEATURES=SQLEngine',
        '/INSTANCENAME=SQLEXPRESS', '/SQLSYSADMINACCOUNTS="BUILTIN\Administrators"', '/IACCEPTSQLSERVERLICENSETERMS',
        '/UPDATEENABLED=1', '/UPDATESOURCE=MU', '/TCPENABLED=0', '/NPENABLED=0',
        '/SQLSVCSTARTUPTYPE=Automatic', '/BROWSERSVCSTARTUPTYPE=Disabled'
    if ($p.ExitCode -ne 0) {
        throw "SQL Server setup failed ($($p.ExitCode)). See C:\Program Files\Microsoft SQL Server\170\Setup Bootstrap\Log. Production is unchanged."
    }
}
Write-Host "Express: $(Sql $new 'SELECT @@VERSION' | Select-Object -First 1)"
if ((Sql $new "SELECT SERVERPROPERTY('EngineEdition')").Trim() -ne '4') { throw "$new is not an Express instance." }

$alreadyMoved = (Sql $new "SELECT COUNT(*) FROM sys.databases WHERE name = N'$db'").Trim() -eq '1'
if (-not $alreadyMoved) {
    # --- 2. Freeze production, back up, restore on Express ------------------------
    & $appcmd stop apppool /apppool.name:$pool | Out-Null
    try {
        New-Item -ItemType Directory -Force $move | Out-Null
        icacls $move /grant 'NT Service\MSSQLSERVER:(OI)(CI)M' 'NT Service\MSSQL$SQLEXPRESS:(OI)(CI)RX' | Out-Null
        $bak = "$move\${db}_move_$(Get-Date -Format yyyyMMdd_HHmm).bak"
        Sql $old "BACKUP DATABASE [$db] TO DISK = N'$bak' WITH COPY_ONLY, INIT, CHECKSUM" | Out-Null

        $data = (Sql $new "SELECT CAST(SERVERPROPERTY('InstanceDefaultDataPath') AS nvarchar(260))").Trim()
        $log  = (Sql $new "SELECT CAST(SERVERPROPERTY('InstanceDefaultLogPath') AS nvarchar(260))").Trim()
        Sql $new ("RESTORE DATABASE [$db] FROM DISK = N'$bak' WITH " +
                  "MOVE N'RegalisWorkSpace_UAT' TO N'${data}$db.mdf', " +
                  "MOVE N'RegalisWorkSpace_UAT_log' TO N'${log}${db}_log.ldf', CHECKSUM, RECOVERY") | Out-Null
        Sql $new "ALTER DATABASE [$db] SET RECOVERY SIMPLE" | Out-Null
    } catch {
        & $appcmd start apppool /apppool.name:$pool | Out-Null
        throw "Move failed before the API was repointed; production still runs on the old instance. $_"
    }
}

# --- 3. Logins (the restored database users map to these Windows SIDs) --------------
Sql $new @"
IF SUSER_ID(N'IIS APPPOOL\$pool') IS NULL CREATE LOGIN [IIS APPPOOL\$pool] FROM WINDOWS WITH DEFAULT_DATABASE = [$db];
IF SUSER_ID(N'NT AUTHORITY\SYSTEM') IS NULL CREATE LOGIN [NT AUTHORITY\SYSTEM] FROM WINDOWS;
"@ | Out-Null
Sql $new @"
IF USER_ID(N'IIS APPPOOL\$pool') IS NULL CREATE USER [IIS APPPOOL\$pool] FOR LOGIN [IIS APPPOOL\$pool];
ALTER ROLE db_datareader ADD MEMBER [IIS APPPOOL\$pool];
ALTER ROLE db_datawriter ADD MEMBER [IIS APPPOOL\$pool];
"@ $db | Out-Null

# --- 4. Repoint the API --------------------------------------------------------------
[xml]$x = Get-Content $apiCfg -Raw
$cs = $x.SelectSingleNode("//environmentVariable[@name='ConnectionStrings__Default']")
$cs.SetAttribute('value', "Server=$new;Database=$db;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=True")
$x.Save($apiCfg)

# --- 5. Nightly backup now targets Express ---------------------------------------
& "$PSScriptRoot\05-backup-task.ps1"

# --- 6. Start and verify --------------------------------------------------------------
& $appcmd start apppool /apppool.name:$pool | Out-Null
& $appcmd recycle apppool /apppool.name:$pool | Out-Null
Start-Sleep -Seconds 5
$health = (Invoke-WebRequest http://127.0.0.1:82/health -UseBasicParsing -TimeoutSec 60).Content
$onNew  = (Sql $new "SELECT COUNT(*) FROM sys.dm_exec_sessions WHERE login_name = N'IIS APPPOOL\$pool' AND database_id = DB_ID(N'$db')").Trim()
if ($health -ne 'Healthy' -or $onNew -eq '0') {
    throw "Production API health '$health', sessions on Express: $onNew. Check C:\inetpub\wwwroot\Regalis_Prod\api\logs. The old database is still ONLINE on $old."
}

# --- 7. Old copy offline (fallback, not dropped) ---------------------------------
Sql $old "IF DB_ID(N'$db') IS NOT NULL AND DATABASEPROPERTYEX(N'$db', 'Status') = 'ONLINE' ALTER DATABASE [$db] SET OFFLINE WITH ROLLBACK IMMEDIATE" | Out-Null
Write-Host "Done. $db runs on $new; the copy on $old is OFFLINE."
