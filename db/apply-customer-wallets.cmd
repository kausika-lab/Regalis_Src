@echo off
REM ---------------------------------------------------------------------------
REM  Seeds customer-wise wallet data for Admin -> Billing & Finance -> Wallet.
REM
REM  Adds seven active customers (Rahul Kumar, Priya Sharma, Arjun R,
REM  Sneha Patel, Vikram Rao, Ananya Singh, Rohit Kumar), a wallet for each and
REM  its own ledger. Kausika RS and her 22 existing transactions are untouched.
REM
REM  -I is not optional: MST_User carries filtered unique indexes, and SQL Server
REM  refuses to insert into such a table with QUOTED_IDENTIFIER OFF, which is
REM  what sqlcmd defaults to. The script also sets it itself.
REM
REM  Safe to run more than once; the migration is idempotent.
REM  To undo: 20260922000000_SeedCustomerWallets_Rollback.sql
REM ---------------------------------------------------------------------------
setlocal
set SQLCMD="C:\Program Files\Microsoft SQL Server\Client SDK\ODBC\180\Tools\Binn\sqlcmd.exe"
set DB=RegalisWorkSpace_UAT
cd /d "%~dp0"

if not exist %SQLCMD% (
  echo ERROR: sqlcmd not found at %SQLCMD%
  echo Open migrations\20260922000000_SeedCustomerWallets.sql in SSMS instead.
  pause
  exit /b 1
)

echo Seeding customer wallets into %DB% ...
echo.
%SQLCMD% -S localhost -d %DB% -E -C -I -W -i "migrations\20260922000000_SeedCustomerWallets.sql"
if errorlevel 1 (
  echo.
  echo The migration reported an error. It runs in a transaction, so nothing was kept.
  pause
  exit /b 1
)

echo.
echo Verifying every ledger adds up ...
%SQLCMD% -S localhost -d %DB% -E -C -I -W -Q "SET NOCOUNT ON; WITH Walk AS (SELECT t.CustomerId, t.BalanceAfter, Running = SUM(CASE WHEN t.Direction = 1 THEN t.Amount ELSE -t.Amount END) OVER (PARTITION BY t.CustomerId ORDER BY t.TransactionUtc, t.WalletTransactionId ROWS UNBOUNDED PRECEDING) FROM TRN_WalletTransaction t WHERE t.IsDeleted = 0) SELECT CASE WHEN EXISTS (SELECT 1 FROM Walk WHERE BalanceAfter <> Running) THEN 'BROKEN - a running balance does not match BalanceAfter' WHEN EXISTS (SELECT 1 FROM MST_Wallet w WHERE w.IsDeleted = 0 AND w.Balance <> w.TotalCredited - w.TotalDebited) THEN 'BROKEN - a wallet header does not match its totals' ELSE 'OK - every wallet reconciles against its ledger' END AS Result;"

echo.
echo Wallet balances now on file:
%SQLCMD% -S localhost -d %DB% -E -C -I -W -Q "SET NOCOUNT ON; SELECT u.FullName, u.Email, w.Balance, Txns = COUNT(t.WalletTransactionId) FROM MST_Wallet w JOIN MST_User u ON u.UserId = w.CustomerId LEFT JOIN TRN_WalletTransaction t ON t.CustomerId = w.CustomerId AND t.IsDeleted = 0 WHERE w.IsDeleted = 0 GROUP BY u.UserId, u.FullName, u.Email, w.Balance ORDER BY u.UserId;"

echo.
pause
endlocal
