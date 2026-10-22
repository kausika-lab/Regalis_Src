<#
  Builds production's frontend web.config from UAT's.

  UAT deploys keep adding no-cache entries for bundles patched in place, so
  production takes UAT's file each time instead of keeping its own copy, with
  three differences: the API proxy points at the production API (port 82),
  online-payment calls are refused, and once the site has an https binding,
  http is redirected to https. Also writes production's env.js (online
  payments off).

  Called by 04-create-sites.ps1, 06-https.ps1 and promote-uat-to-prod.cmd.
#>
$ErrorActionPreference = 'Stop'
$uat    = 'C:\inetpub\wwwroot\Regalis_Src\frontend\web.config'
$prod   = 'C:\inetpub\wwwroot\Regalis_Prod\frontend\web.config'
$appcmd = "$env:SystemRoot\System32\inetsrv\appcmd.exe"

[xml]$x = (Get-Content $uat -Raw) -replace 'http://127\.0\.0\.1:81/', 'http://127.0.0.1:82/'
$rules = $x.SelectSingleNode('//rewrite/rules')

# Production takes no online payments (no gateway; customers pay at the front
# desk and staff credit the wallet). The API's sandbox gateway would otherwise
# "complete" any payment for free, so every call that creates or completes an
# online payment is refused here, before it reaches the API. The API listens on
# 127.0.0.1 only, so this proxy is the only way in.
$block = $x.CreateDocumentFragment()
$block.InnerXml = @'
<rule name="no-online-payments" stopProcessing="true">
  <match url="^api/(payments/(order|sandbox-complete|invoice-order|invoice-verify)|wallet/recharge(/verify)?|passes/order)/?$" />
  <action type="CustomResponse" statusCode="403" statusReason="Forbidden" statusDescription="Online payments are not available. Please pay at the front desk." />
</rule>
'@
[void]$rules.PrependChild($block)

# The SPA hides the online options when this is false (deploy/offline-payments).
[IO.File]::WriteAllText((Join-Path (Split-Path $prod) 'env.js'), @'
// Production: no online payments - customers pay at the front desk.
// Written by Regalis_Src\deploy\production\sync-frontend-config.ps1 on every promote.
window.__REGALIS_ONLINE_PAY__ = false;
'@, (New-Object Text.UTF8Encoding $false))

if ((& $appcmd list site regalis-prod-ui) -match 'https/') {
    $rule = $x.CreateDocumentFragment()
    # ACME renewals must stay on plain http.
    $rule.InnerXml = @'
<rule name="https" stopProcessing="true">
  <match url="(.*)" />
  <conditions logicalGrouping="MatchAll">
    <add input="{HTTPS}" pattern="off" />
    <add input="{REQUEST_URI}" pattern="^/\.well-known/acme-challenge/" negate="true" />
  </conditions>
  <action type="Redirect" url="https://{HTTP_HOST}/{R:1}" redirectType="Permanent" />
</rule>
'@
    [void]$rules.PrependChild($rule)
}
$x.Save($prod)
Write-Host "Wrote $prod"
