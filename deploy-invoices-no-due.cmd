@echo off
REM Customer Invoices list: Due column removed (01 Oct 2026). Frontend only.
REM Re-running is safe.
REM Undo: copy C:\inetpub\wwwroot\Regalis_backup_20261001_invoices_no_due\* into frontend\
node "%~dp0deploy\invoices-no-due\apply.mjs" "%~dp0frontend"
pause
