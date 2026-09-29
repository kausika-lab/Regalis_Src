# Helper for deploy-purchase-orders.cmd (kept out of the .cmd to avoid cmd quoting).
#   -Step verify   : the new PO endpoints are published by the running API
#   -Step bumpsw   : bump the three precache revisions in the live sw.js
param(
    [Parameter(Mandatory)] [ValidateSet('verify', 'bumpsw')] [string] $Step,
    [string] $Front
)
$ErrorActionPreference = 'Stop'

if ($Step -eq 'verify') {
    try {
        $swagger = (Invoke-WebRequest 'http://localhost:81/swagger/v1/swagger.json' -UseBasicParsing -TimeoutSec 60).Content | ConvertFrom-Json
    } catch {
        Write-Host "FAIL- API did not answer: $($_.Exception.Message) - see api\logs"
        exit 1
    }
    $paths = $swagger.paths.PSObject.Properties.Name
    $missing = 0
    foreach ($p in '/api/purchase-orders/form-options', '/api/purchase-orders/{id}/submit',
                   '/api/purchase-orders/{id}/approve', '/api/purchase-orders/{id}/reject',
                   '/api/purchase-orders/{id}/pdf', '/api/purchase-orders/{id}/attachments',
                   '/api/admin/wallets', '/api/admin/wallets/{customerId}',
                   '/api/admin/wallets/{customerId}/add-money', '/api/admin/wallets/{customerId}/statement') {
        if ($paths -contains $p) { Write-Host "OK  - $p" } else { Write-Host "FAIL- missing $p"; $missing++ }
    }
    exit $missing
}

if ($Step -eq 'bumpsw') {
    $file = Join-Path $Front 'sw.js'
    $text = [IO.File]::ReadAllText($file)
    foreach ($name in 'index-DI5X4c9f.js', 'AdminPurchaseOrderPages-C1FTHC0p.js', 'procurement-CDmpFQ22.js') {
        $pattern = '\{url:"assets/' + [regex]::Escape($name) + '",revision:(null|"[^"]*")\}'
        $text = [regex]::Replace($text, $pattern, '{url:"assets/' + $name + '",revision:"po-module-1"}')
    }
    [IO.File]::WriteAllText($file, $text)
    $count = [regex]::Matches($text, 'po-module-1').Count
    Write-Host "sw.js entries at po-module-1: $count (expected 3)"
    if ($count -ne 3) { exit 1 }
}
