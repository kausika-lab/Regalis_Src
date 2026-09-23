@echo off
REM ===========================================================================
REM  Deploys Google sign-in (POST /api/auth/google) to the UAT box.
REM
REM  Run this from an ELEVATED command prompt - it stops and starts an IIS
REM  application pool and writes into C:\inetpub\wwwroot.
REM
REM  ORDER IS NOT OPTIONAL. The new binaries read MST_User.GoogleId, and EF Core
REM  names every column explicitly, so publishing before the migration makes
REM  EVERY query against MST_User fail - that is all login, not just Google.
REM  The migration therefore runs first, while the old API is still serving.
REM
REM  Steps:
REM    1. publish the API from source to a staging folder
REM    2. apply 20260922162733_AddGoogleSignIn.sql   (idempotent)
REM    3. stop the regalis-api app pool
REM    4. copy the staged files over api\  - EXCEPT web.config
REM    5. start the pool and verify the endpoint answers
REM
REM  web.config is never copied. The published one carries none of the UAT
REM  environment variables, so overwriting the live file would take the API
REM  down by losing the connection string and the JWT signing key.
REM
REM  To undo: restore C:\inetpub\wwwroot\Regalis_api_backup_20260922 over api\,
REM  then read the header of
REM  db\migrations\20260922162733_AddGoogleSignIn_Rollback.sql before running it
REM  - that rollback is not loss-free.
REM ===========================================================================
setlocal
cd /d "%~dp0"

set SRC=C:\Users\Administrator\Downloads\Regalis\backend
set STAGE=%TEMP%\regalis-api-publish
set TARGET=%~dp0api
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
where dotnet >nul 2>&1
if errorlevel 1 (
  echo ERROR: the dotnet SDK is not on PATH.
  pause & exit /b 1
)
echo OK.

echo.
echo === 1. Publish ==============================================================
if exist "%STAGE%" rmdir /s /q "%STAGE%"
dotnet publish "%SRC%\src\Regalis.API\Regalis.API.csproj" -c Release -o "%STAGE%" --nologo
if errorlevel 1 (
  echo.
  echo Build failed. Nothing has been changed.
  pause & exit /b 1
)
REM Must not reach the server: it has no UAT environment variables.
if exist "%STAGE%\web.config" del /q "%STAGE%\web.config"
echo Published to %STAGE%.

echo.
echo === 2. Database migration ===================================================
echo Applying 20260922162733_AddGoogleSignIn to %DB% ...
%SQLCMD% -S localhost -d %DB% -E -C -I -b -i "%~dp0db\migrations\20260922162733_AddGoogleSignIn.sql"
if errorlevel 1 (
  echo.
  echo The migration failed. It runs in a transaction, so nothing was kept, and
  echo the API has NOT been touched - the site is still up on the old build.
  pause & exit /b 1
)
%SQLCMD% -S localhost -d %DB% -E -C -I -W -Q "SET NOCOUNT ON; SELECT CASE WHEN COL_LENGTH('MST_User','GoogleId') IS NULL THEN 'FAILED - GoogleId still missing' ELSE 'OK - schema ready' END AS Result;"

echo.
echo === 3. Stop the app pool ====================================================
"%APPCMD%" stop apppool /apppool.name:"%POOL%"
REM Give w3wp a moment to release the DLLs it has loaded.
timeout /t 3 /nobreak >nul

echo.
echo === 4. Copy the binaries ====================================================
REM /XF web.config keeps the live one with its UAT environment variables.
robocopy "%STAGE%" "%TARGET%" /E /XF web.config /NFL /NDL /NJH /NJS /NP
if errorlevel 8 (
  echo.
  echo COPY FAILED. The pool is stopped and the site is DOWN.
  echo Restore with:
  echo   robocopy C:\inetpub\wwwroot\Regalis_api_backup_20260922 "%TARGET%" /E
  echo   "%APPCMD%" start apppool /apppool.name:"%POOL%"
  pause & exit /b 1
)
echo Copied.

echo.
echo === 5. Start and verify =====================================================
"%APPCMD%" start apppool /apppool.name:"%POOL%"
timeout /t 5 /nobreak >nul

powershell -NoProfile -Command ^
  "$ok = $true;" ^
  "try { $s = (Invoke-WebRequest 'http://localhost:81/swagger/v1/swagger.json' -UseBasicParsing -TimeoutSec 30).Content | ConvertFrom-Json;" ^
  "  if ($s.paths.PSObject.Properties.Name -contains '/api/auth/google') { Write-Host 'OK  - POST /api/auth/google is published' } else { Write-Host 'FAIL- endpoint still missing from Swagger'; $ok = $false } }" ^
  "catch { Write-Host ('FAIL- API did not answer: ' + $_.Exception.Message); $ok = $false };" ^
  "try { Invoke-RestMethod 'http://localhost:81/api/auth/google' -Method Post -ContentType 'application/json' -Body '{\"idToken\":\"junk\"}' -TimeoutSec 30 | Out-Null;" ^
  "  Write-Host 'FAIL- a junk token was accepted'; $ok = $false }" ^
  "catch { $c = $_.Exception.Response.StatusCode.value__;" ^
  "  if ($c -eq 401) { Write-Host 'OK  - a junk token is rejected with 401' } else { Write-Host ('FAIL- junk token returned ' + $c + ', expected 401'); $ok = $false } };" ^
  "try { Invoke-RestMethod 'http://localhost:81/api/auth/login' -Method Post -ContentType 'application/json' -Body '{\"email\":\"x@y.z\",\"password\":\"p\",\"portal\":\"customer\"}' -TimeoutSec 30 | Out-Null }" ^
  "catch { $c = $_.Exception.Response.StatusCode.value__;" ^
  "  if ($c -eq 401) { Write-Host 'OK  - password login still works (401 on bad credentials)' } else { Write-Host ('FAIL- login returned ' + $c + ' - check MST_User queries'); $ok = $false } };" ^
  "if ($ok) { Write-Host ''; Write-Host 'DEPLOY OK - try the Google button on http://localhost:8080/login' } else { Write-Host ''; Write-Host 'SOMETHING IS WRONG - see api\logs\stdout*.log' }"

echo.
pause
endlocal
