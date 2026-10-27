-- Adds an "Other" asset category (Assets -> Items), last in every category dropdown:
-- the Items filter, New item and Edit item. Consumable by default, code prefix OTH;
-- both can be changed under Categories & rooms. Existing items keep their category.
-- Data only; safe to run more than once.
--   sqlcmd -S localhost -d RegalisWorkSpace_UAT -E -C -I -b -i 20261025010000_OtherAssetCategory.sql

-- UX_AssetCategory_Name is a filtered index: SQL Server refuses the insert without this.
SET QUOTED_IDENTIFIER ON;

INSERT INTO MST_AssetCategory (CategoryName, DefaultKind, CodePrefix, UsefulLifeYears, DisplayOrder,
                               CreatedDate, IsActive, IsDeleted)
SELECT N'Other', 1, N'OTH', NULL, 99, SYSUTCDATETIME(), 1, 0
WHERE NOT EXISTS (SELECT 1 FROM MST_AssetCategory WHERE CategoryName = N'Other' AND IsDeleted = 0);

SELECT AssetCategoryId, CategoryName, DefaultKind, CodePrefix, DisplayOrder FROM MST_AssetCategory
WHERE CategoryName = N'Other' AND IsDeleted = 0;
