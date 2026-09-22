/* ---------------------------------------------------------------------------
   A day pack runs for N consecutive days from the date the customer picks.

   TRN_CustomerPass.ValidTo is computed server-side as
   ValidFrom + ValidityDays - 1, so the window is set by
   MST_PassPlan.ValidityDays -- data, not code. Every pack carried 90, which
   sold a three-month window no matter how many days were bought:

       DAYPASS-5   5-Day Pack   ValidityDays 90   ->  10 Sept to 09 Dec
       DAYPASS-10  10-Day Pack  ValidityDays 90
       DAYPASS-20  20-Day Pack  ValidityDays 90

   After this script ValidityDays equals the number of days in the pack, so a
   5-Day Pack bought for 20 Sept runs 20-24 Sept and no further. DAYPASS-1 was
   already set to 1 by 20260917000000.

   This replaces the "credits, used on any days you like" model with a
   consecutive run. That is the requested behaviour, and it also removes the
   gap the old model papered over: there is no credit ledger behind a pack.
   CR() in PassesPage derives the credit count from the plan NAME and UD()
   treats a pass as fully used once Status = 2, so "3 of 5 credits left after
   a 90-day spread" was never actually tracked. One credit per day over a
   fixed run is a model the schema can represent.

   Prices are NOT touched. Per-day rates stay as they are; only the window
   changes.

   Already-issued passes are NOT touched. TRN_CustomerPass stores its own
   ValidFrom/ValidTo, so PASS-000001 and PASS-000002 keep the 90-day window
   they were sold under. To realign the UAT test rows as well, run the
   companion script:
       20260917010000_RealignExistingTestPasses_UAT_ONLY.sql
   Do not run that one anywhere real -- it rewrites access a customer paid for.

   Copy that contradicted a consecutive run is updated in the same change
   (frontend/assets/PassesPage-CxAkHUg-.js): the page description, the plan
   card meta line, the start-date hint and the note under it.

   Seeding stays safe: SeedCatalogue only inserts missing rows, never
   overwrites, and UAT runs Database__SeedOnStartup=false.

   Idempotent -- re-running changes nothing.
   Rollback: 20260917010000_DayPacksRunConsecutiveDays_Rollback.sql
   --------------------------------------------------------------------------- */
SET NOCOUNT ON;
SET XACT_ABORT ON;

BEGIN TRANSACTION;

DECLARE @Now datetime2 = SYSUTCDATETIME();
DECLARE @By  bigint    = (SELECT TOP 1 UserId FROM MST_User WHERE Email = 'admin@regalis.in');

DECLARE @Target TABLE (PlanCode nvarchar(50), Days int, Description nvarchar(500));
INSERT INTO @Target (PlanCode, Days, Description) VALUES
    ('DAYPASS-5',   5, '5 consecutive days from the date you choose.'),
    ('DAYPASS-10', 10, '10 consecutive days from the date you choose - our most popular pack.'),
    ('DAYPASS-20', 20, '20 consecutive days from the date you choose - the best per-day rate.');

UPDATE p
SET    p.ValidityDays = t.Days,
       p.Description  = t.Description,
       p.ModifiedBy   = @By,
       p.ModifiedDate = @Now
FROM   MST_PassPlan AS p
JOIN   @Target      AS t ON t.PlanCode = p.PlanCode
WHERE  p.IsDeleted = 0
  AND  (p.ValidityDays <> t.Days OR p.Description <> t.Description);

COMMIT TRANSACTION;

/* Verification -- every day plan now sells a run equal to its name. */
SELECT PlanCode, PlanName, ValidityDays, Price, Description
FROM   MST_PassPlan
WHERE  PlanCode LIKE 'DAYPASS%' AND IsDeleted = 0
ORDER  BY DisplayOrder;

/* Existing passes are untouched -- their windows still stand. */
SELECT PassNumber, ValidFrom, ValidTo,
       DATEDIFF(day, ValidFrom, ValidTo) + 1 AS DaysInclusive, Status
FROM   TRN_CustomerPass
WHERE  IsDeleted = 0
ORDER  BY CustomerPassId;
