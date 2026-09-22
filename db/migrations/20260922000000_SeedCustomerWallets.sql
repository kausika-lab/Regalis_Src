/* 20260922000000_SeedCustomerWallets.sql
   Customer-wise wallet data for Admin -> Billing & Finance -> Wallet.

   The screen was only ever exercised with one customer (Kausika RS), so there was
   nothing to switch between. This adds seven more active customers, each with its
   own wallet and its own ledger.

   Every ledger is walked forward from zero: BalanceAfter on each row is the running
   balance, and MST_Wallet.Balance = TotalCredited - TotalDebited = the last row's
   BalanceAfter. Kausika's existing 22 rows are not touched.

   TransactionNumber is globally unique (UX_WalletTxn_Number), so the new rows
   continue the one WTX series from WTX-000023 in chronological order, and
   MST_NumberSequence is advanced past them.

   The narrations quote booking and payment numbers that are deliberately above the
   live sequences; those sequences are advanced too, so a real booking or payment
   raised later cannot be issued a number a narration already claims.

   ReferenceId / PaymentId stay NULL: the quoted bookings and payments are narration
   text, not rows, and a dangling id would be worse than none.

   The seeded logins reuse the existing demo customer's password hash, so they all
   sign in with the same password as customer@regalis.in. UAT/demo only.

   Idempotent: re-running is a no-op. Rollback: 20260922000000_SeedCustomerWallets_Rollback.sql */

/* MST_User carries filtered unique indexes (UX_User_Email, UX_User_Mobile), and SQL
   Server refuses to modify such a table unless QUOTED_IDENTIFIER and ANSI_NULLS are
   ON. sqlcmd leaves QUOTED_IDENTIFIER OFF unless it is given -I, so set it here and
   let the batch break, which is what makes it take effect for what follows. */
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO

SET NOCOUNT ON;
SET XACT_ABORT ON;

IF EXISTS (SELECT 1 FROM MST_User WHERE Email = N'rahul@regalis.in' AND IsDeleted = 0)
BEGIN
    PRINT 'SeedCustomerWallets: already applied, nothing to do.';
    RETURN;
END

DECLARE @Hash nvarchar(400) = (SELECT PasswordHash FROM MST_User WHERE Email = N'customer@regalis.in' AND IsDeleted = 0);
IF @Hash IS NULL
BEGIN
    RAISERROR ('SeedCustomerWallets: customer@regalis.in not found - cannot copy a password hash.', 16, 1);
    RETURN;
END

DECLARE @Admin bigint = (SELECT TOP 1 UserId FROM MST_User WHERE Email = N'admin@regalis.in' AND IsDeleted = 0);
IF @Admin IS NULL SET @Admin = (SELECT TOP 1 UserId FROM MST_User WHERE UserType = 2 AND IsDeleted = 0 ORDER BY UserId);

DECLARE @Now datetime2 = SYSUTCDATETIME();

/* ---------- the seed, as data ---------- */

DECLARE @Cust TABLE (
    FullName      nvarchar(150) NOT NULL,
    Email         nvarchar(256) NOT NULL PRIMARY KEY,
    MobileNumber  nvarchar(20)  NOT NULL,
    CompanyName   nvarchar(200) NULL,
    Balance       decimal(18,2) NOT NULL,
    TotalCredited decimal(18,2) NOT NULL,
    TotalDebited  decimal(18,2) NOT NULL,
    LastTxnUtc    datetime2     NOT NULL
);

INSERT INTO @Cust (FullName, Email, MobileNumber, CompanyName, Balance, TotalCredited, TotalDebited, LastTxnUtc)
VALUES
    (N'Rahul Kumar', N'rahul@regalis.in', N'9000100001', N'Northline Digital', 2875.00, 8126.00, 5251.00, '2026-09-21T11:40:00'),
    (N'Priya Sharma', N'priya@regalis.in', N'9000100002', N'Sharma Design Studio', 4620.00, 13326.00, 8706.00, '2026-09-21T05:55:00'),
    (N'Arjun R', N'arjun@regalis.in', N'9000100003', NULL, 980.00, 2854.00, 1874.00, '2026-09-20T12:10:00'),
    (N'Sneha Patel', N'sneha@regalis.in', N'9000100004', N'Patel Exports LLP', 6240.00, 22996.00, 16756.00, '2026-09-21T10:20:00'),
    (N'Vikram Rao', N'vikram@regalis.in', N'9000100005', N'Raosoft Labs', 3415.00, 8177.00, 4762.00, '2026-09-21T06:35:00'),
    (N'Ananya Singh', N'ananya@regalis.in', N'9000100006', NULL, 1760.00, 4592.00, 2832.00, '2026-09-21T05:00:00'),
    (N'Rohit Kumar', N'rohit@regalis.in', N'9000100007', N'Kumar & Co Consulting', 5085.00, 19444.00, 14359.00, '2026-09-21T03:50:00');

DECLARE @Txn TABLE (
    Seq          int            NOT NULL PRIMARY KEY,
    Email        nvarchar(256)  NOT NULL,
    TxnNumber    nvarchar(30)   NOT NULL,
    Direction    tinyint        NOT NULL,
    Category     tinyint        NOT NULL,
    Amount       decimal(18,2)  NOT NULL,
    BalanceAfter decimal(18,2)  NOT NULL,
    Narration    nvarchar(300)  NOT NULL,
    TxnUtc       datetime2      NOT NULL,
    ByAdmin      bit            NOT NULL
);

INSERT INTO @Txn (Seq, Email, TxnNumber, Direction, Category, Amount, BalanceAfter, Narration, TxnUtc, ByAdmin)
VALUES
    (23, N'priya@regalis.in', N'WTX-000023', 1, 1, 10000.00, 10000.00, N'Wallet recharge PAY-000039', '2026-09-01T03:15:00', 0),
    (24, N'sneha@regalis.in', N'WTX-000024', 1, 1, 15000.00, 15000.00, N'Wallet recharge PAY-000040', '2026-09-01T03:30:00', 0),
    (25, N'rohit@regalis.in', N'WTX-000025', 1, 1, 12000.00, 12000.00, N'Wallet recharge PAY-000048', '2026-09-01T04:45:00', 0),
    (26, N'rahul@regalis.in', N'WTX-000026', 1, 1, 5000.00, 5000.00, N'Wallet recharge PAY-000037', '2026-09-02T03:42:00', 0),
    (27, N'sneha@regalis.in', N'WTX-000027', 2, 5, 10620.00, 4380.00, N'Membership renewal: Dedicated Desk Monthly', '2026-09-02T05:10:00', 0),
    (28, N'vikram@regalis.in', N'WTX-000028', 1, 1, 4000.00, 4000.00, N'Wallet recharge PAY-000042', '2026-09-02T05:50:00', 0),
    (29, N'ananya@regalis.in', N'WTX-000029', 1, 1, 2500.00, 2500.00, N'Wallet recharge PAY-000044', '2026-09-03T03:25:00', 0),
    (30, N'rahul@regalis.in', N'WTX-000030', 2, 2, 177.00, 4823.00, N'Booking REG-000031', '2026-09-03T04:35:00', 0),
    (31, N'rohit@regalis.in', N'WTX-000031', 2, 5, 8260.00, 3740.00, N'Membership renewal: Open / Hot Desk Monthly', '2026-09-03T07:10:00', 0),
    (32, N'priya@regalis.in', N'WTX-000032', 2, 2, 177.00, 9823.00, N'Booking REG-000034', '2026-09-03T07:45:00', 0),
    (33, N'priya@regalis.in', N'WTX-000033', 2, 2, 708.00, 9115.00, N'Booking REG-000035', '2026-09-04T03:50:00', 0),
    (34, N'arjun@regalis.in', N'WTX-000034', 1, 1, 2000.00, 2000.00, N'Wallet recharge PAY-000038', '2026-09-04T04:30:00', 0),
    (35, N'vikram@regalis.in', N'WTX-000035', 2, 2, 354.00, 3646.00, N'Booking REG-000057', '2026-09-04T09:05:00', 0),
    (36, N'rohit@regalis.in', N'WTX-000036', 2, 2, 177.00, 3563.00, N'Booking REG-000066', '2026-09-05T04:20:00', 0),
    (37, N'ananya@regalis.in', N'WTX-000037', 2, 2, 177.00, 2323.00, N'Booking REG-000061', '2026-09-05T06:15:00', 0),
    (38, N'sneha@regalis.in', N'WTX-000038', 2, 2, 472.00, 3908.00, N'Booking REG-000052', '2026-09-05T06:45:00', 0),
    (39, N'rahul@regalis.in', N'WTX-000039', 2, 2, 354.00, 4469.00, N'Booking REG-000032', '2026-09-05T08:50:00', 0),
    (40, N'arjun@regalis.in', N'WTX-000040', 2, 2, 177.00, 1823.00, N'Booking REG-000047', '2026-09-06T04:05:00', 0),
    (41, N'vikram@regalis.in', N'WTX-000041', 2, 2, 177.00, 3469.00, N'Booking REG-000058', '2026-09-06T04:40:00', 0),
    (42, N'priya@regalis.in', N'WTX-000042', 2, 4, 4248.00, 4867.00, N'5-Day Pack purchase', '2026-09-06T05:35:00', 0),
    (43, N'sneha@regalis.in', N'WTX-000043', 2, 2, 177.00, 3731.00, N'Booking REG-000053', '2026-09-07T03:55:00', 0),
    (44, N'rahul@regalis.in', N'WTX-000044', 2, 4, 944.00, 3525.00, N'Single Day purchase', '2026-09-07T06:10:00', 0),
    (45, N'ananya@regalis.in', N'WTX-000045', 2, 2, 472.00, 1851.00, N'Booking REG-000062', '2026-09-07T09:50:00', 0),
    (46, N'ananya@regalis.in', N'WTX-000046', 1, 3, 472.00, 2323.00, N'Refund 100% for cancelled booking REG-000062', '2026-09-08T03:40:00', 0),
    (47, N'priya@regalis.in', N'WTX-000047', 2, 2, 354.00, 4513.00, N'Booking REG-000036', '2026-09-08T05:00:00', 0),
    (48, N'arjun@regalis.in', N'WTX-000048', 2, 2, 354.00, 1469.00, N'Booking REG-000048', '2026-09-08T08:40:00', 0),
    (49, N'rohit@regalis.in', N'WTX-000049', 2, 2, 944.00, 2619.00, N'Booking REG-000067', '2026-09-08T10:00:00', 0),
    (50, N'sneha@regalis.in', N'WTX-000050', 1, 3, 177.00, 3908.00, N'Refund 100% for cancelled booking REG-000053', '2026-09-08T12:35:00', 0),
    (51, N'arjun@regalis.in', N'WTX-000051', 1, 3, 354.00, 1823.00, N'Refund 100% for cancelled booking REG-000048', '2026-09-09T03:35:00', 0),
    (52, N'rohit@regalis.in', N'WTX-000052', 1, 3, 944.00, 3563.00, N'Refund 100% for cancelled booking REG-000067', '2026-09-09T04:35:00', 0),
    (53, N'vikram@regalis.in', N'WTX-000053', 2, 4, 944.00, 2525.00, N'Single Day purchase', '2026-09-09T07:35:00', 0),
    (54, N'priya@regalis.in', N'WTX-000054', 1, 3, 354.00, 4867.00, N'Refund 100% for cancelled booking REG-000036', '2026-09-09T10:10:00', 0),
    (55, N'rahul@regalis.in', N'WTX-000055', 2, 2, 472.00, 3053.00, N'Booking REG-000033', '2026-09-09T10:35:00', 0),
    (56, N'rahul@regalis.in', N'WTX-000056', 1, 3, 472.00, 3525.00, N'Refund 100% for cancelled booking REG-000033', '2026-09-10T04:00:00', 0),
    (57, N'sneha@regalis.in', N'WTX-000057', 1, 1, 5000.00, 8908.00, N'Wallet recharge PAY-000052', '2026-09-10T06:05:00', 0),
    (58, N'ananya@regalis.in', N'WTX-000058', 2, 2, 354.00, 1969.00, N'Booking REG-000063', '2026-09-10T08:20:00', 0),
    (59, N'priya@regalis.in', N'WTX-000059', 2, 2, 1416.00, 3451.00, N'Booking REG-000039', '2026-09-11T03:40:00', 0),
    (60, N'vikram@regalis.in', N'WTX-000060', 1, 1, 3000.00, 5525.00, N'Wallet recharge PAY-000046', '2026-09-11T04:10:00', 0),
    (61, N'rohit@regalis.in', N'WTX-000061', 1, 1, 2000.00, 5563.00, N'Wallet recharge PAY-000051', '2026-09-11T08:45:00', 0),
    (62, N'arjun@regalis.in', N'WTX-000062', 2, 4, 944.00, 879.00, N'Single Day purchase', '2026-09-11T11:15:00', 0),
    (63, N'ananya@regalis.in', N'WTX-000063', 1, 1, 1500.00, 3469.00, N'Wallet recharge PAY-000047', '2026-09-12T04:55:00', 0),
    (64, N'sneha@regalis.in', N'WTX-000064', 2, 4, 4248.00, 4660.00, N'5-Day Pack purchase', '2026-09-12T09:30:00', 0),
    (65, N'rahul@regalis.in', N'WTX-000065', 2, 2, 177.00, 3348.00, N'Booking REG-000038', '2026-09-12T13:15:00', 0),
    (66, N'rohit@regalis.in', N'WTX-000066', 2, 2, 354.00, 5209.00, N'Booking REG-000068', '2026-09-13T03:55:00', 0),
    (67, N'priya@regalis.in', N'WTX-000067', 1, 1, 2500.00, 5951.00, N'Wallet recharge PAY-000043', '2026-09-13T09:20:00', 0),
    (68, N'vikram@regalis.in', N'WTX-000068', 2, 5, 2950.00, 2575.00, N'Membership renewal: Virtual Office Monthly', '2026-09-13T11:45:00', 0),
    (69, N'sneha@regalis.in', N'WTX-000069', 2, 2, 354.00, 4306.00, N'Booking REG-000054', '2026-09-14T04:15:00', 0),
    (70, N'arjun@regalis.in', N'WTX-000070', 1, 1, 500.00, 1379.00, N'Wallet recharge PAY-000049', '2026-09-14T04:50:00', 0),
    (71, N'ananya@regalis.in', N'WTX-000071', 2, 4, 944.00, 2525.00, N'Single Day purchase', '2026-09-14T10:35:00', 0),
    (72, N'vikram@regalis.in', N'WTX-000072', 2, 2, 177.00, 2398.00, N'Booking REG-000059', '2026-09-15T05:30:00', 0),
    (73, N'rahul@regalis.in', N'WTX-000073', 1, 1, 1000.00, 4348.00, N'Wallet recharge PAY-000041', '2026-09-15T06:40:00', 0),
    (74, N'priya@regalis.in', N'WTX-000074', 2, 2, 944.00, 5007.00, N'Booking REG-000040', '2026-09-15T10:50:00', 0),
    (75, N'rohit@regalis.in', N'WTX-000075', 2, 4, 4248.00, 961.00, N'5-Day Pack purchase', '2026-09-15T11:30:00', 0),
    (76, N'vikram@regalis.in', N'WTX-000076', 1, 3, 177.00, 2575.00, N'Refund 100% for cancelled booking REG-000059', '2026-09-16T04:00:00', 0),
    (77, N'rahul@regalis.in', N'WTX-000077', 2, 5, 2950.00, 1398.00, N'Membership renewal: Virtual Office Monthly', '2026-09-16T04:30:00', 0),
    (78, N'arjun@regalis.in', N'WTX-000078', 2, 2, 177.00, 1202.00, N'Booking REG-000050', '2026-09-16T08:00:00', 0),
    (79, N'sneha@regalis.in', N'WTX-000079', 1, 1, 2500.00, 6806.00, N'Wallet recharge PAY-000055', '2026-09-16T11:00:00', 0),
    (80, N'ananya@regalis.in', N'WTX-000080', 2, 2, 177.00, 2348.00, N'Booking REG-000064', '2026-09-17T04:05:00', 0),
    (81, N'priya@regalis.in', N'WTX-000081', 2, 2, 177.00, 4830.00, N'Booking REG-000044', '2026-09-17T04:45:00', 0),
    (82, N'rohit@regalis.in', N'WTX-000082', 1, 1, 4500.00, 5461.00, N'Wallet recharge PAY-000054', '2026-09-17T06:10:00', 0),
    (83, N'sneha@regalis.in', N'WTX-000083', 2, 2, 708.00, 6098.00, N'Booking REG-000055', '2026-09-18T04:35:00', 0),
    (84, N'arjun@regalis.in', N'WTX-000084', 2, 2, 177.00, 1025.00, N'Booking REG-000051', '2026-09-18T06:20:00', 0),
    (85, N'rahul@regalis.in', N'WTX-000085', 1, 1, 1500.00, 2898.00, N'Wallet recharge PAY-000045', '2026-09-18T09:55:00', 0),
    (86, N'vikram@regalis.in', N'WTX-000086', 1, 1, 1000.00, 3575.00, N'Wallet recharge PAY-000053', '2026-09-18T11:10:00', 0),
    (87, N'rahul@regalis.in', N'WTX-000087', 2, 2, 177.00, 2721.00, N'Booking REG-000042', '2026-09-19T04:25:00', 0),
    (88, N'rohit@regalis.in', N'WTX-000088', 2, 2, 177.00, 5284.00, N'Booking REG-000069', '2026-09-19T05:05:00', 0),
    (89, N'priya@regalis.in', N'WTX-000089', 2, 2, 472.00, 4358.00, N'Booking REG-000046', '2026-09-19T07:05:00', 0),
    (90, N'sneha@regalis.in', N'WTX-000090', 2, 2, 177.00, 5921.00, N'Booking REG-000056', '2026-09-19T08:50:00', 0),
    (91, N'ananya@regalis.in', N'WTX-000091', 2, 2, 708.00, 1640.00, N'Booking REG-000065', '2026-09-19T09:15:00', 0),
    (92, N'sneha@regalis.in', N'WTX-000092', 1, 3, 177.00, 6098.00, N'Refund 100% for cancelled booking REG-000056', '2026-09-20T03:45:00', 0),
    (93, N'vikram@regalis.in', N'WTX-000093', 2, 2, 118.00, 3457.00, N'Booking REG-000060', '2026-09-20T05:20:00', 0),
    (94, N'rohit@regalis.in', N'WTX-000094', 2, 2, 118.00, 5166.00, N'Booking REG-000070', '2026-09-20T10:45:00', 0),
    (95, N'arjun@regalis.in', N'WTX-000095', 2, 8, 45.00, 980.00, N'Admin adjustment: FRONT-DESK', '2026-09-20T12:10:00', 1),
    (96, N'priya@regalis.in', N'WTX-000096', 1, 3, 472.00, 4830.00, N'Refund 100% for cancelled booking REG-000046', '2026-09-20T12:30:00', 0),
    (97, N'rohit@regalis.in', N'WTX-000097', 2, 8, 81.00, 5085.00, N'Admin adjustment: FRONT-DESK', '2026-09-21T03:50:00', 1),
    (98, N'ananya@regalis.in', N'WTX-000098', 1, 8, 120.00, 1760.00, N'Admin adjustment: FRONT-DESK', '2026-09-21T05:00:00', 1),
    (99, N'priya@regalis.in', N'WTX-000099', 2, 8, 210.00, 4620.00, N'Admin adjustment: FRONT-DESK', '2026-09-21T05:55:00', 1),
    (100, N'vikram@regalis.in', N'WTX-000100', 2, 8, 42.00, 3415.00, N'Admin adjustment: FRONT-DESK', '2026-09-21T06:35:00', 1),
    (101, N'sneha@regalis.in', N'WTX-000101', 1, 8, 142.00, 6240.00, N'Admin adjustment: FRONT-DESK', '2026-09-21T10:20:00', 1),
    (102, N'rahul@regalis.in', N'WTX-000102', 1, 8, 154.00, 2875.00, N'Admin adjustment: FRONT-DESK', '2026-09-21T11:40:00', 1);

BEGIN TRAN;

/* ---------- customers ---------- */

INSERT INTO MST_User
    (UserType, FullName, Email, MobileNumber, PasswordHash, CompanyName, Address, PhotoPath,
     DefaultLocationId, IsEmailVerified, IsMobileVerified, Status, FailedLoginCount,
     MustChangePassword, CreatedBy, CreatedDate, IsActive, IsDeleted)
SELECT
    1, c.FullName, c.Email, c.MobileNumber, @Hash, c.CompanyName, NULL, NULL,
    NULL, 1, 1, 1, 0,
    0, @Admin, @Now, 1, 0
FROM @Cust c;

INSERT INTO MAP_UserRole (UserId, RoleId, AssignedDateUtc, AssignedBy)
SELECT u.UserId, 1, @Now, @Admin
FROM MST_User u
JOIN @Cust c ON c.Email = u.Email;

/* ---------- wallets ---------- */

INSERT INTO MST_Wallet
    (CustomerId, Balance, TotalCredited, TotalDebited, LastTransactionUtc,
     CreatedBy, CreatedDate, IsActive, IsDeleted)
SELECT u.UserId, c.Balance, c.TotalCredited, c.TotalDebited, c.LastTxnUtc,
       @Admin, @Now, 1, 0
FROM MST_User u
JOIN @Cust c ON c.Email = u.Email;

/* ---------- ledgers ----------
   Inserted one row at a time in Seq order so WalletTransactionId rises with
   TransactionUtc, which keeps the grid in the same order whichever of the two
   the API sorts on. */

DECLARE @Seq int = (SELECT MIN(Seq) FROM @Txn);
DECLARE @MaxSeq int = (SELECT MAX(Seq) FROM @Txn);

WHILE @Seq <= @MaxSeq
BEGIN
    INSERT INTO TRN_WalletTransaction
        (WalletId, CustomerId, TransactionNumber, Direction, Category, Amount, BalanceAfter,
         ReferenceType, ReferenceId, PaymentId, Narration, TransactionUtc, PerformedByUserId,
         CreatedBy, CreatedDate, IsActive, IsDeleted)
    SELECT
        w.WalletId, u.UserId, t.TxnNumber, t.Direction, t.Category, t.Amount, t.BalanceAfter,
        NULL, NULL, NULL, t.Narration, t.TxnUtc,
        CASE WHEN t.ByAdmin = 1 THEN @Admin ELSE u.UserId END,
        CASE WHEN t.ByAdmin = 1 THEN @Admin ELSE u.UserId END, @Now, 1, 0
    FROM @Txn t
    JOIN MST_User u   ON u.Email = t.Email
    JOIN MST_Wallet w ON w.CustomerId = u.UserId
    WHERE t.Seq = @Seq;

    SET @Seq += 1;
END

/* ---------- reserve the numbers the seed has spent ---------- */

UPDATE MST_NumberSequence SET CurrentValue = 102   WHERE SequenceKey = 'WalletTransaction' AND CurrentValue < 102;
UPDATE MST_NumberSequence SET CurrentValue = 70 WHERE SequenceKey = 'Booking'           AND CurrentValue < 70;
UPDATE MST_NumberSequence SET CurrentValue = 55 WHERE SequenceKey = 'Payment'           AND CurrentValue < 55;

COMMIT;

/* ---------- prove the ledgers add up ---------- */

;WITH Walk AS (
    SELECT t.CustomerId,
           t.BalanceAfter,
           Running = SUM(CASE WHEN t.Direction = 1 THEN t.Amount ELSE -t.Amount END)
                     OVER (PARTITION BY t.CustomerId ORDER BY t.TransactionUtc, t.WalletTransactionId
                           ROWS UNBOUNDED PRECEDING)
    FROM TRN_WalletTransaction t
    WHERE t.IsDeleted = 0
)
SELECT Problem = 'Running balance does not match BalanceAfter',
       u.FullName, w.CustomerId, w.BalanceAfter, w.Running
FROM Walk w JOIN MST_User u ON u.UserId = w.CustomerId
WHERE w.BalanceAfter <> w.Running;

SELECT Problem = 'Wallet header does not match its ledger',
       u.FullName, wl.Balance, wl.TotalCredited, wl.TotalDebited,
       LedgerBalance = ISNULL(SUM(CASE WHEN t.Direction = 1 THEN t.Amount ELSE -t.Amount END), 0)
FROM MST_Wallet wl
JOIN MST_User u ON u.UserId = wl.CustomerId
LEFT JOIN TRN_WalletTransaction t ON t.CustomerId = wl.CustomerId AND t.IsDeleted = 0
WHERE wl.IsDeleted = 0
GROUP BY u.FullName, wl.Balance, wl.TotalCredited, wl.TotalDebited
HAVING wl.Balance <> ISNULL(SUM(CASE WHEN t.Direction = 1 THEN t.Amount ELSE -t.Amount END), 0)
    OR wl.Balance <> wl.TotalCredited - wl.TotalDebited;

SELECT u.FullName, u.Email, wl.Balance, Txns = COUNT(t.WalletTransactionId)
FROM MST_Wallet wl
JOIN MST_User u ON u.UserId = wl.CustomerId
LEFT JOIN TRN_WalletTransaction t ON t.CustomerId = wl.CustomerId AND t.IsDeleted = 0
WHERE wl.IsDeleted = 0
GROUP BY u.UserId, u.FullName, u.Email, wl.Balance
ORDER BY u.UserId;

PRINT 'SeedCustomerWallets: applied.';
