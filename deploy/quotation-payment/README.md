# Quotation -> payment -> invoice workflow, and admin bookings without an invoice (27-28 Nov 2026)

Installed on the **local Dev stack only**. Not on UAT or production. Committed on `DHK/QuotationPayment`, not pushed.

**Needs** the billing-finance, quotation-invoice, quotation-account and admin-booking kits (backend and frontend).
Two files of those kits are edited in place here (see **Files**).

## What was asked

The brief (7 items): an approved quotation goes to the Invoice screen and the Payment screen; its details are carried to
the invoice; the admin Booking screens show no invoice details; quotation payments show paid / partially paid / pending
properly; rejected quotations never reach the Payment or Invoice screen; nothing else changes, no duplicate payments,
consistent figures everywhere.

Added during the work (the user, through the lead): **"if we book any desk, cabin, meeting room or virtual office
manually in the admin portal, don't create an invoice"**, because those bookings were already quoted and the
quotation's invoice bills them. So a staff booking gets **no invoice, no payment and no pay-later tracking**; the money
is billed and collected through the quotation's invoice (Payments > Quotations).

## Review: what was wrong in the existing implementation

1. **The local stack was running an old API without these features (root cause of what the user saw).**
   `local-dev\local.env.cmd` sets `REGALIS_API_DIR=D:\Regalis\local-dev\api` (the build with every installed kit), but
   `local-dev\start-local.cmd` ignored the variable and always started the repo's `api\` (28 Oct build, before the
   quotation-invoice kit). The API log of 27 Nov 22:43-22:58 IST shows the repo's `api\` serving the user: Dev quotation
   88 (`REG/QTN/2026-27/00087`) was created, "sent" with "No mail server is configured - email was not sent." recorded as
   a success, approved, and then searched for on the Invoices screen - with no invoice, because that build has no
   approval-raises-invoice code. **Fixed:** `start-local.cmd` now honours `REGALIS_API_DIR` (default: the repo's
   `api\`, as before) and prints which build it starts.
2. **No Payment-screen view of quotations.** The Payments screen listed payment rows only; an approved quotation whose
   invoice had no payment yet did not appear anywhere on it, so its pending amount could not be tracked there.
3. **"Open invoice" on the Quotations list opened the invoice list,** not the quotation's invoice
   (`navigate("/admin/billing/invoices")`), and the approved quotation's page had no way to reach the invoice or its
   payments.
4. **Details not carried to the invoice:** the customer's reference (PO / enquiry number) was dropped; a **line
   discount** was dropped from the invoice line's text (the line amount is net of it, so Qty x Rate did not explain
   the Amount); an **inter-state** quotation's invoice PDF printed the home state as place of supply (from settings),
   although the quotation's place of supply decided IGST. Latent defect found on the way: a 300-character line
   description plus its duration overflowed `TRN_InvoiceLine.Description` (300), which would make the approval
   itself fail.
5. **Admin booking screens carried invoice wording and buttons** (the New booking review and done steps, the
   header, the cancel dialog), and the admin New booking raised a pay-later invoice (now removed, see above).

Checked and found correct (no change): every approval path (portal approve, admin Mark approved / record decision,
re-approve, Convert to invoice) goes through `QuotationService.DecideAsync` -> `QuotationInvoicingService`, one
idempotent invoice per quotation (`UX_Invoice_Quotation`). There is no separate "Completed" status: `QuotationStatus`
is Draft, Sent, Viewed, Approved, Rejected, Expired, Cancelled, Revised; **Approved** is the accepted state. Rejected
quotations cannot be invoiced or approved later; the payment register and invoice lists already exclude anything linked
to a quotation that is not Approved; duplicate manual references are refused (409); overpayment is refused.

## Coverage, item by item

| Brief | Built |
|---|---|
| 1. Approved -> Invoice screen | Approval raises the invoice (existing, verified by test). The approved quotation's page now has **Open invoice** (opens that invoice) and **Payment status**; the list's "Open invoice X" opens that invoice. Not added: jumping away from the quotation page automatically after Mark approved (the toast names the invoice; one click opens it). |
| 2. Approved -> Payment screen | **Payments** now has two tabs: **All payments** (unchanged) and **Quotations**: every Approved quotation with quotation no., reference, approval date, customer, invoice no. and due date, total, paid, outstanding, payment status, number of payments and the last one, and **+ Record payment**. Summary cards: approved quotations (not invoiced, invoice cancelled), invoiced (due / partially paid / paid), paid, outstanding. Filters: All approved, Awaiting payment, Due, Partially paid, Paid, Not invoiced, Invoice cancelled. Search: quotation no., reference, invoice no., customer. |
| 3. Details carried to the invoice | Already copied: lines, quantity, unit price, line amount, GST % and amount, HSN/SAC, sub total, quotation discount, taxable, CGST / SGST / IGST, TDS, adjustment, round-off, total, terms, customer snapshot and account, quotation number. **Added:** the customer reference in the invoice notes ("Raised from quotation X (customer reference Y)."), the line discount named on the invoice line ("(less discount Rs 500.00)"), the quotation's place of supply on the PDF of an inter-state quotation invoice, and a safe cut at 300 characters. |
| 4. No invoice on admin Booking screens | Admin Bookings list / detail / cancel dialog and New booking (review, done, Virtual Office) show no invoice number, button or wording. And (user's added requirement) a staff booking raises no invoice at all, see below. Customer portal screens unchanged. |
| 5. Paid / partially paid / pending | The Quotations tab uses the invoice's own figures and the labels of the Invoices and Payments screens: **Due** (nothing paid = pending), **Partially Paid**, **Paid**; Cancelled / Refunded for a cancelled invoice; **Not invoiced** (with the reason from the quotation's timeline) when an approved quotation has no invoice. Outstanding = Total - Paid. |
| 6. Rejected quotations | Only Approved quotations are listed (draft, sent, viewed, rejected, expired, cancelled, revised never). A rejected quotation has no invoice and Convert / approve are refused (unchanged, tested). |
| 7. Consistent, no duplicates | Record payment is the existing dialog posting to `/invoices/{id}/payments` against the quotation's invoice: one payment and one receipt, the same duplicate-reference (409) and overpayment checks; no payment is ever tied to a quotation itself. The e2e compares every listed row with the Invoices screen (total, paid, outstanding). |

## Admin manual bookings: no invoice (user requirement)

Path: **Bookings > New booking** (the admin-booking kit's on-behalf flow, `api/admin/on-behalf/{customerId}/...`) for
a meeting room / desk / cabin slot or range, a monthly desk or cabin, and a Virtual Office.

- **Booking** (`BookingService.SettleAndFinalizeAsync`, when the caller is the on-behalf identity): Confirmed, price
  stored on the booking as before, QR token, status history "Confirmed by staff in the admin portal; billed through the
  quotation, no invoice or payment on the booking.", customer notification "Booking confirmed" (without "pay at the
  front desk"). **No invoice, no payment.** `IsPayLater` = 0, `AmountPaid` = 0, `PaymentStatus` = 1 (Pending, nothing
  received on the booking), no due date.
- **Virtual Office** (`PassService.PurchaseAsync`, same condition): **Active** at once (it used to wait for payment),
  no invoice, no payment, not pay later, `AmountPaid` 0, no due date, no "pay later" admin alert.
- **Why not pay later:** pay later means "money to collect on this booking": it would put the booking on Cash
  Collections, send the pay-later reminder, block check-in until paid and **release the booking after its start**
  (`PayLater.AutoCancelAfterStartMinutes`). None of that fits a booking billed through the quotation. So staff
  bookings are not in Cash Collections, Outstanding / ageing, ledgers, the Payments register or reminders (no invoice,
  no payment, not pay later), and check-in is not blocked.
- **Cancelling** refunds nothing: a booking refunds only when its payment status is Paid / Partially paid (unchanged);
  a pass with no payment and no invoice now refunds only what was paid on it (0) instead of its price
  (`PassService.CancelAsync`; without this, cancelling a staff Virtual Office would have credited its full price to the
  customer's wallet).
- **Unchanged:** customer-portal bookings and passes (wallet, gateway, pay later) still raise their invoice; booking
  extension (paid by wallet / cash, raises its own invoice) is unchanged.
- **Not changed (decision needed):** the old `POST /api/bookings/admin` (front-desk booking with cash / wallet / pay
  later) still invoices. No screen has used it since the admin-booking kit; a cash or wallet booking through it takes
  real money, which should carry an invoice. Say if it should change.
- **Customer portal display:** the customer's My Bookings shows a non-pay-later confirmed booking as **PAID**
  (existing rule in the bundle); a staff booking now falls under that rule. The portal was not changed (brief: customer
  portal unchanged). Say if it should read differently.
- **A link between a booking and its quotation** does not exist and was not added (not asked). It would let the
  Bookings screen show which quotation bills a staff booking and let reports tie them together; worth deciding.

## Approved, then something else?

- An approved quotation cannot be rejected, revised or expired afterwards (unchanged).
- An approved quotation **without** an invoice (e.g. refused staff email) can be cancelled; it then leaves the Payments
  view (tested).
- An approved quotation **with** an invoice cannot be cancelled ("Cancel the invoice instead"). If its invoice is
  cancelled, the quotation stays Approved and keeps that cancelled invoice: Convert / re-approve return the same
  cancelled invoice, so **it can never be invoiced again**. The Payments view shows it as "Invoice cancelled" with
  nothing to pay. Not changed (needs a decision: allow a fresh invoice after a cancellation?).
- The security-deposit (advance) invoice has no `QuotationId`; it is matched by the note the app writes on it and shown
  beside the quotation ("Security deposit REG/INV/...: total, paid, status", **+ Deposit payment**), never added into
  the quotation's figures.

## Rules

- Staff only (`[StaffOnly]`) and `PAYMENT.VIEW` for the Quotations view; Record payment needs `PAYMENT.RECORD`
  (unchanged endpoint). A customer token gets 403.
- Read-only: the view writes nothing. Paid / outstanding are the invoice's stored figures (TDS / write-off receipts
  count, as on the Invoices screen).
- Not location-filtered on screen, like the Payments register (the API takes an optional `locationId`).

## Database

**No migration**: no table, column, index, permission, menu or setting changes. The Dev database was backed up first:
`RegalisWorkSpace_Dev_20261127_before_quotation_payment.bak` (SQL Server backup folder of `SQLEXPRESS2025`).

Existing Dev data was not changed. On Dev, before this kit: **101 admin (Front desk) bookings have an invoice** (9 of them still open) and
**13 Virtual Office passes set up by staff have an invoice**; they were left as they are (pay later, invoice due, as
before). Dev quotation 88 (`REG/QTN/2026-27/00087`, approved on the old repo API) still has **no invoice**: *Convert to
invoice* on its page raises it (one click; not done here, it is the user's own test data).

## API

| Call | Change |
|---|---|
| `GET /api/payments/quotations` | **New.** `PAYMENT.VIEW`, staff only. `page`, `pageSize`, `search`, `status` (open, due, partial, paid, noinvoice, cancelled), `locationId`. Rows: quotation id / number / reference / date, approvedUtc, customer, quotationTotal, invoice id / number / date / due date / status / total, paidAmount, outstandingAmount (null when cancelled or not invoiced), paymentStatus, isOverdue, paymentCount, lastPaymentUtc, canRecordPayment, noInvoiceReason, deposit. |
| `GET /api/payments/quotations/summary` | **New.** Same filters except status: approved, notInvoiced, due, partiallyPaid, paid, cancelled, invoicedTotal, paidTotal, outstandingTotal. |
| `POST /api/quotations/{id}/record-decision`, `/approve`, `POST /api/invoices/from-quotation/{id}` | The invoice carries the customer reference and line-discount text. |
| `GET /api/invoices/{id}/pdf` | Inter-state quotation invoice: the quotation's place of supply. |
| `POST /api/admin/on-behalf/{customerId}/bookings/{id}/confirm`, `.../virtual-office` | No invoice, no payment, not pay later; reply `invoiceId` 0, `invoiceNumber` "". |

## Files

- Backend new (`backend/src`): Application `Finance/QuotationPayment/QuotationPaymentContracts.cs`; Infrastructure
  `Services/QuotationPayment/QuotationPaymentService.cs`; API `Controllers/QuotationPaymentsController.cs`.
- Backend existing, `backend/existing-files.diff` (apply by hand on the server source): `DependencyInjection.cs` (1
  registration); `InvoiceService.cs` (`GetInvoicePdfAsync`: inter-state place of supply); `BookingService.cs`
  (`SettleAndFinalizeAsync`: staff booking without invoice / pay later); `PassService.cs` (`PurchaseAsync`: staff
  Virtual Office; `CancelAsync`: refund base); and two kit files edited in place in the repo:
  `deploy/quotation-invoice/.../QuotationInvoicingService.cs` (notes, line text) and
  `deploy/admin-booking/.../OnBehalfBookingService.cs` (doc comment and log line only).
- Frontend: `frontend/apply.mjs` (idempotent, LF / CRLF, stops on a missing anchor; already run on the repo
  `frontend/`): new `assets/QuotationPayments-QP1.js`; patches `BillingMoneyIn-BF1.js`, `AdminQuotationPages-COWOrOkL.js`,
  `AdminBookingsPage-BK2.js`, `BookWorkspacePage-MB2.js` (admin mode only), `AdminNewBooking-AB1.js`; `sw.js` revision
  `quotation-payment-1`.
- Local stack: `local-dev/start-local.cmd` honours `REGALIS_API_DIR`; `local-dev/README.md` and
  `local.env.cmd.sample` say so.
- Tests: `tests/e2e_quotation_payment.py`, `tests/e2e_admin_no_invoice.py`, `tests/ui_quotation_payment.py`.

## Tests (local Dev, mail to the SMTP sink on 127.0.0.1:2525)

| Script | Result |
|---|---|
| `tests/e2e_quotation_payment.py` (with `PDFLIB` = a folder holding pypdf, for the place-of-supply text) | 66 / 66 |
| `tests/e2e_admin_no_invoice.py` | 33 / 33, monthly cabin skipped: no cabin on Dev has a monthly price, so nobody can book one (same code path as the monthly desk, which passes) |
| `tests/ui_quotation_payment.py` (1366 and 390 px) | 27 / 27 |
| `node --test frontend/tests/*.test.mjs` | 24 / 24 |
| Regression `deploy/quotation-account/tests/e2e_quotation_account.py` | 48 / 48 |
| Regression `deploy/quotation-account/tests/ui_quotation_account.py` | 18 / 18 |
| Regression `deploy/quotation-invoice/tests/*` | **not run**: they need the Dev test customer's password (`QI_CUSTOMER_PASSWORD`), not available. Their approval / invoice / payment paths are covered by `e2e_quotation_payment.py` sections 1-5. |
| Regression `deploy/admin-booking/tests/ui_admin_booking.py` | 43 / 59. **Every customer-page check passes** (header, tile, slots, Review & pay with Wallet / Pay online, wallet booking, My Bookings, 390 px, no JS errors) and so do the admin screen checks up to the confirmation. The 16 failures: 4 assert the superseded decision (the staff booking is pay later with an invoice and "amount due"); the rest follow from "monthly: desk picked" - every Dev desk is booked for the test's term (Dev data, as the admin-booking README already noted), so the script stays on that step for the Virtual Office and two-day parts. |
| Regression `deploy/admin-booking/tests/e2e_admin_booking.py` | stops at its monthly-desk step (no free desk on Dev, `IndexError`), as above. Before that, the failures are the superseded decision only: confirm is pay later, one invoice per booking, "pay at the front desk" notification, listed on Cash Collections / customer Invoices / My Bookings as pay later. These two admin-booking scripts encode the decision the user has now replaced; they were **not** changed here. `e2e_admin_no_invoice.py` checks the new rule instead. |

```
set REGALIS_ADMIN_EMAIL=...& set REGALIS_ADMIN_PASSWORD=...& set PYLIB=<websocket-client>& set PDFLIB=<pypdf folder, optional>
python -I deploy\quotation-payment\tests\e2e_quotation_payment.py
python -I deploy\quotation-payment\tests\e2e_admin_no_invoice.py
python -I deploy\quotation-payment\tests\ui_quotation_payment.py
```

Test data: customers `qp-*`, `qpnb-*`, `qpui-*@example.test` are soft-deleted, payments reversed, invoices cancelled,
bookings / passes cancelled; decided quotations stay (numbers are never reused).

## Deploy order (when asked; not done)

1. The billing-finance, quotation-invoice, quotation-account and admin-booking kits first, if not on the server.
2. Backend source (Regalis-live): copy `backend/src/**`, apply `existing-files.diff` (including the two kit-file
   hunks), build.
3. No database step.
4. Frontend: `node deploy/quotation-payment/frontend/apply.mjs <site frontend folder>`, after the earlier kits'
   `apply.mjs`.
5. Check: approve a test quotation -> Payments > Quotations shows it Due; record a part payment -> Partially Paid on
   both tabs and on Invoices; New booking for a test customer -> no invoice, then cancel it.

## Rollback

- Backend: restore the previous DLLs (Dev copy: scratchpad `replaced-dlls/before-quotation-payment`) or revert the diff
  and remove the three new files. Staff bookings made meanwhile stay without an invoice.
- Frontend: `git checkout <previous> -- frontend/assets/{BillingMoneyIn-BF1,AdminQuotationPages-COWOrOkL,AdminBookingsPage-BK2,BookWorkspacePage-MB2,AdminNewBooking-AB1}.js frontend/sw.js`,
  delete `QuotationPayments-QP1.js`, bump the `sw.js` revision again.
