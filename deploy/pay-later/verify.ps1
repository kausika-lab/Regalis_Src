# Helper for deploy-pay-later.cmd: the running API publishes the Pay later endpoints.
$ErrorActionPreference = 'Stop'
try {
    $swagger = (Invoke-WebRequest 'http://localhost:81/swagger/v1/swagger.json' -UseBasicParsing -TimeoutSec 60).Content | ConvertFrom-Json
} catch {
    Write-Host "FAIL- API did not answer: $($_.Exception.Message) - see api\logs"
    exit 1
}
$paths = $swagger.paths.PSObject.Properties.Name
$missing = 0
foreach ($p in '/api/bookings/pay-later', '/api/bookings/{id}/pay-later',
               '/api/cash/outstanding', '/api/cash/collections', '/api/cash/collections/{id}/receipt',
               '/api/cash/collections/{id}/void', '/api/cash/register/current', '/api/cash/register/open',
               '/api/cash/register/{id}/close', '/api/cash/register/{id}/approve', '/api/cash/register') {
    if ($paths -contains $p) { Write-Host "OK  - $p" } else { Write-Host "FAIL- missing $p"; $missing++ }
}
exit $missing
