<#
.SYNOPSIS
    Regalis WorkSpace - browser end-to-end walkthrough runner.

.DESCRIPTION
    Boots whatever is not already running (API on 5200/7200, Vite UI on 5173),
    resolves the seeded admin password from the API user-secrets, then drives a
    real Chromium browser through the whole product:
      - Admin portal: sign in and open every admin feature page
      - Customer: register -> verify with the real OTP (read from the dev API
        log) -> sign in -> open every customer feature page
    Full-page screenshots of every feature land in frontend\e2e-results\screens,
    and an HTML report in frontend\e2e-report.

    The browser is HEADED by default so you can watch the walkthrough. Anything
    this script started is stopped afterwards (use -KeepRunning to leave the
    app up and keep clicking around yourself).

.PARAMETER Headless
    Run the browser invisibly (CI-style) instead of on screen.

.PARAMETER KeepRunning
    Leave the API/UI processes this script started running after the tests.

.EXAMPLE
    .\scripts\run-e2e.ps1               # watch the walkthrough in a browser
    .\scripts\run-e2e.ps1 -Headless
    .\scripts\run-e2e.ps1 -KeepRunning  # then browse http://localhost:5173
#>
[CmdletBinding()]
param(
    [switch]$Headless,
    [switch]$KeepRunning
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$startedPids = @()

function Test-Port {
    param([int]$Port)
    return [bool](Get-NetTCPConnection -LocalPort $Port -State Listen -ErrorAction SilentlyContinue)
}

function Wait-ForUrl {
    param([string]$Url, [int]$Seconds = 90)
    for ($i = 0; $i -lt $Seconds; $i += 2) {
        try {
            $response = Invoke-WebRequest -Uri $Url -UseBasicParsing -TimeoutSec 5
            if ($response.StatusCode -eq 200) { return $true }
        } catch {}
        Start-Sleep -Seconds 2
    }
    return $false
}

# Portable Node install; no-op when node is already on PATH.
if (-not (Get-Command node -ErrorAction SilentlyContinue)) {
    $portableNode = Join-Path $env:LOCALAPPDATA 'node-v22.23.2-win-x64'
    if (Test-Path $portableNode) { $env:Path = "$portableNode;$env:Path" }
}

Push-Location $repoRoot
try {
    # Admin password comes from the API user-secrets (Seed:AdminPassword).
    if (-not $env:E2E_ADMIN_PASSWORD) {
        $secret = dotnet user-secrets list --project backend/src/Regalis.API 2>$null |
            Select-String '^Seed:AdminPassword = (.+)$'
        if (-not $secret) {
            throw 'Seed:AdminPassword not found in user-secrets; set E2E_ADMIN_PASSWORD manually.'
        }
        $env:E2E_ADMIN_PASSWORD = $secret.Matches[0].Groups[1].Value
    }

    if (-not (Test-Port 5200)) {
        Write-Host 'Starting API (Development) on 5200/7200...' -ForegroundColor Cyan
        $env:ASPNETCORE_ENVIRONMENT = 'Development'
        $api = Start-Process -FilePath 'dotnet' -ArgumentList @(
            'run', '--project', 'backend/src/Regalis.API', '--no-launch-profile',
            '--urls', 'https://localhost:7200;http://localhost:5200'
        ) -WorkingDirectory $repoRoot -WindowStyle Hidden -PassThru
        $startedPids += $api.Id
    } else {
        Write-Host 'API already running on 5200 - using it.' -ForegroundColor Cyan
    }
    if (-not (Wait-ForUrl 'http://localhost:5200/health' 120)) {
        throw 'API did not become healthy on http://localhost:5200/health.'
    }

    if (-not (Test-Port 5173)) {
        Write-Host 'Starting Vite dev server on 5173...' -ForegroundColor Cyan
        $npmCmd = (Get-Command npm.cmd -ErrorAction Stop).Source
        $ui = Start-Process -FilePath $npmCmd -ArgumentList @('run', 'dev') `
            -WorkingDirectory (Join-Path $repoRoot 'frontend') -WindowStyle Hidden -PassThru
        $startedPids += $ui.Id
    } else {
        Write-Host 'UI already running on 5173 - using it.' -ForegroundColor Cyan
    }
    if (-not (Wait-ForUrl 'http://localhost:5173/' 90)) {
        throw 'Vite dev server did not come up on http://localhost:5173.'
    }

    Write-Host ''
    Write-Host '=== Browser walkthrough (Playwright) ===' -ForegroundColor Cyan
    Push-Location (Join-Path $repoRoot 'frontend')
    try {
        $npxCmd = (Get-Command npx.cmd -ErrorAction Stop).Source
        if ($Headless) {
            & $npxCmd playwright test
        } else {
            & $npxCmd playwright test --headed
        }
        $exitCode = $LASTEXITCODE
    }
    finally { Pop-Location }

    Write-Host ''
    if ($exitCode -eq 0) {
        Write-Host 'E2E walkthrough PASSED.' -ForegroundColor Green
    } else {
        Write-Host "E2E walkthrough FAILED (exit $exitCode). See frontend\e2e-report\index.html" -ForegroundColor Red
    }
    Write-Host 'Screenshots: frontend\e2e-results\screens'
}
finally {
    Pop-Location
    if (-not $KeepRunning -and $startedPids.Count -gt 0) {
        Write-Host "Stopping processes started by this script: $($startedPids -join ', ')"
        foreach ($procId in $startedPids) {
            taskkill /PID $procId /T /F 2>$null | Out-Null
        }
    }
}

exit $exitCode
