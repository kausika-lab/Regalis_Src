@echo off
REM ===========================================================================
REM  Expenses: charts, adjustments and filter alignment (30 Sep 2026).
REM
REM  Deploys to UAT:
REM    - Expense by category pie (amount + %) and Monthly expense columns (Jan-Dec
REM      of a chosen year); both follow the Search / Category / From / To filters
REM    - Adjustments against an existing expense (Increase / Decrease, amount, date,
REM      reason, remarks) with an Adjustment history tab: View / Edit / Delete
REM    - every expense total is now NET of adjustments: the Expenses summary, the
REM      finance dashboard card, "Expenses this month", the Expense report and the
REM      Finance summary report
REM    - Search, Category, From and To in one aligned row
REM
REM  It publishes the API from source, so it also ships anything else in that source
REM  that is not live yet.
REM
REM  Run from an ELEVATED command prompt.
REM
REM  Steps:
REM    1. back up api\, index and sw.js; publish the API to staging
REM    2. apply 20260930123139_ExpenseAdjustments.sql (idempotent; adds
REM       TRN_Expense.AdjustmentTotal = 0 and the TRN_ExpenseAdjustment table)
REM    3. stop the pool, copy the binaries EXCEPT web.config, start the pool
REM    4. verify the new endpoints answer
REM    5. install the new Expenses screen (patches the index in place)
REM
REM  To undo: robocopy %BACKUP%\api over api\ and %BACKUP%\frontend over frontend\,
REM  restart the pool, then run
REM  db\migrations\20260930123139_ExpenseAdjustments_Rollback.sql.
REM ===========================================================================
setlocal
cd /d "%~dp0"

set SRC=C:\Users\Administrator\Downloads\Regalis\backend
set STAGE=%TEMP%\regalis-api-expense-adjustments
set TARGET=%~dp0api
set FRONT=%~dp0frontend
set STAGED_FRONT=%~dp0deploy\expense-adjustments\frontend
set BACKUP=C:\inetpub\wwwroot\Regalis_backup_20260930_expense_adjustments
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
if not exist "%STAGED_FRONT%\assets\AdminExpensesPage-EX1.js" (
  echo ERROR: staged frontend not found at %STAGED_FRONT%
  pause & exit /b 1
)
echo OK.

echo.
echo === 1. Back up and publish ==================================================
robocopy "%TARGET%" "%BACKUP%\api" /E /NFL /NDL /NJH /NJS /NP
if errorlevel 8 ( echo Backup of api\ failed. Nothing has been changed. & pause & exit /b 1 )
robocopy "%FRONT%" "%BACKUP%\frontend" sw.js /NFL /NDL /NJH /NJS /NP
robocopy "%FRONT%\assets" "%BACKUP%\frontend\assets" index-DI5X4c9f.js /NFL /NDL /NJH /NJS /NP
echo Backed up to %BACKUP%.
if exist "%STAGE%" rmdir /s /q "%STAGE%"
dotnet publish "%SRC%\src\Regalis.API\Regalis.API.csproj" -c Release -o "%STAGE%" --nologo
if errorlevel 1 ( echo. & echo Build failed. Nothing has been changed. & pause & exit /b 1 )
REM Must not reach the server: it has no UAT environment variables.
if exist "%STAGE%\web.config" del /q "%STAGE%\web.config"
echo Published to %STAGE%.

echo.
echo === 2. Database migration ===================================================
%SQLCMD% -S localhost -d %DB% -E -C -I -b -i "%~dp0db\migrations\20260930123139_ExpenseAdjustments.sql"
if errorlevel 1 (
  echo.
  echo The schema migration failed. It runs in a transaction, so nothing was kept,
  echo and the API has NOT been touched - the site is still up on the old build.
  pause & exit /b 1
)

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
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0deploy\expense-adjustments\deploy-steps.ps1" -Step verify
if errorlevel 1 (
  echo.
  echo The new API is not answering correctly. The frontend has NOT been changed.
  echo Check api\logs, or restore the backup as shown above.
  pause & exit /b 1
)

echo.
echo === 5. Install the frontend ================================================
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0deploy\expense-adjustments\deploy-steps.ps1" -Step frontend -Front "%FRONT%"
if errorlevel 1 ( echo The frontend step failed - see the message above. The API is updated; restore %BACKUP%\frontend if the screen looks wrong. & pause & exit /b 1 )
echo.
echo DEPLOY OK. Open http://localhost:8080/admin/expenses and hard-refresh.
pause
endlocal
