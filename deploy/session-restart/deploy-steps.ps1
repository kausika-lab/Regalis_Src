# Helper for deploy-session-restart.cmd (kept out of the .cmd to avoid cmd quoting).
#   -Step verify    : the new API started and ended the earlier sessions (api\logs)
#   -Step frontend  : sign-in always lands on the dashboard (patches the live index)
#
# The frontend step PATCHES the live index in place, so other changes made to it survive.
# Idempotent.
param(
    [Parameter(Mandatory)] [ValidateSet('verify', 'frontend')] [string] $Step,
    [string] $Front,
    [string] $Api
)
$ErrorActionPreference = 'Stop'

if ($Step -eq 'verify') {
    # Any request starts the process if IIS has not yet; startup ends the old sessions.
    try {
        Invoke-WebRequest 'http://localhost:81/swagger/v1/swagger.json' -UseBasicParsing -TimeoutSec 60 | Out-Null
    } catch {
        if (-not $_.Exception.Response) { Write-Host "FAIL- API did not answer: $($_.Exception.Message)"; exit 1 }
    }
    $log = Get-ChildItem (Join-Path $Api 'logs') -Filter 'regalis-api-*.log' |
        Sort-Object LastWriteTime -Descending | Select-Object -First 1
    for ($i = 0; $i -lt 20; $i++) {
        $hit = Get-Content $log.FullName -Tail 400 | Select-String 'earlier session\(s\) ended|Could not end the sessions' | Select-Object -Last 1
        if ($hit) { break }
        Start-Sleep -Seconds 1
    }
    if (-not $hit) { Write-Host "FAIL- no startup session line in $($log.Name)"; exit 1 }
    Write-Host $hit.Line
    if ($hit.Line -match 'Could not end') { exit 1 }
    Write-Host "OK  - API restarted and earlier sessions ended"
    exit 0
}

if ($Step -eq 'frontend') {
    $utf8 = New-Object System.Text.UTF8Encoding($false)
    $index = Join-Path $Front 'assets\index-DI5X4c9f.js'
    $text = [IO.File]::ReadAllText($index)

    $patches = @(
        @{
            # Sign-in used to return to the page the user was last on (location.state.from);
            # it now always opens the dashboard of the portal signed in to.
            Name = 'sign-in opens the dashboard'
            Old  = 'const N=a.state?.from,C=N&&Mz(N)===(s==="admin")?N:Fv(s);t(C,{replace:!0})'
            New  = 't(Fv(s),{replace:!0})'
        },
        @{
            # The protected-route guard trusted any token found in localStorage, so after a
            # server restart it opened the panel with a dead session. It now confirms the
            # stored session with the server once per page load (the normal API client, so an
            # expired access token is refreshed; a refused session is cleared) and shows a
            # spinner meanwhile. It no longer hands the current page to the login screen.
            Name = 'guard confirms the stored session'
            Old  = 'function Gg(){const t=Fn(i=>i.accessToken!==null),a=_n();return t?g.jsx(zr,{}):g.jsx(Os,{to:"/login",replace:!0,state:{from:a.pathname}})}'
            New  = 'let RGsessionOk=!1;function Gg(){const t=Fn(r=>r.accessToken!==null),[a,i]=M.useState(RGsessionOk);M.useEffect(()=>{if(!t||RGsessionOk)return;let r=!1;zf.myMenus().then(s=>{Fn.getState().setMenus(s)}).catch(()=>{}).finally(()=>{Fn.getState().accessToken!==null&&(RGsessionOk=!0),r||i(RGsessionOk)});return()=>{r=!0}},[t]);return t?a?g.jsx(zr,{}):g.jsx(O0,{center:!0}):g.jsx(Os,{to:"/login",replace:!0})}'
        }
    )
    $changed = $false
    foreach ($p in $patches) {
        if ($text.Contains($p.Old)) {
            $text = $text.Replace($p.Old, $p.New)
            $changed = $true
            Write-Host "OK  - index-DI5X4c9f.js: $($p.Name)"
        } elseif ($text.Contains($p.New)) {
            Write-Host "OK  - index-DI5X4c9f.js: $($p.Name) (already patched)"
        } else {
            throw "index-DI5X4c9f.js: code for '$($p.Name)' was not found; nothing written."
        }
    }
    if ($changed) { [IO.File]::WriteAllText($index, $text, $utf8) }

    # Bump index's precache revision so browsers fetch the patched file.
    $sw = Join-Path $Front 'sw.js'
    $swText = [IO.File]::ReadAllText($sw)
    $pattern = '\{url:"assets/index-DI5X4c9f\.js",revision:(null|"[^"]*")\}'
    $swText = [regex]::Replace($swText, $pattern, '{url:"assets/index-DI5X4c9f.js",revision:"login-dashboard-1"}')
    [IO.File]::WriteAllText($sw, $swText, $utf8)
    $count = [regex]::Matches($swText, '"login-dashboard-1"').Count
    Write-Host "sw.js entries at login-dashboard-1: $count (expected 1)"
    if ($count -ne 1) { exit 1 }
}
