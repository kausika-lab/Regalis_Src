/* ---------------------------------------------------------------------------
   Membership plans -> MST_PassPlan (PassType = 2, MonthlyPass)

   Data script, not DDL: it adds the 20 membership plans (5 space types x 4
   terms) the customer Membership screen sells. Before this, membership had no
   backend at all -- prices were hard-coded in the SPA bundle and the
   "purchase" was written to localStorage, so nothing was ever charged and no
   invoice was raised. With these rows the screen buys through the existing
   POST /api/passes/order + /api/passes/purchase flow, which creates a real
   TRN_CustomerPass, TRN_Invoice and TRN_Payment.

   Price is the pre-tax total for the whole term (per-month rate x months);
   the API adds GST on purchase, the same way day passes work.

   Idempotent -- keyed on PlanCode, safe to re-run.
   Rollback: 20260911000000_SeedMembershipPassPlans_Rollback.sql
   --------------------------------------------------------------------------- */
SET NOCOUNT ON;
SET XACT_ABORT ON;

DECLARE @Now   datetime2     = SYSUTCDATETIME();
DECLARE @By    bigint        = (SELECT TOP 1 UserId FROM MST_User WHERE Email = 'admin@regalis.in');
DECLARE @Terms nvarchar(max) = N'Membership runs for the full term from the start date, and the monthly rate is locked for that term. Free meeting-room hours are granted fresh each billing month and do not roll over. Ending the term early does not refund the term discount.';

DECLARE @Plans TABLE (
    PlanCode     nvarchar(40)  NOT NULL,
    PlanName     nvarchar(120) NOT NULL,
    Price        decimal(18,2) NOT NULL,
    ValidityDays int           NOT NULL,
    MaxGuests    int           NOT NULL,
    Description  nvarchar(max) NOT NULL,
    DisplayOrder int           NOT NULL
);

INSERT INTO @Plans (PlanCode, PlanName, Price, ValidityDays, MaxGuests, Description, DisplayOrder)
VALUES
 -- Open / Hot Desk -- 7000 / 6650 / 6300 / 5950 per month
 (N'MEM-HOT-1M',        N'Open / Hot Desk - Monthly',       7000.00,  30, 1, N'A seat on the shared floor, first come first served. Rolling monthly term; meeting rooms at the standard rate.',      11),
 (N'MEM-HOT-3M',        N'Open / Hot Desk - 3 Months',     19950.00,  90, 1, N'A seat on the shared floor, first come first served. 3-month term at 5% off, with 2 free meeting-room hours a month.',  12),
 (N'MEM-HOT-6M',        N'Open / Hot Desk - 6 Months',     37800.00, 180, 1, N'A seat on the shared floor, first come first served. 6-month term at 10% off, with 4 free meeting-room hours a month.', 13),
 (N'MEM-HOT-12M',       N'Open / Hot Desk - 12 Months',    71400.00, 365, 1, N'A seat on the shared floor, first come first served. 12-month term at 15% off, with 8 free meeting-room hours a month.',14),
 -- Dedicated Desk -- 9000 / 8550 / 8100 / 7650 per month
 (N'MEM-DEDICATED-1M',  N'Dedicated Desk - Monthly',        9000.00,  30, 1, N'Your own desk, kept for you. Rolling monthly term; meeting rooms at the standard rate.',                                15),
 (N'MEM-DEDICATED-3M',  N'Dedicated Desk - 3 Months',      25650.00,  90, 1, N'Your own desk, kept for you. 3-month term at 5% off, with 2 free meeting-room hours a month.',                          16),
 (N'MEM-DEDICATED-6M',  N'Dedicated Desk - 6 Months',      48600.00, 180, 1, N'Your own desk, kept for you. 6-month term at 10% off, with 4 free meeting-room hours a month.',                         17),
 (N'MEM-DEDICATED-12M', N'Dedicated Desk - 12 Months',     91800.00, 365, 1, N'Your own desk, kept for you. 12-month term at 15% off, with 8 free meeting-room hours a month.',                        18),
 -- 4-Seater Cabin -- 40000 / 38000 / 36000 / 34000 per month
 (N'MEM-CABIN4-1M',     N'4-Seater Cabin - Monthly',       40000.00,  30, 4, N'A private cabin for a team of four. Rolling monthly term; meeting rooms at the standard rate.',                         19),
 (N'MEM-CABIN4-3M',     N'4-Seater Cabin - 3 Months',     114000.00,  90, 4, N'A private cabin for a team of four. 3-month term at 5% off, with 2 free meeting-room hours a month.',                   20),
 (N'MEM-CABIN4-6M',     N'4-Seater Cabin - 6 Months',     216000.00, 180, 4, N'A private cabin for a team of four. 6-month term at 10% off, with 4 free meeting-room hours a month.',                  21),
 (N'MEM-CABIN4-12M',    N'4-Seater Cabin - 12 Months',    408000.00, 365, 4, N'A private cabin for a team of four. 12-month term at 15% off, with 8 free meeting-room hours a month.',                 22),
 -- 8-Seater Cabin -- 80000 / 76000 / 72000 / 68000 per month
 (N'MEM-CABIN8-1M',     N'8-Seater Cabin - Monthly',       80000.00,  30, 8, N'A private cabin for a team of eight. Rolling monthly term; meeting rooms at the standard rate.',                        23),
 (N'MEM-CABIN8-3M',     N'8-Seater Cabin - 3 Months',     228000.00,  90, 8, N'A private cabin for a team of eight. 3-month term at 5% off, with 2 free meeting-room hours a month.',                  24),
 (N'MEM-CABIN8-6M',     N'8-Seater Cabin - 6 Months',     432000.00, 180, 8, N'A private cabin for a team of eight. 6-month term at 10% off, with 4 free meeting-room hours a month.',                 25),
 (N'MEM-CABIN8-12M',    N'8-Seater Cabin - 12 Months',    816000.00, 365, 8, N'A private cabin for a team of eight. 12-month term at 15% off, with 8 free meeting-room hours a month.',                26),
 -- Virtual Office -- 2500 / 2375 / 2250 / 2125 per month
 (N'MEM-VIRTUAL-1M',    N'Virtual Office - Monthly',        2500.00,  30, 1, N'Business address and mail handling, no desk. Rolling monthly term; meeting rooms at the standard rate.',                27),
 (N'MEM-VIRTUAL-3M',    N'Virtual Office - 3 Months',       7125.00,  90, 1, N'Business address and mail handling, no desk. 3-month term at 5% off, with 2 free meeting-room hours a month.',          28),
 (N'MEM-VIRTUAL-6M',    N'Virtual Office - 6 Months',      13500.00, 180, 1, N'Business address and mail handling, no desk. 6-month term at 10% off, with 4 free meeting-room hours a month.',         29),
 (N'MEM-VIRTUAL-12M',   N'Virtual Office - 12 Months',     25500.00, 365, 1, N'Business address and mail handling, no desk. 12-month term at 15% off, with 8 free meeting-room hours a month.',        30);

INSERT INTO MST_PassPlan
    (PlanCode, PlanName, PassType, LocationId, Price, ValidityDays, MaxGuests,
     Description, TermsAndConditions, ImagePath, DisplayOrder,
     CreatedBy, CreatedDate, IsActive, IsDeleted)
SELECT
    p.PlanCode, p.PlanName, 2, NULL, p.Price, p.ValidityDays, p.MaxGuests,
    p.Description, @Terms, NULL, p.DisplayOrder,
    @By, @Now, 1, 0
FROM @Plans AS p
WHERE NOT EXISTS (SELECT 1 FROM MST_PassPlan AS e WHERE e.PlanCode = p.PlanCode);

/* Verification */
SELECT PassPlanId, PlanCode, PlanName, PassType, Price, ValidityDays, MaxGuests, DisplayOrder, IsActive, IsDeleted
FROM MST_PassPlan
WHERE PassType = 2
ORDER BY DisplayOrder;
