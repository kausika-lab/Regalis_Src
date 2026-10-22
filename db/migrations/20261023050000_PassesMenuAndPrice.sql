/* 20261023050000_PassesMenuAndPrice.sql
   Book Pass / My Passes split and day-pass prices (23 Oct 2026).

   1. Day-pass prices (before GST) as given on 23 Oct 2026. Only Single Day
      changes (800 -> 900); the packs already carried these values.
        DAYPASS-1   Single Day     900
        DAYPASS-5   5-Day Pack   3,600
        DAYPASS-10  10-Day Pack  6,800
        DAYPASS-20  20-Day Pack 12,800
   2. The customer sidebar entry "Passes" (/passes) is renamed "Book Pass"; it
      keeps only the purchase flow.
   3. A new entry "My Passes" (/passes/my) lists the passes bought, mapped to the
      Customer role (RoleId 1) like the other customer menus. The SPA route is
      added by deploy/passes-split/apply.mjs.

   Idempotent: re-running changes nothing.
   Rollback: 20261023050000_PassesMenuAndPrice_Rollback.sql */
SET NOCOUNT ON;
SET XACT_ABORT ON;
BEGIN TRAN;

/* 1. prices */
DECLARE @prices TABLE (Code NVARCHAR(50) NOT NULL, Price DECIMAL(18, 2) NOT NULL);
INSERT INTO @prices (Code, Price) VALUES
    (N'DAYPASS-1', 900.00), (N'DAYPASS-5', 3600.00), (N'DAYPASS-10', 6800.00), (N'DAYPASS-20', 12800.00);
UPDATE p
   SET Price = n.Price, ModifiedDate = GETUTCDATE()
  FROM MST_PassPlan p
  JOIN @prices n ON n.Code = p.PlanCode
 WHERE p.IsDeleted = 0 AND p.Price <> n.Price;
PRINT CONCAT('Day-pass prices changed: ', @@ROWCOUNT);

/* 2. Passes -> Book Pass */
UPDATE MST_Menu
   SET MenuName = N'Book Pass', DisplayName = N'Book Pass', ModifiedDate = GETUTCDATE()
 WHERE MenuCode = N'CUST_PASSES' AND IsDeleted = 0
   AND (MenuName <> N'Book Pass' OR DisplayName <> N'Book Pass');
PRINT CONCAT('Passes menu renamed: ', @@ROWCOUNT);

/* 3. My Passes */
DECLARE @book INT = (SELECT TOP (1) MenuId FROM MST_Menu WHERE MenuCode = N'CUST_PASSES' AND IsDeleted = 0);
IF @book IS NULL
    THROW 50000, 'The CUST_PASSES menu row is missing; nothing to place My Passes after.', 1;

DECLARE @my INT = (SELECT TOP (1) MenuId FROM MST_Menu WHERE MenuCode = N'CUST_MYPASSES' ORDER BY IsDeleted, MenuId);
IF @my IS NULL
BEGIN
    INSERT INTO MST_Menu (ParentMenuId, MenuCode, MenuName, DisplayName, Route, Icon, DisplayOrder,
                          PermissionId, PortalType, MenuLevel, IsVisible, CreatedBy, CreatedDate, IsActive, IsDeleted)
    SELECT NULL, N'CUST_MYPASSES', N'My Passes', N'My Passes', N'/passes/my', N'Tickets', b.DisplayOrder + 2,
           b.PermissionId, b.PortalType, 1, 1, NULL, GETUTCDATE(), 1, 0
      FROM MST_Menu b WHERE b.MenuId = @book;
    SET @my = SCOPE_IDENTITY();
    PRINT 'My Passes menu added';
END
ELSE
BEGIN
    UPDATE MST_Menu
       SET IsActive = 1, IsDeleted = 0, DeletedBy = NULL, DeletedDate = NULL, IsVisible = 1,
           Route = N'/passes/my', ModifiedDate = GETUTCDATE()
     WHERE MenuId = @my AND (IsDeleted = 1 OR IsActive = 0 OR IsVisible = 0 OR Route <> N'/passes/my');
    PRINT 'My Passes menu already present';
END

IF NOT EXISTS (SELECT 1 FROM MAP_RoleMenu WHERE RoleId = 1 AND MenuId = @my)
BEGIN
    INSERT INTO MAP_RoleMenu (RoleId, MenuId) VALUES (1, @my);
    PRINT 'My Passes mapped to the Customer role';
END

COMMIT;
