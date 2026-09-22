/* 20260922010000_RetireUnusedTestCustomers.sql
   Takes two leftover test customers out of the customer pickers.

   dhina007@gmail.com and bavanthiravi@gmail.com were signed up during testing and
   never used: both sat at PendingVerification with an empty wallet, and between them
   they have no bookings, invoices, payments, passes or wallet transactions. They were
   showing up in Admin -> Billing & Finance -> Wallet next to the real customers with
   nothing behind them.

   This is a soft delete, so it is reversible and nothing is destroyed. Their wallets
   are retired with them. Note that the two accounts named here are NOT the staff
   accounts dhina07@gmail.com and bavanthi@gmail.com, which are left alone.

   Run 20260922000000_SeedCustomerWallets.sql first, so the real customers are in
   place before these two are taken out.

   Idempotent: re-running is a no-op. Rollback: 20260922010000_RetireUnusedTestCustomers_Rollback.sql */

/* MST_User carries filtered unique indexes (UX_User_Email, UX_User_Mobile), and SQL
   Server refuses to modify such a table unless QUOTED_IDENTIFIER and ANSI_NULLS are
   ON. sqlcmd leaves QUOTED_IDENTIFIER OFF unless it is given -I, so set it here and
   let the batch break, which is what makes it take effect for what follows. */
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO

SET NOCOUNT ON;
SET XACT_ABORT ON;

DECLARE @Ids TABLE (UserId bigint PRIMARY KEY);
INSERT INTO @Ids (UserId)
SELECT UserId
FROM MST_User
WHERE Email IN (N'dhina007@gmail.com', N'bavanthiravi@gmail.com')
  AND UserType = 1
  AND IsDeleted = 0;

IF NOT EXISTS (SELECT 1 FROM @Ids)
BEGIN
    PRINT 'RetireUnusedTestCustomers: already applied, nothing to do.';
    RETURN;
END

/* Refuse to touch an account that turns out to have history after all - better to
   stop and be looked at than to quietly hide a customer with real records. */
DECLARE @Used int = (
    SELECT COUNT(*) FROM (
        SELECT b.CustomerId FROM TRN_Booking            b JOIN @Ids i ON i.UserId = b.CustomerId
        UNION ALL SELECT v.CustomerId FROM TRN_Invoice  v JOIN @Ids i ON i.UserId = v.CustomerId
        UNION ALL SELECT p.CustomerId FROM TRN_Payment  p JOIN @Ids i ON i.UserId = p.CustomerId
        UNION ALL SELECT c.CustomerId FROM TRN_CustomerPass c JOIN @Ids i ON i.UserId = c.CustomerId
        UNION ALL SELECT t.CustomerId FROM TRN_WalletTransaction t JOIN @Ids i ON i.UserId = t.CustomerId
    ) x
);

IF @Used > 0
BEGIN
    RAISERROR ('RetireUnusedTestCustomers: these accounts now have %d linked record(s). Nothing was changed - review before retiring them.', 16, 1, @Used);
    RETURN;
END

DECLARE @Admin bigint = (SELECT TOP 1 UserId FROM MST_User WHERE Email = N'admin@regalis.in' AND IsDeleted = 0);
IF @Admin IS NULL SET @Admin = (SELECT TOP 1 UserId FROM MST_User WHERE UserType = 2 AND IsDeleted = 0 ORDER BY UserId);

DECLARE @Now datetime2 = SYSUTCDATETIME();

BEGIN TRAN;

UPDATE w
SET w.IsActive = 0, w.IsDeleted = 1, w.DeletedBy = @Admin, w.DeletedDate = @Now,
    w.ModifiedBy = @Admin, w.ModifiedDate = @Now
FROM MST_Wallet w
JOIN @Ids i ON i.UserId = w.CustomerId
WHERE w.IsDeleted = 0;

UPDATE u
SET u.IsActive = 0, u.IsDeleted = 1, u.DeletedBy = @Admin, u.DeletedDate = @Now,
    u.ModifiedBy = @Admin, u.ModifiedDate = @Now
FROM MST_User u
JOIN @Ids i ON i.UserId = u.UserId;

COMMIT;

SELECT Retired = u.FullName, u.Email, u.IsDeleted
FROM MST_User u JOIN @Ids i ON i.UserId = u.UserId;

SELECT StillListed = u.FullName, u.Email, Balance = ISNULL(w.Balance, 0)
FROM MST_User u
LEFT JOIN MST_Wallet w ON w.CustomerId = u.UserId AND w.IsDeleted = 0
WHERE u.UserType = 1 AND u.IsDeleted = 0
ORDER BY u.UserId;

PRINT 'RetireUnusedTestCustomers: applied.';
