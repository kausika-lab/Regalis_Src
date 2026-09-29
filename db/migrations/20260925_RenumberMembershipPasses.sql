-- Membership purchases are numbered MEM-000006 instead of PASS-000006.
--
-- New purchases get the MEM- prefix in PassService.PassNumberFor (same CustomerPass
-- counter, so the two series never collide). This script renumbers the purchases made
-- before that change: every pass whose plan code starts with MEM- keeps its digits and
-- swaps the prefix. Notification text that quotes the old number is updated to match.
--
-- Issued invoice lines (TRN_InvoiceLine.Description) are left as they are: an issued
-- invoice is immutable, so it keeps quoting the PASS- number it was issued with.
--
-- Data only, no schema change, safe to run more than once. Run with:
--   sqlcmd -S localhost -d RegalisWorkSpace_UAT -E -C -I -b -i 20260925_RenumberMembershipPasses.sql

SET XACT_ABORT ON;
BEGIN TRANSACTION;

DECLARE @Renumber TABLE (CustomerPassId bigint PRIMARY KEY, OldNumber nvarchar(30), NewNumber nvarchar(30));

INSERT INTO @Renumber (CustomerPassId, OldNumber, NewNumber)
SELECT cp.CustomerPassId, cp.PassNumber, N'MEM-' + SUBSTRING(cp.PassNumber, 6, 30)
FROM TRN_CustomerPass cp
JOIN MST_PassPlan pp ON pp.PassPlanId = cp.PassPlanId
WHERE pp.PlanCode LIKE N'MEM-%'
  AND cp.PassNumber LIKE N'PASS-%';

UPDATE cp SET cp.PassNumber = r.NewNumber
FROM TRN_CustomerPass cp
JOIN @Renumber r ON r.CustomerPassId = cp.CustomerPassId;

UPDATE n SET n.Message = REPLACE(n.Message, N'(' + r.OldNumber + N')', N'(' + r.NewNumber + N')')
FROM TRN_Notification n
JOIN @Renumber r ON n.Message LIKE N'%(' + r.OldNumber + N')%';

SELECT OldNumber, NewNumber FROM @Renumber ORDER BY OldNumber;

COMMIT TRANSACTION;
