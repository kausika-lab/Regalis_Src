-- Customer UX changes (6 Nov 2026), data only; no schema change.
-- Several day passes per customer: PassService only applies the old "one pass at a time,
-- upgrade only" rule while Passes.OnePassAtATime is true. This row is added as false, so a
-- customer can buy another pass for any dates while one is still running. Each purchase is
-- its own pass at full price (no upgrade credit). Flip it to true on the Settings screen to
-- bring the old rule back without a deploy.
-- The pay-later limit (PayLater.MaxOpenBookings, unpaid bookings + passes) is not changed.
-- Idempotent; one transaction. ASCII only.
--   sqlcmd -S localhost -d RegalisWorkSpace_UAT -E -C -I -b -i 20261106130000_CustomerUxPasses.sql
-- Rollback: 20261106130000_CustomerUxPasses_Rollback.sql
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
SET NOCOUNT ON;
GO
SET XACT_ABORT ON;
BEGIN TRANSACTION;

INSERT INTO MST_AppSetting (SettingKey, SettingValue, SettingGroup, DataType, Description,
                            IsEditableByAdmin, IsSecret, CreatedDate, IsActive, IsDeleted)
SELECT N'Passes.OnePassAtATime', N'false', N'Passes', N'bool',
       N'true: a customer with a running day pass can only upgrade it to a bigger pack. false: customers can hold several day passes, each bought for its own dates.',
       1, 0, SYSUTCDATETIME(), 1, 0
WHERE NOT EXISTS (SELECT 1 FROM MST_AppSetting WHERE SettingKey = N'Passes.OnePassAtATime');

COMMIT TRANSACTION;

SELECT SettingKey, SettingValue, IsEditableByAdmin FROM MST_AppSetting WHERE SettingKey = N'Passes.OnePassAtATime';
