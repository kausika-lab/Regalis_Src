@echo off
REM ===========================================================================
REM  Promote what UAT runs today to PRODUCTION (myregalis.com).
REM
REM  Every deploy-*.cmd in Regalis_Src updates UAT only. Once a change is
REM  tested on UAT, run this to copy the same API + frontend to production and
REM  apply that change's SQL migration(s) to RegalisWorkSpace_PROD.
REM
REM    promote-uat-to-prod.cmd                       (no schema change)
REM    promote-uat-to-prod.cmd db\migrations\X.sql   (one or more, oldest first,
REM                                                   paths relative to Regalis_Src)
REM
REM  Only run migrations meant for production: skip *_UAT_ONLY.sql and
REM  *_Rollback.sql. Production keeps its own web.config files, so its
REM  database, JWT key, uploads and HTTPS rules are never overwritten.
REM  Restarting the API signs everyone out once (Auth:EndSessionsOnRestart).
REM
REM  Run from an ELEVATED command prompt.
REM ===========================================================================
setlocal EnableDelayedExpansion
cd /d "%~dp0..\.."

set SRC=%CD%
set PROD=C:\inetpub\wwwroot\Regalis_Prod
set DB=RegalisWorkSpace_PROD
set POOL=regalis-prod-api
set APPCMD=%SystemRoot%\System32\inetsrv\appcmd.exe
set SQLCMD="C:\Program Files\Microsoft SQL Server\Client SDK\ODBC\180\Tools\Binn\sqlcmd.exe"
for /f %%t in ('powershell -NoProfile -Command "Get-Date -Format yyyyMMdd_HHmm"') do set STAMP=%%t
REM The SQL instance production uses (localhost\SQLEXPRESS after 07-move-to-sqlexpress.ps1).
for /f %%s in ('powershell -NoProfile -Command "([xml](Get-Content '%PROD%\api\web.config' -Raw)).SelectSingleNode('//environmentVariable[@name=''ConnectionStrings__Default'']').value -replace '^.*Server=([^;]+);.*$','$1'"') do set PRODSQL=%%s
set BACKUP=C:\inetpub\wwwroot\Regalis_Prod_backup_%STAMP%

echo.
echo === 0. Checks ===============================================================
net session >nul 2>&1
if errorlevel 1 ( echo ERROR: not elevated. Re-open with "Run as administrator". & pause & exit /b 1 )
if not exist "%PROD%\api\web.config" ( echo ERROR: production is not set up - run deploy\production\04-create-sites.ps1 first. & pause & exit /b 1 )
for %%m in (%*) do (
  if not exist "%SRC%\%%~m" ( echo ERROR: migration not found: %%~m & pause & exit /b 1 )
  echo %%~nm | findstr /i "_Rollback _UAT_ONLY" >nul && ( echo ERROR: %%~nxm must not run on production. & pause & exit /b 1 )
)
echo OK. Promoting UAT to production. Migrations: %*
choice /m "Continue"
if errorlevel 2 exit /b 1

echo.
echo === 1. Back up production ===================================================
robocopy "%PROD%\api" "%BACKUP%\api" /E /XD logs /NFL /NDL /NJH /NJS /NP
if errorlevel 8 ( echo Backup failed. Nothing has been changed. & pause & exit /b 1 )
robocopy "%PROD%\frontend" "%BACKUP%\frontend" /E /NFL /NDL /NJH /NJS /NP
if errorlevel 8 ( echo Backup failed. Nothing has been changed. & pause & exit /b 1 )
%SQLCMD% -S %PRODSQL% -E -C -b -Q "BACKUP DATABASE [%DB%] TO DISK = N'%DB%_%STAMP%_promote.bak' WITH COPY_ONLY, INIT, CHECKSUM"
if errorlevel 1 ( echo Database backup failed. Nothing has been changed. & pause & exit /b 1 )
echo Backed up to %BACKUP% and SQL Server's Backup folder (%DB%_%STAMP%_promote.bak).

echo.
echo === 2. Database migrations ==================================================
for %%m in (%*) do (
  echo Applying %%~nxm ...
  %SQLCMD% -S %PRODSQL% -d %DB% -E -C -I -b -i "%SRC%\%%~m"
  if errorlevel 1 ( echo. & echo %%~nxm FAILED. The API and frontend have NOT been changed. Restore the database from %DB%_%STAMP%_promote.bak if needed. & pause & exit /b 1 )
)

echo.
echo === 3. API ==================================================================
"%APPCMD%" stop apppool /apppool.name:"%POOL%"
timeout /t 3 /nobreak >nul
robocopy "%SRC%\api" "%PROD%\api" /E /XD logs App_Data /XF web.config *.pdb appsettings.Development.json /NFL /NDL /NJH /NJS /NP
if errorlevel 8 (
  echo COPY FAILED. Production API is DOWN. Restore with:
  echo   robocopy "%BACKUP%\api" "%PROD%\api" /E
  echo   "%APPCMD%" start apppool /apppool.name:"%POOL%"
  pause & exit /b 1
)
"%APPCMD%" start apppool /apppool.name:"%POOL%"
timeout /t 5 /nobreak >nul
powershell -NoProfile -Command "try { if ((Invoke-WebRequest http://127.0.0.1:82/health -UseBasicParsing -TimeoutSec 60).Content -ne 'Healthy') { exit 1 } } catch { exit 1 }"
if errorlevel 1 ( echo. & echo The production API is not healthy. Frontend NOT changed. Check %PROD%\api\logs or restore %BACKUP%\api. & pause & exit /b 1 )
echo API healthy.

echo.
echo === 4. Frontend =============================================================
REM /E, not /MIR: keeps win-acme's .well-known folder. web.config and env.js are
REM production's own (sync-frontend-config.ps1 writes both).
robocopy "%SRC%\frontend" "%PROD%\frontend" /E /XF web.config env.js /NFL /NDL /NJH /NJS /NP
if errorlevel 8 ( echo Frontend copy failed. Restore %BACKUP%\frontend. & pause & exit /b 1 )
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0sync-frontend-config.ps1"
if errorlevel 1 ( echo Frontend web.config failed. Restore %BACKUP%\frontend\web.config. & pause & exit /b 1 )

echo.
echo PROMOTED. Production now runs what UAT runs.
pause
endlocal
