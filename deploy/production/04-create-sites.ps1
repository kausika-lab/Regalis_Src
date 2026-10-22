<#
  Production IIS sites, next to UAT on the same server.

    regalis-prod-api   C:\inetpub\wwwroot\Regalis_Prod\api       http 127.0.0.1:82 (local only)
    regalis-prod-ui    C:\inetpub\wwwroot\Regalis_Prod\frontend  http :80  myregalis.com, www.myregalis.com
                                                                 (HTTPS 443 is added by 06-https.ps1)

  The files are copied from what UAT serves right now (Regalis_Src\api and
  \frontend). The API's web.config is built from UAT's at run time, so the
  Graph / Google values never sit in git, with production's own database,
  JWT signing key and uploads folder. An existing production web.config is
  kept, so re-running this script does not sign everyone out.

  Run elevated:  powershell -ExecutionPolicy Bypass -File 04-create-sites.ps1
#>
param(
    [string]$Domain = 'myregalis.com'
)
$ErrorActionPreference = 'Stop'

$src      = 'C:\inetpub\wwwroot\Regalis_Src'
$prod     = 'C:\inetpub\wwwroot\Regalis_Prod'
$uploads  = 'C:\ProgramData\Regalis\uploads_prod'
$appcmd   = "$env:SystemRoot\System32\inetsrv\appcmd.exe"
$apiPool  = 'regalis-prod-api'
$uiPool   = 'regalis-prod-ui'
$apiPort  = 82

function Invoke-AppCmd { & $appcmd @args; if ($LASTEXITCODE -ne 0) { throw "appcmd $($args -join ' ') failed" } }
# cmd /c keeps appcmd's "not found" stderr away from PowerShell 5.1, which would
# turn it into a terminating error under ErrorActionPreference Stop.
function Test-AppCmd($kind, $name) { return [bool](cmd /c "`"$appcmd`" list $kind `"$name`" 2>nul") }
function Invoke-AppCmdQuiet([string]$arguments) { cmd /c "`"$appcmd`" $arguments >nul 2>nul" }

# --- 1. Files -----------------------------------------------------------------
New-Item -ItemType Directory -Force "$prod\api\logs", "$prod\frontend", "$uploads\profiles", "$uploads\quotations" | Out-Null

robocopy "$src\api" "$prod\api" /E /XD logs App_Data /XF web.config *.pdb appsettings.Development.json /NFL /NDL /NJH /NJS /NP
if ($LASTEXITCODE -ge 8) { throw 'Copying the API failed.' }
robocopy "$src\frontend" "$prod\frontend" /E /XF web.config env.js /NFL /NDL /NJH /NJS /NP
if ($LASTEXITCODE -ge 8) { throw 'Copying the frontend failed.' }
$global:LASTEXITCODE = 0

# --- 2. API web.config ----------------------------------------------------------
$apiConfig = "$prod\api\web.config"
if (-not (Test-Path $apiConfig)) {
    [xml]$x = Get-Content "$src\api\web.config" -Raw
    $env_ = $x.SelectSingleNode('//aspNetCore/environmentVariables')
    function Set-Var($name, $value) {
        $n = $env_.SelectSingleNode("environmentVariable[@name='$name']")
        if (-not $n) { $n = $x.CreateElement('environmentVariable'); $n.SetAttribute('name', $name); [void]$env_.AppendChild($n) }
        $n.SetAttribute('value', $value)
    }
    $key = [byte[]]::new(64); [Security.Cryptography.RandomNumberGenerator]::Create().GetBytes($key)

    Set-Var 'ASPNETCORE_ENVIRONMENT'     'Production'
    Set-Var 'Database__SeedOnStartup'    'false'
    Set-Var 'Database__MigrateOnStartup' 'false'
    Set-Var 'ConnectionStrings__Default' 'Server=localhost;Database=RegalisWorkSpace_PROD;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=True'
    Set-Var 'Jwt__SigningKey'            ([Convert]::ToBase64String($key))
    Set-Var 'Storage__UploadRoot'        $uploads
    # Production-only: stdout capture is for start-up crashes; Serilog keeps the real logs.
    $x.SelectSingleNode('//aspNetCore').SetAttribute('stdoutLogEnabled', 'false')
    $x.Save($apiConfig)
    Write-Host "Wrote $apiConfig (new JWT signing key)."
} else {
    Write-Host "Kept existing $apiConfig."
}

# --- 3. Frontend web.config: same rules, proxied to the production API ------
& "$PSScriptRoot\sync-frontend-config.ps1"

# --- 4. App pools (UAT's settings, but recycle at 03:00 instead of every 29 h) --
foreach ($pool in $apiPool, $uiPool) {
    if (-not (Test-AppCmd apppool $pool)) { Invoke-AppCmd add apppool /name:$pool }
    Invoke-AppCmd set apppool $pool '/managedRuntimeVersion:' /startMode:AlwaysRunning /processModel.idleTimeout:00:00:00 `
        /recycling.periodicRestart.time:00:00:00
    Invoke-AppCmdQuiet "set apppool $pool `"/-recycling.periodicRestart.schedule.[value='03:00:00']`""
    Invoke-AppCmd set apppool $pool "/+recycling.periodicRestart.schedule.[value='03:00:00']"
}

# --- 5. Permissions ---------------------------------------------------------------
icacls "$prod\api\logs" /grant "IIS APPPOOL\${apiPool}:(OI)(CI)M" | Out-Null
icacls $uploads          /grant "IIS APPPOOL\${apiPool}:(OI)(CI)M" | Out-Null
icacls $apiConfig        /inheritance:r /grant:r "Administrators:F" "SYSTEM:F" "IIS APPPOOL\${apiPool}:R" | Out-Null

# --- 6. Sites -------------------------------------------------------------------
if (-not (Test-AppCmd site $apiPool)) {
    Invoke-AppCmd add site /name:$apiPool /physicalPath:"$prod\api" /bindings:"http/127.0.0.1:${apiPort}:"
}
Invoke-AppCmd set app "$apiPool/" /applicationPool:$apiPool

if (-not (Test-AppCmd site $uiPool)) {
    Invoke-AppCmd add site /name:$uiPool /physicalPath:"$prod\frontend" /bindings:"http/*:80:$Domain,http/*:80:www.$Domain"
}
Invoke-AppCmd set app "$uiPool/" /applicationPool:$uiPool

foreach ($p in $apiPool, $uiPool) { Invoke-AppCmdQuiet "start apppool /apppool.name:$p" }
foreach ($s in $apiPool, $uiPool) { Invoke-AppCmdQuiet "start site /site.name:$s" }
Write-Host 'Production sites ready.'
