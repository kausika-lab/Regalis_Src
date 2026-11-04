-- Customer portal changes (3 Nov 2026), data only; no schema change.
--  1. Customer sidebar: the Notifications entry is hidden (MST_Menu.IsVisible = 0). The page
--     itself, its route and the bell in the top bar are unchanged.
--  2. Customer notifications by email (CustomerNotificationEmailWorker):
--       Notifications.CustomerEmailEnabled   on/off switch, editable on the Settings screen
--       Notifications.CustomerEmailPortalUrl portal address for the "View ..." button in the
--                                            email; empty = no button
--       Notifications.CustomerEmailLastId    progress mark, not editable. Seeded with the
--                                            newest NotificationId so turning this on never
--                                            emails the notifications already sent in-app.
-- Idempotent; one transaction. ASCII only.
--   sqlcmd -S localhost -d RegalisWorkSpace_UAT -E -C -I -b -i 20261103120000_CustomerPortalChanges.sql
-- Rollback: 20261103120000_CustomerPortalChanges_Rollback.sql
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
SET NOCOUNT ON;
GO
SET XACT_ABORT ON;
BEGIN TRANSACTION;

DECLARE @now datetime2(3) = SYSUTCDATETIME();

UPDATE MST_Menu SET IsVisible = 0, ModifiedDate = @now
WHERE MenuCode = N'CUST_NOTIFICATIONS' AND IsVisible = 1;

INSERT INTO MST_AppSetting (SettingKey, SettingValue, SettingGroup, DataType, Description,
                            IsEditableByAdmin, IsSecret, CreatedDate, IsActive, IsDeleted)
SELECT v.SettingKey, v.SettingValue, N'Notifications', v.DataType, v.Description, v.IsEditableByAdmin, 0, @now, 1, 0
FROM (VALUES
    (N'Notifications.CustomerEmailEnabled', N'true', N'bool',
     N'Email customers a copy of their notifications (bookings, payments, invoices, passes, tickets).', 1),
    (N'Notifications.CustomerEmailPortalUrl', N'', N'string',
     N'Customer portal address used for the button in notification emails, e.g. https://myregalis.com. Empty: no button.', 1),
    (N'Notifications.CustomerEmailLastId', CONVERT(nvarchar(20), (SELECT ISNULL(MAX(NotificationId), 0) FROM TRN_Notification)), N'int',
     N'Internal: the last notification considered for email. Do not edit.', 0)
) v (SettingKey, SettingValue, DataType, Description, IsEditableByAdmin)
WHERE NOT EXISTS (SELECT 1 FROM MST_AppSetting s WHERE s.SettingKey = v.SettingKey);

COMMIT TRANSACTION;

SELECT MenuCode, IsVisible, IsActive FROM MST_Menu WHERE MenuCode = N'CUST_NOTIFICATIONS';
SELECT SettingKey, SettingValue, IsEditableByAdmin FROM MST_AppSetting WHERE SettingKey LIKE N'Notifications.CustomerEmail%';
