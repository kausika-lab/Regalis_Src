@echo off
REM ===========================================================================
REM  Reports & Analysis (24 Oct 2026) -
REM  Regalis_WorkSpace_Reports_Analysis_Feature_Specification.
REM
REM  Deploys to UAT:
REM    - Analytics > Reports & Analysis: a charts overview (four headline cards with
REM      sparklines and comparisons, revenue over time, revenue by category, bookings
REM      by type each week, the busy-hours heatmap, busiest workspaces, money owed by
REM      age, top customers), one date-range bar, remembered per admin; PDF summary.
REM    - 24 reports on one definition-driven page: filters with chips, summary figures,
REM      a chart strip, a table whose total row counts every matching row, server-side
REM      sort and paging, chosen columns remembered, Excel and PDF of every row
REM      (over 5,000 rows: built in the background and emailed as a 7-day link).
REM    - Report library: search, category chips, favourites / most used, four groups,
REM      and reports that email themselves (daily / weekly / monthly, IST, retried,
REM      stopped after a week of failures). Mail goes through Microsoft Graph.
REM    - A nightly 02:00 IST rollup (RPT_DailyMetric) for the overview; it fills any
REM      missing days on start-up, so the first start builds the history once.
REM    - New permissions REPORT.FINANCE (Administrator, Finance), REPORT.CUSTOMERS and
REM      REPORT.MANAGE (Administrator). Sign out and in again to pick them up.
REM    - Old /admin/reports/<key> bookmarks redirect to the new reports.
REM
REM  Builds from C:\Users\Administrator\Downloads\Regalis-live, branch
REM  feature/reports-analysis (= feature/assets-live + this feature).
REM  Database: 20261024033724_ReportsAnalysis (new tables only, one transaction) and
REM  20261024033725_ReportsAnalysisAccess (permissions, grants, sub-menus).
REM  Rollback: db\migrations\20261024033724_ReportsAnalysis_Rollback.sql, then the
REM  backup below.
REM
REM  Restarting the API signs everyone out once (Auth:EndSessionsOnRestart).
REM  Run from an ELEVATED command prompt.
REM ===========================================================================
setlocal
cd /d "%~dp0"

set SRC=C:\Users\Administrator\Downloads\Regalis-live\backend
set STAGE=%TEMP%\regalis-api-reports
set TARGET=%~dp0api
set FRONT=%~dp0frontend
set BACKUP=C:\inetpub\wwwroot\Regalis_backup_20261024_reports
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
if not exist "%~dp0deploy\reports-analysis\frontend\assets\ReportsAnalysis-RA1.js" ( echo ERROR: staged frontend missing & pause & exit /b 1 )
where node >nul 2>&1
if errorlevel 1 ( echo ERROR: node is not on the PATH. & pause & exit /b 1 )
echo OK.

echo.
echo === 1. Back up and publish ==================================================
robocopy "%TARGET%" "%BACKUP%\api" /E /XD logs /NFL /NDL /NJH /NJS /NP
if errorlevel 8 ( echo Backup of api\ failed. Nothing has been changed. & pause & exit /b 1 )
robocopy "%FRONT%" "%BACKUP%\frontend" sw.js /NFL /NDL /NJH /NJS /NP
robocopy "%FRONT%\assets" "%BACKUP%\frontend\assets" index-DI5X4c9f.js /NFL /NDL /NJH /NJS /NP
%SQLCMD% -S localhost -E -C -b -Q "BACKUP DATABASE [%DB%] TO DISK = N'%DB%_20261024_reports.bak' WITH COPY_ONLY, INIT"
if errorlevel 1 ( echo Database backup failed. Nothing has been changed. & pause & exit /b 1 )
echo Backed up to %BACKUP% and SQL Server's Backup folder.
if exist "%STAGE%" rmdir /s /q "%STAGE%"
dotnet publish "%SRC%\src\Regalis.API\Regalis.API.csproj" -c Release -o "%STAGE%" --nologo
if errorlevel 1 ( echo. & echo Build failed. Nothing has been changed. & pause & exit /b 1 )
if exist "%STAGE%\web.config" del /q "%STAGE%\web.config"
echo Published to %STAGE%.

echo.
echo === 2. Database migration ===================================================
REM Before the binaries: the new code reads the new tables. The old code ignores them.
%SQLCMD% -S localhost -d %DB% -E -C -I -b -i "%~dp0db\migrations\20261024033724_ReportsAnalysis.sql"
if errorlevel 1 ( echo. & echo The migration failed; it runs in a transaction, so nothing was kept. Nothing else has been changed. & pause & exit /b 1 )
%SQLCMD% -S localhost -d %DB% -E -C -I -b -W -i "%~dp0db\migrations\20261024033725_ReportsAnalysisAccess.sql"
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
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0deploy\reports-analysis\verify.ps1"
if errorlevel 1 (
  echo.
  echo The new API is not answering correctly. The frontend has NOT been changed.
  echo Check api\logs, or restore the backup as shown above.
  pause & exit /b 1
)

echo.
echo === 5. Install the frontend ================================================
node "%~dp0deploy\reports-analysis\apply.mjs" "%FRONT%"
if errorlevel 1 ( echo The frontend step failed - see above. Restore %BACKUP%\frontend if a page looks wrong. & pause & exit /b 1 )

echo.
echo DEPLOY OK. Sign out and in again, then open Analytics ^> Reports ^& Analysis.
pause
endlocal
