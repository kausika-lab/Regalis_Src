-- Rollback of 20261103120000_CustomerPortalChanges.sql: shows the customer Notifications
-- sidebar entry again and removes the three Notifications.CustomerEmail* settings. Without
-- them the email worker sends nothing. Safe to run more than once. ASCII only.
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
SET NOCOUNT ON;
GO
SET XACT_ABORT ON;
BEGIN TRANSACTION;

UPDATE MST_Menu SET IsVisible = 1, ModifiedDate = SYSUTCDATETIME()
WHERE MenuCode = N'CUST_NOTIFICATIONS' AND IsVisible = 0;

DELETE FROM MST_AppSetting WHERE SettingKey IN (N'Notifications.CustomerEmailEnabled',
    N'Notifications.CustomerEmailPortalUrl', N'Notifications.CustomerEmailLastId');

COMMIT TRANSACTION;

SELECT MenuCode, IsVisible FROM MST_Menu WHERE MenuCode = N'CUST_NOTIFICATIONS';
SELECT COUNT(*) AS EmailSettings FROM MST_AppSetting WHERE SettingKey LIKE N'Notifications.CustomerEmail%';
