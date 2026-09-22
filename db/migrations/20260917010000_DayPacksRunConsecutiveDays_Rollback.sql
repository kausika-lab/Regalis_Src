/* ---------------------------------------------------------------------------
   Rollback: put the day packs back on the 90-day credit window.

   Restores ValidityDays = 90 and the original "use them on any days you like"
   descriptions for DAYPASS-5 / -10 / -20. DAYPASS-1 is left at 1 day; its own
   rollback is 20260917000000_SingleDayPassValidOnChosenDateOnly_Rollback.sql.

   Passes sold while the consecutive-run setting was live keep the window they
   were sold under; this script does not rewrite TRN_CustomerPass.

   Pair with reverting the day-pass copy in
   frontend/assets/PassesPage-CxAkHUg-.js if you want the wording to match.

   Idempotent -- re-running changes nothing.
   Forward: 20260917010000_DayPacksRunConsecutiveDays.sql
   --------------------------------------------------------------------------- */
SET NOCOUNT ON;
SET XACT_ABORT ON;

BEGIN TRANSACTION;

DECLARE @Now datetime2 = SYSUTCDATETIME();
DECLARE @By  bigint    = (SELECT TOP 1 UserId FROM MST_User WHERE Email = 'admin@regalis.in');

DECLARE @Prior TABLE (PlanCode nvarchar(50), Days int, Description nvarchar(500));
INSERT INTO @Prior (PlanCode, Days, Description) VALUES
    ('DAYPASS-5',  90, '5 day credits - use them on any days you like.'),
    ('DAYPASS-10', 90, '10 day credits - our most popular pack.'),
    ('DAYPASS-20', 90, '20 day credits - the best per-day rate.');

UPDATE p
SET    p.ValidityDays = t.Days,
       p.Description  = t.Description,
       p.ModifiedBy   = @By,
       p.ModifiedDate = @Now
FROM   MST_PassPlan AS p
JOIN   @Prior       AS t ON t.PlanCode = p.PlanCode
WHERE  p.IsDeleted = 0
  AND  (p.ValidityDays <> t.Days OR p.Description <> t.Description);

COMMIT TRANSACTION;

/* Verification */
SELECT PlanCode, PlanName, ValidityDays, Price, Description
FROM   MST_PassPlan
WHERE  PlanCode LIKE 'DAYPASS%' AND IsDeleted = 0
ORDER  BY DisplayOrder;
