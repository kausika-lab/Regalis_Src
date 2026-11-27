# Quotation -> invoice -> payment fixes (27 Nov 2026)

Built from the tester's validated defect list (B1 to B5, B7, attempt 1) and the read-only payment audit
(item 6). Installed on the **local Dev stack only**. Not on UAT or production. Nothing committed or pushed.

**Needs the Billing & Finance kit** (its migration, backend and frontend). Two of its files are edited
by this kit (see **Files**).

Decisions taken by the user before the fix:

| Question | Decision |
|---|---|
| What an approval does | Either approval path (customer portal / email link, admin *Mark approved on behalf*) raises **one issued invoice** (not a draft), shown as *Due*. Approving or converting again returns that invoice, never a second one. |
| Invoice payment status | paid = sum of successful payments (TDS / write-off receipts, modes 7 and 8, already count and are unchanged). 0 = **Due**, between = **Partially Paid**, total or more = **Paid**. Outstanding = Total - Paid. Pending and failed payments never change Paid. Labels only: the stored status numbers stay. |
| Duplicate manual reference | Refused (409) while the first receipt stands. |
| Payments screen | Ten columns: Payment ID, Customer Name, Quotation Number, Invoice Number, Payment Date, Payment Method, Total Invoice Amount, Amount Paid, Outstanding, Payment Status. |
| Rejected quotations | Never invoiced, never paid, never listed. |
| Wallet recharge by webhook | Not touched (a wallet rule). Reported below. |
| Quotation GST split | Not touched (a GST rule). Reported below. |

**Existing behaviour is unchanged where these defects are not involved.** No price, GST, discount,
refund or wallet rule changes; no permission or row scoping changes; no data is changed by the migration.

## The items

### B1 Quotation email reported as sent when nothing was sent

- **Root cause.** `Regalis.Infrastructure.Adapters.QuotationDispatcher:SendEmailAsync` only knew the
  `Smtp` section. With `Smtp:Host` empty it returned `QuotationDeliveryResult.Ok("No mail server is
  configured - email was not sent.")`, so `QuotationService:SendAsync` logged the Email event as
  *Sent*, moved the quotation to *Sent*, and the screen showed success. Production mails through
  Microsoft Graph (`Otp__Channel=Graph`, `MicrosoftGraph__*` in the live web.config) with `Smtp:Host`
  empty, so on production quotation mail was never sent but reported sent.
- **Fix.** New `Adapters/QuotationMailDispatcher` (used by `QuotationService` only): Microsoft Graph
  when the `MicrosoftGraph` section is complete (as `Reporting.Analytics.ReportMailer` does, same 3 MB
  attachment limit with a clear message), else SMTP when `Smtp:Host` is set, else **Fail** with
  "No mail server is configured (Microsoft Graph or SMTP), so the email was not sent." Addresses are
  checked first ("'x' is not a valid email address."). Every failure is logged with its exception. The
  existing `SendAsync` failure path then does the rest: the Email event is *Failed* with the reason,
  the quotation stays where it was, and the API answers 422 with the reason. WhatsApp is unchanged.
- **Screen.** The Send popup already showed the server's message on a failure. The success toast now
  names the recipient: "Quotation sent by Email to <address>." The *To* field was already pre-filled with
  the customer's registered email (`MST_User.Email`, live for a draft, snapshotted at send). The PDF
  was already attached; it still is.
- **Proof, old vs new, on a second instance on :82** (Dev DB, `Smtp__Host` and `MicrosoftGraph__*`
  blank): old DLLs (`local-dev-backup-20261127-before-quotation-invoice\api`) answered **200**, quotation
  **Sent**, event DeliveryStatus 2 "No mail server is configured". New DLLs answered **422** with the
  reason, event **Failed** (5), quotation still **Draft**. The :82 instance was stopped afterwards.
- **Not changed (report).** The other callers of `IQuotationDispatcher` keep the old SMTP-only path:
  payment receipts (`CustomerPaymentService.EmailReceiptAsync`, which already fails loudly on a
  failure), finance reminders, ledger statements, announcements, wallet desk and admin notification
  emails. On production they have the same blind spot (no SMTP host = "Ok, not sent"). Not fixed here,
  to keep this kit to quotations.
- **Production needs configuration, not code:** either the Graph settings (already in the live
  web.config) or the `Smtp` settings. Without either, a quotation send now fails with the reason
  instead of pretending.
- **Dev history (not changed, needs a decision).** Quotation 1 event 2 (and the old-build proof on :82,
  quotation 29) say "No mail server is configured" with DeliveryStatus 2 (Sent). `TRN_QuotationEvent` is
  the append-only timeline, so these rows were **not** corrected. The tester's check "B1 failure (code
  path, Dev history)" keeps failing on them until someone decides to correct or accept them.

### B2 Approval did not raise the invoice

- **Root cause.** `QuotationService:DecideAsync` only called
  `InvoiceService.CreateAdvanceFromQuotationAsync` (when `AdvanceInvoiceEnabled`). The full invoice
  needed a manual `POST /api/invoices/from-quotation/{id}`, and a second call answered 409 "already been
  invoiced".
- **Fix.** New `IQuotationInvoicing` / `QuotationInvoicingService`, used by approval and by *Convert to
  invoice*. The invoice is built exactly as `InvoiceService.CreateFromQuotationAsync` built it (the code
  moved, unchanged): customer and company snapshot, lines, quantity, unit price, discount, CGST / SGST /
  IGST, TDS, adjustment, round-off and total; status Pending (shown *Due*), paid 0, due after
  `Invoice.DueDays`; the customer notification as before; a *Converted* event "Invoice X raised".
  - **One transaction.** `DecideAsync` saves the decision and raises the invoice in one transaction:
    both commit or neither does.
  - **Idempotent.** Approving an approved quotation again answers **200** with the quotation and its
    invoice (it used to be 422 "already approved"). Convert again answers **200** "Already invoiced as
    X" with the same invoice (it used to be 409). The database already enforces one invoice per
    quotation: `UX_Invoice_Quotation` (unique on `TRN_Invoice.QuotationId` where set). Dev had no
    duplicates; the tester's cancelled invoices 30153-30164 stay linked, so those quotations return
    their cancelled invoice and never get a second one.
  - Links kept: `TRN_Quotation.InvoiceId`, `TRN_Invoice.QuotationId` / `CustomerId`, payments by
    `InvoiceId`. Status history unchanged (Draft->Sent, Sent->Approved).
  - The advance (security deposit) invoice is unchanged: raised after the approval, as before.
  - Rejection, and approval after a rejection, are refused as before.
- **Assumption (tell the user).** A quotation typed in for someone **without a customer account** cannot
  have an invoice (`CustomerOf` refuses it, as before). Its approval is recorded as before, without an
  invoice, plus a timeline note "No invoice raised: the customer has no account". Blocking the approval
  instead would change existing behaviour.
- **Screen.** *Mark approved* now says "Recorded as approved. Invoice X raised." The page already named
  the invoice ("Invoiced as REG/INV/...").
- **Not done.** Dev quotation 5 (approved before the fix, no invoice) was **not** backfilled. Approving
  it again or *Convert to invoice* would now raise its invoice.

### B3 Rejected quotations

Checked layer by layer; with valid data nothing changes.

- Backend: `CreateFromQuotation` requires *Approved* (unchanged); approval after rejection is refused
  (unchanged); an approved quotation cannot change status afterwards. So no path links an invoice or a
  payment to a rejected quotation (audit category 6: 0 on Dev and on the production copy).
- **Added** at query level, through `InvoicePaymentRules.Billable`: the payment register
  (`CustomerPaymentService:GetRegisterAsync`), the older payments list (`PaymentService:GetPaymentsAsync`)
  and both invoice lists (`InvoiceService:GetInvoicesAsync`, `GetMyInvoicesAsync`) leave out anything
  linked to a quotation that is not *Approved*.
- **Not added (relies on the rule above):** ledgers / outstanding / ageing, GST registers, accounting
  export, finance dashboard and reminders read invoices directly. They cannot see such an invoice
  because none can exist; adding the filter to each would have meant 10 more edits in drifted files.
- Quotation screen: *Convert to invoice* is shown only for *Approved* (server flag
  `CanConvertToInvoice`); *Record payment* is on invoices only. Nothing to add.

### B4 Invoice payment status

- **(a) Duplicate reference. Root cause:** `CustomerPaymentService:RecordAsync` (billing-finance kit) had
  no duplicate check. **Fix:** inside its serializable transaction, a receipt with the same invoice +
  mode + reference that is not a reversal and not reversed is refused with **409** `DUPLICATE_RECORD`:
  "A UPI payment with reference X is already recorded on REG/INV/.... Check the payments list; reverse
  that payment first if it was wrong." Backed by the filtered unique index
  `UX_InvoicePayment_ManualReference`. Cash without a reference and wallet are not affected. Gateway
  references were already unique (`UX_Payment_GatewayPaymentId`).
- **(b) Labels. Root cause:** `BillingMoneyIn-BF1.js` `INVOICE_STATUS` said "Pending" and "Part paid".
  **Fix (label map only):** 1 Pending -> **Due**, 3 PartiallyPaid -> **Partially Paid**, 2 **Paid**;
  6 Sent -> **Due** plus a "Sent" chip (filter shows "Due (marked sent)"). The column is now *Payment
  Status*. The Record payment preview says "becomes Partially Paid".
- **Write paths, checked.** Manual receipt and reversal (`CustomerPaymentService.ApplyAsync`), browser
  verify (`PaymentService.VerifyInvoicePaymentAsync`), bank-match receipts and the TDS / write-off
  receipts already keep `PaidAmount` / `Status` in step in their transaction. The webhook did not: B7.
  Overpayment was already refused; several part payments were already allowed.

### B5 Payments screen

- **Root cause.** `CustomerPaymentService:GetRegisterAsync` rows had no quotation number, invoice total or
  outstanding, and the screen showed Payment / Mode / Reference / Against / Amount / Status / Paid.
- **Fix.** `PaymentRegisterRowDto` gains `quotationNumber`, `totalInvoiceAmount`, `outstandingAmount`
  (Total - Paid of the invoice) and `paymentStatus` (additive; existing fields kept). The older
  `GET /api/payments` (`PaymentDto`) gains `quotationNumber`, `outstandingAmount`, `paymentStatus`.
  - `paymentStatus` = the invoice's Due / Partially Paid / Paid (or Cancelled / Refunded) for a payment
    against an invoice; the payment's own Pending / Success / Failed / Refunded / Cancelled otherwise.
  - The register search also finds a quotation number.
  - The screen shows the ten columns; the payment's own status is shown next to the invoice's when the
    payment is not a success; Reversed / Reconciled chips and the actions are kept. Status, mode, source
    and date filters work as before. Online, manual (cash / UPI / card / bank / cheque / wallet) and pay
    later payments all show, as before.
- 10 columns do not fit 1366 px beside the menu; the table scrolls inside its panel, the page does not
  (checked at 1366 and 390 px).

### B7 Webhook did not move the invoice

- **Root cause.** `PaymentService:HandleWebhookAsync` marked an invoice payment *Success* without
  updating `TRN_Invoice.PaidAmount` / `Status` (only the browser verify path did).
- **Failing check first.** On :82 with the old DLLs and a webhook secret, a signed `payment.captured`
  for a Pending invoice order made the payment Success while the invoice stayed Paid 0.00 / Due.
- **Fix, invoice payments only** (payment with an invoice and no pass; booking payments already went
  through `BookingService`): `InvoicePaymentRules.SettleInvoiceWebhookAsync` does what the browser
  verify does, in one transaction: payment Success, invoice paid amount and status, a pay-later booking
  or pass behind it, the notification. More than the outstanding -> payment Failed with the verify
  path's reason, logged as an error. Failures are logged with the exception.
  - **Once only.** Webhook and verify both take an `UPDLOCK` on the payment row first and re-read it.
    The webhook marks the payment `IsSignatureVerified` (its own HMAC was checked); a later browser
    verify of the same gateway payment id with a valid signature then answers 200 without applying it
    again (before: 422 "already settled"). A repeated webhook is ignored.
- **Not changed (report, wallet rule).** A wallet recharge settled by the webhook is still only marked
  Success: the wallet is **not** credited by the webhook. Needs the user's decision.

### Not fixed: quotation GST split (needs the user)

Taxable 8,474.58 at 18 % within the state gives CGST 762.72 + SGST 762.71 with a -0.01 round-off on
the quotation (the tester's hand-worked figures expect 762.71 + 762.71). That is a GST rounding rule.
The invoice copies the quotation's figures verbatim, so it has the same split.

## Item 6: payment audit (read-only)

`audit/audit_payments.sql` (read-only) and `audit/repair_payments.sql` (**not run** anywhere, Dev
included). Dev `RegalisWorkSpace_Dev`, run 03:44 UTC 27 Nov 2026 (payments went 184 -> 191 during the
run). The production backup named in the brief is not on this PC; a substitute was used
(`RegalisWorkSpace_Dev_restored_prod.bak`, the 25 Oct production restore plus about 2 minutes of Dev),
restored read-only as `RegalisWorkSpace_ProdAudit_20261127`, audited and dropped. **Re-run
`audit_payments.sql` on a real production restore when one is available.**

How payments link: `TRN_Payment` has `CustomerId` and optional `BookingId` / `InvoiceId` /
`CustomerPassId` / `ServiceOrderId`; no quotation column. Quotation -> payment is only through
`TRN_Invoice.QuotationId` then `TRN_Payment.InvoiceId`. `OutstandingAmount` is computed (Total - Paid);
`PaidAmount` and `Status` are stored. TDS / write-off receipts (modes 7, 8) raise `PaidAmount` without a
payment row.

| # | Category | Dev | Production copy |
|---|---|---|---|
| 1 | Payments (Pending 16, Success 158, Failed 5, Refunded 6, Cancelled 6) | 191 | 1 (desk top-up, Success) |
| 1 | Not shown on the register's default view | 0 | 0 |
| 1 | Hidden when a Mode filter is chosen (the filter needs a receipt row) | 184 | 0 |
| 1 | Blank customer name / dropped by the older grid (soft-deleted test customers) | 135 | 0 |
| 2 | Successful gateway transactions without a payment record | 0 | 0 |
| 3 | Invoices whose stored paid / status differ from the ledger | 8 (20038, 20044, 20050, 20062, 30057, 30116, 30123 stored Paid 177.00 with no payment; 10002 cancelled, report only) | 0 |
| 4 | Duplicate payments | 0 (5 equal Cash pairs are pay-later pass instalments: invoices 20005, 20007, 20012, 20015, 20022) | 0 |
| 5 | Payments without an invoice | 23 (20 wallet top-ups by design, 3 never succeeded) | 0 |
| 5 | Soft-deleted customers' payments / invoices | 135 / 152 | 0 |
| 5 | Approved quotations with no invoice | 1 (quotation 5) | 0 |
| 5 | Quotations with more than one invoice | 0 | 0 |
| 6 | Payments / invoices on rejected or not-approved quotations | 0 | 0 |
| 7 | Pending checkouts that never complete | 16 (6, 10, 11, 16, 20, 21, 27, 28, 39, 40, 10056, 10079, 10102, 10113, 10122, 10154) | 0 |

The production copy has 1 payment, 0 invoices, 0 quotations, and no `TRN_InvoicePayment` /
`TRN_CashCollection` tables (Billing & Finance not deployed there).

Root causes found:

- Category 3 is test SQL, not app code: `deploy/wifi/tests/e2e_wifi.py` (about line 412) sets
  `PaidAmount = TotalAmount, Status = 2` directly; there is no audited app update.
- Blank names: test clean-up soft-deletes customers, and the screens join `db.Users`.
- The register's Mode filter only matches payments that have a receipt row (`TRN_InvoicePayment`), so
  gateway payments disappear when a mode is chosen. Not changed by this kit.
- The older `GET /api/payments` (`PaymentService.GetPaymentsAsync`) inner-joins users, so payments of
  soft-deleted customers drop out, and its total is counted before the join. Not changed by this kit.
- Quotation 5: approval never raised an invoice (B2).
- Pending payments never expire: `PaymentService.CreateOrderAsync` / `CreateInvoiceOrderAsync` and
  `WalletAdminService.RechargeAsync` insert the payment before the gateway; nothing closes abandoned
  ones. They never count as paid.
- Advance (security deposit) invoices (`InvoiceService.CreateAdvanceFromQuotationAsync`) have no
  `QuotationId`; they are linked to the quotation only through their notes.
- Webhook: invoice payment (fixed, B7) and wallet recharge (not fixed, wallet rule).

Repair: no payment records were created (there is no gateway order / webhook table to copy from, and
category 2 is 0). `repair_payments.sql` recomputes `PaidAmount` / `Status` / `PaidUtc` from successful
payments plus TDS / write-off receipts, skips cancelled, refunded and overpaid invoices, keeps the old
values in `dbo.ZZ_RepairPayments_20261127`, and `Mode=ROLLBACK` restores them. **Not run**: the 7
category 3 rows are stored-paid test artefacts, not rows backed by a real transaction.

## Database

`db/migrations/20261127120000_QuotationInvoice.sql` (+ `_Rollback.sql`). Idempotent, one transaction,
ASCII. No table, column or row changes. Stops without changing anything if `TRN_InvoicePayment` is
missing or existing rows would break an index.

- `UX_InvoicePayment_ManualReference` on `TRN_InvoicePayment (InvoiceId, Mode, Reference)` where
  `ReversalOfId IS NULL AND IsReversed = 0 AND IsDeleted = 0 AND Reference IS NOT NULL`. New.
- `UX_Invoice_Quotation` (unique `TRN_Invoice.QuotationId` where set): already in the EF model and on
  Dev; created only if missing. The rollback leaves it.

Duplicate check (run before the migration on a server):
`SELECT InvoiceId, Mode, Reference, COUNT(*) FROM TRN_InvoicePayment WHERE ReversalOfId IS NULL AND IsReversed = 0 AND IsDeleted = 0 AND Reference IS NOT NULL GROUP BY InvoiceId, Mode, Reference HAVING COUNT(*) > 1`

## API (changes only; no new route, no new permission)

| Call | Change |
|---|---|
| `POST /api/quotations/{id}/send` | Email through Graph / SMTP; no transport or a refused address -> 422 with the reason, event Failed, status unchanged. |
| `POST /api/quotations/{id}/record-decision`, `/approve` | Approval raises the invoice in the same transaction (`invoiceId`, `invoiceNumber` on the reply). Approving an approved quotation again -> 200, same invoice. |
| `POST /api/invoices/from-quotation/{id}` | 201 when raised; **200** with the existing invoice when already invoiced (was 409). |
| `POST /api/invoices/{id}/payments` | Same invoice + mode + reference while the first stands -> **409** `DUPLICATE_RECORD`. |
| `GET /api/payments/register` | New fields `quotationNumber`, `totalInvoiceAmount`, `outstandingAmount`, `paymentStatus`; search also by quotation number; rows linked to a not-approved quotation left out. |
| `GET /api/payments` | New fields `quotationNumber`, `outstandingAmount`, `paymentStatus`; same exclusion. |
| `GET /api/invoices`, `/invoices/my` | Invoices linked to a not-approved quotation left out (none exist). |
| `POST /api/payments/webhook` | Captured invoice payment moves the invoice, once. |
| `POST /api/payments/invoice-verify` | A payment already settled (webhook or earlier verify) with the same gateway payment id and a valid signature -> 200, nothing applied again (was 422). |

## Files

- **Backend, new** (`backend/src`):
  - Application: `Finance/QuotationInvoice/IQuotationInvoicing.cs`
  - Infrastructure: `Adapters/QuotationMailDispatcher.cs`,
    `Services/QuotationInvoice/{QuotationInvoicingService, InvoicePaymentRules}.cs`
- **Backend, existing:** `backend/existing-files.diff` (apply by hand on the server source):
  - `DependencyInjection.cs`: 3 registrations (concrete `QuotationDispatcher`, `QuotationMailDispatcher`,
    `IQuotationInvoicing`). `IQuotationDispatcher` still maps to the old dispatcher.
  - `QuotationService`: 2 constructor parameters; the email send line; `DecideAsync` (idempotent
    re-approve, one transaction with the invoice).
  - `InvoiceService`: 1 constructor parameter; `CreateFromQuotationAsync` delegates (its old body moved
    to `QuotationInvoicingService`, unchanged); the two list queries use `Billable`.
  - `InvoicesController`: 1 constructor parameter; convert answers 200 for an existing invoice.
  - `PaymentService`: list (fields + `Billable`), webhook branch for invoice payments, verify lock and
    idempotent answer.
  - `PaymentDto`: 3 optional fields.
  - **Billing & Finance kit files, edited in place** (`deploy/billing-finance/backend/src`):
    `Application/Finance/Billing/ReceivableDtos.cs` (4 optional fields on `PaymentRegisterRowDto`) and
    `Infrastructure/Services/Billing/CustomerPaymentService.cs` (duplicate reference check; register
    fields, filter and search). On a server where Billing & Finance is already in the source, apply these
    two hunks there.
- **Frontend:** `frontend/apply.mjs` (idempotent, LF or CRLF, stops on a missing anchor; already run on
  the repo `frontend/`). No new files. Patches `BillingMoneyIn-BF1.js`, `BillingShared-BF1.js`,
  `AdminQuotationPages-COWOrOkL.js`; `sw.js` revision `quotation-invoice-1` (the two billing chunks are
  added to the precache, so the 1-year `/assets` cache on IIS does not keep the old copy).
- **Tests:** `tests/e2e_quotation_invoice.py`, `tests/ui_quotation_invoice.py` (written by the tester).
  Added to the e2e with the fix, assertions otherwise untouched: endpoint names `sandbox_complete` and
  `webhook` in `EP`, a helper `alt_call`, env `QI_NOMAIL_API` / `QI_WEBHOOK_SECRET`, one B1 check (send
  with no mail transport) and two B7 checks (webhook moves the invoice; webhook then verify applies once).

## Tests (local Dev, 27 Nov 2026)

| Script | Result |
|---|---|
| `node --test frontend/tests/` | 24 / 24 |
| `tests/ui_quotation_invoice.py` (1366 and 390 px) | 19 / 19 |
| `tests/e2e_quotation_invoice.py` | **not run**: the Dev test customer's password (`QI_CUSTOMER_PASSWORD`) was not available. Expected to fail by design: "A: quotation totals worked by hand" (the GST split above) and "B1 failure (code path, Dev history)" (append-only events above). |
| Admin-side probe of the items (scratch script, not in the kit) | old build on :82: B1 and B7 fail (defects shown); new build: 21 / 21 (B1 on :82 and :81, B2, B3, B4, B5, B7 webhook on :82) |
| Regression `deploy/coupons/tests/e2e_coupons.py` | 65 passed, then stops "no free slot"; **the same on the old DLLs** (pre-existing: no free booking slot in Dev) |
| Regression `deploy/offers/tests/e2e_offers.py` | 67 passed, 2 failed ("auto: best saving...", "auto on a desk..."), then stops "no free slot"; **the same on the old DLLs** (pre-existing: other live offers in Dev, then no free slot) |
| Regression `deploy/billing-finance/tests/e2e_billing.py` | skipped: written for a disposable database `RegalisWorkSpace_BFTest` on :83, which does not exist; against Dev it would add bank accounts, bills and period locks |
| Regression `deploy/pay-later/tests/e2e_paylater.py` | skipped: needs `RegalisWorkSpace_PayLaterTest` on :5099 prepared for it (29 Oct copy, older than Billing & Finance) |
| pay-later-passes, offline-payments, invoices-no-due | no e2e in those kits |

Run with the API sending mail to the SMTP sink on 127.0.0.1:2525:

```
set REGALIS_ADMIN_EMAIL=...& set REGALIS_ADMIN_PASSWORD=...& set QI_CUSTOMER_PASSWORD=...
set QI_NOMAIL_API=http://localhost:82& set QI_WEBHOOK_SECRET=...
python -I deploy\quotation-invoice\tests\e2e_quotation_invoice.py
```

The :82 instance: same Dev database and JWT key as :81, `Smtp__Host=` and `MicrosoftGraph__*=` blank,
`Payments__Razorpay__WebhookSecret=%QI_WEBHOOK_SECRET%`.

## Deploy order (when asked; not done)

1. Billing & Finance first, if it is not on the server yet.
2. Backend source (Regalis-live): copy `backend/src/**`, apply `existing-files.diff` (including the two
   Billing & Finance hunks), build.
3. Database: the duplicate check above, then `db/migrations/20261127120000_QuotationInvoice.sql`.
4. Frontend: `node deploy/quotation-invoice/frontend/apply.mjs <site frontend folder>`.
5. Configuration: Graph (already in the live web.config) or Smtp settings must be present for quotation
   mail.
6. Check: send a test quotation to an internal address; mark it approved and see the invoice named;
   open Billing > Payments and see the ten columns.
