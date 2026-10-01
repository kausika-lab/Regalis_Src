@echo off
REM ===========================================================================
REM  Sign-in behaviour (30 Sep 2026).
REM
REM  Deploys to UAT:
REM    - a server restart (or app-pool recycle) ends every session: the browser
REM      goes to the sign-in page instead of straight into a panel. Access tokens
REM      carry the id of the process that issued them, and open refresh tokens
REM      are revoked at startup. Setting Auth:EndSessionsOnRestart=false in
REM      api\web.config switches this off.
REM    - the SPA confirms a stored session with the server before it opens a panel,
REM      so a dead session never shows the panel; it goes straight to sign-in
REM    - a successful sign-in always opens the dashboard (/admin or /), not the
REM      page that was open when the session last ended
REM
REM  Running this script restarts the API, so it signs everyone out once.
REM  No database change. It publishes the API from source, so it also ships
REM  anything else in that source that is not live yet.
REM
REM  Run from an ELEVATED command prompt.
REM
REM  To undo: robocopy %BACKUP%\api over api\ and %BACKUP%\frontend over frontend\,
REM  then restart the pool.
REM ===========================================================================
setlocal
cd /d "%~dp0"

set SRC=C:\Users\Administrator\Downloads\Regalis\backend
set STAGE=%TEMP%\regalis-api-session-restart
set TARGET=%~dp0api
set FRONT=%~dp0frontend
set BACKUP=C:\inetpub\wwwroot\Regalis_backup_20260930_session_restart
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
robocopy "%TARGET%" "%BACKUP%\api" /E /XD logs /NFL /NDL /NJH /NJS /NP
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
echo === 2. Swap the binaries ====================================================
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
echo === 3. Verify the API ======================================================
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0deploy\session-restart\deploy-steps.ps1" -Step verify -Api "%TARGET%"
if errorlevel 1 (
  echo.
  echo The new API did not report ending the earlier sessions. The frontend has NOT
  echo been changed. Check api\logs, or restore the backup as shown above.
  pause & exit /b 1
)

echo.
echo === 4. Install the frontend ================================================
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0deploy\session-restart\deploy-steps.ps1" -Step frontend -Front "%FRONT%"
if errorlevel 1 ( echo The frontend step failed - see the message above. The API is updated; restore %BACKUP%\frontend if sign-in looks wrong. & pause & exit /b 1 )
echo.
echo DEPLOY OK. Everyone has been signed out once. Open http://localhost:8080 and sign in.
pause
endlocal
