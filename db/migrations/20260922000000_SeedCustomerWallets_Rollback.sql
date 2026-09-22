/* 20260922000000_SeedCustomerWallets_Rollback.sql
   Undoes 20260922000000_SeedCustomerWallets.sql.

   Removes the seven seeded customers outright - wallet transactions, wallet, role
   mapping and login. Kausika RS and her 22 transactions are untouched.

   The number sequences are left where the seed pushed them. Winding them back would
   hand out numbers that the deleted narrations already quoted in anything exported
   or printed in the meantime, and a gap in a sequence is harmless. */

/* Same reason as the forward script: MST_User carries filtered unique indexes, so it
   cannot be written to with QUOTED_IDENTIFIER OFF, which is sqlcmd's default. */
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO

SET NOCOUNT ON;
SET XACT_ABORT ON;

DECLARE @Ids TABLE (UserId bigint PRIMARY KEY);
INSERT INTO @Ids (UserId)
SELECT UserId FROM MST_User WHERE Email IN (N'rahul@regalis.in', N'priya@regalis.in', N'arjun@regalis.in', N'sneha@regalis.in', N'vikram@regalis.in', N'ananya@regalis.in', N'rohit@regalis.in');

IF NOT EXISTS (SELECT 1 FROM @Ids)
BEGIN
    PRINT 'SeedCustomerWallets rollback: nothing to remove.';
    RETURN;
END

BEGIN TRAN;

DELETE t FROM TRN_WalletTransaction t JOIN @Ids i ON i.UserId = t.CustomerId;
DELETE w FROM MST_Wallet            w JOIN @Ids i ON i.UserId = w.CustomerId;
DELETE r FROM MAP_UserRole          r JOIN @Ids i ON i.UserId = r.UserId;
DELETE u FROM MST_User              u JOIN @Ids i ON i.UserId = u.UserId;

COMMIT;

PRINT 'SeedCustomerWallets rollback: seeded customers removed.';
