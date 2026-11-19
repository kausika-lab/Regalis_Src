-- Rollback of 20261119120000_Crm.sql. Run it together with going back to an API without the CRM kit.
-- WARNING: drops the CRM tables, so every lead, follow-up and visitor session is lost. Export the
-- leads (CRM > Leads > Export) first if they matter.
-- Users who held only the Sales / Front desk role keep their account but lose that role.
-- Idempotent; one transaction. ASCII only.
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
SET NOCOUNT ON;
GO
SET XACT_ABORT ON;
BEGIN TRANSACTION;

DELETE rm FROM dbo.MAP_RoleMenu rm JOIN dbo.MST_Menu m ON m.MenuId = rm.MenuId
WHERE m.MenuCode IN (N'ADM_CRM_LEADS', N'ADM_CRM_FOLLOWUPS', N'ADM_CRM_VISITORS', N'ADM_CRM_REPORTS', N'ADM_CRM_SETTINGS', N'ADM_CRM', N'ADM_SEC_SALES');
DELETE FROM dbo.MST_Menu WHERE MenuCode IN (N'ADM_CRM_LEADS', N'ADM_CRM_FOLLOWUPS', N'ADM_CRM_VISITORS', N'ADM_CRM_REPORTS', N'ADM_CRM_SETTINGS');
DELETE FROM dbo.MST_Menu WHERE MenuCode = N'ADM_CRM';
DELETE FROM dbo.MST_Menu WHERE MenuCode = N'ADM_SEC_SALES';

DECLARE @sales int = (SELECT RoleId FROM dbo.MST_Role WHERE RoleCode = N'SALES');
IF @sales IS NOT NULL
BEGIN
    DELETE FROM dbo.MAP_RoleMenu WHERE RoleId = @sales;
    DELETE FROM dbo.MAP_RolePermission WHERE RoleId = @sales;
    DELETE FROM dbo.MAP_UserRole WHERE RoleId = @sales;
    DELETE FROM dbo.MST_Role WHERE RoleId = @sales;
END;

DELETE rp FROM dbo.MAP_RolePermission rp JOIN dbo.MST_Permission p ON p.PermissionId = rp.PermissionId WHERE p.PermissionCode LIKE N'CRM.%';
DELETE FROM dbo.MST_Permission WHERE PermissionCode LIKE N'CRM.%';
DELETE FROM dbo.MST_NumberSequence WHERE SequenceKey = N'CrmLead';

IF OBJECT_ID(N'dbo.TRN_CrmNotification', N'U') IS NOT NULL DROP TABLE dbo.TRN_CrmNotification;
IF OBJECT_ID(N'dbo.TRN_CrmWhatsAppRef', N'U') IS NOT NULL DROP TABLE dbo.TRN_CrmWhatsAppRef;
IF OBJECT_ID(N'dbo.TRN_CrmPageView', N'U') IS NOT NULL DROP TABLE dbo.TRN_CrmPageView;
IF OBJECT_ID(N'dbo.TRN_CrmVisitor', N'U') IS NOT NULL DROP TABLE dbo.TRN_CrmVisitor;
IF OBJECT_ID(N'dbo.TRN_CrmLeadConsent', N'U') IS NOT NULL DROP TABLE dbo.TRN_CrmLeadConsent;
IF OBJECT_ID(N'dbo.TRN_CrmFollowUp', N'U') IS NOT NULL DROP TABLE dbo.TRN_CrmFollowUp;
IF OBJECT_ID(N'dbo.TRN_CrmLeadActivity', N'U') IS NOT NULL DROP TABLE dbo.TRN_CrmLeadActivity;
IF OBJECT_ID(N'dbo.TRN_CrmLead', N'U') IS NOT NULL DROP TABLE dbo.TRN_CrmLead;
IF OBJECT_ID(N'dbo.MST_CrmSetting', N'U') IS NOT NULL DROP TABLE dbo.MST_CrmSetting;
IF OBJECT_ID(N'dbo.MST_CrmTeamMember', N'U') IS NOT NULL DROP TABLE dbo.MST_CrmTeamMember;
IF OBJECT_ID(N'dbo.MST_CrmAssignmentRule', N'U') IS NOT NULL DROP TABLE dbo.MST_CrmAssignmentRule;
IF OBJECT_ID(N'dbo.MST_CrmLookingFor', N'U') IS NOT NULL DROP TABLE dbo.MST_CrmLookingFor;
IF OBJECT_ID(N'dbo.MST_CrmLostReason', N'U') IS NOT NULL DROP TABLE dbo.MST_CrmLostReason;
IF OBJECT_ID(N'dbo.MST_CrmStatus', N'U') IS NOT NULL DROP TABLE dbo.MST_CrmStatus;
IF OBJECT_ID(N'dbo.MST_CrmSource', N'U') IS NOT NULL DROP TABLE dbo.MST_CrmSource;

COMMIT TRANSACTION;
PRINT 'CRM rolled back.';
