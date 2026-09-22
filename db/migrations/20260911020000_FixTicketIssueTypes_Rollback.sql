/* Rollback for 20260911020000_FixTicketIssueTypes.sql
   Restores the four category-name issue types under "Booking Related" and
   switches "Password Reset / Login Issue" back off. Ids are the ones the fix
   touched in UAT; check them before running elsewhere. */
SET NOCOUNT ON;
SET XACT_ABORT ON;

UPDATE MST_TicketIssueType
SET    IsActive = 1, IsDeleted = 0, DeletedBy = NULL, DeletedDate = NULL
WHERE  TicketIssueTypeId IN (22, 23, 24, 25);

UPDATE MST_TicketIssueType
SET    IsActive = 0
WHERE  TicketIssueTypeId = 12;

SELECT TicketIssueTypeId, TicketCategoryId, IssueTypeName, DisplayOrder, IsActive, IsDeleted
FROM   MST_TicketIssueType
WHERE  TicketCategoryId = 1
ORDER  BY TicketIssueTypeId;
