@echo off
REM ===========================================================================
REM  Billing & Finance build (Regalis_WorkSpace_Billing_Finance_Build_Document, Oct 2026).
REM
REM  Deploys to UAT:
REM    - Invoices: Paid and Outstanding columns, + Record payment (cash, UPI, bank transfer,
REM      cheque, card, wallet), an invoice page with its payments and reminders.
REM    - Payments: every payment whatever its mode, receipts, reverse with a reason.
REM    - New pages: Purchase Bills (+ bill detail), Vendor Payments, Petty Cash, GST, TDS,
REM      Compliance Calendar, Period lock, Ledgers / outstanding / ageing.
REM    - Changed pages: Expenses (department, receipts, approval, recurring, reimbursement),
REM      Transactions (statement import, matching with explained differences), Suppliers
REM      (PAN, GST state, TDS section, terms, Ledger). Reconciliation is back in the sidebar.
REM    - Sidebar bands MONEY IN / CASH DESK / MONEY OUT / BANK / COMPLIANCE.
REM    - Roles Accountant and Auditor; 21 new permissions (deploy\billing-finance\README.md).
REM      Sign out and in again to pick them up.
REM
REM  Reminders ship OFF (Reminders.Enabled = false) until email from production is proven.
REM
REM  BEFORE RUNNING: apply deploy\billing-finance\backend to the source in %SRC%
REM  (deploy\billing-finance\README.md: new files to copy, two lines in DependencyInjection.cs).
REM  This script refuses to run until PurchaseBillService.cs is there.
REM  Database: 20261101165945_BillingFinance (29 new tables, one transaction) and
REM  20261101170000_BillingFinanceAccess (permissions, roles, sidebar, settings, master data,
REM  sequences). Both are idempotent.
REM  Rollback: db\migrations\20261101170000_BillingFinanceAccess_Rollback.sql, then
REM  20261101165945_BillingFinance_Rollback.sql (drops the new tables and their data), then
REM  the backups below.
REM
REM  Restarting the API signs everyone out once (Auth:EndSessionsOnRestart).
REM  Run from an ELEVATED command prompt.
REM ===========================================================================
setlocal
cd /d "%~dp0"

set SRC=C:\Users\Administrator\Downloads\Regalis-live\backend
set STAGE=%TEMP%\regalis-api-billing
set TARGET=%~dp0api
set FRONT=%~dp0frontend
set BACKUP=C:\inetpub\wwwroot\Regalis_backup_20261101_billing
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
if not exist "%SRC%\src\Regalis.Infrastructure\Services\Billing\PurchaseBillService.cs" (
  echo ERROR: the Billing ^& Finance source changes are not in %SRC%.
  echo Apply deploy\billing-finance\backend first - see deploy\billing-finance\README.md.
  pause & exit /b 1
)
findstr /c:"AddBillingFinance" "%SRC%\src\Regalis.Infrastructure\DependencyInjection.cs" >nul
if errorlevel 1 ( echo ERROR: DependencyInjection.cs does not call services.AddBillingFinance^(^) - see the README. & pause & exit /b 1 )
findstr /c:"BillingFinanceRulesInterceptor" "%SRC%\src\Regalis.Infrastructure\DependencyInjection.cs" >nul
if errorlevel 1 ( echo ERROR: DependencyInjection.cs does not add BillingFinanceRulesInterceptor - see the README. & pause & exit /b 1 )
if not exist %SQLCMD% ( echo ERROR: sqlcmd not found at %SQLCMD% & pause & exit /b 1 )
if not exist "%~dp0deploy\billing-finance\frontend\assets\BillingShared-BF1.js" ( echo ERROR: staged frontend missing & pause & exit /b 1 )
where node >nul 2>&1
if errorlevel 1 ( echo ERROR: node is not on the PATH. & pause & exit /b 1 )
echo OK.

echo.
echo === 1. Back up and publish ==================================================
robocopy "%TARGET%" "%BACKUP%\api" /E /XD logs /NFL /NDL /NJH /NJS /NP
if errorlevel 8 ( echo Backup of api\ failed. Nothing has been changed. & pause & exit /b 1 )
robocopy "%FRONT%" "%BACKUP%\frontend" sw.js web.config /NFL /NDL /NJH /NJS /NP
robocopy "%FRONT%\assets" "%BACKUP%\frontend\assets" index-DI5X4c9f.js AdminPurchaseOrderPages-C1FTHC0p.js AdminCustomersPages-IA5T7i_C.js AdminBankPages-BZwouxUO.js /NFL /NDL /NJH /NJS /NP
%SQLCMD% -S localhost -E -C -b -Q "BACKUP DATABASE [%DB%] TO DISK = N'%DB%_20261101_billing.bak' WITH COPY_ONLY, INIT"
if errorlevel 1 ( echo Database backup failed. Nothing has been changed. & pause & exit /b 1 )
echo Backed up to %BACKUP% and SQL Server's Backup folder.
if exist "%STAGE%" rmdir /s /q "%STAGE%"
dotnet publish "%SRC%\src\Regalis.API\Regalis.API.csproj" -c Release -o "%STAGE%" --nologo
if errorlevel 1 ( echo. & echo Build failed. Nothing has been changed. & pause & exit /b 1 )
if exist "%STAGE%\web.config" del /q "%STAGE%\web.config"
echo Published to %STAGE%.

echo.
echo === 2. Database migration ===================================================
REM Before the binaries: the new tables are new, so the old code is unaffected by them.
%SQLCMD% -S localhost -d %DB% -E -C -I -b -i "%~dp0db\migrations\20261101165945_BillingFinance.sql"
if errorlevel 1 ( echo. & echo The migration failed; it runs in a transaction, so nothing was kept. Nothing else has been changed. & pause & exit /b 1 )
%SQLCMD% -S localhost -d %DB% -E -C -I -b -W -i "%~dp0db\migrations\20261101170000_BillingFinanceAccess.sql"
if errorlevel 1 ( echo. & echo The access script failed; it runs in a transaction. The new tables exist; nothing else has been changed. & pause & exit /b 1 )
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
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0deploy\billing-finance\verify.ps1"
if errorlevel 1 (
  echo.
  echo The new API is not answering correctly. The frontend has NOT been changed.
  echo Check api\logs, or restore the backup as shown above.
  pause & exit /b 1
)

echo.
echo === 5. Install the frontend ================================================
node "%~dp0deploy\billing-finance\frontend\apply.mjs" "%FRONT%"
if errorlevel 1 ( echo The frontend step failed - see above. Restore %BACKUP%\frontend if a page looks wrong. & pause & exit /b 1 )

echo.
echo DEPLOY OK. Sign out and in again, then open Billing ^& Finance.
echo Still to do by hand: Settings ^> Company.Gstin, Company.Address and Company.Phone
echo (every TAX INVOICE needs them), the approval limits, and Reminders.Enabled once
echo email from this server is proven.
pause
endlocal
