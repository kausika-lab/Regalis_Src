/* 20261023000000_WorkspaceCustomisationTickets_Rollback.sql
   Undoes 20261023000000_WorkspaceCustomisationTickets.sql.

   If no ticket uses the "Workspace Customisation" category yet, the category and
   its issue types are deleted. If tickets exist they keep their category, and the
   rows are soft-deleted instead so the Book Workspace form stops offering them
   (the page then falls back to the "Others" category). */
SET NOCOUNT ON;
SET XACT_ABORT ON;
BEGIN TRAN;

DECLARE @cat INT = (SELECT TOP (1) TicketCategoryId
                      FROM MST_TicketCategory
                     WHERE CategoryName = N'Workspace Customisation' AND IsDeleted = 0);

IF @cat IS NULL
BEGIN
    PRINT 'Nothing to roll back';
END
ELSE IF EXISTS (SELECT 1 FROM TRN_Ticket WHERE TicketCategoryId = @cat)
BEGIN
    UPDATE MST_TicketIssueType
       SET IsActive = 0, IsDeleted = 1, DeletedDate = GETUTCDATE()
     WHERE TicketCategoryId = @cat AND IsDeleted = 0;
    UPDATE MST_TicketCategory
       SET IsActive = 0, IsDeleted = 1, DeletedDate = GETUTCDATE()
     WHERE TicketCategoryId = @cat;
    PRINT 'Tickets use the category: category and issue types soft-deleted';
END
ELSE
BEGIN
    DELETE FROM MST_TicketIssueType WHERE TicketCategoryId = @cat;
    DELETE FROM MST_TicketCategory WHERE TicketCategoryId = @cat;
    PRINT 'Category and issue types removed';
END

COMMIT;
