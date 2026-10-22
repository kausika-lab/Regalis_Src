/* 20261023030000_CabinMonthlyPrices.sql
   Monthly cabin prices (23 Oct 2026).

   Cabins are sold by the month on Book Workspace, but no cabin ever had a
   PricePerMonth, so none could be booked ("Monthly price not set"). Prices given
   on 23 Oct 2026:

     CAB-C-01  The Monarch     45,000 / month
     CAB-C-02  The Sovereign   45,000 / month
     CAB-C-03  The Throne      45,000 / month
     CAB-C-04  Crown           90,000 / month   (the eight-seater)

   Hourly and daily rates are not changed. Idempotent: re-running changes nothing.
   Rollback: 20261023030000_CabinMonthlyPrices_Rollback.sql */
SET NOCOUNT ON;
SET XACT_ABORT ON;
BEGIN TRAN;

DECLARE @prices TABLE (Code NVARCHAR(40) NOT NULL, Monthly DECIMAL(18, 2) NOT NULL);
INSERT INTO @prices (Code, Monthly) VALUES
    (N'CAB-C-01', 45000.00), (N'CAB-C-02', 45000.00), (N'CAB-C-03', 45000.00), (N'CAB-C-04', 90000.00);

UPDATE w
   SET PricePerMonth = p.Monthly, ModifiedDate = GETUTCDATE()
  FROM MST_Workspace w
  JOIN @prices p ON p.Code = w.WorkspaceCode
 WHERE w.IsDeleted = 0
   AND (w.PricePerMonth IS NULL OR w.PricePerMonth <> p.Monthly);
PRINT CONCAT('Monthly prices set: ', @@ROWCOUNT);

COMMIT;
