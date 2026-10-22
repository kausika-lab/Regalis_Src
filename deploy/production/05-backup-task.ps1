<#
  Nightly full backup of RegalisWorkSpace_PROD at 02:00, keeping 14 days.

  Targets whichever instance production uses (from the production API's
  connection string): localhost\SQLEXPRESS after 07-move-to-sqlexpress.ps1,
  localhost before it. Express has no SQL Agent and no backup compression, so a
  Windows scheduled task (as SYSTEM) runs a plain backup. SYSTEM gets
  db_backupoperator on the production database only. Files land in that
  instance's Backup folder as RegalisWorkSpace_PROD_yyyyMMdd_HHmm.bak.

  Run elevated:  powershell -ExecutionPolicy Bypass -File 05-backup-task.ps1
#>
$ErrorActionPreference = 'Stop'
$sqlcmd = 'C:\Program Files\Microsoft SQL Server\Client SDK\ODBC\180\Tools\Binn\sqlcmd.exe'
$runner = 'C:\ProgramData\Regalis\backup-prod.ps1'

[xml]$cfg = Get-Content 'C:\inetpub\wwwroot\Regalis_Prod\api\web.config' -Raw
$cs = $cfg.SelectSingleNode("//environmentVariable[@name='ConnectionStrings__Default']").value
$server = [regex]::Match($cs, 'Server=([^;]+)').Groups[1].Value

& $sqlcmd -S $server -E -C -b -Q "IF SUSER_ID(N'NT AUTHORITY\SYSTEM') IS NULL CREATE LOGIN [NT AUTHORITY\SYSTEM] FROM WINDOWS;"
if ($LASTEXITCODE -ne 0) { throw 'Creating the SYSTEM login failed.' }
& $sqlcmd -S $server -d RegalisWorkSpace_PROD -E -C -b -Q @"
IF USER_ID(N'NT AUTHORITY\SYSTEM') IS NULL CREATE USER [NT AUTHORITY\SYSTEM] FOR LOGIN [NT AUTHORITY\SYSTEM];
ALTER ROLE db_backupoperator ADD MEMBER [NT AUTHORITY\SYSTEM];
"@
if ($LASTEXITCODE -ne 0) { throw 'Granting backup rights failed.' }
$dir = (& $sqlcmd -S $server -E -C -b -W -h -1 -Q "SET NOCOUNT ON; SELECT CAST(SERVERPROPERTY('InstanceDefaultBackupPath') AS nvarchar(260))" | Select-Object -First 1).Trim()

Set-Content $runner -Encoding UTF8 -Value @"
# Written by Regalis_Src\deploy\production\05-backup-task.ps1 - edit that, not this.
`$sqlcmd = '$sqlcmd'
`$dir    = '$dir'
`$file   = "RegalisWorkSpace_PROD_`$(Get-Date -Format yyyyMMdd_HHmm).bak"
& `$sqlcmd -S '$server' -E -C -b -Q "BACKUP DATABASE [RegalisWorkSpace_PROD] TO DISK = N'`$dir\`$file' WITH INIT, CHECKSUM"
if (`$LASTEXITCODE -ne 0) { exit `$LASTEXITCODE }
Get-ChildItem `$dir -Filter 'RegalisWorkSpace_PROD_*.bak' |
    Where-Object LastWriteTime -lt (Get-Date).AddDays(-14) | Remove-Item -Force
"@

$action    = New-ScheduledTaskAction -Execute 'powershell.exe' -Argument "-NoProfile -ExecutionPolicy Bypass -File `"$runner`""
$trigger   = New-ScheduledTaskTrigger -Daily -At 02:00
$principal = New-ScheduledTaskPrincipal -UserId 'SYSTEM' -LogonType ServiceAccount -RunLevel Highest
$settings  = New-ScheduledTaskSettingsSet -StartWhenAvailable -ExecutionTimeLimit (New-TimeSpan -Hours 1)
Register-ScheduledTask -TaskName 'Regalis PROD nightly backup' -Action $action -Trigger $trigger `
    -Principal $principal -Settings $settings -Force | Out-Null
Write-Host "Scheduled: Regalis PROD nightly backup, daily 02:00, on $server -> $dir"
