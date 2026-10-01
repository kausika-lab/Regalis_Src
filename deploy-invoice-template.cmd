@echo off
REM ===========================================================================
REM  New tax invoice PDF layout (02 Oct 2026). API + data script, no frontend change.
REM
REM  Invoice PDF follows the Amstag reference: logo, status stamp, company block,
REM  Bill To with customer GSTIN, HSN/SAC + CGST/SGST (or IGST) per line, Payment
REM  Made / Balance Due, amount in words, bank details, terms, signatures.
REM  New invoices store the customer GSTIN; existing ones get it from the profile
REM  (deploy\invoice-template\rollback.sql undoes that).
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
set STAGE=%TEMP%\regalis-api-invoice-template
set TARGET=%~dp0api
set BACKUP=C:\inetpub\wwwroot\Regalis_backup_20261002_invoice_template
set POOL=regalis-api
set APPCMD=%SystemRoot%\System32\inetsrv\appcmd.exe

net session >nul 2>&1
if errorlevel 1 ( echo ERROR: not elevated. & pause & exit /b 1 )

echo === 1. Back up, publish, data ===============================================
robocopy "%TARGET%" "%BACKUP%\api" /E /XD logs /NFL /NDL /NJH /NJS /NP
if errorlevel 8 ( echo Backup failed. Nothing has been changed. & pause & exit /b 1 )
if exist "%STAGE%" rmdir /s /q "%STAGE%"
dotnet publish "%SRC%\src\Regalis.API\Regalis.API.csproj" -c Release -o "%STAGE%" --nologo
if errorlevel 1 ( echo Build failed. Nothing has been changed. & pause & exit /b 1 )
if exist "%STAGE%\web.config" del /q "%STAGE%\web.config"
"C:\Program Files\Microsoft SQL Server\Client SDK\ODBC\180\Tools\Binn\sqlcmd.exe" -S localhost -d RegalisWorkSpace_UAT -E -C -I -b -W -i "%~dp0deploy\invoice-template\data.sql"
if errorlevel 1 ( echo Data script failed and was rolled back. The API is unchanged. & pause & exit /b 1 )

echo === 2. Swap the binaries ====================================================
"%APPCMD%" stop apppool /apppool.name:"%POOL%"
ping -n 4 127.0.0.1 >nul
robocopy "%STAGE%" "%TARGET%" /E /XF web.config /NFL /NDL /NJH /NJS /NP
if errorlevel 8 (
  echo COPY FAILED. The site is DOWN. Restore: robocopy "%BACKUP%\api" "%TARGET%" /E, then start the pool.
  pause & exit /b 1
)
"%APPCMD%" start apppool /apppool.name:"%POOL%"
ping -n 6 127.0.0.1 >nul

echo === 3. Verify ===============================================================
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0deploy\session-restart\deploy-steps.ps1" -Step verify -Api "%TARGET%"
if errorlevel 1 ( echo The API did not start cleanly. Check api\logs or restore %BACKUP%. & pause & exit /b 1 )
echo DEPLOY OK.
pause
endlocal
