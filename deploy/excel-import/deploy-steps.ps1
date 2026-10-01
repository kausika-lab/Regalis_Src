# Helper for deploy-excel-import.cmd (kept out of the .cmd to avoid cmd quoting).
#   -Step verify    : the new inventory Excel endpoints are published by the running API
#   -Step frontend  : install the Assets screens with Excel import/export into -Front
#
# The frontend step PATCHES the live index in place instead of copying a staged copy over
# it, so other changes made to it since staging survive. Idempotent.
param(
    [Parameter(Mandatory)] [ValidateSet('verify', 'frontend')] [string] $Step,
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
    foreach ($p in '/api/inventory/incoming/export', '/api/inventory/outgoing/export',
                   '/api/inventory/stock/export', '/api/inventory/{kind}/import/template',
                   '/api/inventory/{kind}/import/preview', '/api/inventory/{kind}/import',
                   '/api/inventory/{kind}/import/errors') {
        if ($paths -contains $p) { Write-Host "OK  - $p" } else { Write-Host "FAIL- missing $p"; $missing++ }
    }
    exit $missing
}

if ($Step -eq 'frontend') {
    $assets = Join-Path $Front 'assets'
    $staged = Join-Path $PSScriptRoot 'frontend\assets'
    $utf8 = New-Object System.Text.UTF8Encoding($false)

    # 1. The new chunks (new files: no precache entry needed).
    foreach ($f in 'InventoryExcel-IX1.js', 'AdminInventoryPages-IX1.js', 'AdminInventoryGoodsPages-IX1.js') {
        Copy-Item (Join-Path $staged $f) $assets -Force
        Write-Host "OK  - $f installed"
    }

    # 2. The inventory routes load the new pages. The old chunks stay on disk so a browser
    #    still holding the old index does not 404. Both the lazy import and the
    #    modulepreload list name each chunk; point both at the new file.
    $index = Join-Path $assets 'index-DI5X4c9f.js'
    $text = [IO.File]::ReadAllText($index)
    $patched = $text
    $swaps = @{
        'AdminInventoryPages-CeeePoXy.js'       = 'AdminInventoryPages-IX1.js'
        'AdminInventoryGoodsPages-C9Wm4-j5.js'  = 'AdminInventoryGoodsPages-IX1.js'
    }
    foreach ($old in $swaps.Keys) {
        $new = $swaps[$old]
        $patched = $patched.Replace("import(`"./$old`")", "import(`"./$new`")").Replace("`"assets/$old`"", "`"assets/$new`"")
        if (-not $patched.Contains("import(`"./$new`")") -or -not $patched.Contains("`"assets/$new`"")) {
            throw "index-DI5X4c9f.js has no $old import to replace; nothing written."
        }
    }
    if ($patched -ne $text) {
        [IO.File]::WriteAllText($index, $patched, $utf8)
        Write-Host "OK  - index-DI5X4c9f.js patched"
    } else {
        Write-Host "OK  - index-DI5X4c9f.js already patched"
    }

    # 3. Bump index's precache revision so browsers fetch the patched file.
    $sw = Join-Path $Front 'sw.js'
    $swText = [IO.File]::ReadAllText($sw)
    $pattern = '\{url:"assets/index-DI5X4c9f\.js",revision:(null|"[^"]*")\}'
    $swText = [regex]::Replace($swText, $pattern, '{url:"assets/index-DI5X4c9f.js",revision:"excel-import-1"}')
    [IO.File]::WriteAllText($sw, $swText, $utf8)
    $count = [regex]::Matches($swText, '"excel-import-1"').Count
    Write-Host "sw.js entries at excel-import-1: $count (expected 1)"
    if ($count -ne 1) { exit 1 }
}
