# Verifies the new API after the swap: the Assets routes are in Swagger, an anonymous call
# is refused (not 404), and the database holds the migration, categories and rooms.
# Exit 1 stops the deploy before the frontend changes.
$ErrorActionPreference = 'Stop'
$json = $null
for ($i = 0; $i -lt 30 -and -not $json; $i++) {
    try { $json = (Invoke-WebRequest http://localhost:81/swagger/v1/swagger.json -UseBasicParsing -TimeoutSec 10).Content }
    catch { Start-Sleep -Seconds 2 }
}
if (-not $json) { Write-Host "FAIL - the API did not answer on :81"; exit 1 }
$paths = ($json | ConvertFrom-Json).paths.PSObject.Properties.Name
$failed = $false
foreach ($p in '/api/admin/items', '/api/admin/items/{id}', '/api/admin/items/labels', '/api/admin/items/import/preview',
               '/api/admin/goods-receipts', '/api/admin/goods-receipts/{id}/post', '/api/admin/goods-receipts/{id}/reverse',
               '/api/admin/goods-issues', '/api/admin/stock', '/api/admin/stock/ledger', '/api/admin/stock-counts') {
    if ($paths -contains $p) { Write-Host "OK  - $p" } else { Write-Host "FAIL - $p missing"; $failed = $true }
}
try {
    Invoke-WebRequest http://localhost:81/api/admin/items -UseBasicParsing -TimeoutSec 15 | Out-Null
    Write-Host "FAIL - /api/admin/items answered an anonymous caller"; $failed = $true
} catch {
    $code = [int]$_.Exception.Response.StatusCode
    if ($code -eq 401) { Write-Host "OK  - /api/admin/items refuses an anonymous caller (401)" }
    else { Write-Host "FAIL - /api/admin/items answered $code"; $failed = $true }
}
$sqlcmd = "C:\Program Files\Microsoft SQL Server\Client SDK\ODBC\180\Tools\Binn\sqlcmd.exe"
$row = & $sqlcmd -S localhost -d RegalisWorkSpace_UAT -E -C -h-1 -W -Q "SET NOCOUNT ON; SELECT CAST((SELECT COUNT(*) FROM __EFMigrationsHistory WHERE MigrationId = '20261016030926_AssetsModule') AS varchar) + '|' + CAST((SELECT COUNT(*) FROM MST_AssetCategory WHERE IsDeleted = 0) AS varchar) + '|' + CAST((SELECT COUNT(*) FROM MST_AssetLocation WHERE IsDeleted = 0) AS varchar) + '|' + CAST((SELECT COUNT(*) FROM MST_Permission WHERE PermissionCode LIKE 'INVENTORY.%') AS varchar)"
$parts = "$row".Trim().Split('|')
if ($parts[0] -eq '1') { Write-Host "OK  - migration 20261016030926_AssetsModule recorded" } else { Write-Host "FAIL - migration not recorded"; $failed = $true }
if ([int]$parts[1] -ge 1) { Write-Host "OK  - $($parts[1]) asset categories" } else { Write-Host "FAIL - no asset categories"; $failed = $true }
if ([int]$parts[2] -ge 1) { Write-Host "OK  - $($parts[2]) rooms" } else { Write-Host "FAIL - no rooms"; $failed = $true }
if ([int]$parts[3] -ge 10) { Write-Host "OK  - $($parts[3]) INVENTORY permissions" } else { Write-Host "FAIL - INVENTORY permissions: $($parts[3])"; $failed = $true }
if ($failed) { exit 1 }
exit 0
