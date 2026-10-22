@echo off
REM ===========================================================================
REM  Workspace types as data (14 Oct 2026).
REM
REM  Deploys to UAT:
REM    - Admin > Workspaces: the Type dropdown ends with "+ Add new type", which
REM      opens a small form in the same spot (name, code prefix, how it is
REM      booked). The New / Edit workspace dialog keeps everything filled in.
REM    - A "Workspace types" tab: name, code prefix, how it is booked, workspaces
REM      using it, "Customers can book online", active/inactive. Rename,
REM      deactivate, delete (only unused, never a system type).
REM    - Workspace codes must be XXX-P-NN with the type's letter; the Code hint
REM      shows the next free real codes. The required price follows the type.
REM    - Hot Desk becomes its own type (hourly, not bookable online); the 7
REM      DEK-D-* workspaces and their bookings move onto it. The name rule
REM      "Hot Desk*" is gone. Customers see the same bookable workspaces, flows
REM      and prices; only the Desk card count (9 -> 4) and the 5 greyed hot
REM      desks on the Desk monthly list change.
REM
REM  Database: migration 20261014075619_WorkspaceTypeManagement, one
REM  transaction with safety checks (it stops and keeps nothing if a check
REM  fails). Rollback: db\migrations\20261014075619_WorkspaceTypeManagement_Rollback.sql
REM  plus the backup below. Before a rollback, untick "Open for customer
REM  booking" on workspaces of any type added after this deploy: the old
REM  booking page lists every type.
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
set STAGE=%TEMP%\regalis-api-workspace-types
set TARGET=%~dp0api
set FRONT=%~dp0frontend
set BACKUP=C:\inetpub\wwwroot\Regalis_backup_20261014_workspace_types
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
if not exist "%~dp0deploy\workspace-types\frontend\assets\WorkspacesPage-WT1.js" ( echo ERROR: staged frontend missing & pause & exit /b 1 )
echo OK.

echo.
echo === 1. Back up and publish ==================================================
robocopy "%TARGET%" "%BACKUP%\api" /E /XD logs /NFL /NDL /NJH /NJS /NP
if errorlevel 8 ( echo Backup of api\ failed. Nothing has been changed. & pause & exit /b 1 )
robocopy "%FRONT%" "%BACKUP%\frontend" sw.js /NFL /NDL /NJH /NJS /NP
robocopy "%FRONT%\assets" "%BACKUP%\frontend\assets" index-DI5X4c9f.js /NFL /NDL /NJH /NJS /NP
REM A bare file name lands in SQL Server's own Backup folder, which its service
REM account can write to (MSSQL17.MSSQLSERVER\MSSQL\Backup).
%SQLCMD% -S localhost -E -C -b -Q "BACKUP DATABASE [%DB%] TO DISK = N'%DB%_20261014_workspace_types.bak' WITH COPY_ONLY, INIT"
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
REM would break every workspace query. The old code ignores the new columns.
%SQLCMD% -S localhost -d %DB% -E -C -I -b -i "%~dp0db\migrations\20261014075619_WorkspaceTypeManagement.sql"
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
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0deploy\workspace-types\verify.ps1"
if errorlevel 1 (
  echo.
  echo The new API is not answering correctly. The frontend has NOT been changed.
  echo Check api\logs, or restore the backup as shown above.
  pause & exit /b 1
)

echo.
echo === 5. Install the frontend ================================================
node "%~dp0deploy\workspace-types\apply.mjs" "%FRONT%"
if errorlevel 1 ( echo The frontend step failed - see above. Restore %BACKUP%\frontend if a page looks wrong. & pause & exit /b 1 )

echo.
echo DEPLOY OK. Admin ^> Workspaces now has the Workspace types tab.
pause
endlocal
