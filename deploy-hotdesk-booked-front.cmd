@echo off
REM Hot desk cards: "Booked" instead of "Monthly price not set" (01 Oct 2026). Frontend only.
REM Already applied on UAT; re-running is safe.
REM Undo: copy C:\inetpub\wwwroot\Regalis_backup_20261001_hotdesk_front\* into frontend\
node "%~dp0deploy\hotdesk-booked\apply.mjs" "%~dp0frontend"
pause
