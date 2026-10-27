-- Rollback of 20261025010000_OtherAssetCategory.sql.
-- Soft-deletes the "Other" category, and only while no item uses it — otherwise it
-- stops with a message: move those items to another category first.
--   sqlcmd -S localhost -d RegalisWorkSpace_UAT -E -C -I -b -i 20261025010000_OtherAssetCategory_Rollback.sql

SET QUOTED_IDENTIFIER ON;

DECLARE @id INT = (SELECT AssetCategoryId FROM MST_AssetCategory WHERE CategoryName = N'Other' AND IsDeleted = 0);
IF @id IS NULL
BEGIN
  PRINT 'No active "Other" category - nothing to do.';
  RETURN;
END;

IF EXISTS (SELECT 1 FROM MST_InventoryItem WHERE AssetCategoryId = @id AND IsDeleted = 0)
   OR EXISTS (SELECT 1 FROM TRN_GoodsReceiptLine WHERE AssetCategoryId = @id)
BEGIN
  RAISERROR('Items still use the "Other" category. Move them to another category first.', 16, 1);
  RETURN;
END;

UPDATE MST_AssetCategory SET IsDeleted = 1, IsActive = 0, DeletedDate = SYSUTCDATETIME() WHERE AssetCategoryId = @id;

SELECT AssetCategoryId, CategoryName, IsActive, IsDeleted FROM MST_AssetCategory WHERE AssetCategoryId = @id;
