@echo off
REM ===========================================================================
REM  Separate sign-in pages (30 Sep 2026). Frontend only - no API or DB change.
REM
REM    /login        customers  (email + password, Google, Create Account)
REM    /admin/login  admins and staff (email + password)
REM
REM  The Customer / Admin switch is removed. Signed-out visits to /admin/* and
REM  admin logout go to /admin/login; customer pages go to /login. Forgot /
REM  reset password and OTP pages return to the sign-in page the user came from.
REM
REM  Patches index, ForgotPassword, ResetPassword and VerifyOtp chunks in place
REM  and bumps their sw.js revisions (split-login-1). Idempotent.
REM
REM  To undo: copy %BACKUP%\sw.js and %BACKUP%\assets\* back into frontend\.
REM ===========================================================================
setlocal
cd /d "%~dp0"
set FRONT=%~dp0frontend
set BACKUP=C:\inetpub\wwwroot\Regalis_backup_20260930_split_login

echo === 1. Back up ===============================================================
robocopy "%FRONT%" "%BACKUP%" sw.js /NFL /NDL /NJH /NJS /NP
robocopy "%FRONT%\assets" "%BACKUP%\assets" index-DI5X4c9f.js ForgotPasswordPage-COYpk7Vw.js ResetPasswordPage-BB_P8AiB.js VerifyOtpPage-B1bv1-9D.js /NFL /NDL /NJH /NJS /NP
if errorlevel 8 ( echo Backup failed. Nothing has been changed. & pause & exit /b 1 )
echo Backed up to %BACKUP%.

echo === 2. Patch the frontend ====================================================
node "%~dp0deploy\split-login\apply.mjs" "%FRONT%"
if errorlevel 1 ( echo. & echo The patch failed - nothing was written for the step that failed. Restore %BACKUP% if needed. & pause & exit /b 1 )

echo.
echo DEPLOY OK. Customers sign in at http://localhost:8080/login, staff at http://localhost:8080/admin/login
pause
endlocal
