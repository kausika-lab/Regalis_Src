/* =====================================================================================
   Regalis WorkSpace - payment data REPAIR (quotation / invoice / payment brief, item 6)
   -------------------------------------------------------------------------------------
   NOT EXECUTED by the audit. Review, take a backup, then run on ONE database at a time:

     sqlcmd -S "localhost\SQLEXPRESS2025" -E -C -d <database> -b -v Mode=REPAIR   -i repair_payments.sql
     sqlcmd -S "localhost\SQLEXPRESS2025" -E -C -d <database> -b -v Mode=ROLLBACK -i repair_payments.sql
     sqlcmd ... -v Mode=PREVIEW -i repair_payments.sql  changes nothing, lists what REPAIR would do.
   -v Mode=... is required (no default, so the script cannot run in a mode nobody chose).

   Run audit_payments.sql before and after.

   WHAT IT DOES, PER AUDIT CATEGORY
   (a) Category 3. TRN_Invoice stores PaidAmount, Status, PaidUtc and PaymentId; OutstandingAmount
       is a computed column (TotalAmount - PaidAmount) and is never written. For every invoice
       that is not Cancelled (4) or Refunded (5) and whose stored PaidAmount or Status disagrees
       with its successful payments, the script sets:
         PaidAmount = sum of Success (2), non-deleted TRN_Payment rows with p.InvoiceId = invoice
                      (or the invoice's own PaymentId when that payment has no InvoiceId)
                      + TDS-credit / write-off receipts (TRN_InvoicePayment Mode 7/8, no PaymentId),
         Status     = Paid (2) if that covers TotalAmount, PartiallyPaid (3) if > 0, otherwise
                      Sent (6) if it was Sent, else Pending (1),
         PaidUtc    = the latest PaidUtc of those payments when Paid, otherwise NULL,
         PaymentId  = kept; filled with the first successful payment when Paid and empty;
                      cleared only when it points at a payment that is not Success.
       Skipped (report only, never written): Cancelled/Refunded invoices, and invoices whose
       payments add up to MORE than the total (overpayment needs a person, not a script).
       Every changed invoice's previous values are saved first in
       dbo.ZZ_RepairPayments_20261127 (one row per invoice, RepairTag 'REPAIR-PAY-20261127'),
       which is what ROLLBACK restores from.
       Not touched: TRN_Booking.AmountPaid / PaymentStatus and TRN_CustomerPass amounts of a
       pay-later booking or pass behind the invoice, wallets, receipts, cash drawers.
       Direct SQL bypasses the EF audit interceptor: no TRN_AuditLog row is written.

   (b) Category 2 - insert missing payment records: NOTHING TO REPAIR.
       This schema has no gateway order / attempt / webhook / settlement table. The gateway
       order id, payment id, signature and raw webhook body are stored only on TRN_Payment itself,
       and the row is written before the customer pays (PaymentService.CreateOrderAsync,
       CreateInvoiceOrderAsync, WalletAdminService.RechargeAsync). So there is no record of a
       successful gateway transaction that could exist without a TRN_Payment row, and no source
       to copy a gateway id, amount and date from. Audit on Dev (27 Nov 2026) and on the
       25 Oct 2026 production copy found 0 such cases (sections 2a-2f). This script therefore
       inserts no payment. A payment is never created from an assumption.

   Categories 4, 5, 6, 7: NOTHING TO REPAIR BY SCRIPT. Dev's equal-amount pairs are split
       pay-later payments (sum = invoice total), not duplicates; soft-deleted customers,
       the approved quotation without an invoice and stale Pending sandbox orders need a
       decision, not a data fix. See FINDINGS.md.

   On the 25 Oct 2026 production copy the audit found nothing in any category: REPAIR is a no-op
   there. On Dev (27 Nov 2026) it would change 7 invoices (20038, 20044, 20050, 20062, 30057,
   30116, 30123: Paid 177.00 with no payment -> Pending 0.00) and skip 10002 (Cancelled).
   ===================================================================================== */

SET NOCOUNT ON;
SET XACT_ABORT ON;

DECLARE @mode varchar(10) = UPPER('$(Mode)');
DECLARE @tag  varchar(40) = 'REPAIR-PAY-20261127';
IF @mode NOT IN ('PREVIEW', 'REPAIR', 'ROLLBACK')
BEGIN
    RAISERROR('Mode must be PREVIEW, REPAIR or ROLLBACK.', 16, 1);
    RETURN;
END

/* ---------------------------------------------------------------- computed figures */
IF OBJECT_ID('tempdb..#calc') IS NOT NULL DROP TABLE #calc;
CREATE TABLE #calc (
    InvoiceId bigint PRIMARY KEY, StoredStatus tinyint, TotalAmount decimal(18,2), StoredPaid decimal(18,2),
    StoredPaidUtc datetime2(3) NULL, StoredPaymentId bigint NULL,
    SuccessSum decimal(18,2), NonCashSum decimal(18,2), LastPaidUtc datetime2(3) NULL, FirstPaymentId bigint NULL);

INSERT #calc (InvoiceId, StoredStatus, TotalAmount, StoredPaid, StoredPaidUtc, StoredPaymentId, SuccessSum, NonCashSum, LastPaidUtc, FirstPaymentId)
SELECT i.InvoiceId, i.Status, i.TotalAmount, i.PaidAmount, i.PaidUtc, i.PaymentId,
       ISNULL(s.SuccessSum, 0), 0, s.LastPaidUtc, s.FirstPaymentId
FROM TRN_Invoice i
OUTER APPLY (SELECT SUM(p.Amount) AS SuccessSum, MAX(p.PaidUtc) AS LastPaidUtc, MIN(p.PaymentId) AS FirstPaymentId
             FROM TRN_Payment p
             WHERE p.Status = 2 AND p.IsDeleted = 0
               AND (p.InvoiceId = i.InvoiceId OR (p.InvoiceId IS NULL AND p.PaymentId = i.PaymentId))) s
WHERE i.IsDeleted = 0;

IF OBJECT_ID('TRN_InvoicePayment') IS NOT NULL
    UPDATE c SET NonCashSum = r.NonCashSum
    FROM #calc c
    JOIN (SELECT ip.InvoiceId, SUM(ip.Amount) AS NonCashSum
          FROM TRN_InvoicePayment ip
          WHERE ip.IsDeleted = 0 AND ip.PaymentId IS NULL AND ip.Mode IN (7, 8)
          GROUP BY ip.InvoiceId) r ON r.InvoiceId = c.InvoiceId;

IF OBJECT_ID('tempdb..#fix') IS NOT NULL DROP TABLE #fix;
SELECT c.*, c.SuccessSum + c.NonCashSum AS NewPaid,
       CAST(CASE WHEN c.SuccessSum + c.NonCashSum >= c.TotalAmount AND c.TotalAmount > 0 THEN 2
                 WHEN c.SuccessSum + c.NonCashSum > 0 THEN 3
                 WHEN c.StoredStatus = 6 THEN 6 ELSE 1 END AS tinyint) AS NewStatus
INTO #fix
FROM #calc c
WHERE c.StoredStatus NOT IN (4, 5)
  AND c.SuccessSum + c.NonCashSum <= c.TotalAmount;

DELETE FROM #fix WHERE NewPaid = StoredPaid AND NewStatus = StoredStatus;

/* ---------------------------------------------------------------- PREVIEW */
IF @mode = 'PREVIEW'
BEGIN
    SELECT 'would change' AS action, InvoiceId, StoredStatus, NewStatus, StoredPaid, NewPaid, TotalAmount
    FROM #fix ORDER BY InvoiceId;
    SELECT 'skipped, report only' AS action, InvoiceId, StoredStatus, StoredPaid, SuccessSum + NonCashSum AS ComputedPaid, TotalAmount,
           CASE WHEN StoredStatus IN (4, 5) THEN 'cancelled/refunded' ELSE 'payments exceed total' END AS reason
    FROM #calc
    WHERE (StoredStatus IN (4, 5) AND SuccessSum + NonCashSum <> StoredPaid)
       OR (StoredStatus NOT IN (4, 5) AND SuccessSum + NonCashSum > TotalAmount)
    ORDER BY InvoiceId;
    SELECT 'payments to insert' AS action, 0 AS n, 'none: no gateway transaction source exists (see header)' AS note;
    RETURN;
END

/* ---------------------------------------------------------------- REPAIR */
IF @mode = 'REPAIR'
BEGIN
    BEGIN TRANSACTION;

    IF OBJECT_ID('dbo.ZZ_RepairPayments_20261127') IS NULL
        CREATE TABLE dbo.ZZ_RepairPayments_20261127 (
            RepairTag varchar(40) NOT NULL, InvoiceId bigint NOT NULL,
            OldStatus tinyint NOT NULL, OldPaidAmount decimal(18,2) NOT NULL,
            OldPaidUtc datetime2(3) NULL, OldPaymentId bigint NULL,
            NewStatus tinyint NOT NULL, NewPaidAmount decimal(18,2) NOT NULL,
            RepairedUtc datetime2(3) NOT NULL DEFAULT SYSUTCDATETIME(),
            CONSTRAINT PK_ZZ_RepairPayments_20261127 PRIMARY KEY (RepairTag, InvoiceId));

    -- Save the old values once per invoice (a re-run never overwrites the first saved values).
    INSERT dbo.ZZ_RepairPayments_20261127 (RepairTag, InvoiceId, OldStatus, OldPaidAmount, OldPaidUtc, OldPaymentId, NewStatus, NewPaidAmount)
    SELECT @tag, f.InvoiceId, f.StoredStatus, f.StoredPaid, f.StoredPaidUtc, f.StoredPaymentId, f.NewStatus, f.NewPaid
    FROM #fix f
    WHERE NOT EXISTS (SELECT 1 FROM dbo.ZZ_RepairPayments_20261127 z WHERE z.RepairTag = @tag AND z.InvoiceId = f.InvoiceId);

    UPDATE i SET
        i.PaidAmount = f.NewPaid,
        i.Status     = f.NewStatus,
        i.PaidUtc    = CASE WHEN f.NewStatus = 2 THEN ISNULL(f.LastPaidUtc, i.PaidUtc) ELSE NULL END,
        i.PaymentId  = CASE WHEN f.NewStatus = 2 AND i.PaymentId IS NULL THEN f.FirstPaymentId
                            WHEN i.PaymentId IS NOT NULL AND NOT EXISTS (SELECT 1 FROM TRN_Payment p
                                 WHERE p.PaymentId = i.PaymentId AND p.Status = 2 AND p.IsDeleted = 0) THEN NULL
                            ELSE i.PaymentId END,
        i.ModifiedDate = SYSUTCDATETIME()
    FROM TRN_Invoice i
    JOIN #fix f ON f.InvoiceId = i.InvoiceId
    WHERE i.PaidAmount = f.StoredPaid AND i.Status = f.StoredStatus;   -- unchanged since it was read

    SELECT 'repaired' AS action, z.InvoiceId, z.OldStatus, z.NewStatus, z.OldPaidAmount, z.NewPaidAmount
    FROM dbo.ZZ_RepairPayments_20261127 z JOIN #fix f ON f.InvoiceId = z.InvoiceId
    WHERE z.RepairTag = @tag ORDER BY z.InvoiceId;

    -- (b) Missing payment records: nothing to insert (see header). No INSERT INTO TRN_Payment here.

    COMMIT TRANSACTION;
    RETURN;
END

/* ---------------------------------------------------------------- ROLLBACK
   Restores exactly the invoices this script changed, from the saved old values, and only where
   the invoice still holds the value the script wrote (so a payment taken since is not undone).
   Removes nothing else: the script inserted no payment rows. */
IF @mode = 'ROLLBACK'
BEGIN
    IF OBJECT_ID('dbo.ZZ_RepairPayments_20261127') IS NULL
    BEGIN
        SELECT 'rollback' AS action, 'nothing to roll back: the repair log table does not exist' AS note;
        RETURN;
    END

    BEGIN TRANSACTION;

    SELECT 'not restored: changed since the repair' AS action, z.InvoiceId, i.Status, i.PaidAmount
    FROM dbo.ZZ_RepairPayments_20261127 z JOIN TRN_Invoice i ON i.InvoiceId = z.InvoiceId
    WHERE z.RepairTag = @tag AND NOT (i.PaidAmount = z.NewPaidAmount AND i.Status = z.NewStatus);

    UPDATE i SET
        i.PaidAmount = z.OldPaidAmount,
        i.Status     = z.OldStatus,
        i.PaidUtc    = z.OldPaidUtc,
        i.PaymentId  = z.OldPaymentId,
        i.ModifiedDate = SYSUTCDATETIME()
    FROM TRN_Invoice i
    JOIN dbo.ZZ_RepairPayments_20261127 z ON z.InvoiceId = i.InvoiceId AND z.RepairTag = @tag
    WHERE i.PaidAmount = z.NewPaidAmount AND i.Status = z.NewStatus;

    DELETE z FROM dbo.ZZ_RepairPayments_20261127 z
    JOIN TRN_Invoice i ON i.InvoiceId = z.InvoiceId
    WHERE z.RepairTag = @tag AND i.PaidAmount = z.OldPaidAmount AND i.Status = z.OldStatus;

    IF NOT EXISTS (SELECT 1 FROM dbo.ZZ_RepairPayments_20261127)
        DROP TABLE dbo.ZZ_RepairPayments_20261127;

    COMMIT TRANSACTION;
END
