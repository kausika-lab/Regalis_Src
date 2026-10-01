@echo off
REM ===========================================================================
REM  New Regalis logo (01 Oct 2026). No API restart - nobody is signed out.
REM
REM    - sidebar mark (gold "R" tile)      -> the crown
REM    - sign-in pages (crown + REGALIS)   -> full logo: crown, Regalis
REM                                           WORKSPACE, Premium Office Solutions
REM    - browser tab icon and app icons    -> the crown
REM    - purchase order PDF header         -> full logo (Company.LogoPath)
REM
REM  To undo: copy %BACKUP%\* back into frontend\ and set Company.LogoPath back
REM  to an empty string.
REM ===========================================================================
setlocal
cd /d "%~dp0"
set FRONT=%~dp0frontend
set BACKUP=C:\inetpub\wwwroot\Regalis_backup_20261001_new_logo
set SQLCMD="C:\Program Files\Microsoft SQL Server\Client SDK\ODBC\180\Tools\Binn\sqlcmd.exe"

echo === 1. Back up ===============================================================
robocopy "%FRONT%" "%BACKUP%" sw.js index.html manifest.webmanifest /NFL /NDL /NJH /NJS /NP
robocopy "%FRONT%\assets" "%BACKUP%\assets" index-DI5X4c9f.js /NFL /NDL /NJH /NJS /NP
if errorlevel 8 ( echo Backup failed. Nothing has been changed. & pause & exit /b 1 )

echo === 2. Frontend ==============================================================
node "%~dp0deploy\new-logo\apply.mjs" "%FRONT%"
if errorlevel 1 ( echo The frontend step failed - see above. Restore %BACKUP% if needed. & pause & exit /b 1 )

echo === 3. Purchase order PDF logo ===============================================
if not exist "%~dp0api\brand" mkdir "%~dp0api\brand"
copy /y "%~dp0deploy\new-logo\files\brand\regalis-logo-pdf.png" "%~dp0api\brand\regalis-logo.png" >nul
%SQLCMD% -S localhost -d RegalisWorkSpace_UAT -E -C -I -b -Q "UPDATE MST_AppSetting SET SettingValue = N'C:\inetpub\wwwroot\Regalis_Src\api\brand\regalis-logo.png' WHERE SettingKey = 'Company.LogoPath'"
if errorlevel 1 ( echo Could not set Company.LogoPath. The website logo is done; the PO PDF keeps no logo. & pause & exit /b 1 )

echo.
echo DEPLOY OK. Hard-refresh (Ctrl+F5) to see the new logo.
pause
endlocal
