/* =====================================================================================
   Regalis WorkSpace - payment data audit (quotation / invoice / payment brief, item 6)
   -------------------------------------------------------------------------------------
   READ ONLY. Every statement is a SELECT. No parameters. Run against any copy of the
   database (Dev, UAT, a restored prod backup):

     sqlcmd -S "localhost\SQLEXPRESS2025" -E -C -d <database> -W -s "|" -i audit_payments.sql

   Output never contains names, emails or phone numbers: only ids, numbers, amounts,
   statuses and dates.

   Enum values used below (Regalis.Domain.Enums, all 1-based):
     PaymentStatus   1 Pending  2 Success  3 Failed  4 Refunded  5 Cancelled
     PaymentMethod   1 Wallet  2 Upi  3 CreditCard  4 DebitCard  5 NetBanking  6 Cash  7 Other
     InvoiceStatus   1 Pending  2 Paid  3 PartiallyPaid  4 Cancelled  5 Refunded  6 Sent
     QuotationStatus 1 Draft 2 Sent 3 Viewed 4 Approved 5 Rejected 6 Expired 7 Cancelled 8 Revised
     InvoicePaymentMode 1 Cash 2 Upi 3 BankTransfer 4 Cheque 5 Card 6 Wallet 7 TdsCredit 8 WriteOff
     WalletCategory  1 Recharge 2 Booking 3 Refund 4 DayPass 5 MonthlyPass 6 ServiceOrder
                     7 SecurityDeposit 8 AdminAdjustment 9 Cancellation;  WalletDirection 1 Credit 2 Debit

   The admin Payments screen (/admin/billing/payments, BillingMoneyIn-BF1.js
   AdminPaymentsPageBF) calls GET /api/payments/register ->
   CustomerPaymentService.GetRegisterAsync (billing-finance kit). Its query is:
     TRN_Payment p  (EF global filter p.IsDeleted = 0)
       LEFT JOIN TRN_InvoicePayment ip ON ip.PaymentId = p.PaymentId
                 AND ip.ReversalOfId IS NULL AND ip.IsDeleted = 0
       LEFT JOIN MST_User c   ON c.UserId = p.CustomerId AND c.IsDeleted = 0   (name only)
       LEFT JOIN TRN_Invoice i ON i.InvoiceId = p.InvoiceId AND i.IsDeleted = 0 (number only)
     default chip source=all, no status/mode/date filter, ORDER BY PaymentId DESC, 20 per page.
     source=online : ip IS NULL AND Gateway NOT NULL AND Gateway NOT IN ('Desk','Offline')
     source=offline: ip NOT NULL OR Gateway IS NULL OR Gateway IN ('Desk','Offline')
     mode=<n>      : ip NOT NULL AND ip.Mode = n   (every payment without a receipt drops out)
     from/to       : ISNULL(p.PaidUtc, p.CreatedDate) in the IST day range
   The older grid GET /api/payments (PaymentService.GetPaymentsAsync) INNER JOINs MST_User
   (IsDeleted = 0), so it drops payments whose customer is missing or soft-deleted, while its
   totalCount does not (the pager over-counts).
   ===================================================================================== */

SET NOCOUNT ON;
SET TRANSACTION ISOLATION LEVEL READ COMMITTED;
/* Sections are separate GO batches; tables added by later kits (TRN_InvoicePayment from
   billing-finance, TRN_CashCollection from pay-later) are guarded with OBJECT_ID, so the
   script runs on older copies too. Run WITHOUT sqlcmd -b only if you want it to carry on
   past an unexpected error. */

SELECT 'DB' AS section, DB_NAME() AS database_name, CONVERT(varchar(19), SYSUTCDATETIME(), 120) AS run_utc,
       (SELECT CONVERT(varchar(19), MAX(CreatedDate), 120) FROM TRN_Payment) AS newest_payment_created_utc;

GO

/* ---------------------------------------------------------------- 1. payments by status/method */
SELECT '1a payments by status/method/gateway' AS section,
       p.Status, p.PaymentMethod, ISNULL(p.Gateway, '(null)') AS Gateway, p.IsDeleted,
       COUNT(*) AS n, SUM(p.Amount) AS amount
FROM TRN_Payment p
GROUP BY p.Status, p.PaymentMethod, p.Gateway, p.IsDeleted
ORDER BY p.Status, p.PaymentMethod, Gateway, p.IsDeleted;

IF OBJECT_ID('TRN_InvoicePayment') IS NOT NULL
BEGIN
WITH ipc AS (
    SELECT ip.PaymentId, COUNT(*) AS receipts
    FROM TRN_InvoicePayment ip
    WHERE ip.ReversalOfId IS NULL AND ip.IsDeleted = 0 AND ip.PaymentId IS NOT NULL
    GROUP BY ip.PaymentId)
SELECT '1b register screen (source=all, no filter)' AS section,
       COUNT(*)                                                         AS payment_rows_in_db,
       SUM(CASE WHEN p.IsDeleted = 0 THEN 1 ELSE 0 END)                 AS payments_shown,
       SUM(CASE WHEN p.IsDeleted = 1 THEN 1 ELSE 0 END)                 AS hidden_soft_deleted,
       SUM(CASE WHEN p.IsDeleted = 0 THEN ISNULL(ipc.receipts, 1) ELSE 0 END) AS grid_rows_incl_dup_join,
       SUM(CASE WHEN p.IsDeleted = 0 AND ipc.receipts > 1 THEN 1 ELSE 0 END)  AS payments_shown_more_than_once,
       SUM(CASE WHEN p.IsDeleted = 0 AND ipc.PaymentId IS NULL THEN 1 ELSE 0 END) AS hidden_when_mode_filter_used,
       SUM(CASE WHEN p.IsDeleted = 0 AND ipc.PaymentId IS NULL AND p.Gateway IS NOT NULL
                 AND p.Gateway NOT IN ('Desk','Offline') THEN 1 ELSE 0 END) AS in_online_chip,
       SUM(CASE WHEN p.IsDeleted = 0 AND NOT (ipc.PaymentId IS NULL AND p.Gateway IS NOT NULL
                 AND p.Gateway NOT IN ('Desk','Offline')) THEN 1 ELSE 0 END) AS in_offline_chip
FROM TRN_Payment p
LEFT JOIN ipc ON ipc.PaymentId = p.PaymentId;
END
ELSE
BEGIN
WITH ipc AS (
    SELECT CAST(NULL AS bigint) AS PaymentId, 0 AS receipts WHERE 1 = 0)
SELECT '1b register screen (source=all, no filter)' AS section,
       COUNT(*)                                                         AS payment_rows_in_db,
       SUM(CASE WHEN p.IsDeleted = 0 THEN 1 ELSE 0 END)                 AS payments_shown,
       SUM(CASE WHEN p.IsDeleted = 1 THEN 1 ELSE 0 END)                 AS hidden_soft_deleted,
       SUM(CASE WHEN p.IsDeleted = 0 THEN ISNULL(ipc.receipts, 1) ELSE 0 END) AS grid_rows_incl_dup_join,
       SUM(CASE WHEN p.IsDeleted = 0 AND ipc.receipts > 1 THEN 1 ELSE 0 END)  AS payments_shown_more_than_once,
       SUM(CASE WHEN p.IsDeleted = 0 AND ipc.PaymentId IS NULL THEN 1 ELSE 0 END) AS hidden_when_mode_filter_used,
       SUM(CASE WHEN p.IsDeleted = 0 AND ipc.PaymentId IS NULL AND p.Gateway IS NOT NULL
                 AND p.Gateway NOT IN ('Desk','Offline') THEN 1 ELSE 0 END) AS in_online_chip,
       SUM(CASE WHEN p.IsDeleted = 0 AND NOT (ipc.PaymentId IS NULL AND p.Gateway IS NOT NULL
                 AND p.Gateway NOT IN ('Desk','Offline')) THEN 1 ELSE 0 END) AS in_offline_chip
FROM TRN_Payment p
LEFT JOIN ipc ON ipc.PaymentId = p.PaymentId;
END
GO

SELECT '1c legacy /api/payments grid' AS section,
       SUM(CASE WHEN p.IsDeleted = 0 THEN 1 ELSE 0 END) AS counted_in_total,
       SUM(CASE WHEN p.IsDeleted = 0 AND u.UserId IS NOT NULL THEN 1 ELSE 0 END) AS rows_returned,
       SUM(CASE WHEN p.IsDeleted = 0 AND u.UserId IS NULL THEN 1 ELSE 0 END) AS dropped_customer_missing_or_deleted
FROM TRN_Payment p
LEFT JOIN MST_User u ON u.UserId = p.CustomerId AND u.IsDeleted = 0;
GO

IF OBJECT_ID('TRN_InvoicePayment') IS NOT NULL
BEGIN
SELECT '1d detail: not shown or shown twice' AS section, p.PaymentId, p.PaymentNumber, p.Status, p.Amount,
       CASE WHEN p.IsDeleted = 1 THEN 'soft-deleted payment (EF global filter)'
            ELSE 'more than one non-reversal receipt row joins (duplicate grid row, same React key)' END AS reason
FROM TRN_Payment p
WHERE p.IsDeleted = 1
   OR (SELECT COUNT(*) FROM TRN_InvoicePayment ip WHERE ip.PaymentId = p.PaymentId
        AND ip.ReversalOfId IS NULL AND ip.IsDeleted = 0) > 1
ORDER BY p.PaymentId;
END
ELSE
BEGIN
SELECT '1d detail: not shown or shown twice' AS section, p.PaymentId, p.PaymentNumber, p.Status, p.Amount,
       CASE WHEN p.IsDeleted = 1 THEN 'soft-deleted payment (EF global filter)'
            ELSE 'more than one non-reversal receipt row joins (duplicate grid row, same React key)' END AS reason
FROM TRN_Payment p
WHERE p.IsDeleted = 1
ORDER BY p.PaymentId;
END
GO

/* ---------------------------------------------------------------- 2. money in with no payment row
   There is no gateway order / attempt / webhook table in this schema: the gateway order id,
   payment id, signature and raw webhook body live on TRN_Payment itself (PaymentService
   CreateOrderAsync / CreateInvoiceOrderAsync / WalletAdminService.RechargeAsync write the row
   BEFORE the customer pays). The evidence of money that can exist without a successful
   payment row is therefore:                                                                */
SELECT '2a gateway body says captured, row not Success' AS section, p.PaymentId, p.PaymentNumber,
       p.Status, p.Amount, p.GatewayOrderId, p.GatewayPaymentId, p.PaidUtc, p.CreatedDate
FROM TRN_Payment p
WHERE p.Status <> 2 AND p.IsDeleted = 0 AND p.RawGatewayResponse IS NOT NULL
  AND (p.RawGatewayResponse LIKE '%"status":"captured"%' OR p.RawGatewayResponse LIKE '%payment.captured%'
       OR p.RawGatewayResponse LIKE '%order.paid%')
ORDER BY p.PaymentId;

SELECT '2b wallet recharge credit with no successful payment' AS section, w.WalletTransactionId,
       w.TransactionNumber, w.CustomerId, w.Amount, w.TransactionUtc, w.PaymentId,
       CASE WHEN w.PaymentId IS NULL THEN 'no PaymentId'
            WHEN p.PaymentId IS NULL THEN 'PaymentId points to no row'
            WHEN p.IsDeleted = 1 THEN 'payment soft-deleted'
            ELSE 'payment status ' + CAST(p.Status AS varchar(3)) END AS reason
FROM TRN_WalletTransaction w
LEFT JOIN TRN_Payment p ON p.PaymentId = w.PaymentId
WHERE w.Direction = 1 AND w.Category = 1 AND w.IsDeleted = 0
  AND (p.PaymentId IS NULL OR p.IsDeleted = 1 OR p.Status <> 2)
ORDER BY w.WalletTransactionId;

SELECT '2c successful recharge payment with no wallet credit' AS section, p.PaymentId, p.PaymentNumber,
       p.CustomerId, p.Amount, p.Gateway, p.IsSignatureVerified, p.PaidUtc
FROM TRN_Payment p
WHERE p.Status = 2 AND p.IsDeleted = 0 AND p.InvoiceId IS NULL AND p.BookingId IS NULL
  AND p.CustomerPassId IS NULL AND p.ServiceOrderId IS NULL
  AND NOT EXISTS (SELECT 1 FROM TRN_WalletTransaction w WHERE w.PaymentId = p.PaymentId AND w.Direction = 1)
ORDER BY p.PaymentId;

IF OBJECT_ID('TRN_CashCollection') IS NOT NULL
BEGIN
SELECT '2d cash collection (recorded) with no payment' AS section, c.CashCollectionId, c.ReceiptNumber,
       c.InvoiceId, c.BookingId, c.CustomerPassId, c.Amount, c.CollectedUtc, c.PaymentId
FROM TRN_CashCollection c
LEFT JOIN TRN_Payment p ON p.PaymentId = c.PaymentId AND p.IsDeleted = 0
WHERE c.Status = 1 AND c.IsDeleted = 0 AND p.PaymentId IS NULL
ORDER BY c.CashCollectionId;
END
ELSE
BEGIN
SELECT '2d cash collection (recorded) with no payment' AS section, 'TRN_CashCollection not present in this database (kit not installed)' AS note;
END
GO

IF OBJECT_ID('TRN_InvoicePayment') IS NOT NULL
BEGIN
SELECT '2e receipt (TRN_InvoicePayment) with no payment' AS section, ip.InvoicePaymentId, ip.ReceiptNumber,
       ip.InvoiceId, ip.Amount, ip.Mode, ip.PaidOn, ip.PaymentId
FROM TRN_InvoicePayment ip
LEFT JOIN TRN_Payment p ON p.PaymentId = ip.PaymentId AND p.IsDeleted = 0
WHERE ip.IsDeleted = 0 AND ip.ReversalOfId IS NULL AND p.PaymentId IS NULL
  AND ip.Mode NOT IN (7, 8)   -- TDS credit / write-off receipts have no payment by design
ORDER BY ip.InvoicePaymentId;
END
ELSE
BEGIN
SELECT '2e receipt (TRN_InvoicePayment) with no payment' AS section, 'TRN_InvoicePayment not present in this database (kit not installed)' AS note;
END
GO

IF OBJECT_ID('TRN_BankTransaction') IS NOT NULL
BEGIN
SELECT '2f bank credit matched to a missing payment' AS section, b.BankTransactionId, b.TransactionDate,
       b.Amount, b.MatchedPaymentId
FROM TRN_BankTransaction b
LEFT JOIN TRN_Payment p ON p.PaymentId = b.MatchedPaymentId AND p.IsDeleted = 0
WHERE b.IsDeleted = 0 AND b.MatchedPaymentId IS NOT NULL AND p.PaymentId IS NULL
ORDER BY b.BankTransactionId;
END
ELSE
BEGIN
SELECT '2f bank credit matched to a missing payment' AS section, 'TRN_BankTransaction not present in this database (kit not installed)' AS note;
END
GO

SELECT '2 summary (core tables; see 2d/2e/2f for kit tables)' AS section,
  (SELECT COUNT(*) FROM TRN_Payment p WHERE p.Status <> 2 AND p.IsDeleted = 0 AND p.RawGatewayResponse IS NOT NULL
     AND (p.RawGatewayResponse LIKE '%"status":"captured"%' OR p.RawGatewayResponse LIKE '%payment.captured%'
          OR p.RawGatewayResponse LIKE '%order.paid%')) AS captured_not_success,
  (SELECT COUNT(*) FROM TRN_WalletTransaction w LEFT JOIN TRN_Payment p ON p.PaymentId = w.PaymentId
     WHERE w.Direction = 1 AND w.Category = 1 AND w.IsDeleted = 0
       AND (p.PaymentId IS NULL OR p.IsDeleted = 1 OR p.Status <> 2)) AS recharge_credit_no_success_payment,
  (SELECT COUNT(*) FROM TRN_Payment p WHERE p.Status = 2 AND p.IsDeleted = 0 AND p.InvoiceId IS NULL
     AND p.BookingId IS NULL AND p.CustomerPassId IS NULL AND p.ServiceOrderId IS NULL
     AND NOT EXISTS (SELECT 1 FROM TRN_WalletTransaction w WHERE w.PaymentId = p.PaymentId AND w.Direction = 1)) AS success_recharge_no_credit;
GO

/* ---------------------------------------------------------------- 3. stored paid vs computed
   TRN_Invoice stores PaidAmount, Status, PaidUtc, PaymentId; OutstandingAmount is a computed
   column (TotalAmount - PaidAmount). Computed paid = sum of Success, non-deleted payments that
   point at the invoice (p.InvoiceId), plus the invoice's own PaymentId when that payment has no
   InvoiceId (older booking/pass rows), plus TDS-credit (7) and short-payment write-off (8)
   receipts, which move PaidAmount with no TRN_Payment row (BankMatchService.ReceiptAsync).
   Expected status: Cancelled / Refunded are left alone;
   otherwise Paid if computed >= total, PartiallyPaid if > 0, else Pending (or Sent if it was Sent). */
IF OBJECT_ID('TRN_InvoicePayment') IS NOT NULL
BEGIN
WITH pay AS (
    SELECT i.InvoiceId,
           SUM(p.Amount) AS success_sum, COUNT(*) AS success_count
    FROM TRN_Invoice i
    JOIN TRN_Payment p ON p.Status = 2 AND p.IsDeleted = 0
         AND (p.InvoiceId = i.InvoiceId OR (p.InvoiceId IS NULL AND p.PaymentId = i.PaymentId))
    GROUP BY i.InvoiceId),
rcp AS (
    SELECT ip.InvoiceId, SUM(ip.Amount) AS receipt_sum,
           SUM(CASE WHEN ip.PaymentId IS NULL AND ip.Mode IN (7, 8) THEN ip.Amount ELSE 0 END) AS non_cash_sum
    FROM TRN_InvoicePayment ip WHERE ip.IsDeleted = 0 GROUP BY ip.InvoiceId),
calc AS (
    SELECT i.InvoiceId, i.InvoiceNumber, i.Status, i.TotalAmount, i.PaidAmount, i.OutstandingAmount,
           i.PaidUtc, i.PaymentId, i.BookingId, i.CustomerPassId, i.QuotationId,
           ISNULL(pay.success_sum, 0) + ISNULL(rcp.non_cash_sum, 0) AS computed_paid,
           ISNULL(pay.success_count, 0) AS success_count, rcp.receipt_sum,
           CASE WHEN i.Status IN (4, 5) THEN i.Status
                WHEN ISNULL(pay.success_sum, 0) + ISNULL(rcp.non_cash_sum, 0) >= i.TotalAmount AND i.TotalAmount > 0 THEN 2
                WHEN ISNULL(pay.success_sum, 0) + ISNULL(rcp.non_cash_sum, 0) > 0 THEN 3
                WHEN i.Status = 6 THEN 6 ELSE 1 END AS expected_status
    FROM TRN_Invoice i
    LEFT JOIN pay ON pay.InvoiceId = i.InvoiceId
    LEFT JOIN rcp ON rcp.InvoiceId = i.InvoiceId
    WHERE i.IsDeleted = 0)
SELECT '3a invoice stored vs computed' AS section, InvoiceId, InvoiceNumber, Status AS stored_status,
       expected_status, TotalAmount, PaidAmount AS stored_paid, computed_paid,
       TotalAmount - computed_paid AS computed_outstanding, OutstandingAmount AS stored_outstanding,
       receipt_sum, success_count, CASE WHEN PaidUtc IS NULL THEN 0 ELSE 1 END AS has_paid_utc,
       BookingId, CustomerPassId, QuotationId,
       (SELECT COUNT(*) FROM TRN_AuditLog a WHERE a.EntityName = 'Invoice' AND a.EntityId = CAST(calc.InvoiceId AS nvarchar(40))
          AND a.Action = 'UPDATE') AS audited_updates,
       CASE WHEN Status IN (4,5) AND computed_paid <> PaidAmount THEN 'cancelled/refunded invoice: paid differs (report only)'
            WHEN computed_paid <> PaidAmount THEN 'paid amount differs'
            WHEN expected_status <> Status THEN 'status differs'
            ELSE 'paid but no PaidUtc' END AS mismatch
FROM calc
WHERE computed_paid <> PaidAmount OR expected_status <> Status OR (Status = 2 AND PaidUtc IS NULL)
ORDER BY InvoiceId;
END
ELSE
BEGIN
WITH pay AS (
    SELECT i.InvoiceId,
           SUM(p.Amount) AS success_sum, COUNT(*) AS success_count
    FROM TRN_Invoice i
    JOIN TRN_Payment p ON p.Status = 2 AND p.IsDeleted = 0
         AND (p.InvoiceId = i.InvoiceId OR (p.InvoiceId IS NULL AND p.PaymentId = i.PaymentId))
    GROUP BY i.InvoiceId),
rcp AS (
    SELECT CAST(NULL AS bigint) AS InvoiceId, CAST(NULL AS decimal(18,2)) AS receipt_sum,
           CAST(0 AS decimal(18,2)) AS non_cash_sum WHERE 1 = 0),
calc AS (
    SELECT i.InvoiceId, i.InvoiceNumber, i.Status, i.TotalAmount, i.PaidAmount, i.OutstandingAmount,
           i.PaidUtc, i.PaymentId, i.BookingId, i.CustomerPassId, i.QuotationId,
           ISNULL(pay.success_sum, 0) + ISNULL(rcp.non_cash_sum, 0) AS computed_paid,
           ISNULL(pay.success_count, 0) AS success_count, rcp.receipt_sum,
           CASE WHEN i.Status IN (4, 5) THEN i.Status
                WHEN ISNULL(pay.success_sum, 0) + ISNULL(rcp.non_cash_sum, 0) >= i.TotalAmount AND i.TotalAmount > 0 THEN 2
                WHEN ISNULL(pay.success_sum, 0) + ISNULL(rcp.non_cash_sum, 0) > 0 THEN 3
                WHEN i.Status = 6 THEN 6 ELSE 1 END AS expected_status
    FROM TRN_Invoice i
    LEFT JOIN pay ON pay.InvoiceId = i.InvoiceId
    LEFT JOIN rcp ON rcp.InvoiceId = i.InvoiceId
    WHERE i.IsDeleted = 0)
SELECT '3a invoice stored vs computed' AS section, InvoiceId, InvoiceNumber, Status AS stored_status,
       expected_status, TotalAmount, PaidAmount AS stored_paid, computed_paid,
       TotalAmount - computed_paid AS computed_outstanding, OutstandingAmount AS stored_outstanding,
       receipt_sum, success_count, CASE WHEN PaidUtc IS NULL THEN 0 ELSE 1 END AS has_paid_utc,
       BookingId, CustomerPassId, QuotationId,
       (SELECT COUNT(*) FROM TRN_AuditLog a WHERE a.EntityName = 'Invoice' AND a.EntityId = CAST(calc.InvoiceId AS nvarchar(40))
          AND a.Action = 'UPDATE') AS audited_updates,
       CASE WHEN Status IN (4,5) AND computed_paid <> PaidAmount THEN 'cancelled/refunded invoice: paid differs (report only)'
            WHEN computed_paid <> PaidAmount THEN 'paid amount differs'
            WHEN expected_status <> Status THEN 'status differs'
            ELSE 'paid but no PaidUtc' END AS mismatch
FROM calc
WHERE computed_paid <> PaidAmount OR expected_status <> Status OR (Status = 2 AND PaidUtc IS NULL)
ORDER BY InvoiceId;
END
GO

/* ---------------------------------------------------------------- 4. duplicates */
SELECT '4a same GatewayOrderId on several rows' AS section, p.GatewayOrderId, COUNT(*) AS n,
       SUM(CASE WHEN p.Status = 2 THEN 1 ELSE 0 END) AS success_n,
       STRING_AGG(CAST(p.PaymentId AS varchar(20)), ',') AS payment_ids
FROM TRN_Payment p WHERE p.GatewayOrderId IS NOT NULL AND p.IsDeleted = 0
GROUP BY p.GatewayOrderId HAVING COUNT(*) > 1;

SELECT '4b same GatewayPaymentId on several rows' AS section, p.GatewayPaymentId, COUNT(*) AS n,
       STRING_AGG(CAST(p.PaymentId AS varchar(20)), ',') AS payment_ids
FROM TRN_Payment p WHERE p.GatewayPaymentId IS NOT NULL
GROUP BY p.GatewayPaymentId HAVING COUNT(*) > 1;

/* Two equal successful payments on one invoice minutes apart are a duplicate only when the
   invoice's successful payments add up to more than its total; pay-later desk collections are
   often split into equal halves (CashCollectionService.RecordAsync, booking or pass). */
SELECT '4c same invoice+amount success within 10 min' AS section, a.InvoiceId, a.PaymentId AS first_payment_id,
       b.PaymentId AS second_payment_id, a.Amount, a.PaymentMethod AS first_method, b.PaymentMethod AS second_method,
       DATEDIFF(SECOND, ISNULL(a.PaidUtc, a.CreatedDate), ISNULL(b.PaidUtc, b.CreatedDate)) AS seconds_apart,
       i.TotalAmount AS invoice_total, t.success_sum,
       CASE WHEN t.success_sum > i.TotalAmount THEN 'DUPLICATE: success sum exceeds invoice total'
            ELSE 'split payment (sum within total) - not a duplicate' END AS verdict
FROM TRN_Payment a
JOIN TRN_Payment b ON b.InvoiceId = a.InvoiceId AND b.Amount = a.Amount AND b.PaymentId > a.PaymentId
JOIN TRN_Invoice i ON i.InvoiceId = a.InvoiceId
CROSS APPLY (SELECT SUM(x.Amount) AS success_sum FROM TRN_Payment x
             WHERE x.InvoiceId = a.InvoiceId AND x.Status = 2 AND x.IsDeleted = 0) t
WHERE a.InvoiceId IS NOT NULL AND a.Status = 2 AND b.Status = 2 AND a.IsDeleted = 0 AND b.IsDeleted = 0
  AND ABS(DATEDIFF(SECOND, ISNULL(a.PaidUtc, a.CreatedDate), ISNULL(b.PaidUtc, b.CreatedDate))) <= 600
ORDER BY a.InvoiceId;

SELECT '4d same booking+amount success within 10 min' AS section, a.BookingId, a.PaymentId AS first_payment_id,
       b.PaymentId AS second_payment_id, a.Amount,
       DATEDIFF(SECOND, ISNULL(a.PaidUtc, a.CreatedDate), ISNULL(b.PaidUtc, b.CreatedDate)) AS seconds_apart,
       bk.TotalAmount AS booking_total, t.success_sum,
       CASE WHEN t.success_sum > bk.TotalAmount THEN 'DUPLICATE: success sum exceeds booking total'
            ELSE 'split payment (sum within total) - not a duplicate' END AS verdict
FROM TRN_Payment a
JOIN TRN_Payment b ON b.BookingId = a.BookingId AND b.Amount = a.Amount AND b.PaymentId > a.PaymentId
JOIN TRN_Booking bk ON bk.BookingId = a.BookingId
CROSS APPLY (SELECT SUM(x.Amount) AS success_sum FROM TRN_Payment x
             WHERE x.BookingId = a.BookingId AND x.Status = 2 AND x.IsDeleted = 0) t
WHERE a.BookingId IS NOT NULL AND a.Status = 2 AND b.Status = 2 AND a.IsDeleted = 0 AND b.IsDeleted = 0
  AND ABS(DATEDIFF(SECOND, ISNULL(a.PaidUtc, a.CreatedDate), ISNULL(b.PaidUtc, b.CreatedDate))) <= 600
ORDER BY a.BookingId;

IF OBJECT_ID('TRN_InvoicePayment') IS NOT NULL
BEGIN
SELECT '4e same receipt invoice+amount+reference' AS section, a.InvoiceId, a.InvoicePaymentId AS first_receipt_id,
       b.InvoicePaymentId AS second_receipt_id, a.Amount, a.Mode
FROM TRN_InvoicePayment a
JOIN TRN_InvoicePayment b ON b.InvoiceId = a.InvoiceId AND b.Amount = a.Amount
     AND ISNULL(b.Reference, '') = ISNULL(a.Reference, '') AND b.InvoicePaymentId > a.InvoicePaymentId
WHERE a.IsDeleted = 0 AND b.IsDeleted = 0 AND a.ReversalOfId IS NULL AND b.ReversalOfId IS NULL
  AND a.IsReversed = 0 AND b.IsReversed = 0
  AND ABS(DATEDIFF(SECOND, a.RecordedUtc, b.RecordedUtc)) <= 600
ORDER BY a.InvoiceId;
END
ELSE
BEGIN
SELECT '4e same receipt invoice+amount+reference' AS section, 'TRN_InvoicePayment not present in this database (kit not installed)' AS note;
END
GO

/* ---------------------------------------------------------------- 5. broken links */
SELECT '5a payments: link summary' AS section,
  SUM(f.no_inv) AS no_invoice_total,
  SUM(f.topup) AS no_invoice_wallet_or_desk_topup,
  SUM(f.bp_noinv) AS success_for_booking_pass_no_invoice_link,
  SUM(f.inv_missing) AS invoice_missing_or_deleted,
  SUM(f.cust_missing) AS customer_missing,
  SUM(f.cust_deleted) AS customer_soft_deleted,
  SUM(f.cust_differs) AS customer_differs_from_invoice
FROM (
  SELECT
    CASE WHEN p.InvoiceId IS NULL THEN 1 ELSE 0 END AS no_inv,
    CASE WHEN p.InvoiceId IS NULL AND p.BookingId IS NULL AND p.CustomerPassId IS NULL AND p.ServiceOrderId IS NULL THEN 1 ELSE 0 END AS topup,
    CASE WHEN p.InvoiceId IS NULL AND p.Status = 2 AND (p.BookingId IS NOT NULL OR p.CustomerPassId IS NOT NULL OR p.ServiceOrderId IS NOT NULL)
              AND NOT EXISTS (SELECT 1 FROM TRN_Invoice i WHERE i.PaymentId = p.PaymentId) THEN 1 ELSE 0 END AS bp_noinv,
    CASE WHEN p.InvoiceId IS NOT NULL AND NOT EXISTS (SELECT 1 FROM TRN_Invoice i WHERE i.InvoiceId = p.InvoiceId AND i.IsDeleted = 0) THEN 1 ELSE 0 END AS inv_missing,
    CASE WHEN NOT EXISTS (SELECT 1 FROM MST_User u WHERE u.UserId = p.CustomerId) THEN 1 ELSE 0 END AS cust_missing,
    CASE WHEN EXISTS (SELECT 1 FROM MST_User u WHERE u.UserId = p.CustomerId AND u.IsDeleted = 1) THEN 1 ELSE 0 END AS cust_deleted,
    CASE WHEN EXISTS (SELECT 1 FROM TRN_Invoice i WHERE i.InvoiceId = p.InvoiceId AND i.CustomerId <> p.CustomerId) THEN 1 ELSE 0 END AS cust_differs
  FROM TRN_Payment p WHERE p.IsDeleted = 0) f;

SELECT '5b detail: payment link problems' AS section, p.PaymentId, p.PaymentNumber, p.Status, p.Amount,
       p.InvoiceId, p.BookingId, p.CustomerPassId,
       CASE WHEN NOT EXISTS (SELECT 1 FROM MST_User u WHERE u.UserId = p.CustomerId) THEN 'customer missing'
            WHEN EXISTS (SELECT 1 FROM MST_User u WHERE u.UserId = p.CustomerId AND u.IsDeleted = 1) THEN 'customer soft-deleted'
            WHEN p.InvoiceId IS NOT NULL AND NOT EXISTS (SELECT 1 FROM TRN_Invoice i WHERE i.InvoiceId = p.InvoiceId AND i.IsDeleted = 0) THEN 'invoice missing/deleted'
            WHEN EXISTS (SELECT 1 FROM TRN_Invoice i WHERE i.InvoiceId = p.InvoiceId AND i.CustomerId <> p.CustomerId) THEN 'customer differs from invoice'
            ELSE 'success booking/pass payment, no invoice points to it' END AS problem
FROM TRN_Payment p
WHERE p.IsDeleted = 0 AND (
      NOT EXISTS (SELECT 1 FROM MST_User u WHERE u.UserId = p.CustomerId AND u.IsDeleted = 0)
   OR (p.InvoiceId IS NOT NULL AND NOT EXISTS (SELECT 1 FROM TRN_Invoice i WHERE i.InvoiceId = p.InvoiceId AND i.IsDeleted = 0))
   OR EXISTS (SELECT 1 FROM TRN_Invoice i WHERE i.InvoiceId = p.InvoiceId AND i.CustomerId <> p.CustomerId)
   OR (p.InvoiceId IS NULL AND p.Status = 2 AND (p.BookingId IS NOT NULL OR p.CustomerPassId IS NOT NULL OR p.ServiceOrderId IS NOT NULL)
       AND NOT EXISTS (SELECT 1 FROM TRN_Invoice i WHERE i.PaymentId = p.PaymentId)))
ORDER BY p.PaymentId;

SELECT '5c invoices without customer' AS section, i.InvoiceId, i.InvoiceNumber, i.Status, i.CustomerId,
       CASE WHEN u.UserId IS NULL THEN 'customer missing' ELSE 'customer soft-deleted' END AS problem
FROM TRN_Invoice i LEFT JOIN MST_User u ON u.UserId = i.CustomerId
WHERE i.IsDeleted = 0 AND (u.UserId IS NULL OR u.IsDeleted = 1)
ORDER BY i.InvoiceId;

SELECT '5d invoice from quotation that is not approved' AS section, i.InvoiceId, i.InvoiceNumber,
       i.Status AS invoice_status, i.PaidAmount, i.QuotationId, q.QuotationNumber,
       q.Status AS quotation_status, CASE WHEN q.QuotationId IS NULL THEN 'quotation missing' ELSE '' END AS note
FROM TRN_Invoice i LEFT JOIN TRN_Quotation q ON q.QuotationId = i.QuotationId
WHERE i.IsDeleted = 0 AND i.QuotationId IS NOT NULL AND (q.QuotationId IS NULL OR q.Status <> 4 OR q.IsDeleted = 1)
ORDER BY i.InvoiceId;

/* Advance (security-deposit) invoices are raised by InvoiceService.CreateAdvanceFromQuotationAsync
   without QuotationId; the only link is the Notes text written there. */
WITH inv AS (
    SELECT q.QuotationId,
           (SELECT COUNT(*) FROM TRN_Invoice i WHERE i.QuotationId = q.QuotationId AND i.IsDeleted = 0) AS linked_invoices,
           (SELECT COUNT(*) FROM TRN_Invoice i WHERE i.QuotationId IS NULL AND i.IsDeleted = 0
              AND i.Notes = 'Refundable security deposit against quotation ' + q.QuotationNumber + '.') AS advance_invoices
    FROM TRN_Quotation q WHERE q.IsDeleted = 0)
SELECT '5e quotation/invoice link per quotation' AS section, q.QuotationId, q.QuotationNumber, q.Status,
       q.InvoiceId AS quotation_invoice_id, inv.linked_invoices, inv.advance_invoices, q.AdvanceInvoiceEnabled,
       CASE WHEN q.Status = 4 AND q.InvoiceId IS NULL AND inv.linked_invoices = 0 THEN 'approved, no invoice'
            WHEN inv.linked_invoices > 1 THEN 'more than one invoice carries this QuotationId'
            WHEN q.InvoiceId IS NOT NULL AND NOT EXISTS (SELECT 1 FROM TRN_Invoice i WHERE i.InvoiceId = q.InvoiceId AND i.QuotationId = q.QuotationId)
                 THEN 'quotation.InvoiceId does not point back'
            WHEN inv.linked_invoices = 1 AND q.InvoiceId IS NULL THEN 'invoice carries QuotationId, quotation.InvoiceId empty'
            WHEN q.Status = 4 AND q.AdvanceInvoiceEnabled = 1 AND inv.advance_invoices = 0 THEN 'advance enabled, no advance invoice'
            WHEN inv.advance_invoices > 1 THEN 'more than one advance invoice'
            WHEN q.Status <> 4 AND (inv.linked_invoices + inv.advance_invoices) > 0 THEN 'not approved but invoiced'
            ELSE 'ok' END AS finding
FROM TRN_Quotation q JOIN inv ON inv.QuotationId = q.QuotationId
ORDER BY q.QuotationId;

GO

/* ---------------------------------------------------------------- 6. linked to not-approved quotations */
SELECT '6 payments/invoices on not-approved quotation' AS section, i.InvoiceId, i.InvoiceNumber,
       i.Status AS invoice_status, q.QuotationId, q.Status AS quotation_status,
       p.PaymentId, p.Status AS payment_status, p.Amount
FROM TRN_Invoice i
JOIN TRN_Quotation q ON (q.QuotationId = i.QuotationId
                         OR (i.QuotationId IS NULL AND i.Notes = 'Refundable security deposit against quotation ' + q.QuotationNumber + '.'))
LEFT JOIN TRN_Payment p ON p.InvoiceId = i.InvoiceId AND p.IsDeleted = 0
WHERE i.IsDeleted = 0 AND q.Status <> 4
ORDER BY i.InvoiceId, p.PaymentId;

GO

/* ---------------------------------------------------------------- 7. displayed wrongly */
IF OBJECT_ID('TRN_InvoicePayment') IS NOT NULL
BEGIN
SELECT '7a display issues summary' AS section,
  SUM(CASE WHEN p.Status = 2 AND p.PaidUtc IS NULL THEN 1 ELSE 0 END) AS success_no_paid_date_shows_dash,
  SUM(CASE WHEN p.PaymentMethod NOT BETWEEN 1 AND 6 THEN 1 ELSE 0 END) AS method_other_or_unset,
  SUM(CASE WHEN p.Status NOT BETWEEN 1 AND 5 THEN 1 ELSE 0 END) AS status_unknown_shows_dash,
  SUM(CASE WHEN p.Amount <= 0 THEN 1 ELSE 0 END) AS zero_or_negative_amount,
  SUM(CASE WHEN p.Status = 4 AND p.RefundedAmount IS NULL THEN 1 ELSE 0 END) AS refunded_no_refund_amount,
  SUM(CASE WHEN p.Status = 2 AND p.Gateway IS NOT NULL AND p.Gateway NOT IN ('Desk','Offline') AND p.IsSignatureVerified = 0 THEN 1 ELSE 0 END) AS online_success_signature_not_verified,
  SUM(CASE WHEN p.Gateway IS NULL AND p.PaymentMethod = 2 THEN 1 ELSE 0 END) AS upi_without_gateway_listed_offline,
  SUM(CASE WHEN p.Status = 1 AND p.CreatedDate < DATEADD(DAY, -1, m.newest) THEN 1 ELSE 0 END) AS stale_pending_over_1_day,
  SUM(ISNULL(r.differs, 0)) AS receipt_amount_differs_from_payment
FROM TRN_Payment p
CROSS JOIN (SELECT MAX(CreatedDate) AS newest FROM TRN_Payment) m
LEFT JOIN (SELECT ip.PaymentId, MAX(1) AS differs FROM TRN_InvoicePayment ip JOIN TRN_Payment q ON q.PaymentId = ip.PaymentId
           WHERE ip.ReversalOfId IS NULL AND ip.IsDeleted = 0 AND ip.Amount <> q.Amount GROUP BY ip.PaymentId) r ON r.PaymentId = p.PaymentId
WHERE p.IsDeleted = 0;
END
ELSE
BEGIN
SELECT '7a display issues summary' AS section,
  SUM(CASE WHEN p.Status = 2 AND p.PaidUtc IS NULL THEN 1 ELSE 0 END) AS success_no_paid_date_shows_dash,
  SUM(CASE WHEN p.PaymentMethod NOT BETWEEN 1 AND 6 THEN 1 ELSE 0 END) AS method_other_or_unset,
  SUM(CASE WHEN p.Status NOT BETWEEN 1 AND 5 THEN 1 ELSE 0 END) AS status_unknown_shows_dash,
  SUM(CASE WHEN p.Amount <= 0 THEN 1 ELSE 0 END) AS zero_or_negative_amount,
  SUM(CASE WHEN p.Status = 4 AND p.RefundedAmount IS NULL THEN 1 ELSE 0 END) AS refunded_no_refund_amount,
  SUM(CASE WHEN p.Status = 2 AND p.Gateway IS NOT NULL AND p.Gateway NOT IN ('Desk','Offline') AND p.IsSignatureVerified = 0 THEN 1 ELSE 0 END) AS online_success_signature_not_verified,
  SUM(CASE WHEN p.Gateway IS NULL AND p.PaymentMethod = 2 THEN 1 ELSE 0 END) AS upi_without_gateway_listed_offline,
  SUM(CASE WHEN p.Status = 1 AND p.CreatedDate < DATEADD(DAY, -1, m.newest) THEN 1 ELSE 0 END) AS stale_pending_over_1_day,
  SUM(ISNULL(r.differs, 0)) AS receipt_amount_differs_from_payment
FROM TRN_Payment p
CROSS JOIN (SELECT MAX(CreatedDate) AS newest FROM TRN_Payment) m
LEFT JOIN (SELECT CAST(NULL AS bigint) AS PaymentId, 0 AS differs WHERE 1 = 0) r ON r.PaymentId = p.PaymentId
WHERE p.IsDeleted = 0;
END
GO

SELECT '7b detail: display issues' AS section, p.PaymentId, p.PaymentNumber, p.Status, p.PaymentMethod,
       ISNULL(p.Gateway, '(null)') AS Gateway, p.Amount, p.PaidUtc, p.CreatedDate, p.IsSignatureVerified,
       CONCAT(
         CASE WHEN p.Status = 2 AND p.PaidUtc IS NULL THEN 'success-no-PaidUtc; ' ELSE '' END,
         CASE WHEN p.PaymentMethod NOT BETWEEN 1 AND 6 THEN 'method-other/unset; ' ELSE '' END,
         CASE WHEN p.Status NOT BETWEEN 1 AND 5 THEN 'status-unknown; ' ELSE '' END,
         CASE WHEN p.Amount <= 0 THEN 'amount<=0; ' ELSE '' END,
         CASE WHEN p.Status = 4 AND p.RefundedAmount IS NULL THEN 'refunded-no-amount; ' ELSE '' END,
         CASE WHEN p.Status = 2 AND p.Gateway IS NOT NULL AND p.Gateway NOT IN ('Desk','Offline') AND p.IsSignatureVerified = 0 THEN 'online-success-unverified; ' ELSE '' END,
         CASE WHEN p.Status = 1 AND p.CreatedDate < DATEADD(DAY, -1, (SELECT MAX(CreatedDate) FROM TRN_Payment)) THEN 'stale-pending; ' ELSE '' END) AS issues
FROM TRN_Payment p
WHERE p.IsDeleted = 0 AND (
      (p.Status = 2 AND p.PaidUtc IS NULL) OR p.PaymentMethod NOT BETWEEN 1 AND 6 OR p.Status NOT BETWEEN 1 AND 5
   OR p.Amount <= 0 OR (p.Status = 4 AND p.RefundedAmount IS NULL)
   OR (p.Status = 2 AND p.Gateway IS NOT NULL AND p.Gateway NOT IN ('Desk','Offline') AND p.IsSignatureVerified = 0)
   OR (p.Status = 1 AND p.CreatedDate < DATEADD(DAY, -1, (SELECT MAX(CreatedDate) FROM TRN_Payment))))
ORDER BY p.PaymentId;

SELECT '7c invoices displayed wrongly' AS section, i.InvoiceId, i.InvoiceNumber, i.Status, i.TotalAmount, i.PaidAmount,
       CONCAT(
         CASE WHEN i.Status = 2 AND i.PaidUtc IS NULL THEN 'paid-no-PaidUtc; ' ELSE '' END,
         CASE WHEN i.Status NOT BETWEEN 1 AND 6 THEN 'status-unknown; ' ELSE '' END,
         CASE WHEN i.PaidAmount > i.TotalAmount THEN 'overpaid-negative-outstanding; ' ELSE '' END,
         CASE WHEN i.Status = 2 AND i.PaymentId IS NULL AND i.PaidAmount > 0 THEN 'paid-no-PaymentId; ' ELSE '' END,
         CASE WHEN i.PaymentId IS NOT NULL AND NOT EXISTS (SELECT 1 FROM TRN_Payment p WHERE p.PaymentId = i.PaymentId AND p.IsDeleted = 0) THEN 'PaymentId-dangling; ' ELSE '' END) AS issues
FROM TRN_Invoice i
WHERE i.IsDeleted = 0 AND ((i.Status = 2 AND i.PaidUtc IS NULL) OR i.Status NOT BETWEEN 1 AND 6 OR i.PaidAmount > i.TotalAmount
   OR (i.Status = 2 AND i.PaymentId IS NULL AND i.PaidAmount > 0)
   OR (i.PaymentId IS NOT NULL AND NOT EXISTS (SELECT 1 FROM TRN_Payment p WHERE p.PaymentId = i.PaymentId AND p.IsDeleted = 0)))
ORDER BY i.InvoiceId;
GO
