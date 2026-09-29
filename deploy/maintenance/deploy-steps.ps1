# Helper for deploy-maintenance.cmd (kept out of the .cmd to avoid cmd quoting).
#   -Step verify    : the new maintenance endpoints are published by the running API
#   -Step frontend  : install the Maintenance screens into -Front (the live frontend\)
#
# The frontend step PATCHES the live index and schedule chunk instead of copying staged
# copies over them, so any other change made to those files since staging survives.
# Every edit is idempotent: running it twice leaves the files as after the first run.
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
    foreach ($p in '/api/maintenance/me', '/api/maintenance/options', '/api/maintenance/work',
                   '/api/maintenance/work/{id}/verify', '/api/maintenance/work/{id}/done',
                   '/api/maintenance/my-tasks', '/api/maintenance/housekeepers',
                   '/api/maintenance/housekeepers/{userId}/deactivate') {
        if ($paths -contains $p) { Write-Host "OK  - $p" } else { Write-Host "FAIL- missing $p"; $missing++ }
    }
    exit $missing
}

if ($Step -eq 'frontend') {
    $assets = Join-Path $Front 'assets'
    $staged = Join-Path $PSScriptRoot 'frontend\assets'
    $utf8 = New-Object System.Text.UTF8Encoding($false)

    function Edit-File([string] $name, [string] $find, [string] $replace, [string] $doneMarker) {
        $file = Join-Path $assets $name
        $text = [IO.File]::ReadAllText($file)
        if ($text.Contains($doneMarker)) { Write-Host "OK  - $name already patched"; return }
        if (-not $text.Contains($find)) { throw "$name does not contain the expected text; it has changed since staging. Nothing written for it." }
        [IO.File]::WriteAllText($file, $text.Replace($find, $replace), $utf8)
        Write-Host "OK  - $name patched"
    }

    # 1. The new page chunk (a new file: no precache entry needed).
    Copy-Item (Join-Path $staged 'AdminMaintenancePage-MT4.js') $assets -Force
    Write-Host "OK  - AdminMaintenancePage-MT4.js installed"

    # 2. The old page keeps working and lends its schedule form, complete dialog and date picker.
    Edit-File 'AdminHousekeepingPage-BVEQUG24.js' `
        'export{ie as AdminHousekeepingPage};' `
        'export{ie as AdminHousekeepingPage,G as ScheduleFormModal,Q as ScheduleCompleteModal,RgDate,D as toIstInput,A as fromIstInput};' `
        'G as ScheduleFormModal'

    # 3. /admin/housekeeping loads the new page.
    Edit-File 'index-DI5X4c9f.js' `
        'import("./AdminHousekeepingPage-BVEQUG24.js"),__vite__mapDeps([54,55,29,6,7,45,22,23,12,13,10,14,56,8])),"AdminHousekeepingPage")' `
        'import("./AdminMaintenancePage-MT4.js"),__vite__mapDeps([54,55,29,6,7,45,22,23,12,13,10,14,56,8])),"AdminMaintenancePage")' `
        'AdminMaintenancePage-MT4.js'

    # 4. Bump the two precache revisions so browsers fetch the patched files.
    $sw = Join-Path $Front 'sw.js'
    $text = [IO.File]::ReadAllText($sw)
    foreach ($name in 'index-DI5X4c9f.js', 'AdminHousekeepingPage-BVEQUG24.js') {
        $pattern = '\{url:"assets/' + [regex]::Escape($name) + '",revision:(null|"[^"]*")\}'
        $text = [regex]::Replace($text, $pattern, '{url:"assets/' + $name + '",revision:"maintenance-4"}')
    }
    [IO.File]::WriteAllText($sw, $text, $utf8)
    $count = [regex]::Matches($text, '"maintenance-4"').Count
    Write-Host "sw.js entries at maintenance-4: $count (expected 2)"
    if ($count -ne 2) { exit 1 }
}
