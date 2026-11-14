/* Rollback of 20261114120000_ProfileJobTitle.sql: drops MST_User.JobTitle (the roles people
   typed are lost). Put the API build without User.JobTitle back FIRST, or every user query
   fails. Safe to run more than once. ASCII only. */
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
SET NOCOUNT ON;
GO
IF COL_LENGTH('dbo.MST_User', 'JobTitle') IS NOT NULL
    ALTER TABLE dbo.MST_User DROP COLUMN JobTitle;
GO
SELECT CASE WHEN COL_LENGTH('dbo.MST_User', 'JobTitle') IS NULL THEN 'dropped' ELSE 'present' END AS MST_User_JobTitle;
