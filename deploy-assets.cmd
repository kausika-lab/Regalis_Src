@echo off
REM ===========================================================================
REM  Assets module (16 Oct 2026) - Regalis_WorkSpace_Assets_Feature_Specification.
REM
REM  Deploys to UAT:
REM    - Assets > Items: assets and consumables in one list, five summary cards,
REM      filters, an item drawer (overview, movements, repairs, value, documents)
REM      with its QR code, New item (an asset form that creates one record per
REM      unit, -001 to -004), QR label sheets (A4, 24 or 40 a page), Excel export,
REM      the one-off import of the team's asset sheet, categories and rooms.
REM    - Assets > Incoming Goods: receipts (draft, post, reverse), against a PO,
REM      with what each line will do worked out before posting.
REM    - Assets > Outgoing Goods: used up, moved, sent for repair, returned,
REM      scrapped, lost - never below zero; reversals, never deletes.
REM    - Assets > Current Stock: opening / in / out / on hand for a period, assets
REM      by room, the movement ledger with running balances, stock counts.
REM    - Purchase orders: "Receive" now posts an Incoming Goods receipt.
REM    - New permissions INVENTORY.RECEIVE / ISSUE / LABELS / COUNT (roles that can
REM      add items today) and INVENTORY.REVERSE (Administrator only).
REM
REM  REQUIRES the Workspace types release first (deploy-workspace-types.cmd): this
REM  build contains it, and its migration must already be on the database. The
REM  script checks and stops if it is not.
REM
REM  Database: migration 20261016030926_AssetsModule, one transaction. Opening
REM  quantities typed on the old item form become an opening-balance receipt; a
REM  built-in check stops the migration if any stock figure would change.
REM  Rollback: db\migrations\20261016030926_AssetsModule_Rollback.sql, or the
REM  backup below.
REM
REM  Restarting the API signs everyone out once (Auth:EndSessionsOnRestart).
REM  It publishes the API from the assets worktree, so it also ships anything else
REM  in that source that is not live yet.
REM
REM  Run from an ELEVATED command prompt.
REM ===========================================================================
setlocal
cd /d "%~dp0"

set SRC=C:\Users\Administrator\Downloads\Regalis-assets\backend
set STAGE=%TEMP%\regalis-api-assets
set TARGET=%~dp0api
set FRONT=%~dp0frontend
set BACKUP=C:\inetpub\wwwroot\Regalis_backup_20261016_assets
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
if not exist "%~dp0deploy\assets\frontend\assets\AssetItemsPage-AS1.js" ( echo ERROR: staged frontend missing & pause & exit /b 1 )
where node >nul 2>&1
if errorlevel 1 ( echo ERROR: node is not on the PATH. & pause & exit /b 1 )
set WT=
for /f %%c in ('call %SQLCMD% -S localhost -d %DB% -E -C -h-1 -W -Q "SET NOCOUNT ON; SELECT COUNT(*) FROM __EFMigrationsHistory WHERE MigrationId = '20261014075619_WorkspaceTypeManagement'"') do set WT=%%c
if not "%WT%"=="1" (
  echo ERROR: the Workspace types release is not on %DB% yet.
  echo        Run deploy-workspace-types.cmd first - this build contains it.
  pause & exit /b 1
)
echo OK.

echo.
echo === 1. Back up and publish ==================================================
robocopy "%TARGET%" "%BACKUP%\api" /E /XD logs /NFL /NDL /NJH /NJS /NP
if errorlevel 8 ( echo Backup of api\ failed. Nothing has been changed. & pause & exit /b 1 )
robocopy "%FRONT%" "%BACKUP%\frontend" sw.js /NFL /NDL /NJH /NJS /NP
robocopy "%FRONT%\assets" "%BACKUP%\frontend\assets" index-DI5X4c9f.js /NFL /NDL /NJH /NJS /NP
REM A bare file name lands in SQL Server's own Backup folder, which its service
REM account can write to (MSSQL17.MSSQLSERVER\MSSQL\Backup).
%SQLCMD% -S localhost -E -C -b -Q "BACKUP DATABASE [%DB%] TO DISK = N'%DB%_20261016_assets.bak' WITH COPY_ONLY, INIT"
if errorlevel 1 ( echo Database backup failed. Nothing has been changed. & pause & exit /b 1 )
echo Backed up to %BACKUP% and SQL Server's Backup folder.
if exist "%STAGE%" rmdir /s /q "%STAGE%"
dotnet publish "%SRC%\src\Regalis.API\Regalis.API.csproj" -c Release -o "%STAGE%" --nologo
if errorlevel 1 ( echo. & echo Build failed. Nothing has been changed. & pause & exit /b 1 )
if exist "%STAGE%\web.config" del /q "%STAGE%\web.config"
echo Published to %STAGE%.

echo.
echo === 2. Database migration ===================================================
REM Before the binaries: EF names every column, so new code on the old schema
REM would break every item query. The old code ignores the new columns and tables.
REM -I: the new tables carry filtered unique indexes (QUOTED_IDENTIFIER must be ON).
%SQLCMD% -S localhost -d %DB% -E -C -I -b -i "%~dp0db\migrations\20261016030926_AssetsModule.sql"
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
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0deploy\assets\verify.ps1"
if errorlevel 1 (
  echo.
  echo The new API is not answering correctly. The frontend has NOT been changed.
  echo Check api\logs, or restore the backup as shown above.
  pause & exit /b 1
)

echo.
echo === 5. Install the frontend ================================================
node "%~dp0deploy\assets\apply.mjs" "%FRONT%"
if errorlevel 1 ( echo The frontend step failed - see above. Restore %BACKUP%\frontend if a page looks wrong. & pause & exit /b 1 )

echo.
echo DEPLOY OK. Admin ^> Assets now opens the new Items, Incoming Goods, Outgoing Goods
echo and Current Stock pages. Next: Items ^> Import Excel with the team's asset sheet.
pause
endlocal
