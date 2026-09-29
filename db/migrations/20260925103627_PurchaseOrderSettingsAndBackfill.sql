-- Purchase order module: Settings -> Billing rows, and a backfill of existing POs.
--
-- Run AFTER 20260925103626_AddPurchaseOrderWorkflow.sql (it needs the new columns).
-- UAT runs with Database__SeedOnStartup=false, so the seeder will not add these rows;
-- this script does. Safe to run more than once: each setting is inserted only when
-- its key is missing, and the backfill only touches rows that still need it.
--
-- ASCII only on purpose (see sqlcmd notes): no en-dashes or curly quotes.
--   sqlcmd -S localhost -d RegalisWorkSpace_UAT -E -C -I -b -i 20260925103627_PurchaseOrderSettingsAndBackfill.sql

SET XACT_ABORT ON;
BEGIN TRANSACTION;

DECLARE @now datetime2(3) = SYSUTCDATETIME();
DECLARE @nl nchar(1) = NCHAR(10);

DECLARE @Settings TABLE (SettingKey nvarchar(100), SettingValue nvarchar(max), DataType nvarchar(20), Description nvarchar(500));
INSERT INTO @Settings VALUES
 (N'Purchase.RequireApproval', N'true', N'bool',
  N'Purchase orders need approval: Draft, Submit, Approve, then Send'),
 (N'Purchase.TermsAndConditions',
  N'1. Goods must reach the Regalis Workspace address above on or before the expected delivery date.' + @nl +
  N'2. The supplier invoice must quote this purchase order number and the supplier GSTIN.' + @nl +
  N'3. Payment will be processed as per the agreed payment terms from the date of a correct invoice.' + @nl +
  N'4. Any damaged, short or incorrect items will be returned at the supplier''s cost.',
  N'string', N'Default purchase order terms, copied onto each new PO (editable per PO)'),
 (N'Purchase.TdsOptions',
  N'194C1|194C Contractor - individual/HUF 1%|1;194C2|194C Contractor - others 2%|2;194J|194J Professional services 10%|10;194I|194I Rent 10%|10;194Q|194Q Purchase of goods 0.1%|0.1',
  N'string', N'TDS sections deducted from supplier payments, as code|label|percent rows'),
 (N'Purchase.GstRateOptions', N'0,5,12,18,28', N'string', N'GST rates the purchase order line dropdown offers'),
 (N'Purchase.DefaultGstPercent', N'18', N'decimal', N'Default GST rate on a new purchase order line'),
 (N'Purchase.ShipmentPreferences', N'Supplier delivery,Pickup,Courier,Other', N'string',
  N'Shipment preferences offered on a purchase order, comma-separated'),
 (N'Purchase.AccountCategories',
  N'Pantry & supplies,Furniture & fixtures,Office supplies,Housekeeping supplies,Repairs & maintenance,IT & electronics,Professional services,Other',
  N'string', N'Account heads for purchase order lines, comma-separated');

INSERT INTO MST_AppSetting (SettingKey, SettingValue, SettingGroup, DataType, Description,
                            IsEditableByAdmin, IsSecret, CreatedDate, IsActive, IsDeleted)
SELECT s.SettingKey, s.SettingValue, N'Billing', s.DataType, s.Description, 1, 0, @now, 1, 0
FROM @Settings s
WHERE NOT EXISTS (SELECT 1 FROM MST_AppSetting a WHERE a.SettingKey = s.SettingKey);

-- Existing POs were priced before the CGST/SGST split existed. They were all treated
-- as intra-state, so split each line's tax in half the way the service does
-- (CGST rounded, SGST the remainder), then roll the halves up to the header.
UPDATE l SET
    l.CgstAmount = ROUND(l.TaxAmount / 2, 2),
    l.SgstAmount = l.TaxAmount - ROUND(l.TaxAmount / 2, 2)
FROM TRN_PurchaseOrderLine l
WHERE l.TaxAmount <> 0 AND l.CgstAmount = 0 AND l.SgstAmount = 0 AND l.IgstAmount = 0;

UPDATE p SET
    p.CgstAmount = x.Cgst,
    p.SgstAmount = x.Sgst
FROM TRN_PurchaseOrder p
JOIN (SELECT PurchaseOrderId, SUM(CgstAmount) Cgst, SUM(SgstAmount) Sgst
      FROM TRN_PurchaseOrderLine GROUP BY PurchaseOrderId) x ON x.PurchaseOrderId = p.PurchaseOrderId
WHERE p.TaxAmount <> 0 AND p.CgstAmount = 0 AND p.SgstAmount = 0 AND p.IgstAmount = 0;

-- Snapshot a delivery address from the PO's location, as new POs do.
UPDATE p SET p.DeliveryAddress = LEFT(CONCAT_WS(N', ',
        NULLIF(LTRIM(RTRIM(l.AddressLine1)), N''),
        NULLIF(LTRIM(RTRIM(l.AddressLine2)), N''),
        NULLIF(LTRIM(RTRIM(CONCAT_WS(N' ', NULLIF(l.City, N''), NULLIF(l.PostalCode, N'')))), N''),
        NULLIF(LTRIM(RTRIM(l.State)), N'')), 600)
FROM TRN_PurchaseOrder p
JOIN MST_Location l ON l.LocationId = p.LocationId
WHERE p.DeliveryAddress IS NULL;

-- Give every existing PO the "Created" history row the workflow now writes.
INSERT INTO TRN_PurchaseOrderStatusHistory (PurchaseOrderId, FromStatus, ToStatus, Action, ChangedByUserId, ChangedUtc, Reason)
SELECT p.PurchaseOrderId, NULL, 1, N'Created', ISNULL(p.CreatedBy, 0), p.CreatedDate, N'Recorded when the approval workflow was introduced'
FROM TRN_PurchaseOrder p
WHERE NOT EXISTS (SELECT 1 FROM TRN_PurchaseOrderStatusHistory h WHERE h.PurchaseOrderId = p.PurchaseOrderId);

COMMIT;

SELECT SettingKey, LEFT(SettingValue, 60) AS Value, SettingGroup FROM MST_AppSetting WHERE SettingKey LIKE N'Purchase.%' ORDER BY SettingKey;
SELECT PoNumber, TaxAmount, CgstAmount, SgstAmount, IgstAmount, LEFT(DeliveryAddress, 60) AS DeliveryAddress FROM TRN_PurchaseOrder;
