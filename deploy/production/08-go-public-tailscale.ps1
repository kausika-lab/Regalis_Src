<#
  Puts production on the internet for free with Tailscale Funnel, until the
  company buys its own domain:

      https://amstagserver.discus-ordinal.ts.net  ->  regalis-prod-ui

  The server is behind Jio CGNAT, so nothing can reach it directly; Funnel
  carries the traffic over Tailscale (HTTPS certificate included). Only the
  production website is published - not UAT, not the API port.

    1. regalis-prod-ui gets a local-only entrance, http 127.0.0.1:8090.
    2. tailscale funnel --bg publishes it (survives reboots).
       If Tailscale prints "Funnel is not enabled", open the link it shows,
       sign in as the tailnet owner, enable it, and run this script again.
    3. Quotation.PortalBaseUrl (the link in quotation emails) -> that address.

  Turn it off:  tailscale funnel --https=443 off
  Run elevated: powershell -ExecutionPolicy Bypass -File 08-go-public-tailscale.ps1
#>
$ErrorActionPreference = 'Stop'
$appcmd    = "$env:SystemRoot\System32\inetsrv\appcmd.exe"
$tailscale = 'C:\Program Files\Tailscale\tailscale.exe'
$sqlcmd    = 'C:\Program Files\Microsoft SQL Server\Client SDK\ODBC\180\Tools\Binn\sqlcmd.exe'

# --- 1. Local-only entrance --------------------------------------------------------
if (-not ((& $appcmd list site regalis-prod-ui) -match '127\.0\.0\.1:8090')) {
    & $appcmd set site /site.name:regalis-prod-ui "/+bindings.[protocol='http',bindingInformation='127.0.0.1:8090:']"
    if ($LASTEXITCODE -ne 0) { throw 'Adding the 127.0.0.1:8090 binding failed.' }
}
if ((Invoke-WebRequest http://127.0.0.1:8090/ -UseBasicParsing -TimeoutSec 30).StatusCode -ne 200) { throw 'Production site does not answer on 127.0.0.1:8090.' }

# --- 2. Funnel ---------------------------------------------------------------------
$dns = ((& $tailscale status --json | ConvertFrom-Json).Self.DNSName).TrimEnd('.')
& $tailscale funnel --bg http://127.0.0.1:8090
if ($LASTEXITCODE -ne 0) { throw 'tailscale funnel failed - if it showed an enable link, open it, enable Funnel, and run this again.' }
& $tailscale funnel status

# --- 3. Quotation links ----------------------------------------------------------------
[xml]$cfg = Get-Content 'C:\inetpub\wwwroot\Regalis_Prod\api\web.config' -Raw
$server = [regex]::Match($cfg.SelectSingleNode("//environmentVariable[@name='ConnectionStrings__Default']").value, 'Server=([^;]+)').Groups[1].Value
& $sqlcmd -S $server -d RegalisWorkSpace_PROD -E -C -I -b -Q "UPDATE MST_AppSetting SET SettingValue = N'https://$dns' WHERE SettingKey = N'Quotation.PortalBaseUrl'"
if ($LASTEXITCODE -ne 0) { throw 'Updating Quotation.PortalBaseUrl failed.' }

Write-Host ''
Write-Host "PUBLIC: https://$dns"
Write-Host 'Next: add that address under Authorised JavaScript origins of the Google OAuth client (for Google sign-in).'
