@echo off
REM ===========================================================================
REM  Pay later at the front desk (27 Oct 2026) - "Pay Later (Cash) - Feature Design &
REM  Implementation Plan", decisions of 27 Oct 2026.
REM
REM  Deploys to UAT:
REM    - Checkout: "Pay later at the front desk" for hourly, multi-day and monthly
REM      bookings. The booking is confirmed and the invoice issued as unpaid; check-in is
REM      refused until it is paid in full. 2 unpaid pay-later checkouts per customer, no
REM      amount limit. Reminder 24 h before; released if nothing is paid 30 min after the
REM      start; pay later switched off for a customer after 3 such releases.
REM    - Billing & Finance > Cash Collections: what is owed, Take payment (cash, UPI or
REM      card), numbered receipts (PDF), void with a reason (Admin only).
REM    - Billing & Finance > Cash Register: each collector's drawer per day, close with a
REM      count (a difference needs a reason), Admin approves.
REM    - My Bookings / Admin Bookings show what a pay-later booking still owes; admin
REM      booking can choose Pay later.
REM    - New permissions PAYMENT.CASH_COLLECT, .CASH_CLOSE (ADMIN, FINANCE, MANAGER),
REM      .CASH_VOID, .CASH_APPROVE (ADMIN). Sign out and in again to pick them up.
REM
REM  SHIPS SWITCHED OFF: PayLater.Enabled = false, Wallet.DeskAddMoneyEnabled = true, so
REM  nothing changes for customers. Go-live = Settings > PayLater.Enabled = true and
REM  Wallet.DeskAddMoneyEnabled = false (wallets are then topped up online only).
REM
REM  BEFORE RUNNING: apply deploy\pay-later\backend to the source in %SRC% (README.md
REM  in that folder: new files to copy, edits to existing files). This script refuses to
REM  run until CashCollectionService.cs is there.
REM  Database: 20261027182059_PayLaterCash (new tables and columns, one transaction) and
REM  20261027182100_PayLaterAccess (permissions, grants, sidebar, settings, sequences).
REM  Rollback: db\migrations\20261027182100_PayLaterAccess_Rollback.sql, then
REM  20261027182059_PayLaterCash_Rollback.sql, then the backup below.
REM
REM  Restarting the API signs everyone out once (Auth:EndSessionsOnRestart).
REM  Run from an ELEVATED command prompt.
REM ===========================================================================
setlocal
cd /d "%~dp0"

set SRC=C:\Users\Administrator\Downloads\Regalis-live\backend
set STAGE=%TEMP%\regalis-api-paylater
set TARGET=%~dp0api
set FRONT=%~dp0frontend
set BACKUP=C:\inetpub\wwwroot\Regalis_backup_20261028_paylater
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
if not exist "%SRC%\src\Regalis.Infrastructure\Services\CashCollectionService.cs" (
  echo ERROR: the Pay later source changes are not in %SRC%.
  echo Apply deploy\pay-later\backend first - see deploy\pay-later\README.md.
  pause & exit /b 1
)
if not exist %SQLCMD% ( echo ERROR: sqlcmd not found at %SQLCMD% & pause & exit /b 1 )
if not exist "%~dp0deploy\pay-later\frontend\assets\AdminCashPages-PL1.js" ( echo ERROR: staged frontend missing & pause & exit /b 1 )
where node >nul 2>&1
if errorlevel 1 ( echo ERROR: node is not on the PATH. & pause & exit /b 1 )
echo OK.

echo.
echo === 1. Back up and publish ==================================================
robocopy "%TARGET%" "%BACKUP%\api" /E /XD logs /NFL /NDL /NJH /NJS /NP
if errorlevel 8 ( echo Backup of api\ failed. Nothing has been changed. & pause & exit /b 1 )
robocopy "%FRONT%" "%BACKUP%\frontend" sw.js web.config /NFL /NDL /NJH /NJS /NP
robocopy "%FRONT%\assets" "%BACKUP%\frontend\assets" index-DI5X4c9f.js BookWorkspacePage-MB2.js MyBookingsPage-Jm2rP9kD.js AdminBookingsPage-BK2.js /NFL /NDL /NJH /NJS /NP
%SQLCMD% -S localhost -E -C -b -Q "BACKUP DATABASE [%DB%] TO DISK = N'%DB%_20261028_paylater.bak' WITH COPY_ONLY, INIT"
if errorlevel 1 ( echo Database backup failed. Nothing has been changed. & pause & exit /b 1 )
echo Backed up to %BACKUP% and SQL Server's Backup folder.
if exist "%STAGE%" rmdir /s /q "%STAGE%"
dotnet publish "%SRC%\src\Regalis.API\Regalis.API.csproj" -c Release -o "%STAGE%" --nologo
if errorlevel 1 ( echo. & echo Build failed. Nothing has been changed. & pause & exit /b 1 )
if exist "%STAGE%\web.config" del /q "%STAGE%\web.config"
echo Published to %STAGE%.

echo.
echo === 2. Database migration ===================================================
REM Before the binaries: the new code reads the new columns. The old code ignores them.
%SQLCMD% -S localhost -d %DB% -E -C -I -b -i "%~dp0db\migrations\20261027182059_PayLaterCash.sql"
if errorlevel 1 ( echo. & echo The migration failed; it runs in a transaction, so nothing was kept. Nothing else has been changed. & pause & exit /b 1 )
%SQLCMD% -S localhost -d %DB% -E -C -I -b -W -i "%~dp0db\migrations\20261027182100_PayLaterAccess.sql"
if errorlevel 1 ( echo. & echo The permissions script failed; it runs in a transaction. Nothing else has been changed. & pause & exit /b 1 )
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
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0deploy\pay-later\verify.ps1"
if errorlevel 1 (
  echo.
  echo The new API is not answering correctly. The frontend has NOT been changed.
  echo Check api\logs, or restore the backup as shown above.
  pause & exit /b 1
)

echo.
echo === 5. Install the frontend ================================================
if exist "%~dp0deploy\workspace-types\frontend\assets\BookWorkspacePage-MB3.js" (
  node "%~dp0deploy\pay-later\frontend\apply.mjs" "%FRONT%" "%~dp0deploy\workspace-types\frontend\assets"
) else (
  node "%~dp0deploy\pay-later\frontend\apply.mjs" "%FRONT%"
)
if errorlevel 1 ( echo The frontend step failed - see above. Restore %BACKUP%\frontend if a page looks wrong. & pause & exit /b 1 )

echo.
echo DEPLOY OK. Pay later is still OFF. Sign out and in again, then open
echo Billing ^& Finance ^> Cash Collections. To go live: Settings ^> PayLater.Enabled = true
echo and Wallet.DeskAddMoneyEnabled = false.
pause
endlocal
