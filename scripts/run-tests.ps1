<#
.SYNOPSIS
    Regalis WorkSpace - full test runner.

.DESCRIPTION
    Builds and tests the whole project from a clean checkout:
      1. Backend  : dotnet build + unit tests + integration tests
      2. Frontend : npm install (if needed) + vitest + production build (tsc + vite)

    Integration tests DROP and RECREATE the RegalisWorkSpace_Test database on the
    local default SQL Server instance - never point them at a database you care about.

.PARAMETER SkipBackend
    Skip the .NET build and all backend tests.

.PARAMETER SkipFrontend
    Skip npm test and the frontend production build.

.PARAMETER SkipIntegration
    Run backend unit tests only (use when no local SQL Server is available).

.PARAMETER UpdateDatabase
    Also apply pending EF Core migrations to the local dev database
    (RegalisWorkSpace_Dev) before running tests.

.EXAMPLE
    .\scripts\run-tests.ps1                     # everything
    .\scripts\run-tests.ps1 -SkipIntegration    # fast lane, no SQL Server needed
    .\scripts\run-tests.ps1 -UpdateDatabase     # migrate dev DB first, then test
#>
[CmdletBinding()]
param(
    [switch]$SkipBackend,
    [switch]$SkipFrontend,
    [switch]$SkipIntegration,
    [switch]$UpdateDatabase
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$results = [ordered]@{}
$failed = $false

function Invoke-Step {
    param([string]$Name, [scriptblock]$Body)
    Write-Host ""
    Write-Host "=== $Name ===" -ForegroundColor Cyan
    $sw = [System.Diagnostics.Stopwatch]::StartNew()
    & $Body
    $ok = ($LASTEXITCODE -eq 0)
    $sw.Stop()
    $script:results[$Name] = @{ Ok = $ok; Seconds = [int]$sw.Elapsed.TotalSeconds }
    if (-not $ok) {
        $script:failed = $true
        Write-Host "FAILED: $Name (exit $LASTEXITCODE)" -ForegroundColor Red
    }
}

Push-Location $repoRoot
try {
    # Portable Node install (this machine has no MSI Node); no-op when node is already on PATH.
    if (-not (Get-Command node -ErrorAction SilentlyContinue)) {
        $portableNode = Join-Path $env:LOCALAPPDATA 'node-v22.23.2-win-x64'
        if (Test-Path $portableNode) { $env:Path = "$portableNode;$env:Path" }
    }

    if ($UpdateDatabase) {
        Invoke-Step 'EF Core migrations (dev DB)' {
            $env:ASPNETCORE_ENVIRONMENT = 'Development'
            dotnet tool run dotnet-ef -- database update `
                --project backend/src/Regalis.Infrastructure `
                --startup-project backend/src/Regalis.API
        }
    }

    if (-not $SkipBackend) {
        Invoke-Step 'Backend build' {
            dotnet build backend/Regalis.sln --nologo -v minimal
        }

        if ($results['Backend build'].Ok) {
            Invoke-Step 'Backend unit tests' {
                dotnet test tests/backend/Regalis.UnitTests --no-build --nologo -v minimal
            }
            if (-not $SkipIntegration) {
                Invoke-Step 'Backend integration tests' {
                    dotnet test tests/backend/Regalis.IntegrationTests --no-build --nologo -v minimal
                }
            }
        }
    }

    if (-not $SkipFrontend) {
        Push-Location (Join-Path $repoRoot 'frontend')
        try {
            if (-not (Test-Path node_modules)) {
                Invoke-Step 'Frontend npm install' { npm install }
            }
            Invoke-Step 'Frontend tests (vitest)' { npm run test }
            Invoke-Step 'Frontend build (tsc + vite)' { npm run build }
        }
        finally { Pop-Location }
    }
}
finally { Pop-Location }

Write-Host ""
Write-Host "=== Summary ===" -ForegroundColor Cyan
foreach ($name in $results.Keys) {
    $r = $results[$name]
    if ($r.Ok) { $status = 'PASS'; $color = 'Green' } else { $status = 'FAIL'; $color = 'Red' }
    Write-Host ("{0,-32} {1}  ({2}s)" -f $name, $status, $r.Seconds) -ForegroundColor $color
}

if ($failed) { exit 1 } else { exit 0 }
