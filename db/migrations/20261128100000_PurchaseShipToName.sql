-- po-last-used-defaults kit (28 Nov 2026): Purchase.ShipToName.
-- The name printed in place of the location name on purchase orders (PDF "Shipping to" box,
-- PO detail / form / delivery-address picker). Blank = the location name, as before.
-- MST_Location is NOT renamed: bookings, invoices and admin screens keep the location name.
-- Safe to run more than once: the row is inserted only when the key is missing.
-- ASCII only on purpose (see sqlcmd notes).
--   sqlcmd -S localhost -d RegalisWorkSpace_UAT -E -C -I -b -i 20261128100000_PurchaseShipToName.sql

SET XACT_ABORT ON;
BEGIN TRANSACTION;

INSERT INTO MST_AppSetting (SettingKey, SettingValue, SettingGroup, DataType, Description,
                            IsEditableByAdmin, IsSecret, CreatedDate, IsActive, IsDeleted)
SELECT N'Purchase.ShipToName', N'Rupesh', N'Billing', N'string',
       N'Name printed in place of the location on purchase orders (Shipping to block); blank = location name',
       1, 0, SYSUTCDATETIME(), 1, 0
WHERE NOT EXISTS (SELECT 1 FROM MST_AppSetting WHERE SettingKey = N'Purchase.ShipToName');

COMMIT TRANSACTION;

SELECT SettingKey, SettingValue, SettingGroup, DataType FROM MST_AppSetting WHERE SettingKey = N'Purchase.ShipToName';
