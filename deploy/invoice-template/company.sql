-- Customer company name on existing invoices (02 Oct 2026). Run after the
-- 20261001185237_InvoiceCustomerCompanyName migration. ASCII only. Idempotent.
-- Undo = the migration rollback (drops the column).
SET NOCOUNT ON;
UPDATE i SET CustomerCompanyName = LTRIM(RTRIM(
    CASE WHEN i.QuotationId IS NOT NULL THEN q.CustomerCompanyName ELSE u.CompanyName END))
FROM TRN_Invoice i
JOIN MST_User u ON u.UserId = i.CustomerId
LEFT JOIN TRN_Quotation q ON q.QuotationId = i.QuotationId
WHERE i.CustomerCompanyName IS NULL
  AND ISNULL(CASE WHEN i.QuotationId IS NOT NULL THEN q.CustomerCompanyName ELSE u.CompanyName END, '') <> '';
SELECT @@ROWCOUNT AS InvoicesGivenCompanyName;
