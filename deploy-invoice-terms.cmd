@echo off
REM ===========================================================================
REM  Invoice PDF, part 2 (02 Oct 2026). API + migration + frontend.
REM
REM    - Bill To prints the customer's company name above the contact name
REM      (new column TRN_Invoice.CustomerCompanyName; old invoices filled from
REM      the customer profile, or the quotation for quotation invoices)
REM    - faint diagonal "Regalis WorkSpace" watermark on every page
REM    - Terms & Conditions moved to the very end, after the signatures
REM    - one Terms & Conditions text for every invoice, edited by staff from
REM      Admin > Invoices > "Terms & Conditions" (GET/PUT /api/invoices/terms)
REM
REM  Restarting the API signs everyone out once (Auth:EndSessionsOnRestart).
REM  To undo: restore %BACKUP%\api and %BACKUP%\frontend, then run
REM  db\migrations\20261001185237_InvoiceCustomerCompanyName_Rollback.sql.
REM
REM  Run from an ELEVATED command prompt.
REM ===========================================================================
setlocal
cd /d "%~dp0"
set SRC=C:\Users\Administrator\Downloads\Regalis\backend
set STAGE=%TEMP%\regalis-api-invoice-terms
set TARGET=%~dp0api
set FRONT=%~dp0frontend
set BACKUP=C:\inetpub\wwwroot\Regalis_backup_20261002_invoice_terms
set POOL=regalis-api
set APPCMD=%SystemRoot%\System32\inetsrv\appcmd.exe
set SQLCMD="C:\Program Files\Microsoft SQL Server\Client SDK\ODBC\180\Tools\Binn\sqlcmd.exe"

net session >nul 2>&1
if errorlevel 1 ( echo ERROR: not elevated. & pause & exit /b 1 )

echo === 1. Back up and publish ==================================================
robocopy "%TARGET%" "%BACKUP%\api" /E /XD logs /NFL /NDL /NJH /NJS /NP
if errorlevel 8 ( echo Backup failed. Nothing has been changed. & pause & exit /b 1 )
robocopy "%FRONT%" "%BACKUP%\frontend" sw.js /NFL /NDL /NJH /NJS /NP
robocopy "%FRONT%\assets" "%BACKUP%\frontend\assets" index-DI5X4c9f.js /NFL /NDL /NJH /NJS /NP
if errorlevel 8 ( echo Backup failed. Nothing has been changed. & pause & exit /b 1 )
if exist "%STAGE%" rmdir /s /q "%STAGE%"
dotnet publish "%SRC%\src\Regalis.API\Regalis.API.csproj" -c Release -o "%STAGE%" --nologo
if errorlevel 1 ( echo Build failed. Nothing has been changed. & pause & exit /b 1 )
if exist "%STAGE%\web.config" del /q "%STAGE%\web.config"

echo === 2. Database ==============================================================
%SQLCMD% -S localhost -d RegalisWorkSpace_UAT -E -C -I -b -i "%~dp0db\migrations\20261001185237_InvoiceCustomerCompanyName.sql"
if errorlevel 1 ( echo Migration failed. The API is unchanged. & pause & exit /b 1 )
%SQLCMD% -S localhost -d RegalisWorkSpace_UAT -E -C -I -b -W -i "%~dp0deploy\invoice-template\company.sql"
if errorlevel 1 ( echo Company-name fill failed. The column exists; the API is unchanged. & pause & exit /b 1 )

echo === 3. Swap the binaries ====================================================
"%APPCMD%" stop apppool /apppool.name:"%POOL%"
ping -n 4 127.0.0.1 >nul
robocopy "%STAGE%" "%TARGET%" /E /XF web.config /NFL /NDL /NJH /NJS /NP
if errorlevel 8 (
  echo COPY FAILED. The site is DOWN. Restore: robocopy "%BACKUP%\api" "%TARGET%" /E, then start the pool.
  pause & exit /b 1
)
"%APPCMD%" start apppool /apppool.name:"%POOL%"
ping -n 6 127.0.0.1 >nul

echo === 4. Frontend =============================================================
node "%~dp0deploy\invoice-template\apply.mjs" "%FRONT%"
if errorlevel 1 ( echo The frontend step failed - see above. The API is deployed. & pause & exit /b 1 )

echo === 5. Verify ===============================================================
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0deploy\session-restart\deploy-steps.ps1" -Step verify -Api "%TARGET%"
if errorlevel 1 ( echo The API did not start cleanly. Check api\logs or restore %BACKUP%. & pause & exit /b 1 )
echo DEPLOY OK. Hard-refresh (Ctrl+F5) the admin Invoices page.
pause
endlocal
