-- Rollback of 20261128100000_PurchaseShipToName.sql: removes Purchase.ShipToName, so purchase
-- orders show the location name again. Also removes the two rows the po-last-used-defaults
-- backend creates on the first PO save (Purchase.LastDeliveryLocationId / LastDeliveryAddress);
-- run it after the backend has been rolled back, or the next PO save creates them again.
-- Purchase.TermsAndConditions is left as it is (it existed before the kit).

SET XACT_ABORT ON;
BEGIN TRANSACTION;

DELETE FROM MST_AppSetting
WHERE SettingKey IN (N'Purchase.ShipToName', N'Purchase.LastDeliveryLocationId', N'Purchase.LastDeliveryAddress');

COMMIT TRANSACTION;
