/* ---------------------------------------------------------------------------
   Rollback: put the Single Day pass back on a 90-day window.

   Restores MST_PassPlan.ValidityDays = 90 and the original description for
   DAYPASS-1. Passes sold while the 1-day setting was live keep the single-day
   window they were sold under -- TRN_CustomerPass stores its own dates and this
   script does not rewrite them.

   Pair with reverting the date-picker hint in
   frontend/assets/PassesPage-CxAkHUg-.js if you want the copy to match again.

   Idempotent -- re-running changes nothing.
   Forward: 20260917000000_SingleDayPassValidOnChosenDateOnly.sql
   --------------------------------------------------------------------------- */
SET NOCOUNT ON;
SET XACT_ABORT ON;

DECLARE @Now datetime2 = SYSUTCDATETIME();
DECLARE @By  bigint    = (SELECT TOP 1 UserId FROM MST_User WHERE Email = 'admin@regalis.in');

UPDATE MST_PassPlan
SET    ValidityDays = 90,
       Description  = 'One day credit for walk-in shared seating.',
       ModifiedBy   = @By,
       ModifiedDate = @Now
WHERE  PlanCode     = 'DAYPASS-1'
  AND  IsDeleted    = 0
  AND  ValidityDays <> 90;

/* Verification */
SELECT PassPlanId, PlanCode, PlanName, ValidityDays, Price, Description
FROM   MST_PassPlan
WHERE  PlanCode LIKE 'DAYPASS%' AND IsDeleted = 0
ORDER  BY DisplayOrder;
