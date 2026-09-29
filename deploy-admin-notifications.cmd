@echo off
REM ===========================================================================
REM  Deploys admin notifications (booking / support / profile alerts) to UAT.
REM
REM  Run this from an ELEVATED command prompt - it stops and starts an IIS
REM  application pool and writes into C:\inetpub\wwwroot.
REM
REM  No database migration: the new "Account" category (13) is a tinyint enum
REM  value, and NotificationDto's new ReferenceType/ReferenceId fields read
REM  columns TRN_Notification already has.
REM
REM  The frontend half (frontend\assets\AdminNotificationsPage-DMB3N4VK.js and
REM  the ?booking= deep link in AdminBookingsPage-BqwuNlMp.js) is already live.
REM  Until this runs, the admin feed stays empty ("All caught up").
REM
REM  Steps:
REM    1. back up api\ and publish the API from source to a staging folder
REM    2. stop the regalis-api app pool
REM    3. copy the staged files over api\  - EXCEPT web.config
REM    4. start the pool and verify the API answers
REM
REM  web.config is never copied. The published one carries none of the UAT
REM  environment variables, so overwriting the live file would take the API
REM  down by losing the connection string and the JWT signing key.
REM
REM  To undo: robocopy %BACKUP% over api\ and restart the pool.
REM ===========================================================================
setlocal
cd /d "%~dp0"

set SRC=C:\Users\Administrator\Downloads\Regalis\backend
set STAGE=%TEMP%\regalis-api-publish
set TARGET=%~dp0api
set BACKUP=C:\inetpub\wwwroot\Regalis_api_backup_20260925_notifications
set POOL=regalis-api
set APPCMD=%SystemRoot%\System32\inetsrv\appcmd.exe

echo.
echo === 0. Checks ===============================================================
net session >nul 2>&1
if errorlevel 1 (
  echo ERROR: not elevated. Re-open the command prompt with "Run as administrator".
  pause & exit /b 1
)
if not exist "%SRC%\Regalis.sln" (
  echo ERROR: API source not found at %SRC%
  pause & exit /b 1
)
echo OK.

echo.
echo === 1. Back up and publish ==================================================
robocopy "%TARGET%" "%BACKUP%" /E /NFL /NDL /NJH /NJS /NP
if errorlevel 8 (
  echo Backup failed. Nothing has been changed.
  pause & exit /b 1
)
echo Backed up to %BACKUP%.
if exist "%STAGE%" rmdir /s /q "%STAGE%"
dotnet publish "%SRC%\src\Regalis.API\Regalis.API.csproj" -c Release -o "%STAGE%" --nologo
if errorlevel 1 (
  echo.
  echo Build failed. Nothing has been changed.
  pause & exit /b 1
)
REM Must not reach the server: it has no UAT environment variables.
if exist "%STAGE%\web.config" del /q "%STAGE%\web.config"
echo Published to %STAGE%.

echo.
echo === 2. Stop the app pool ====================================================
"%APPCMD%" stop apppool /apppool.name:"%POOL%"
REM Give w3wp a moment to release the DLLs it has loaded.
timeout /t 3 /nobreak >nul

echo.
echo === 3. Copy the binaries ====================================================
robocopy "%STAGE%" "%TARGET%" /E /XF web.config /NFL /NDL /NJH /NJS /NP
if errorlevel 8 (
  echo.
  echo COPY FAILED. The pool is stopped and the site is DOWN.
  echo Restore with:
  echo   robocopy "%BACKUP%" "%TARGET%" /E
  echo   "%APPCMD%" start apppool /apppool.name:"%POOL%"
  pause & exit /b 1
)
echo Copied.

echo.
echo === 4. Start and verify =====================================================
"%APPCMD%" start apppool /apppool.name:"%POOL%"
timeout /t 5 /nobreak >nul

powershell -NoProfile -Command ^
  "try { $s = (Invoke-WebRequest 'http://localhost:81/swagger/v1/swagger.json' -UseBasicParsing -TimeoutSec 30).Content | ConvertFrom-Json;" ^
  "  if ($s.components.schemas.NotificationDto.properties.PSObject.Properties.Name -contains 'referenceId') { Write-Host 'DEPLOY OK - NotificationDto carries referenceId; raise a ticket or booking and check /admin/notifications' } else { Write-Host 'FAIL- the old build is still serving' } }" ^
  "catch { Write-Host ('FAIL- API did not answer: ' + $_.Exception.Message + ' - see api\logs\stdout*.log') }"

echo.
pause
endlocal
