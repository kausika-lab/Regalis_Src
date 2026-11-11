-- Rollback of 20261106130000_CustomerUxPasses.sql: brings back "one day pass at a time,
-- upgrade only" by setting Passes.OnePassAtATime to true. The row is kept on purpose: the
-- new PassService treats a missing row as false (several passes allowed). Passes bought in
-- the meantime stay as they are. Safe to run more than once. ASCII only.
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
SET NOCOUNT ON;
GO
SET XACT_ABORT ON;
BEGIN TRANSACTION;

UPDATE MST_AppSetting SET SettingValue = N'true', ModifiedDate = SYSUTCDATETIME()
WHERE SettingKey = N'Passes.OnePassAtATime';

INSERT INTO MST_AppSetting (SettingKey, SettingValue, SettingGroup, DataType, Description,
                            IsEditableByAdmin, IsSecret, CreatedDate, IsActive, IsDeleted)
SELECT N'Passes.OnePassAtATime', N'true', N'Passes', N'bool',
       N'true: a customer with a running day pass can only upgrade it to a bigger pack. false: customers can hold several day passes, each bought for its own dates.',
       1, 0, SYSUTCDATETIME(), 1, 0
WHERE NOT EXISTS (SELECT 1 FROM MST_AppSetting WHERE SettingKey = N'Passes.OnePassAtATime');

COMMIT TRANSACTION;

SELECT SettingKey, SettingValue FROM MST_AppSetting WHERE SettingKey = N'Passes.OnePassAtATime';
