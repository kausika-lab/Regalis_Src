@echo off
REM ===========================================================================
REM  One day pass at a time (23 Oct 2026).
REM
REM  API (source since 23 Oct: Downloads\Regalis-live, branch feature/assets-live,
REM  which contains feature/pass-upgrade plus Assets; building from Regalis-passes
REM  would remove Assets from UAT):
REM    - GET /api/passes/purchase-options?type=1: the live day pass and, per plan,
REM      whether it can be bought, whether it is an upgrade, and the price after the
REM      unused days of the live pass are credited (price paid x remaining / total).
REM    - POST /api/passes/order and /purchase refuse a day pass while another day
REM      pass is live unless it is a longer pack; an upgrade starts today (or on the
REM      live pass's start date if ahead), cancels the live pass, revokes its QR,
REM      and charges the plan price less the credit. Invoice and notification say so.
REM    No database change.
REM  Frontend: deploy\pass-upgrade\apply.mjs patches Book Pass (PassesPage-CxAkHUg-.js)
REM    and the staged workspace-types copy; bumps sw.js.
REM
REM  Run from an ELEVATED command prompt. Re-running is safe.
REM  Undo: robocopy %BACKUP%\api over api\ (keep api\web.config), restart the pool,
REM        git checkout the Passes chunk and sw.js.
REM ===========================================================================
setlocal
cd /d "%~dp0"
set SRC=C:\Users\Administrator\Downloads\Regalis-live\backend
set STAGE=%TEMP%\regalis-api-publish-passes
set TARGET=%~dp0api
set BACKUP=C:\inetpub\wwwroot\Regalis_backup_20261023_pass_upgrade
set POOL=regalis-api
set APPCMD=%SystemRoot%\System32\inetsrv\appcmd.exe

net session >nul 2>&1
if errorlevel 1 ( echo ERROR: not elevated. & pause & exit /b 1 )
if not exist "%SRC%\Regalis.sln" ( echo ERROR: API source not found at %SRC% & pause & exit /b 1 )

echo === 1. Back up and publish
robocopy "%TARGET%" "%BACKUP%\api" /E /NFL /NDL /NJH /NJS /NP
if errorlevel 8 ( echo Backup failed. Nothing changed. & pause & exit /b 1 )
if exist "%STAGE%" rmdir /s /q "%STAGE%"
dotnet publish "%SRC%\src\Regalis.API\Regalis.API.csproj" -c Release -o "%STAGE%" --nologo
if errorlevel 1 ( echo Build failed. Nothing changed. & pause & exit /b 1 )
REM Must not reach the server: it has no UAT environment variables.
if exist "%STAGE%\web.config" del /q "%STAGE%\web.config"

echo === 2. Swap the binaries
"%APPCMD%" stop apppool /apppool.name:"%POOL%"
timeout /t 3 /nobreak >nul
robocopy "%STAGE%" "%TARGET%" /E /XF web.config /NFL /NDL /NJH /NJS /NP
if errorlevel 8 (
  echo COPY FAILED. Restore with: robocopy "%BACKUP%\api" "%TARGET%" /E /XF web.config
  pause & exit /b 1
)
"%APPCMD%" start apppool /apppool.name:"%POOL%"
timeout /t 5 /nobreak >nul

echo === 3. Verify
powershell -NoProfile -Command "$s=(Invoke-WebRequest 'http://localhost:81/swagger/v1/swagger.json' -UseBasicParsing -TimeoutSec 60).Content | ConvertFrom-Json; if ($s.paths.PSObject.Properties.Name -contains '/api/passes/purchase-options') { 'OK  - /api/passes/purchase-options'; exit 0 } else { 'FAIL- endpoint missing'; exit 1 }"
if errorlevel 1 ( echo The new API is not answering. Frontend NOT changed. & pause & exit /b 1 )

echo === 4. Frontend
node "%~dp0deploy\pass-upgrade\apply.mjs" "%~dp0frontend" "%~dp0deploy\workspace-types\frontend\assets"
if errorlevel 1 ( echo Frontend patch failed - the API is live, the page is unchanged. & pause & exit /b 1 )
echo DEPLOY OK.
pause
