/* 20261023100000_DashboardBannerPhotos.sql
   Customer dashboard carousel: the Regalis photos (23 Oct 2026).

   The four stock-photo banners (/samples/hot-desks.jpg, private-cabins, meeting-rooms,
   private-office) are retired (soft-deleted) and replaced by thirteen banners, one per
   photo supplied on 23 Oct 2026, served from /samples/regalis-01.jpg .. regalis-13.jpg
   (the files ship in frontend\samples). Same window and portal as the old banners;
   every banner opens Book Workspace. Admins can edit titles under Banners.

   Idempotent: a banner is only added when no live banner uses its image.
   Rollback: 20261023100000_DashboardBannerPhotos_Rollback.sql */
SET NOCOUNT ON;
SET XACT_ABORT ON;
BEGIN TRAN;

DECLARE @now DATETIME2 = SYSUTCDATETIME();

UPDATE MST_Banner
   SET IsActive = 0, IsDeleted = 1, DeletedDate = @now
 WHERE ImagePath IN (N'/samples/hot-desks.jpg', N'/samples/private-cabins.jpg',
                     N'/samples/meeting-rooms.jpg', N'/samples/private-office.jpg')
   AND IsDeleted = 0;
PRINT CONCAT('Old banners retired: ', @@ROWCOUNT);

DECLARE @b TABLE (Ord INT NOT NULL, Img NVARCHAR(200) NOT NULL, Title NVARCHAR(200) NOT NULL, Sub NVARCHAR(400) NOT NULL);
INSERT INTO @b (Ord, Img, Title, Sub) VALUES
 (1,  N'/samples/regalis-01.jpg', N'Meeting rooms that close deals',     N'Glass-walled rooms with power at every seat, steps from your desk.'),
 (2,  N'/samples/regalis-02.jpg', N'Team spaces that grow with you',     N'Workstations for the whole team in one room, ready to move into.'),
 (3,  N'/samples/regalis-03.jpg', N'Quiet rooms for focused talks',      N'Four-seater rooms for interviews, calls and one-to-ones.'),
 (4,  N'/samples/regalis-04.jpg', N'Welcome to Regalis WorkSpace',       N'A front desk, private cabins and open desks under one roof.'),
 (5,  N'/samples/regalis-05.jpg', N'Desks ready when you are',           N'Dedicated and hot desks with power and fibre at every place.'),
 (6,  N'/samples/regalis-06.jpg', N'A private office of your own',       N'A furnished cabin with storage and seating for your visitors.'),
 (7,  N'/samples/regalis-07.jpg', N'Huddle rooms with a whiteboard',     N'Plan, sketch and decide in a room of your own.'),
 (8,  N'/samples/regalis-08.jpg', N'Private cabins for focused teams',   N'Lockable cabins with desks and a meeting corner, by the month.'),
 (9,  N'/samples/regalis-09.jpg', N'Cabins built for the way you work',  N'Desks along the wall, a table for your meetings.'),
 (10, N'/samples/regalis-10.jpg', N'An executive cabin with a view',     N'Screen, storage and a glass wall onto the board room.'),
 (11, N'/samples/regalis-11.jpg', N'Thoughtful spaces, end to end',      N'Quiet corridors, numbered rooms and storage where you need it.'),
 (12, N'/samples/regalis-12.jpg', N'A board room for big decisions',     N'Seats for the whole board, with power built into the table.'),
 (13, N'/samples/regalis-13.jpg', N'A front desk that has your back',    N'Visitors met, deliveries signed for, every working day.');

INSERT INTO MST_Banner (Title, Subtitle, ImagePath, LinkUrl, PromotionalPrice, DisplayOrder,
                        ValidFromUtc, ValidToUtc, LocationId, PortalType, CreatedBy, CreatedDate, IsActive, IsDeleted)
SELECT b.Title, b.Sub, b.Img, N'/book', NULL, b.Ord,
       DATEADD(DAY, -1, @now), DATEADD(YEAR, 5, @now), NULL, 1, NULL, @now, 1, 0
  FROM @b b
 WHERE NOT EXISTS (SELECT 1 FROM MST_Banner x WHERE x.ImagePath = b.Img AND x.IsDeleted = 0);
PRINT CONCAT('Banners added: ', @@ROWCOUNT);

COMMIT;
