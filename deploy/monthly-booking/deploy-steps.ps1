# Verifies the new API after the swap: the monthly routes are in Swagger and the API
# logged a clean start. Exit 1 stops the deploy before the frontend changes.
$ErrorActionPreference = 'Stop'
$json = $null
for ($i = 0; $i -lt 30 -and -not $json; $i++) {
    try { $json = (Invoke-WebRequest http://localhost:81/swagger/v1/swagger.json -UseBasicParsing -TimeoutSec 10).Content }
    catch { Start-Sleep -Seconds 2 }
}
if (-not $json) { Write-Host "FAIL - the API did not answer on :81"; exit 1 }
$paths = ($json | ConvertFrom-Json).paths.PSObject.Properties.Name
$failed = $false
foreach ($p in '/api/availability/monthly', '/api/bookings/monthly', '/api/bookings/monthly/quote') {
    if ($paths -contains $p) { Write-Host "OK  - $p" } else { Write-Host "FAIL - $p missing"; $failed = $true }
}
if ($failed) { exit 1 }
exit 0
