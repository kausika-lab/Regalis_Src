/* Profile "Role" (14 Nov 2026, kit deploy/admin-customer).
   Adds MST_User.JobTitle nvarchar(100) NULL: a free-text job title / role the person types
   on their own profile (e.g. "Founder"). It is NOT an access role and grants nothing.
   Existing rows stay NULL. Run BEFORE starting the API build that maps User.JobTitle --
   EF selects the column on every user query.
   Idempotent; ASCII only.
     sqlcmd -S localhost -d RegalisWorkSpace_UAT -E -C -I -b -i 20261114120000_ProfileJobTitle.sql
   Rollback: 20261114120000_ProfileJobTitle_Rollback.sql */
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
SET NOCOUNT ON;
GO
IF COL_LENGTH('dbo.MST_User', 'JobTitle') IS NULL
    ALTER TABLE dbo.MST_User ADD JobTitle nvarchar(100) NULL;
GO
SELECT CASE WHEN COL_LENGTH('dbo.MST_User', 'JobTitle') IS NULL THEN 'missing' ELSE 'present' END AS MST_User_JobTitle;
