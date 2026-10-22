<#
  HTTPS for production: a free Let's Encrypt certificate via win-acme, bound
  to regalis-prod-ui on 443 (SNI), renewed automatically by win-acme's own
  scheduled task. Afterwards every http:// request is redirected to https://.

  PREREQUISITE: myregalis.com and www.myregalis.com must resolve, in PUBLIC
  DNS, to this server's public IP, with ports 80 and 443 forwarded to it.
  Let's Encrypt checks over port 80 from the internet. The script stops
  first if DNS still points somewhere else.

  Run elevated:
    powershell -ExecutionPolicy Bypass -File 06-https.ps1 -Email you@example.com
  (Let's Encrypt mails expiry warnings to -Email if renewal ever fails.)
#>
param(
    [Parameter(Mandatory)] [string]$Email,
    [string]$Domain = 'myregalis.com',
    [string]$Site   = 'regalis-prod-ui'
)
$ErrorActionPreference = 'Stop'
$wacsDir = 'C:\Tools\win-acme'
$wacsUrl = 'https://github.com/win-acme/win-acme/releases/download/v2.2.9.1701/win-acme.v2.2.9.1701.x64.trimmed.zip'
$appcmd  = "$env:SystemRoot\System32\inetsrv\appcmd.exe"
$hosts   = @($Domain, "www.$Domain")

# --- 1. DNS must already point here ------------------------------------------
$publicIp = (Invoke-RestMethod https://api.ipify.org -TimeoutSec 15).Trim()
foreach ($h in $hosts) {
    $ips = @(Resolve-DnsName "$h." -Server 8.8.8.8 -DnsOnly -NoHostsFile -Type A | Where-Object Type -eq 'A' | ForEach-Object IPAddress)
    if ($ips -notcontains $publicIp) {
        throw "$h resolves to $($ips -join ', ') in public DNS, not this server ($publicIp). Fix the A record first; nothing was changed."
    }
}
Write-Host "DNS OK: $($hosts -join ', ') -> $publicIp"

# --- 2. win-acme ------------------------------------------------------------------
if (-not (Test-Path "$wacsDir\wacs.exe")) {
    New-Item -ItemType Directory -Force $wacsDir | Out-Null
    $zip = Join-Path $env:TEMP 'win-acme.zip'
    [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
    Invoke-WebRequest $wacsUrl -OutFile $zip -UseBasicParsing
    Expand-Archive $zip -DestinationPath $wacsDir -Force
    Remove-Item $zip
}

# --- 3. Certificate + 443 binding + renewal task -------------------------------
$siteId = ([regex]::Match((& $appcmd list site $Site), 'id:(\d+)')).Groups[1].Value
if (-not $siteId) { throw "IIS site $Site not found." }
& "$wacsDir\wacs.exe" --source iis --siteid $siteId --host ($hosts -join ',') `
    --installation iis --installationsiteid $siteId `
    --accepttos --emailaddress $Email --closeonfinish
if ($LASTEXITCODE -ne 0) { throw 'win-acme did not issue the certificate - see its output above. HTTP still works; nothing else was changed.' }

# --- 4. http -> https (ACME renewals keep using plain http) ----------------------
& "$PSScriptRoot\sync-frontend-config.ps1"
Write-Host "HTTPS ready: https://$Domain"
