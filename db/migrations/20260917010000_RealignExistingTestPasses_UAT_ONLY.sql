/* ---------------------------------------------------------------------------
   *** UAT TEST DATA ONLY. NEVER RUN THIS ON PRODUCTION. ***

   Rewrites ValidTo on already-issued day passes so the seeded UAT rows match
   the consecutive-run rule, instead of showing the 90-day window they were
   actually sold under:

       PASS-000001  Single Day  10 Sept -> 08 Dec   becomes  10 Sept -> 10 Sept
       PASS-000002  5-Day Pack  10 Sept -> 09 Dec   becomes  10 Sept -> 14 Sept

   The forward migration (20260917010000) deliberately leaves issued passes
   alone, because shortening a window a customer paid for removes access they
   bought. That is the right default and it stays the default. This script
   exists only so the UAT screens stop showing test rows that contradict the
   new plan settings.

   Scope guards, so a stray run cannot do real damage:
     - day passes only (PassType = 1)
     - Active only (Status = 1); cancelled, used and expired rows are left
     - only rows whose window is longer than their plan now sells
     - ValidFrom is never touched, so the start date the customer chose stands

   ValidTo is recomputed as ValidFrom + plan.ValidityDays - 1, the same
   arithmetic the API uses on purchase.

   There is no rollback: the original ValidTo values were derived, not stored
   anywhere else. The values before this script are printed below -- keep the
   output if you want to put them back by hand.

   Idempotent -- re-running changes nothing.
   --------------------------------------------------------------------------- */
SET NOCOUNT ON;
SET XACT_ABORT ON;

/* What is about to change -- keep this output. */
SELECT cp.PassNumber, pp.PlanCode, cp.ValidFrom,
       cp.ValidTo                                          AS ValidTo_Before,
       DATEADD(day, pp.ValidityDays - 1, cp.ValidFrom)      AS ValidTo_After,
       DATEDIFF(day, cp.ValidFrom, cp.ValidTo) + 1          AS Days_Before,
       pp.ValidityDays                                      AS Days_After
FROM   TRN_CustomerPass AS cp
JOIN   MST_PassPlan     AS pp ON pp.PassPlanId = cp.PassPlanId
WHERE  cp.IsDeleted = 0
  AND  cp.PassType  = 1
  AND  cp.Status    = 1
  AND  cp.ValidTo   > DATEADD(day, pp.ValidityDays - 1, cp.ValidFrom);

BEGIN TRANSACTION;

DECLARE @Now datetime2 = SYSUTCDATETIME();
DECLARE @By  bigint    = (SELECT TOP 1 UserId FROM MST_User WHERE Email = 'admin@regalis.in');

UPDATE cp
SET    cp.ValidTo      = DATEADD(day, pp.ValidityDays - 1, cp.ValidFrom),
       cp.ModifiedBy   = @By,
       cp.ModifiedDate = @Now
FROM   TRN_CustomerPass AS cp
JOIN   MST_PassPlan     AS pp ON pp.PassPlanId = cp.PassPlanId
WHERE  cp.IsDeleted = 0
  AND  cp.PassType  = 1
  AND  cp.Status    = 1
  AND  cp.ValidTo   > DATEADD(day, pp.ValidityDays - 1, cp.ValidFrom);

COMMIT TRANSACTION;

/* Verification -- every pass, after. */
SELECT cp.PassNumber, pp.PlanCode, cp.ValidFrom, cp.ValidTo,
       DATEDIFF(day, cp.ValidFrom, cp.ValidTo) + 1 AS DaysInclusive, cp.Status
FROM   TRN_CustomerPass AS cp
JOIN   MST_PassPlan     AS pp ON pp.PassPlanId = cp.PassPlanId
WHERE  cp.IsDeleted = 0
ORDER  BY cp.CustomerPassId;
