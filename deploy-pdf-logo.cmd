@echo off
REM ===========================================================================
REM  Logo on every PDF (01 Oct 2026). API only - no database or frontend change.
REM
REM  Invoice, receipt, quotation and wallet statement PDFs print the company logo
REM  (Company.LogoPath) at the left of the letterhead, like the purchase order.
REM  Quotations already sent keep the copy frozen at sending (no logo); new ones
REM  and drafts get it.
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
set STAGE=%TEMP%\regalis-api-pdf-logo
set TARGET=%~dp0api
set BACKUP=C:\inetpub\wwwroot\Regalis_backup_20261001_pdf_logo
set POOL=regalis-api
set APPCMD=%SystemRoot%\System32\inetsrv\appcmd.exe

net session >nul 2>&1
if errorlevel 1 ( echo ERROR: not elevated. & pause & exit /b 1 )

echo === 1. Back up and publish ==================================================
robocopy "%TARGET%" "%BACKUP%\api" /E /XD logs /NFL /NDL /NJH /NJS /NP
if errorlevel 8 ( echo Backup failed. Nothing has been changed. & pause & exit /b 1 )
if exist "%STAGE%" rmdir /s /q "%STAGE%"
dotnet publish "%SRC%\src\Regalis.API\Regalis.API.csproj" -c Release -o "%STAGE%" --nologo
if errorlevel 1 ( echo Build failed. Nothing has been changed. & pause & exit /b 1 )
if exist "%STAGE%\web.config" del /q "%STAGE%\web.config"

echo === 2. Swap the binaries ====================================================
"%APPCMD%" stop apppool /apppool.name:"%POOL%"
timeout /t 3 /nobreak >nul
robocopy "%STAGE%" "%TARGET%" /E /XF web.config /NFL /NDL /NJH /NJS /NP
if errorlevel 8 (
  echo COPY FAILED. The site is DOWN. Restore: robocopy "%BACKUP%\api" "%TARGET%" /E, then start the pool.
  pause & exit /b 1
)
"%APPCMD%" start apppool /apppool.name:"%POOL%"
timeout /t 5 /nobreak >nul

echo === 3. Verify ===============================================================
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0deploy\session-restart\deploy-steps.ps1" -Step verify -Api "%TARGET%"
if errorlevel 1 ( echo The API did not start cleanly. Check api\logs or restore %BACKUP%. & pause & exit /b 1 )
echo DEPLOY OK.
pause
endlocal
