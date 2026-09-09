/* ---------------------------------------------------------------------------
   Regalis WorkSpace — remove MST_User.IsMobileVerified
   Equivalent of EF Core migration 20260909000000_RemoveMobileVerification.

   *** RUN THIS ONLY AFTER the API that no longer maps the property is live. ***

   EF Core names every column explicitly in its SELECT statements. While
   User.IsMobileVerified is still in the model, dropping the column makes every
   query against MST_User fail with:

       Invalid column name 'IsMobileVerified'.

   which breaks login, the profile endpoint and every admin user screen.
   Sequence: remove property + EF config + UserDetailDto field -> deploy API ->
   run this script.

   Verified before writing this script (UAT):
     * no index references the column
     * no default constraint on it
     * no foreign key or computed column depends on it
     * all 5 users have IsMobileVerified = 0 — no data is lost
     * every TRN_Otp row is Channel 1 / Purpose 1, so no OTP data relates to it

   IsEmailVerified is deliberately untouched — email OTP verification is live.
   TRN_Otp is deliberately untouched — its Channel column carries email too.
--------------------------------------------------------------------------- */

SET NOCOUNT ON;
SET XACT_ABORT ON;
GO

/* Guard: refuse to run while any row claims a verified mobile, so the script
   can never silently discard real data if this is run on another environment. */
IF COL_LENGTH('dbo.MST_User', 'IsMobileVerified') IS NOT NULL
   AND EXISTS (SELECT 1 FROM dbo.MST_User WHERE IsMobileVerified = 1)
BEGIN
    RAISERROR('Aborted: MST_User has rows with IsMobileVerified = 1. Export or review them before dropping the column.', 16, 1);
END
GO

BEGIN TRANSACTION;
GO

IF COL_LENGTH('dbo.MST_User', 'IsMobileVerified') IS NOT NULL
    ALTER TABLE dbo.MST_User DROP COLUMN IsMobileVerified;
GO

COMMIT TRANSACTION;
GO

/* Verification — expect 0 rows */
SELECT c.name
FROM sys.columns c
WHERE c.object_id = OBJECT_ID('dbo.MST_User')
  AND c.name = 'IsMobileVerified';
GO
