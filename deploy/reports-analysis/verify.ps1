# Verifies the new API after the swap: the Reports & Analysis routes are in Swagger, an
# anonymous call is refused (not 404), and the database holds the migration, the three
# new permissions and the five sidebar children. Exit 1 stops the deploy before the
# frontend changes.
$ErrorActionPreference = 'Stop'
$json = $null
for ($i = 0; $i -lt 30 -and -not $json; $i++) {
    try { $json = (Invoke-WebRequest http://localhost:81/swagger/v1/swagger.json -UseBasicParsing -TimeoutSec 10).Content }
    catch { Start-Sleep -Seconds 2 }
}
if (-not $json) { Write-Host "FAIL - the API did not answer on :81"; exit 1 }
$paths = ($json | ConvertFrom-Json).paths.PSObject.Properties.Name
$failed = $false
foreach ($p in '/api/admin/reports/overview', '/api/admin/reports/overview/pdf', '/api/admin/reports/definitions',
               '/api/admin/reports/{key}/run', '/api/admin/reports/{key}/export', '/api/admin/reports/schedules',
               '/api/admin/reports/favourites/{key}', '/api/admin/reports/exports/{token}', '/api/admin/reports/metrics/rebuild') {
    if ($paths -contains $p) { Write-Host "OK  - $p" } else { Write-Host "FAIL - $p missing"; $failed = $true }
}
try {
    Invoke-WebRequest http://localhost:81/api/admin/reports/overview -UseBasicParsing -TimeoutSec 15 | Out-Null
    Write-Host "FAIL - /api/admin/reports/overview answered an anonymous caller"; $failed = $true
} catch {
    $code = [int]$_.Exception.Response.StatusCode
    if ($code -eq 401) { Write-Host "OK  - /api/admin/reports/overview refuses an anonymous caller (401)" }
    else { Write-Host "FAIL - /api/admin/reports/overview answered $code"; $failed = $true }
}
$sqlcmd = "C:\Program Files\Microsoft SQL Server\Client SDK\ODBC\180\Tools\Binn\sqlcmd.exe"
$row = & $sqlcmd -S localhost -d RegalisWorkSpace_UAT -E -C -h-1 -W -Q "SET NOCOUNT ON; SELECT CAST((SELECT COUNT(*) FROM __EFMigrationsHistory WHERE MigrationId = '20261024033724_ReportsAnalysis') AS varchar) + '|' + CAST((SELECT COUNT(*) FROM MST_Permission WHERE PermissionCode IN ('REPORT.FINANCE','REPORT.CUSTOMERS','REPORT.MANAGE')) AS varchar) + '|' + CAST((SELECT COUNT(*) FROM MST_Menu WHERE MenuCode LIKE 'ADM_REPORTS[_]%' AND IsDeleted = 0) AS varchar)"
$parts = "$row".Trim().Split('|')
if ($parts[0] -eq '1') { Write-Host "OK  - migration 20261024033724_ReportsAnalysis recorded" } else { Write-Host "FAIL - migration not recorded"; $failed = $true }
if ($parts[1] -eq '3') { Write-Host "OK  - 3 new REPORT permissions" } else { Write-Host "FAIL - REPORT permissions: $($parts[1])"; $failed = $true }
if ($parts[2] -eq '5') { Write-Host "OK  - 5 Reports & Analysis sub-menus" } else { Write-Host "FAIL - sub-menus: $($parts[2])"; $failed = $true }
if ($failed) { exit 1 }
exit 0
