-- Adds Wallet.LowBalanceThreshold (Settings -> Billing), default Rs 100.
-- Read by the admin wallet list (Low balance filter, amber highlight, summary card)
-- and meant for any future low-balance reminder. Data only; safe to run more than once.
-- UAT runs with Database__SeedOnStartup=false, so the seeder will not add this row.
--   sqlcmd -S localhost -d RegalisWorkSpace_UAT -E -C -I -b -i 20260928_WalletLowBalanceSetting.sql

INSERT INTO MST_AppSetting (SettingKey, SettingValue, SettingGroup, DataType, Description,
                            IsEditableByAdmin, IsSecret, CreatedDate, IsActive, IsDeleted)
SELECT N'Wallet.LowBalanceThreshold', N'100', N'Billing', N'decimal',
       N'A customer wallet below this balance (Rs) is flagged as low', 1, 0, SYSUTCDATETIME(), 1, 0
WHERE NOT EXISTS (SELECT 1 FROM MST_AppSetting WHERE SettingKey = N'Wallet.LowBalanceThreshold');

SELECT SettingKey, SettingValue, SettingGroup FROM MST_AppSetting WHERE SettingKey = N'Wallet.LowBalanceThreshold';
