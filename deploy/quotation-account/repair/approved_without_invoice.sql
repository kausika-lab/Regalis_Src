-- quotation-account kit (27 Nov 2026): READ-ONLY list of approved quotations that have no invoice,
-- with what Convert to invoice will do for each. Changes nothing; safe to run any number of times.
-- The repair itself is done through the app (admin quotation page > Convert to invoice), so the
-- customer is linked / created by the Customers module and the invoice is raised by the same code
-- as an approval. Do not repair with raw SQL.
SET NOCOUNT ON;

SELECT q.QuotationId,
       q.QuotationNumber,
       q.CustomerId,
       q.CustomerName,
       q.CustomerEmail,
       q.CustomerMobile,
       CASE
         WHEN q.CustomerId IS NOT NULL THEN 'Has an account: Convert to invoice raises the invoice'
         WHEN se.UserId IS NOT NULL THEN 'Email belongs to a STAFF account (' + se.FullName + '): correct the email in Convert to invoice'
         WHEN ce.UserId IS NOT NULL THEN 'Links to existing customer #' + CAST(ce.UserId AS varchar(20)) + ' by email'
         WHEN sm.UserId IS NOT NULL THEN 'Mobile belongs to a STAFF account (' + sm.FullName + '): correct the mobile'
         WHEN cm.UserId IS NOT NULL THEN 'Links to existing customer #' + CAST(cm.UserId AS varchar(20)) + ' by mobile'
         WHEN NULLIF(LTRIM(RTRIM(q.CustomerEmail)), '') IS NULL THEN 'No email: add it in Convert to invoice'
         WHEN NULLIF(LTRIM(RTRIM(q.CustomerMobile)), '') IS NULL THEN 'No mobile: a new account needs one; add it in Convert to invoice'
         ELSE 'A customer account will be created from the quotation'
       END AS WhatConvertWillDo
FROM TRN_Quotation q
OUTER APPLY (SELECT TOP 1 u.UserId, u.FullName FROM MST_User u WHERE u.IsDeleted = 0 AND u.UserType <> 1
             AND LOWER(u.Email) = LOWER(LTRIM(RTRIM(q.CustomerEmail)))) se
OUTER APPLY (SELECT TOP 1 u.UserId FROM MST_User u WHERE u.IsDeleted = 0 AND u.UserType = 1
             AND LOWER(u.Email) = LOWER(LTRIM(RTRIM(q.CustomerEmail)))) ce
OUTER APPLY (SELECT TOP 1 u.UserId, u.FullName FROM MST_User u WHERE u.IsDeleted = 0 AND u.UserType <> 1 AND u.MobileNumber <> ''
             AND RIGHT(u.MobileNumber, 10) = RIGHT(REPLACE(REPLACE(REPLACE(LTRIM(RTRIM(q.CustomerMobile)), ' ', ''), '-', ''), '+', ''), 10)) sm
OUTER APPLY (SELECT TOP 1 u.UserId FROM MST_User u WHERE u.IsDeleted = 0 AND u.UserType = 1 AND u.MobileNumber <> ''
             AND RIGHT(u.MobileNumber, 10) = RIGHT(REPLACE(REPLACE(REPLACE(LTRIM(RTRIM(q.CustomerMobile)), ' ', ''), '-', ''), '+', ''), 10)) cm
WHERE q.IsDeleted = 0
  AND q.Status = 4               -- Approved
  AND q.InvoiceId IS NULL
  AND NOT EXISTS (SELECT 1 FROM TRN_Invoice i WHERE i.QuotationId = q.QuotationId)
ORDER BY q.QuotationId;
