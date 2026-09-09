/* ---------------------------------------------------------------------------
   Rollback for 20260909000000_RemoveMobileVerification.

   Restores MST_User.IsMobileVerified to its original shape: bit NOT NULL with
   NO default constraint. A default is required to add a non-nullable column to
   a populated table, so one is created and then dropped again — which leaves
   the column exactly as it was before the removal.

   Existing rows come back as 0, which is what every row held before the drop.
--------------------------------------------------------------------------- */

SET NOCOUNT ON;
SET XACT_ABORT ON;
GO

BEGIN TRANSACTION;
GO

IF COL_LENGTH('dbo.MST_User', 'IsMobileVerified') IS NULL
BEGIN
    ALTER TABLE dbo.MST_User
        ADD IsMobileVerified bit NOT NULL
            CONSTRAINT DF_MST_User_IsMobileVerified_tmp DEFAULT 0;
END
GO

IF EXISTS (SELECT 1 FROM sys.default_constraints
           WHERE name = 'DF_MST_User_IsMobileVerified_tmp'
             AND parent_object_id = OBJECT_ID('dbo.MST_User'))
    ALTER TABLE dbo.MST_User DROP CONSTRAINT DF_MST_User_IsMobileVerified_tmp;
GO

COMMIT TRANSACTION;
GO
