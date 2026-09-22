/* ---------------------------------------------------------------------------
   A Single Day pass is valid on the date the customer picked, and only that date.

   TRN_CustomerPass.ValidTo is computed server-side as
   ValidFrom + ValidityDays - 1, which is data, not code:

       PASS-000001  DAYPASS-1  ValidFrom 2026-09-10  ValidTo 2026-12-08   (90 days)

   So a pass called "Single Day" sold a ninety-day window. Setting
   MST_PassPlan.ValidityDays = 1 makes ValidTo land on ValidFrom, so the pass is
   valid on the chosen date alone. No API change is involved.

   Scope: DAYPASS-1 ONLY.

   The 5/10/20-day packs keep their 90-day window on purpose. Their whole
   product is "N day credits - use them on any days you like", and there is no
   credit ledger behind them: CR() in PassesPage derives the credit count from
   the plan NAME and UD() treats a pass as all-used once Status = 2. Collapsing
   their window to one day would sell N credits that expire the same evening.
   Per-date scheduling for a pack needs the credit wallet that does not exist
   yet, not a validity edit.

   Already-issued passes are NOT touched. TRN_CustomerPass stores its own
   ValidFrom/ValidTo, so PASS-000001 and PASS-000002 keep the window they were
   sold under. Rewriting them would shorten access a customer has paid for.
   This affects new purchases only.

   The SPA needs no change to agree with this: the buy card renders
   plan.validityDays, so it will read "1 day credit - 1 day validity" on its
   own. The date-picker hint is updated in the same change to stop promising a
   window (frontend/assets/PassesPage-CxAkHUg-.js).

   Seeding stays safe: SeedCatalogue only inserts missing rows and never
   overwrites existing ones, and UAT runs Database__SeedOnStartup=false.

   Idempotent -- re-running changes nothing.
   Rollback: 20260917000000_SingleDayPassValidOnChosenDateOnly_Rollback.sql
   --------------------------------------------------------------------------- */
SET NOCOUNT ON;
SET XACT_ABORT ON;

DECLARE @Now datetime2 = SYSUTCDATETIME();
DECLARE @By  bigint    = (SELECT TOP 1 UserId FROM MST_User WHERE Email = 'admin@regalis.in');

UPDATE MST_PassPlan
SET    ValidityDays = 1,
       Description  = 'One day credit for walk-in shared seating, valid on the date you choose.',
       ModifiedBy   = @By,
       ModifiedDate = @Now
WHERE  PlanCode     = 'DAYPASS-1'
  AND  IsDeleted    = 0
  AND  ValidityDays <> 1;

/* Verification -- what each day plan now sells. */
SELECT PassPlanId, PlanCode, PlanName, ValidityDays, Price, Description
FROM   MST_PassPlan
WHERE  PlanCode LIKE 'DAYPASS%' AND IsDeleted = 0
ORDER  BY DisplayOrder;

/* Existing passes are untouched -- confirm their windows still stand. */
SELECT PassNumber, ValidFrom, ValidTo, DATEDIFF(day, ValidFrom, ValidTo) + 1 AS DaysInclusive, Status
FROM   TRN_CustomerPass
WHERE  IsDeleted = 0
ORDER  BY CustomerPassId;
