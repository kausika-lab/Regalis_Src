@echo off
REM ===========================================================================
REM  Monthly desk / cabin booking (01 Oct 2026).
REM
REM  Deploys to UAT:
REM    - Book Workspace: Desk and Cabin are booked by the month (1, 3, 6 or 12
REM      months from a start date) instead of by time slot; the customer then
REM      picks one workspace that is free for the whole term. Price = the
REM      workspace's "Price / month" x months, plus GST; one invoice per term.
REM    - Taken workspaces stay on the pick list, greyed out and unselectable
REM      (Desk, Cabin and Meeting Room), labelled Booked / No monthly price.
REM    - Admin > Workspaces gets a "Price / month" field. Until it is filled
REM      in, a desk or cabin shows "No monthly price" and cannot be booked.
REM    - A daily check-out no longer ends a monthly booking; reschedule and
REM      extend are refused for monthly bookings.
REM
REM  Database: adds TRN_Booking.BillingMonths (nullable tinyint). Rollback:
REM  db\migrations\20260930183340_MonthlyWorkspaceBooking_Rollback.sql.
REM
REM  Restarting the API signs everyone out once (Auth:EndSessionsOnRestart).
REM  It publishes the API from source, so it also ships anything else in that
REM  source that is not live yet.
REM
REM  Run from an ELEVATED command prompt.
REM ===========================================================================
setlocal
cd /d "%~dp0"

set SRC=C:\Users\Administrator\Downloads\Regalis\backend
set STAGE=%TEMP%\regalis-api-monthly-booking
set TARGET=%~dp0api
set FRONT=%~dp0frontend
set BACKUP=C:\inetpub\wwwroot\Regalis_backup_20261001_monthly_booking
set POOL=regalis-api
set APPCMD=%SystemRoot%\System32\inetsrv\appcmd.exe
set DB=RegalisWorkSpace_UAT
set SQLCMD="C:\Program Files\Microsoft SQL Server\Client SDK\ODBC\180\Tools\Binn\sqlcmd.exe"

echo.
echo === 0. Checks ===============================================================
net session >nul 2>&1
if errorlevel 1 (
  echo ERROR: not elevated. Re-open the command prompt with "Run as administrator".
  pause & exit /b 1
)
if not exist "%SRC%\Regalis.sln" ( echo ERROR: API source not found at %SRC% & pause & exit /b 1 )
if not exist %SQLCMD% ( echo ERROR: sqlcmd not found at %SQLCMD% & pause & exit /b 1 )
if not exist "%~dp0deploy\monthly-booking\frontend\assets\BookWorkspacePage-MB1.js" ( echo ERROR: staged frontend missing & pause & exit /b 1 )
echo OK.

echo.
echo === 1. Back up and publish ==================================================
robocopy "%TARGET%" "%BACKUP%\api" /E /XD logs /NFL /NDL /NJH /NJS /NP
if errorlevel 8 ( echo Backup of api\ failed. Nothing has been changed. & pause & exit /b 1 )
robocopy "%FRONT%" "%BACKUP%\frontend" sw.js /NFL /NDL /NJH /NJS /NP
robocopy "%FRONT%\assets" "%BACKUP%\frontend\assets" index-DI5X4c9f.js /NFL /NDL /NJH /NJS /NP
echo Backed up to %BACKUP%.
if exist "%STAGE%" rmdir /s /q "%STAGE%"
dotnet publish "%SRC%\src\Regalis.API\Regalis.API.csproj" -c Release -o "%STAGE%" --nologo
if errorlevel 1 ( echo. & echo Build failed. Nothing has been changed. & pause & exit /b 1 )
if exist "%STAGE%\web.config" del /q "%STAGE%\web.config"
echo Published to %STAGE%.

echo.
echo === 2. Database migration ===================================================
REM Before the binaries: EF names every column, so new code on the old schema
REM would break every booking query. The old code ignores the new column.
%SQLCMD% -S localhost -d %DB% -E -C -I -b -i "%~dp0db\migrations\20260930183340_MonthlyWorkspaceBooking.sql"
if errorlevel 1 ( echo. & echo The migration failed; it runs in a transaction, so nothing was kept. Nothing else has been changed. & pause & exit /b 1 )
echo Migration applied.

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
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0deploy\monthly-booking\deploy-steps.ps1"
if errorlevel 1 (
  echo.
  echo The new API is not answering correctly. The frontend has NOT been changed.
  echo Check api\logs, or restore the backup as shown above.
  pause & exit /b 1
)

echo.
echo === 5. Install the frontend ================================================
node "%~dp0deploy\monthly-booking\apply.mjs" "%FRONT%"
if errorlevel 1 ( echo The frontend step failed - see above. Restore %BACKUP%\frontend if the booking page looks wrong. & pause & exit /b 1 )

echo.
echo DEPLOY OK. Next: Admin ^> Workspaces - enter a "Price / month" for every desk
echo and cabin customers may book by the month.
pause
endlocal
