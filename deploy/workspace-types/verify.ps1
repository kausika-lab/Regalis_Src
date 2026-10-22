# Verifies the new API after the swap: the workspace-type routes are in Swagger and the
# database holds the five system types. Exit 1 stops the deploy before the frontend changes.
$ErrorActionPreference = 'Stop'
$json = $null
for ($i = 0; $i -lt 30 -and -not $json; $i++) {
    try { $json = (Invoke-WebRequest http://localhost:81/swagger/v1/swagger.json -UseBasicParsing -TimeoutSec 10).Content }
    catch { Start-Sleep -Seconds 2 }
}
if (-not $json) { Write-Host "FAIL - the API did not answer on :81"; exit 1 }
$paths = ($json | ConvertFrom-Json).paths.PSObject.Properties.Name
$failed = $false
foreach ($p in '/api/admin/workspace-types', '/api/admin/workspace-types/{id}', '/api/admin/workspace-types/code-examples') {
    if ($paths -contains $p) { Write-Host "OK  - $p" } else { Write-Host "FAIL - $p missing"; $failed = $true }
}
$sqlcmd = "C:\Program Files\Microsoft SQL Server\Client SDK\ODBC\180\Tools\Binn\sqlcmd.exe"
$types = & $sqlcmd -S localhost -d RegalisWorkSpace_UAT -E -C -h-1 -W -Q "SET NOCOUNT ON; SELECT COUNT(*) FROM MST_WorkspaceType WHERE IsSystem = 1 AND IsDeleted = 0 AND TypeCode IN ('DESK','HOT_DESK','CABIN','MEETING_ROOM','VIRTUAL_WORKSPACE')"
if ("$types".Trim() -eq '5') { Write-Host "OK  - 5 system workspace types" } else { Write-Host "FAIL - system workspace types: $types"; $failed = $true }
if ($failed) { exit 1 }
exit 0
