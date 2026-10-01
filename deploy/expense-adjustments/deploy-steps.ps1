# Helper for deploy-expense-adjustments.cmd (kept out of the .cmd to avoid cmd quoting).
#   -Step verify    : the new expense endpoints are published by the running API
#   -Step frontend  : install the new Expenses screen into -Front (the live frontend\)
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
    foreach ($p in '/api/expenses/monthly', '/api/expenses/adjustments',
                   '/api/expenses/adjustments/{adjustmentId}') {
        if ($paths -contains $p) { Write-Host "OK  - $p" } else { Write-Host "FAIL- missing $p"; $missing++ }
    }
    exit $missing
}

if ($Step -eq 'frontend') {
    $assets = Join-Path $Front 'assets'
    $staged = Join-Path $PSScriptRoot 'frontend\assets'
    $utf8 = New-Object System.Text.UTF8Encoding($false)

    # 1. The new page chunk (a new file: no precache entry needed).
    Copy-Item (Join-Path $staged 'AdminExpensesPage-EX1.js') $assets -Force
    Write-Host "OK  - AdminExpensesPage-EX1.js installed"

    # 2. /admin/expenses loads the new page. The old chunk stays on disk so a browser
    #    still holding the old index does not 404.
    $index = Join-Path $assets 'index-DI5X4c9f.js'
    $text = [IO.File]::ReadAllText($index)
    # Both the lazy import and the modulepreload list name the chunk; point both at the new file.
    $new = 'AdminExpensesPage-EX1.js'
    $patched = $text
    foreach ($old in @('AdminExpensesPage-CstB2vPx.js')) {
        $patched = $patched.Replace("import(`"./$old`")", "import(`"./$new`")").Replace("`"assets/$old`"", "`"assets/$new`"")
    }
    if (-not $patched.Contains("import(`"./$new`")") -or -not $patched.Contains("`"assets/$new`"")) {
        throw "index-DI5X4c9f.js has no Expenses import to replace; nothing written."
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
    $swText = [regex]::Replace($swText, $pattern, '{url:"assets/index-DI5X4c9f.js",revision:"expense-adjustments-1"}')
    [IO.File]::WriteAllText($sw, $swText, $utf8)
    $count = [regex]::Matches($swText, '"expense-adjustments-1"').Count
    Write-Host "sw.js entries at expense-adjustments-1: $count (expected 1)"
    if ($count -ne 1) { exit 1 }
}
