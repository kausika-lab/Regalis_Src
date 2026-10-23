/* 20261023110000_DashboardBannerPhotosTrim_Rollback.sql
   Brings back the two banners removed by 20261023110000_DashboardBannerPhotosTrim.sql.
   Their photos (frontend/samples/regalis-06.jpg, -10.jpg) were deleted from disk; restore
   them from git (commit 57ebc44) first. */
SET NOCOUNT ON;
SET XACT_ABORT ON;
BEGIN TRAN;
UPDATE MST_Banner
   SET IsActive = 1, IsDeleted = 0, DeletedBy = NULL, DeletedDate = NULL, ModifiedDate = SYSUTCDATETIME()
 WHERE ImagePath IN (N'/samples/regalis-06.jpg', N'/samples/regalis-10.jpg') AND IsDeleted = 1;
COMMIT;
