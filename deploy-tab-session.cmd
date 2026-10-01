@echo off
REM Sign-in lasts only as long as the browser tab (02 Oct 2026). Frontend only.
REM Closing the tab ends the session; a refresh keeps it. Everyone signed in today
REM is asked to sign in again once. Re-running is safe.
REM Undo: copy C:\inetpub\wwwroot\Regalis_backup_20261002_tab_session\* into frontend\
setlocal
set FRONT=%~dp0frontend
set BACKUP=C:\inetpub\wwwroot\Regalis_backup_20261002_tab_session
robocopy "%FRONT%" "%BACKUP%" sw.js /NFL /NDL /NJH /NJS /NP
robocopy "%FRONT%\assets" "%BACKUP%\assets" index-DI5X4c9f.js /NFL /NDL /NJH /NJS /NP
if errorlevel 8 ( echo Backup failed. Nothing has been changed. & pause & exit /b 1 )
node "%~dp0deploy\tab-session\apply.mjs" "%FRONT%"
if errorlevel 1 ( echo Failed - see above. Restore %BACKUP% if needed. & pause & exit /b 1 )
echo DEPLOY OK.
pause
endlocal
