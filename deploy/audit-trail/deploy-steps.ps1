# Helper for deploy-audit-trail.cmd (kept out of the .cmd to avoid cmd quoting).
#   -Step verify    : the new audit endpoints are published by the running API
#   -Step frontend  : install the new Audit Trail screen into -Front (the live frontend\)
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
    foreach ($p in '/api/admin/audit', '/api/admin/audit/{id}', '/api/admin/audit/{id}/related',
                   '/api/admin/audit/lookups', '/api/admin/audit/export') {
        if ($paths -contains $p) { Write-Host "OK  - $p" } else { Write-Host "FAIL- missing $p"; $missing++ }
    }
    exit $missing
}

if ($Step -eq 'frontend') {
    $assets = Join-Path $Front 'assets'
    $staged = Join-Path $PSScriptRoot 'frontend\assets'
    $utf8 = New-Object System.Text.UTF8Encoding($false)

    # 1. The new page chunk (a new file: no precache entry needed).
    Copy-Item (Join-Path $staged 'AdminAuditPage-AT6.js') $assets -Force
    Write-Host "OK  - AdminAuditPage-AT6.js installed"

    # 2. /admin/audit loads the new page.
    $index = Join-Path $assets 'index-DI5X4c9f.js'
    $text = [IO.File]::ReadAllText($index)
    # Both the lazy import and the modulepreload list name the chunk; point both at the new file.
    $new = 'AdminAuditPage-AT6.js'
    $patched = $text
    foreach ($old in 'AdminAuditPage-SlEVI_Ol.js', 'AdminAuditPage-AT1.js', 'AdminAuditPage-AT2.js', 'AdminAuditPage-AT3.js', 'AdminAuditPage-AT4.js', 'AdminAuditPage-AT5.js') {
        $patched = $patched.Replace("import(`"./$old`")", "import(`"./$new`")").Replace("`"assets/$old`"", "`"assets/$new`"")
    }
    if (-not $patched.Contains("import(`"./$new`")")) {
        throw "index-DI5X4c9f.js has no Audit Trail import to replace; nothing written."
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
    $swText = [regex]::Replace($swText, $pattern, '{url:"assets/index-DI5X4c9f.js",revision:"audit-trail-7"}')
    [IO.File]::WriteAllText($sw, $swText, $utf8)
    $count = [regex]::Matches($swText, '"audit-trail-7"').Count
    Write-Host "sw.js entries at audit-trail-7: $count (expected 1)"
    if ($count -ne 1) { exit 1 }
}
