@echo off
rem ===========================================================================
rem  Regalis local stack - starts the API (:81) and the website (:8080), each in
rem  its own window, and does nothing for a part that is already listening.
rem  The VS Code launch entries in .vscode/launch.json run this first.
rem
rem  It lives in the repo (it used to be D:\Regalis\local-dev\start-local.cmd on
rem  one developer's machine, which no other machine has) and takes every path
rem  from its own location, so a fresh clone works.
rem
rem  Machine-specific values go in local-dev\local.env.cmd, which git ignores.
rem  See local-dev\README.md.
rem ===========================================================================
setlocal EnableExtensions

for %%I in ("%~dp0..") do set "ROOT=%%~fI"

rem ---- defaults (override them in local.env.cmd) ----------------------------
set "REGALIS_SQL_INSTANCE=localhost\SQLEXPRESS"
set "REGALIS_DB=RegalisWorkSpace_Dev"
set "REGALIS_API_PORT=81"
set "REGALIS_WEB_PORT=8080"
set "REGALIS_API_HOST=127.0.0.1"
rem The API build to run. Default: the repo's api\ (the deploy snapshot). local.env.cmd
rem can point it at a newer build, e.g. D:\Regalis\local-dev\api, which carries the
rem installed kits. (quotation-payment kit, 27 Nov 2026: the variable used to be set in
rem local.env.cmd and ignored here, so the old api\ ran without those kits.)
set "REGALIS_API_DIR=%ROOT%\api"
rem Startup seeding is OFF here, unlike appsettings.Development.json.
rem The build in api\ is older than the Dev database snapshots we restore, and its
rem seeder lists existing menus with "WHERE IsDeleted = 0" while UX_Menu_Code is a
rem unique index over every row, soft-deleted ones included. So it re-inserts a menu
rem that a later migration soft-deleted (ADM_FINANCE_DASHBOARD, ADM_FINANCE_BANK) and
rem dies with SQL error 2601 in DatabaseSeeder.SeedMenusAsync before Kestrel binds.
rem A restored snapshot is already seeded, so there is nothing to add. Set this to
rem true in local.env.cmd when you point the stack at an empty database.
set "REGALIS_SEED=false"
rem Local-only HS256 key. Never used anywhere but this machine; UAT and
rem production take theirs from the host secret store.
set "REGALIS_JWT_KEY=regalis-local-dev-signing-key-do-not-use-anywhere-else-0123456789"

if exist "%~dp0local.env.cmd" (
  echo [local stack] applying local-dev\local.env.cmd
  call "%~dp0local.env.cmd"
)

echo [local stack] repo    %ROOT%
echo [local stack] api     http://localhost:%REGALIS_API_PORT%  from %REGALIS_API_DIR%
echo [local stack] website http://localhost:%REGALIS_WEB_PORT%
echo [local stack] db      %REGALIS_SQL_INSTANCE% / %REGALIS_DB%

rem ---- prerequisites -------------------------------------------------------
if not exist "%REGALIS_API_DIR%\Regalis.API.exe" (
  echo [local stack] ERROR: %REGALIS_API_DIR%\Regalis.API.exe is missing. Check REGALIS_API_DIR in local.env.cmd.
  exit /b 1
)
if not exist "%ROOT%\frontend\index.html" (
  echo [local stack] ERROR: %ROOT%\frontend\index.html is missing.
  exit /b 1
)
where dotnet >nul 2>&1 || (
  echo [local stack] ERROR: the .NET runtime is not on PATH. Install the ASP.NET Core 8 runtime.
  exit /b 1
)
where node >nul 2>&1 || (
  echo [local stack] ERROR: node is not on PATH. The website is served by local-dev\web-server.mjs.
  exit /b 1
)

if not exist "%REGALIS_API_DIR%\App_Data\uploads" mkdir "%REGALIS_API_DIR%\App_Data\uploads" >nul 2>&1
if not exist "%REGALIS_API_DIR%\logs" mkdir "%REGALIS_API_DIR%\logs" >nul 2>&1

rem Both windows run their process through "powershell -NoExit" rather than "cmd /k".
rem Two reasons, both found the hard way on this machine:
rem   * "start ... cmd /k Regalis.API.exe" leaves the API dead - the window opens and
rem     the host never binds. The same form works for node, so it is specific to the
rem     apphost. Through powershell it starts in about 14 seconds every time.
rem   * -NoExit keeps the window and its output after a crash, which is the only place
rem     a failure before Serilog starts can be read.

rem ---- the API -------------------------------------------------------------
call :listening %REGALIS_API_PORT%
if errorlevel 1 (
  echo [local stack] starting the API...
  set "ASPNETCORE_ENVIRONMENT=Development"
  set "ASPNETCORE_URLS=http://localhost:%REGALIS_API_PORT%"
  rem Overrides appsettings.Development.json, which points at another
  rem developer's SQL instance. Environment wins over the JSON files.
  set "ConnectionStrings__Default=Server=%REGALIS_SQL_INSTANCE%;Database=%REGALIS_DB%;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=True"
  set "Jwt__SigningKey=%REGALIS_JWT_KEY%"
  set "Database__SeedOnStartup=%REGALIS_SEED%"
  start "Regalis API :%REGALIS_API_PORT%" /D "%REGALIS_API_DIR%" powershell -NoProfile -NoExit -ExecutionPolicy Bypass -Command "& '.\Regalis.API.exe'"
) else (
  call :report %REGALIS_API_PORT%
)

rem ---- the website ---------------------------------------------------------
call :listening %REGALIS_WEB_PORT%
if errorlevel 1 (
  echo [local stack] starting the website...
  start "Regalis website :%REGALIS_WEB_PORT%" /D "%ROOT%" powershell -NoProfile -NoExit -ExecutionPolicy Bypass -Command "& node local-dev\web-server.mjs frontend"
) else (
  call :report %REGALIS_WEB_PORT%
)

rem ---- wait for the API, so the browser does not open onto a dead page -----
echo [local stack] waiting for the API to accept connections...
set /a WAITED=0
:waitapi
call :listening %REGALIS_API_PORT%
if not errorlevel 1 goto apiready
set /a WAITED+=1
if %WAITED% GEQ 60 goto apilate
ping -n 2 127.0.0.1 >nul 2>&1
goto waitapi

:apilate
echo [local stack] ERROR: the API never opened port %REGALIS_API_PORT% within 60s.
echo                Read its window, and %REGALIS_API_DIR%\logs\regalis-api-^<date^>.log.
echo                Usual causes: the wrong SQL instance in local.env.cmd, a database
echo                that is not restored yet, or a seeding clash (see REGALIS_SEED above).
exit /b 1

:apiready
powershell -NoProfile -ExecutionPolicy Bypass -Command "try { $r = Invoke-WebRequest -Uri 'http://localhost:%REGALIS_API_PORT%/health' -UseBasicParsing -TimeoutSec 10; Write-Host ('[local stack] /health ' + $r.StatusCode + ' ' + $r.Content) } catch { Write-Host ('[local stack] /health did not answer cleanly: ' + $_.Exception.Message) }"

echo [local stack] ready - http://localhost:%REGALIS_WEB_PORT%
exit /b 0

rem ===========================================================================
:listening
rem errorlevel 0 = something is listening on %1, 1 = free.
rem netstat rather than Get-NetTCPConnection: no PowerShell start-up cost per call,
rem and the ":<port> " match keeps 81 from matching 8100.
netstat -ano | findstr /C:":%1 " | findstr /I /C:"LISTENING" >nul 2>&1
exit /b %errorlevel%

:report
rem Says who already holds port %1, so a stale process or a port clash is obvious.
set "OWNER_PID="
set "OWNER_NAME="
for /f "tokens=5" %%P in ('netstat -ano ^| findstr /C:":%1 " ^| findstr /I /C:"LISTENING"') do if not defined OWNER_PID set "OWNER_PID=%%P"
if defined OWNER_PID for /f "tokens=1 delims=," %%N in ('tasklist /fi "PID eq %OWNER_PID%" /fo csv /nh 2^>nul') do if not defined OWNER_NAME set "OWNER_NAME=%%~N"
if not defined OWNER_PID set "OWNER_PID=?"
if not defined OWNER_NAME set "OWNER_NAME=unknown"
echo [local stack] port %1 is already serving (%OWNER_NAME%, pid %OWNER_PID%) - left alone.
exit /b 0
