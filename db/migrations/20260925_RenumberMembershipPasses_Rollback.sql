-- Rollback of 20260925_RenumberMembershipPasses.sql: MEM-000006 back to PASS-000006.
-- Only needed together with rolling the API back to a build without
-- PassService.PassNumberFor; a newer build keeps issuing MEM- numbers.

SET XACT_ABORT ON;
BEGIN TRANSACTION;

DECLARE @Renumber TABLE (CustomerPassId bigint PRIMARY KEY, OldNumber nvarchar(30), NewNumber nvarchar(30));

INSERT INTO @Renumber (CustomerPassId, OldNumber, NewNumber)
SELECT cp.CustomerPassId, cp.PassNumber, N'PASS-' + SUBSTRING(cp.PassNumber, 5, 30)
FROM TRN_CustomerPass cp
WHERE cp.PassNumber LIKE N'MEM-%';

UPDATE cp SET cp.PassNumber = r.NewNumber
FROM TRN_CustomerPass cp
JOIN @Renumber r ON r.CustomerPassId = cp.CustomerPassId;

UPDATE n SET n.Message = REPLACE(n.Message, N'(' + r.OldNumber + N')', N'(' + r.NewNumber + N')')
FROM TRN_Notification n
JOIN @Renumber r ON n.Message LIKE N'%(' + r.OldNumber + N')%';

SELECT OldNumber, NewNumber FROM @Renumber ORDER BY OldNumber;

COMMIT TRANSACTION;
