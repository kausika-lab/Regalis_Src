/* ---------------------------------------------------------------------------
   Fix the Raise Ticket wizard's step 2 (MST_TicketIssueType)

   Two separate data faults made "Booking Related" list 12 issue types:

   1. Four issue types created on 2026-09-08 by admin@regalis.in are not issue
      types at all -- they are copies of the CATEGORY names (Building
      Management, Housekeeping, Pantry, Others) parented under "Booking
      Related". Their DisplayOrder is 1-4, so they sorted ABOVE the real
      booking issues: step 2 led with a second copy of step 1, and picking one
      filed the ticket as category "Booking Related" / issue "Housekeeping",
      which routes to the wrong team. None of them has ever been used by a
      ticket, so they are soft-deleted here.

   2. "Password Reset / Login Issue" (TicketIssueTypeId 12) was deactivated
      (IsActive = 0) but never soft-deleted, and the categories endpoint
      filters issue types on IsDeleted only -- so a switched-off row kept
      appearing. It is the one row of the original 2026-09-01 seed batch that
      was never replaced by the 2026-09-08 re-seed, and TKT-000001 is filed
      against it, so it is REACTIVATED rather than deleted: deleting it risks
      a null issueTypeName on that ticket's detail page.

   Leaves alone: the soft-deleted duplicate categories/issue types the seeder
   produced (see the SeedOnStartup note), and TRN_Ticket rows.

   Idempotent -- re-running changes nothing.
   Rollback: 20260911020000_FixTicketIssueTypes_Rollback.sql
   --------------------------------------------------------------------------- */
SET NOCOUNT ON;
SET XACT_ABORT ON;

DECLARE @Now datetime2 = SYSUTCDATETIME();
DECLARE @By  bigint    = (SELECT TOP 1 UserId FROM MST_User WHERE Email = 'admin@regalis.in');

/* 1. Retire issue types that merely repeat a live category name, and that no
      ticket has ever used. */
UPDATE it
SET    it.IsActive    = 0,
       it.IsDeleted   = 1,
       it.DeletedBy   = @By,
       it.DeletedDate = @Now
FROM   MST_TicketIssueType AS it
WHERE  it.IsDeleted = 0
  AND  EXISTS (SELECT 1
               FROM   MST_TicketCategory AS c
               WHERE  c.IsDeleted = 0
                 AND  c.CategoryName = it.IssueTypeName)
  AND  NOT EXISTS (SELECT 1
                   FROM   TRN_Ticket AS t
                   WHERE  t.TicketIssueTypeId = it.TicketIssueTypeId);

/* 2. A live, in-use issue type must not sit switched off. */
UPDATE MST_TicketIssueType
SET    IsActive     = 1,
       ModifiedBy   = @By,
       ModifiedDate = @Now
WHERE  IsDeleted = 0
  AND  IsActive  = 0
  AND  EXISTS (SELECT 1
               FROM   TRN_Ticket AS t
               WHERE  t.TicketIssueTypeId = MST_TicketIssueType.TicketIssueTypeId);

/* Verification -- what the wizard will now show, in the order it shows it */
SELECT c.TicketCategoryId, c.CategoryName, c.DisplayOrder AS CatOrder,
       it.TicketIssueTypeId, it.IssueTypeName, it.DisplayOrder AS TypeOrder
FROM   MST_TicketCategory  AS c
JOIN   MST_TicketIssueType AS it ON it.TicketCategoryId = c.TicketCategoryId
                                AND it.IsDeleted = 0
WHERE  c.IsActive = 1 AND c.IsDeleted = 0
ORDER  BY c.DisplayOrder, c.TicketCategoryId, it.DisplayOrder, it.TicketIssueTypeId;
