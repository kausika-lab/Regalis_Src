/* 20261023110000_DashboardBannerPhotosTrim.sql
   Removes two of the thirteen dashboard photos (23 Oct 2026): regalis-06 (the MD cabin
   with blue armchairs, "A private office of your own") and regalis-10 (the executive
   desk, "An executive cabin with a view"). The other eleven stay in their order.
   Soft-delete, idempotent. Rollback: 20261023110000_DashboardBannerPhotosTrim_Rollback.sql */
SET NOCOUNT ON;
SET XACT_ABORT ON;
BEGIN TRAN;
UPDATE MST_Banner
   SET IsActive = 0, IsDeleted = 1, DeletedDate = SYSUTCDATETIME()
 WHERE ImagePath IN (N'/samples/regalis-06.jpg', N'/samples/regalis-10.jpg') AND IsDeleted = 0;
PRINT CONCAT('Banners removed: ', @@ROWCOUNT);
COMMIT;
