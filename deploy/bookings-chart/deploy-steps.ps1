# Helper for deploy-bookings-chart.cmd (kept out of the .cmd to avoid cmd quoting).
#   -Step verify    : the new monthly endpoint is published by the running API
#   -Step frontend  : install the new Bookings screen (and the Expenses gridline fix)
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
    foreach ($p in '/api/bookings/monthly', '/api/bookings', '/api/expenses/monthly') {
        if ($paths -contains $p) { Write-Host "OK  - $p" } else { Write-Host "FAIL- missing $p"; $missing++ }
    }
    exit $missing
}

if ($Step -eq 'frontend') {
    $assets = Join-Path $Front 'assets'
    $staged = Join-Path $PSScriptRoot 'frontend\assets'
    $utf8 = New-Object System.Text.UTF8Encoding($false)

    # 1. New chunks (new files: no precache entries needed). Old ones stay on disk so a
    #    browser still holding the old index does not 404.
    foreach ($f in 'BookingsMonthlyChart-BK1.js', 'AdminBookingsPage-BK1.js', 'AdminExpensesPage-EX2.js') {
        Copy-Item (Join-Path $staged $f) $assets -Force
        Write-Host "OK  - $f installed"
    }

    # 2. Point the index at them: both the lazy import and the modulepreload list.
    $index = Join-Path $assets 'index-DI5X4c9f.js'
    $text = [IO.File]::ReadAllText($index)
    $patched = $text
    $moves = @(
        @{ New = 'AdminBookingsPage-BK1.js'; Old = @('AdminBookingsPage-BqwuNlMp.js') },
        @{ New = 'AdminExpensesPage-EX2.js'; Old = @('AdminExpensesPage-CstB2vPx.js', 'AdminExpensesPage-EX1.js') }
    )
    foreach ($m in $moves) {
        foreach ($old in $m.Old) {
            $patched = $patched.Replace("import(`"./$old`")", "import(`"./$($m.New)`")").Replace("`"assets/$old`"", "`"assets/$($m.New)`"")
        }
        if (-not $patched.Contains("import(`"./$($m.New)`")") -or -not $patched.Contains("`"assets/$($m.New)`"")) {
            throw "index-DI5X4c9f.js: could not point at $($m.New); nothing written."
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
    $swText = [regex]::Replace($swText, $pattern, '{url:"assets/index-DI5X4c9f.js",revision:"bookings-chart-1"}')
    [IO.File]::WriteAllText($sw, $swText, $utf8)
    $count = [regex]::Matches($swText, '"bookings-chart-1"').Count
    Write-Host "sw.js entries at bookings-chart-1: $count (expected 1)"
    if ($count -ne 1) { exit 1 }
}
