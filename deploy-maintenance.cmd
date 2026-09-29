@echo off
REM ===========================================================================
REM  Maintenance & Housekeeping, step 1 (Maintenance Feature Specification v1.0).
REM
REM  Deploys to UAT:
REM    - the Maintenance page with Checklist, Schedules and Housekeepers tabs,
REM      summary cards and a progress bar (Done + Verified count as complete)
REM    - Add work / edit / reassign / cancel / delete, verify or send back for rework
REM    - housekeepers with a login (HK- codes, temporary password), deactivate with
REM      reassignment, work log
REM    - the housekeeper "My tasks" phone view: Start task, Mark done with note + photo
REM    - automatic Overdue every 5 minutes, with alerts to admins and managers
REM    - Housekeeping/Cleaning schedules also create their checklist task
REM
REM  It publishes the API from source, so it also ships anything else in that source
REM  that is not live yet.
REM
REM  Run from an ELEVATED command prompt.
REM
REM  Steps:
REM    1. back up api\ and the three frontend files; publish the API to staging
REM    2. apply 20260928172213_AddMaintenanceChecklist.sql  (idempotent; new tables,
REM       MW-/HK- sequences, housekeeper profiles for existing Housekeeping Staff)
REM       and   20260928172214_MaintenanceSettings.sql       (idempotent)
REM    3. stop the pool, copy the binaries EXCEPT web.config, start the pool
REM    4. verify the new endpoints answer
REM    5. install the Maintenance screens (patches index + schedule chunk in place,
REM       copies the new chunk, bumps the two service-worker revisions)
REM
REM  To undo: robocopy %BACKUP%\api over api\ and %BACKUP%\frontend over frontend\,
REM  restart the pool, then run
REM  db\migrations\20260928172213_AddMaintenanceChecklist_Rollback.sql.
REM  The rollback drops the checklist and housekeeper-profile tables, so any work
REM  items recorded since are lost. Housekeeper user accounts stay.
REM ===========================================================================
setlocal
cd /d "%~dp0"

set SRC=C:\Users\Administrator\Downloads\Regalis\backend
set STAGE=%TEMP%\regalis-api-maintenance
set TARGET=%~dp0api
set FRONT=%~dp0frontend
set STAGED_FRONT=%~dp0deploy\maintenance\frontend
set BACKUP=C:\inetpub\wwwroot\Regalis_backup_20260928_maintenance
set POOL=regalis-api
set DB=RegalisWorkSpace_UAT
set SQLCMD="C:\Program Files\Microsoft SQL Server\Client SDK\ODBC\180\Tools\Binn\sqlcmd.exe"
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
if not exist %SQLCMD% (
  echo ERROR: sqlcmd not found at %SQLCMD%
  pause & exit /b 1
)
if not exist "%STAGED_FRONT%\assets\AdminMaintenancePage-MT4.js" (
  echo ERROR: staged frontend not found at %STAGED_FRONT%
  pause & exit /b 1
)
echo OK.

echo.
echo === 1. Back up and publish ==================================================
robocopy "%TARGET%" "%BACKUP%\api" /E /NFL /NDL /NJH /NJS /NP
if errorlevel 8 ( echo Backup of api\ failed. Nothing has been changed. & pause & exit /b 1 )
robocopy "%FRONT%" "%BACKUP%\frontend" sw.js /NFL /NDL /NJH /NJS /NP
robocopy "%FRONT%\assets" "%BACKUP%\frontend\assets" index-DI5X4c9f.js AdminHousekeepingPage-BVEQUG24.js /NFL /NDL /NJH /NJS /NP
echo Backed up to %BACKUP%.
if exist "%STAGE%" rmdir /s /q "%STAGE%"
dotnet publish "%SRC%\src\Regalis.API\Regalis.API.csproj" -c Release -o "%STAGE%" --nologo
if errorlevel 1 ( echo. & echo Build failed. Nothing has been changed. & pause & exit /b 1 )
REM Must not reach the server: it has no UAT environment variables.
if exist "%STAGE%\web.config" del /q "%STAGE%\web.config"
echo Published to %STAGE%.

echo.
echo === 2. Database migration ===================================================
%SQLCMD% -S localhost -d %DB% -E -C -I -b -i "%~dp0db\migrations\20260928172213_AddMaintenanceChecklist.sql"
if errorlevel 1 (
  echo.
  echo The schema migration failed. It runs in a transaction, so nothing was kept,
  echo and the API has NOT been touched - the site is still up on the old build.
  pause & exit /b 1
)
%SQLCMD% -S localhost -d %DB% -E -C -I -b -W -i "%~dp0db\migrations\20260928172214_MaintenanceSettings.sql"
if errorlevel 1 ( echo The settings script failed - the page falls back to the default categories and areas until it is re-run. )

echo.
echo === 3. Swap the binaries ====================================================
"%APPCMD%" stop apppool /apppool.name:"%POOL%"
timeout /t 3 /nobreak >nul
robocopy "%STAGE%" "%TARGET%" /E /XF web.config /NFL /NDL /NJH /NJS /NP
if errorlevel 8 (
  echo.
  echo COPY FAILED. The pool is stopped and the site is DOWN. Restore with:
  echo   robocopy "%BACKUP%\api" "%TARGET%" /E
  echo   "%APPCMD%" start apppool /apppool.name:"%POOL%"
  pause & exit /b 1
)
"%APPCMD%" start apppool /apppool.name:"%POOL%"
timeout /t 5 /nobreak >nul

echo.
echo === 4. Verify the API ======================================================
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0deploy\maintenance\deploy-steps.ps1" -Step verify
if errorlevel 1 (
  echo.
  echo The new API is not answering correctly. The frontend has NOT been changed.
  echo Check api\logs, or restore the backup as shown above.
  pause & exit /b 1
)

echo.
echo === 5. Install the frontend ================================================
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0deploy\maintenance\deploy-steps.ps1" -Step frontend -Front "%FRONT%"
if errorlevel 1 ( echo The frontend step failed - see the message above. The API is updated; restore %BACKUP%\frontend if the screens look wrong. & pause & exit /b 1 )
echo.
echo DEPLOY OK. Open http://localhost:8080/admin/housekeeping and hard-refresh.
echo Existing Housekeeping Staff (Bavanthi Ravi, Dhinakaran) now appear on the
echo Housekeepers tab and can be assigned work straight away.
pause
endlocal
