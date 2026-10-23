/* 20261023100000_DashboardBannerPhotos_Rollback.sql
   Undoes 20261023100000_DashboardBannerPhotos.sql: the thirteen photo banners are
   retired and the four stock-photo banners come back. */
SET NOCOUNT ON;
SET XACT_ABORT ON;
BEGIN TRAN;

UPDATE MST_Banner
   SET IsActive = 0, IsDeleted = 1, DeletedDate = SYSUTCDATETIME()
 WHERE ImagePath LIKE N'/samples/regalis-[0-9][0-9].jpg' AND IsDeleted = 0;

UPDATE MST_Banner
   SET IsActive = 1, IsDeleted = 0, DeletedBy = NULL, DeletedDate = NULL, ModifiedDate = SYSUTCDATETIME()
 WHERE ImagePath IN (N'/samples/hot-desks.jpg', N'/samples/private-cabins.jpg',
                     N'/samples/meeting-rooms.jpg', N'/samples/private-office.jpg')
   AND IsDeleted = 1;

COMMIT;
