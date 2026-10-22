@echo off
REM Customisation requests from Book Workspace (23 Oct 2026).
REM   1. Ticket category "Workspace Customisation" + issue types Desk / Cabin /
REM      Meeting Room / Virtual Office (db\migrations\20261023000000_WorkspaceCustomisationTickets.sql).
REM   2. Patches frontend\assets\BookWorkspacePage-MB2.js in place (Customisation card,
REM      form, request), bumps its sw.js revision, and patches the staged
REM      deploy\workspace-types\frontend\assets\BookWorkspacePage-MB3.js too.
REM The request lands in Admin > Support tickets and the admin notification centre.
REM Re-running is safe. Undo: the _Rollback.sql, then git checkout the two chunks and sw.js.
set SQLCMD="C:\Program Files\Microsoft SQL Server\Client SDK\ODBC\180\Tools\Binn\sqlcmd.exe"
if not exist %SQLCMD% ( echo ERROR: sqlcmd not found at %SQLCMD% & pause & exit /b 1 )
%SQLCMD% -S localhost -d RegalisWorkSpace_UAT -E -C -I -b -W -i "%~dp0db\migrations\20261023000000_WorkspaceCustomisationTickets.sql"
if errorlevel 1 ( echo FAILED: migration & pause & exit /b 1 )
node "%~dp0deploy\customisation-requests\apply.mjs" "%~dp0frontend" "%~dp0deploy\workspace-types\frontend\assets"
if errorlevel 1 ( echo FAILED: frontend patch & pause & exit /b 1 )
echo Done.
pause
