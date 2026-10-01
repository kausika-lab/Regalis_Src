@echo off
REM ===========================================================================
REM  Wallet minimum recharge Rs 1,000 (01 Oct 2026). No API restart - nobody is
REM  signed out. The API already checks Wallet.MinRecharge; this raises it from
REM  100 to 1000 (picked up within a minute) and updates the customer Add funds
REM  dialog to match.
REM
REM  To undo: copy %BACKUP%\* back into frontend\ and set Wallet.MinRecharge to 100.
REM ===========================================================================
setlocal
cd /d "%~dp0"
set FRONT=%~dp0frontend
set BACKUP=C:\inetpub\wwwroot\Regalis_backup_20261001_wallet_min
set SQLCMD="C:\Program Files\Microsoft SQL Server\Client SDK\ODBC\180\Tools\Binn\sqlcmd.exe"

echo === 1. Back up ===============================================================
robocopy "%FRONT%" "%BACKUP%" sw.js /NFL /NDL /NJH /NJS /NP
robocopy "%FRONT%\assets" "%BACKUP%\assets" index-DI5X4c9f.js /NFL /NDL /NJH /NJS /NP
if errorlevel 8 ( echo Backup failed. Nothing has been changed. & pause & exit /b 1 )

echo === 2. Setting ===============================================================
%SQLCMD% -S localhost -d RegalisWorkSpace_UAT -E -C -I -b -Q "UPDATE MST_AppSetting SET SettingValue = N'1000' WHERE SettingKey = 'Wallet.MinRecharge'; SELECT SettingKey, SettingValue FROM MST_AppSetting WHERE SettingKey = 'Wallet.MinRecharge'"
if errorlevel 1 ( echo Could not set Wallet.MinRecharge. Nothing else changed. & pause & exit /b 1 )

echo === 3. Frontend ==============================================================
node "%~dp0deploy\wallet-min-recharge\apply.mjs" "%FRONT%"
if errorlevel 1 ( echo The frontend step failed - see above. The API limit is already 1000. & pause & exit /b 1 )

echo.
echo DEPLOY OK. Hard-refresh (Ctrl+F5) to see the new Add funds dialog.
pause
endlocal
