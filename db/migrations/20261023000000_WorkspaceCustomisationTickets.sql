/* 20261023000000_WorkspaceCustomisationTickets.sql
   Customisation requests from Book Workspace (23 Oct 2026).

   Book Workspace gets a "Customisation" card beside Virtual Office. The customer
   picks what to customise (Desk, Cabin, Meeting Room or Virtual Office), describes
   it and submits. The request is an ordinary support ticket, so it lands in
   Admin > Support tickets and in the admin notification centre ("New support
   ticket", Needs action), and the team replies on the ticket itself.

   This adds the ticket category the page looks up by name ("Workspace
   Customisation") with one issue type per workspace kind. Nothing else changes.

   Idempotent: re-running is a no-op; a soft-deleted or inactive copy is
   reactivated rather than duplicated.
   Rollback: 20261023000000_WorkspaceCustomisationTickets_Rollback.sql */
SET NOCOUNT ON;
SET XACT_ABORT ON;
BEGIN TRAN;

DECLARE @cat INT = (SELECT TOP (1) TicketCategoryId
                      FROM MST_TicketCategory
                     WHERE CategoryName = N'Workspace Customisation'
                     ORDER BY IsDeleted, TicketCategoryId);

IF @cat IS NULL
BEGIN
    INSERT INTO MST_TicketCategory (CategoryName, DisplayOrder, CreatedBy, CreatedDate, IsActive, IsDeleted)
    VALUES (N'Workspace Customisation', 6, NULL, GETUTCDATE(), 1, 0);
    SET @cat = SCOPE_IDENTITY();
    PRINT 'Added ticket category "Workspace Customisation"';
END
ELSE
BEGIN
    UPDATE MST_TicketCategory
       SET IsActive = 1, IsDeleted = 0, DeletedBy = NULL, DeletedDate = NULL, ModifiedDate = GETUTCDATE()
     WHERE TicketCategoryId = @cat AND (IsActive = 0 OR IsDeleted = 1);
    PRINT 'Ticket category "Workspace Customisation" already present';
END

DECLARE @kinds TABLE (Name NVARCHAR(100) NOT NULL, Ord INT NOT NULL);
INSERT INTO @kinds (Name, Ord) VALUES (N'Desk', 1), (N'Cabin', 2), (N'Meeting Room', 3), (N'Virtual Office', 4);

UPDATE t
   SET IsActive = 1, IsDeleted = 0, DeletedBy = NULL, DeletedDate = NULL, ModifiedDate = GETUTCDATE()
  FROM MST_TicketIssueType t
  JOIN @kinds k ON k.Name = t.IssueTypeName
 WHERE t.TicketCategoryId = @cat AND (t.IsActive = 0 OR t.IsDeleted = 1);

INSERT INTO MST_TicketIssueType (TicketCategoryId, IssueTypeName, DisplayOrder, CreatedBy, CreatedDate, IsActive, IsDeleted)
SELECT @cat, k.Name, k.Ord, NULL, GETUTCDATE(), 1, 0
  FROM @kinds k
 WHERE NOT EXISTS (SELECT 1 FROM MST_TicketIssueType t
                    WHERE t.TicketCategoryId = @cat AND t.IssueTypeName = k.Name);
PRINT CONCAT('Issue types added: ', @@ROWCOUNT);

COMMIT;
