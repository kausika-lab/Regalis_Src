@echo off
REM ---------------------------------------------------------------------------
REM  Grants PASS.EDIT to the Customer role so a customer can cancel their own
REM  pass (POST /api/passes/{id}/cancel is guarded with PASS.EDIT).
REM
REM  Run this, then have the customer LOG OUT AND BACK IN -- permissions travel
REM  as perm claims inside the JWT, so an existing token keeps the old set.
REM
REM  Safe to run more than once; the migration is idempotent.
REM  To undo: 20260916000000_GrantPassEditToCustomer_Rollback.sql
REM ---------------------------------------------------------------------------
setlocal
set SQLCMD="C:\Program Files\Microsoft SQL Server\Client SDK\ODBC\180\Tools\Binn\sqlcmd.exe"
set DB=RegalisWorkSpace_UAT
cd /d "%~dp0"

if not exist %SQLCMD% (
  echo ERROR: sqlcmd not found at %SQLCMD%
  echo Open migrations\20260916000000_GrantPassEditToCustomer.sql in SSMS instead.
  pause
  exit /b 1
)

echo Applying the grant to %DB% ...
echo.
%SQLCMD% -S localhost -d %DB% -E -C -W -i "migrations\20260916000000_GrantPassEditToCustomer.sql"
if errorlevel 1 (
  echo.
  echo The migration reported an error. Nothing further was run.
  pause
  exit /b 1
)

echo.
echo Verifying ...
%SQLCMD% -S localhost -d %DB% -E -C -W -Q "SET NOCOUNT ON; SELECT CASE WHEN EXISTS (SELECT 1 FROM MAP_RolePermission rp JOIN MST_Permission p ON p.PermissionId = rp.PermissionId WHERE rp.RoleId = 1 AND p.PermissionCode = 'PASS.EDIT') THEN 'GRANTED - now log the customer out and back in' ELSE 'STILL NOT GRANTED - the insert did not apply' END AS Result;"

echo.
pause
endlocal
