@echo off
REM ===========================================================================
REM  Billing release. Also ships the rebuilt Customer Wallet screens
REM  (/admin/billing/wallet list + /wallet/:customerId, Add money, Adjust balance,
REM  statements; API /api/admin/wallets) and the Wallet.LowBalanceThreshold setting.
REM
REM  Deploys the Purchase Order module to UAT:
REM    - approval workflow (Draft > Pending approval > Approved > Sent > Received)
REM    - reference no., shipment, reverse charge, delivery address, discount,
REM      TDS, CGST/SGST/IGST, adjustment, per-PO terms, account per line
REM    - multiple attachments per PO, PO PDF, status history + audit rows
REM    - the full-page New / Edit Purchase Order screen and the new list/detail
REM
REM  It publishes the API from source, so it ALSO ships everything else in that
REM  source that is not live yet (the admin notification alerts). It therefore
REM  replaces deploy-admin-notifications.cmd; you do not need to run both.
REM
REM  Run from an ELEVATED command prompt.
REM
REM  ORDER IS NOT OPTIONAL. The new binaries read the new TRN_PurchaseOrder
REM  columns, and EF names every column, so the migration runs first while the
REM  old API is still serving (the new columns are all nullable or defaulted, so
REM  the old API keeps working against the migrated schema).
REM
REM  Steps:
REM    1. back up api\ and the four frontend files; publish the API to staging
REM    2. apply 20260925103626_AddPurchaseOrderWorkflow.sql          (idempotent)
REM       and   20260925103627_PurchaseOrderSettingsAndBackfill.sql  (idempotent)
REM    3. stop the pool, copy the binaries EXCEPT web.config, start the pool
REM    4. verify the new endpoints answer
REM    5. install the staged frontend from deploy\purchase-orders\frontend and
REM       bump the service-worker revisions so browsers fetch it
REM
REM  To undo: robocopy %BACKUP%\api over api\ (and %BACKUP%\frontend over
REM  frontend\), restart the pool, then run
REM  db\migrations\20260925103626_AddPurchaseOrderWorkflow_Rollback.sql.
REM  The rollback drops the new columns and tables, so any approval history,
REM  attachments list and TDS/adjustment figures recorded since are lost.
REM ===========================================================================
setlocal
cd /d "%~dp0"

set SRC=C:\Users\Administrator\Downloads\Regalis\backend
set STAGE=%TEMP%\regalis-api-publish
set TARGET=%~dp0api
set FRONT=%~dp0frontend
set STAGED_FRONT=%~dp0deploy\purchase-orders\frontend
set BACKUP=C:\inetpub\wwwroot\Regalis_backup_20260925_purchase_orders
set POOL=regalis-api
set DB=RegalisWorkSpace_UAT
set SQLCMD="C:\Program Files\Microsoft SQL Server\Client SDK\ODBC\180\Tools\Binn\sqlcmd.exe"
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
if not exist %SQLCMD% (
  echo ERROR: sqlcmd not found at %SQLCMD%
  pause & exit /b 1
)
if not exist "%STAGED_FRONT%\assets\AdminPurchaseOrderPages-C1FTHC0p.js" (
  echo ERROR: staged frontend not found at %STAGED_FRONT%
  pause & exit /b 1
)
echo OK.

echo.
echo === 1. Back up and publish ==================================================
robocopy "%TARGET%" "%BACKUP%\api" /E /NFL /NDL /NJH /NJS /NP
if errorlevel 8 ( echo Backup of api\ failed. Nothing has been changed. & pause & exit /b 1 )
robocopy "%FRONT%" "%BACKUP%\frontend" sw.js /NFL /NDL /NJH /NJS /NP
robocopy "%FRONT%\assets" "%BACKUP%\frontend\assets" index-DI5X4c9f.js AdminPurchaseOrderPages-C1FTHC0p.js procurement-CDmpFQ22.js /NFL /NDL /NJH /NJS /NP
echo Backed up to %BACKUP%.
if exist "%STAGE%" rmdir /s /q "%STAGE%"
dotnet publish "%SRC%\src\Regalis.API\Regalis.API.csproj" -c Release -o "%STAGE%" --nologo
if errorlevel 1 ( echo. & echo Build failed. Nothing has been changed. & pause & exit /b 1 )
REM Must not reach the server: it has no UAT environment variables.
if exist "%STAGE%\web.config" del /q "%STAGE%\web.config"
echo Published to %STAGE%.

echo.
echo === 2. Database migration ===================================================
%SQLCMD% -S localhost -d %DB% -E -C -I -b -i "%~dp0db\migrations\20260925103626_AddPurchaseOrderWorkflow.sql"
if errorlevel 1 (
  echo.
  echo The schema migration failed. It runs in a transaction, so nothing was kept,
  echo and the API has NOT been touched - the site is still up on the old build.
  pause & exit /b 1
)
%SQLCMD% -S localhost -d %DB% -E -C -I -b -W -i "%~dp0db\migrations\20260925103627_PurchaseOrderSettingsAndBackfill.sql"
if errorlevel 1 (
  echo.
  echo The settings/backfill script failed ^(it rolls back on error^). The schema is
  echo migrated and the old API still works against it; fix the error and re-run.
  pause & exit /b 1
)
REM Wallet: the low-balance threshold setting (Settings - Billing, default Rs 100).
%SQLCMD% -S localhost -d %DB% -E -C -I -b -W -i "%~dp0db\migrations\20260928_WalletLowBalanceSetting.sql"
if errorlevel 1 ( echo The wallet setting script failed - the wallets list falls back to Rs 100 until it is re-run. )

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
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0deploy\purchase-orders\deploy-steps.ps1" -Step verify
if errorlevel 1 (
  echo.
  echo The new API is not answering correctly. The frontend has NOT been changed.
  echo Check api\logs, or restore the backup as shown above.
  pause & exit /b 1
)

echo.
echo === 5. Install the frontend ================================================
robocopy "%STAGED_FRONT%\assets" "%FRONT%\assets" index-DI5X4c9f.js AdminPurchaseOrderPages-C1FTHC0p.js procurement-CDmpFQ22.js AdminWalletPages-RGw1.js /NFL /NDL /NJH /NJS /NP
if errorlevel 8 ( echo Frontend copy failed - the API is updated but the old screens are still live. & pause & exit /b 1 )
REM Bump only these three precache revisions, leaving the rest of the live sw.js alone.
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0deploy\purchase-orders\deploy-steps.ps1" -Step bumpsw -Front "%FRONT%"
if errorlevel 1 ( echo The service-worker revisions were not bumped - browsers may keep the old screens. Check frontend\sw.js. )
echo.
echo DEPLOY OK. Open http://localhost:8080/admin/billing/purchase-orders and
echo sign out and back in (or hard-refresh) so the browser picks up the new screens.
echo Settings - Billing now holds the purchase order options (approval on/off,
echo default terms, TDS sections, GST rates, shipment methods, account heads).
pause
endlocal
