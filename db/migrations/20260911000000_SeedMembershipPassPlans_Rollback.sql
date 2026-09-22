/* Rollback for 20260911000000_SeedMembershipPassPlans.sql

   Removes the 20 membership plans. Refuses to run if any customer has already
   bought one -- MST_PassPlan rows are referenced by TRN_CustomerPass
   (FK_TRN_CustomerPass_MST_PassPlan_PassPlanId), and the API itself blocks
   deactivating a plan with live passes for the same reason. If they have been
   sold, set IsActive = 0 instead of deleting. */
SET NOCOUNT ON;
SET XACT_ABORT ON;

IF EXISTS (
    SELECT 1
    FROM TRN_CustomerPass AS cp
    JOIN MST_PassPlan     AS pp ON pp.PassPlanId = cp.PassPlanId
    WHERE pp.PlanCode LIKE 'MEM-%'
)
BEGIN
    RAISERROR ('Membership plans have already been sold -- set IsActive = 0 instead of deleting.', 16, 1);
    RETURN;
END

DELETE FROM MST_PassPlan WHERE PlanCode LIKE 'MEM-%' AND PassType = 2;

SELECT COUNT(*) AS RemainingMembershipPlans FROM MST_PassPlan WHERE PassType = 2;
