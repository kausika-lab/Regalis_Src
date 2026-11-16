<#
  Puts production on its own domain with a Cloudflare Tunnel:

      https://myregalis.com, https://www.myregalis.com  ->  regalis-prod-ui

  The server is behind Jio CGNAT, so an A record can't reach it. cloudflared runs
  here as a Windows service and dials out to Cloudflare, which serves the domain
  (HTTPS certificate included). Same local entrance as Tailscale Funnel
  (08-go-public-tailscale.ps1): http 127.0.0.1:8090. Funnel is left on as a fallback.

  Before running, in a browser:
    1. Cloudflare (free plan): add myregalis.com.
    2. BigRock: name servers -> the two Cloudflare gives; wait until Cloudflare
       shows the domain "Active".

  Then this script, which is safe to re-run (each step is skipped once done):
    1. cloudflared tunnel login - prints a link; open it, pick myregalis.com, Authorize.
    2. Creates the tunnel "regalis-prod" and its DNS records (myregalis.com, www).
    3. Writes C:\ProgramData\cloudflared\config.yml and runs the tunnel as the
       "cloudflared" Windows service.
    4. Quotation.PortalBaseUrl (the link in quotation emails) -> https://myregalis.com
       once the domain answers.

  Run elevated: powershell -ExecutionPolicy Bypass -File 09-go-public-cloudflare.ps1
  Turn it off:  cloudflared service uninstall   (then point the links back with 08)
#>
param([string]$Domain = 'myregalis.com', [string]$Tunnel = 'regalis-prod')
$ErrorActionPreference = 'Stop'
$cf     = "${env:ProgramFiles(x86)}\cloudflared\cloudflared.exe"
$sqlcmd = 'C:\Program Files\Microsoft SQL Server\Client SDK\ODBC\180\Tools\Binn\sqlcmd.exe'
$home_  = Join-Path $env:USERPROFILE '.cloudflared'
$svcDir = 'C:\ProgramData\cloudflared'

if ((Invoke-WebRequest http://127.0.0.1:8090/ -UseBasicParsing -TimeoutSec 30).StatusCode -ne 200) {
    throw 'Production site does not answer on 127.0.0.1:8090 - run 08-go-public-tailscale.ps1 step 1 first.'
}
if (-not (Test-Path $cf)) {
    [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
    $msi = Join-Path $env:TEMP 'cloudflared-windows-amd64.msi'
    Invoke-WebRequest 'https://github.com/cloudflare/cloudflared/releases/latest/download/cloudflared-windows-amd64.msi' -OutFile $msi -UseBasicParsing
    if ((Get-AuthenticodeSignature $msi).Status -ne 'Valid') { throw 'cloudflared installer signature is not valid.' }
    $p = Start-Process msiexec.exe -ArgumentList '/i', "`"$msi`"", '/qn' -Wait -PassThru
    if ($p.ExitCode -ne 0) { throw "cloudflared install failed (msiexec exit $($p.ExitCode))." }
}

# --- 1. Login --------------------------------------------------------------------------
if (-not (Test-Path "$home_\cert.pem")) {
    & $cf tunnel login
    if ($LASTEXITCODE -ne 0 -or -not (Test-Path "$home_\cert.pem")) { throw 'cloudflared login did not finish.' }
}

# --- 2. Tunnel + DNS ---------------------------------------------------------------------
$t = & $cf tunnel list --name $Tunnel --output json | ConvertFrom-Json
if (-not $t) {
    & $cf tunnel create $Tunnel
    if ($LASTEXITCODE -ne 0) { throw 'tunnel create failed.' }
    $t = & $cf tunnel list --name $Tunnel --output json | ConvertFrom-Json
}
$id = @($t)[0].id
foreach ($h in $Domain, "www.$Domain") {
    & $cf tunnel route dns --overwrite-dns $Tunnel $h
    if ($LASTEXITCODE -ne 0) { throw "DNS record for $h failed." }
}

# --- 3. Service ------------------------------------------------------------------------------
New-Item -ItemType Directory -Force $svcDir | Out-Null
if (-not (Test-Path "$svcDir\$id.json")) { Copy-Item "$home_\$id.json" "$svcDir\$id.json" }
@"
tunnel: $id
credentials-file: $svcDir\$id.json
ingress:
  - hostname: $Domain
    service: http://127.0.0.1:8090
  - hostname: www.$Domain
    service: http://127.0.0.1:8090
  - service: http_status:404
"@ | Set-Content "$svcDir\config.yml" -Encoding ascii
& $cf tunnel --config "$svcDir\config.yml" ingress validate
if ($LASTEXITCODE -ne 0) { throw 'config.yml is not valid.' }

if (-not (Get-Service cloudflared -ErrorAction SilentlyContinue)) {
    & $cf service install
    if ($LASTEXITCODE -ne 0) { throw 'cloudflared service install failed.' }
}
# The service runs as LocalSystem: point it at our config explicitly. Freshly
# installed it starts without a config and hangs in "Stop Pending" on Stop-Service
# (seen 6 Nov 2026), so its process is killed instead.
$svcPid = (Get-CimInstance Win32_Service -Filter "Name='cloudflared'").ProcessId
if ($svcPid) { Stop-Process -Id $svcPid -Force; Start-Sleep -Seconds 3 }
Set-ItemProperty HKLM:\SYSTEM\CurrentControlSet\Services\cloudflared ImagePath "`"$cf`" --config `"$svcDir\config.yml`" tunnel run"
Set-Service cloudflared -StartupType Automatic
Start-Service cloudflared
Start-Sleep -Seconds 15
Get-Service cloudflared | Format-Table Status, Name -AutoSize

# --- 4. Public check + quotation links -------------------------------------------------------
$ok = $false
try { $ok = (Invoke-WebRequest "https://$Domain/" -UseBasicParsing -TimeoutSec 30).StatusCode -eq 200 } catch { }
if (-not $ok) {
    Write-Warning "https://$Domain does not answer yet (DNS can take a while). Run this script again later."
    return
}
[xml]$cfg = Get-Content 'C:\inetpub\wwwroot\Regalis_Prod\api\web.config' -Raw
$server = [regex]::Match($cfg.SelectSingleNode("//environmentVariable[@name='ConnectionStrings__Default']").value, 'Server=([^;]+)').Groups[1].Value
& $sqlcmd -S $server -d RegalisWorkSpace_PROD -E -C -I -b -Q "UPDATE MST_AppSetting SET SettingValue = N'https://$Domain' WHERE SettingKey = N'Quotation.PortalBaseUrl'"
if ($LASTEXITCODE -ne 0) { throw 'Updating Quotation.PortalBaseUrl failed.' }

Write-Host ''
Write-Host "PUBLIC: https://$Domain (and https://www.$Domain)"
Write-Host "Next: add https://$Domain and https://www.$Domain under Authorised JavaScript origins of the Google OAuth client, then set `$googleOriginApproved in sync-frontend-config.ps1."
