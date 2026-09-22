/* 20260922010000_RetireUnusedTestCustomers_Rollback.sql
   Brings dhina007@gmail.com and bavanthiravi@gmail.com back.

   Restores them exactly as they were: UserStatus is left untouched, so they return to
   PendingVerification with an empty wallet, which is what they were before.

   UX_User_Email and UX_User_Mobile are filtered on IsDeleted = 0, so retiring these
   two released their email and mobile. If either has since been taken by a live
   account, un-deleting would break that index - this script checks first and stops
   rather than failing halfway. */

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
  AND IsDeleted = 1;

IF NOT EXISTS (SELECT 1 FROM @Ids)
BEGIN
    PRINT 'RetireUnusedTestCustomers rollback: nothing to restore.';
    RETURN;
END

IF EXISTS (
    SELECT 1
    FROM MST_User live
    JOIN MST_User dead ON dead.UserId IN (SELECT UserId FROM @Ids)
    WHERE live.IsDeleted = 0
      AND live.UserId <> dead.UserId
      AND (live.Email = dead.Email OR live.MobileNumber = dead.MobileNumber)
)
BEGIN
    RAISERROR ('RetireUnusedTestCustomers rollback: an active account has since taken one of these emails or mobile numbers. Nothing was changed.', 16, 1);
    RETURN;
END

BEGIN TRAN;

UPDATE u
SET u.IsActive = 1, u.IsDeleted = 0, u.DeletedBy = NULL, u.DeletedDate = NULL
FROM MST_User u JOIN @Ids i ON i.UserId = u.UserId;

UPDATE w
SET w.IsActive = 1, w.IsDeleted = 0, w.DeletedBy = NULL, w.DeletedDate = NULL
FROM MST_Wallet w JOIN @Ids i ON i.UserId = w.CustomerId;

COMMIT;

SELECT Restored = u.FullName, u.Email, u.Status, u.IsDeleted
FROM MST_User u JOIN @Ids i ON i.UserId = u.UserId;

PRINT 'RetireUnusedTestCustomers rollback: accounts restored.';
