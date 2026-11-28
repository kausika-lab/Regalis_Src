# Quotation to invoice for quotations without a customer account (27 Nov 2026)

The user said "quotation to invoice is not working". Installed on the **local Dev stack only**. Not on UAT or
production. Committed on `DHK/QuotationAccount`, not pushed.

**Needs the quotation-invoice kit** (and, through it, Billing & Finance). One file of that kit is edited here.

A second item was added during the work: **Billing > Invoices search finds the quotation number** (see below).

## Cause

A quotation can be typed in for a lead or walk-in who has no customer account: `TRN_Quotation.CustomerId` is
NULL and only the snapshot (name, email set by Send, mobile set by WhatsApp send, billing address, GSTIN) is on
the quotation. The quotation-invoice kit raised the invoice on approval only when `CustomerId` was set
(`QuotationService.DecideAsync`); otherwise it recorded the approval with the note "No invoice raised: the
customer has no account". `QuotationInvoicingService` (line 83) refused such a quotation, and the
*Convert to invoice* action was hidden for it (`ActionsFor`: `canConvertToInvoice` needed `CustomerId`). So
nothing in the app could ever invoice it. (Customer portal approval cannot happen for these quotations: a
customer only sees quotations with their own `CustomerId`, and the portal link is offered only when
`CustomerId` is set.)

## Decisions (from the caller's brief) and what the code does

When such a quotation is approved (admin *Mark approved*) or converted (*Convert to invoice*), the customer is
resolved and linked first, then the one invoice is raised, in the same transaction as before:

1. The email belongs to a **staff / admin account** (not deleted) -> refused. A quotation is never linked to a
   staff account.
2. A **customer** with the same email (case-insensitive) -> linked.
3. The mobile belongs to a staff / admin account -> refused.
4. A customer with the same mobile (`+91XXXXXXXXXX` and the 10-digit form are the same number) -> linked.
5. Otherwise a **customer account is created** from the quotation (name, email, mobile, company name, location;
   then GSTIN and billing address are copied to the new account's `Gstin` / `Address`) by
   `IUserAdminService.CreateUserAsync`, the path admin *New user* and CRM *convert* use: the same duplicate
   check, CUSTOMER role, wallet, "New profile created" admin alert, `MustChangePassword`. The
   `CreateUserRequest` validator runs first (that path's validation). Like CRM, the password is random and
   told to nobody; the customer signs in with *Forgot password* or Google. That path sends **no** welcome
   mail, so none is sent.

Each link or creation is written to the quotation timeline ("Linked to the existing customer ..." /
"Customer account ... created from the quotation's details ...") and to `TRN_AuditLog`
(`QUOTATION_CUSTOMER_LINKED` / `QUOTATION_CUSTOMER_CREATED`, module Finance, entity Quotation); the new
`MST_User` row also gets its normal CREATE audit row.

**Refused** (staff email or mobile, no email, no mobile for a new account, the Customers module refuses):

- *Mark approved*: the approval is still recorded (existing decision), **without** an invoice, and the timeline
  gets "No invoice raised: <exact reason>". The screen shows a **warning** toast "Recorded as approved. No
  invoice raised: ..." and the approved banner says the reason plus what to do.
- *Convert to invoice*: nothing changes; 422 `QUOTATION_NO_ACCOUNT` with the reason.

**Convert to invoice** is now offered on every approved quotation without an invoice. For a quotation without a
customer account it opens a small dialog with the quotation's email and mobile, which the admin can correct
(only while there is no account and no invoice; needs `QUOTATION.EDIT`; the change is written to the timeline
"Contact corrected before raising the invoice: email a -> b."). It posts the new
`POST /api/quotations/{id}/raise-invoice`. The refusal reason shows inside the dialog. For a quotation with a
customer account the button behaves as before.

Kept: one invoice per quotation (`UX_Invoice_Quotation`; re-approve / re-convert return the same invoice); the
raise now takes an `UPDLOCK` on the quotation row so concurrent approve / convert calls queue and the second
finds the first one's invoice and customer; rejected quotations are never invoiced and no account is created
for them; the advance (security deposit) invoice is unchanged and goes to the linked / created customer;
quotations that already have a customer account behave exactly as before (no new note, no audit row).

## Not decided here (open)

- **A lead quotation without a mobile number** (Dev QTN 63) cannot get a new account: the admin *New user*
  validation requires a mobile, and the brief asked for identical validation. Its approval note says so; the
  admin adds the mobile in the Convert dialog. Accounts without a mobile do exist (10 Dev customers, e.g. Google
  sign-ups); if the user wants accounts created without a mobile here, that is a change to decide.
- Re-approving an already approved quotation through the API (not offered on screen) writes the refusal note
  again only if its text changed.

## Invoices search by quotation number (added item)

`InvoiceService.GetInvoicesAsync` (admin `GET /api/invoices?search=`) matched invoice number, customer name and
email only. It now also matches the **quotation number** and the quotation's **reference number** of the
quotation the invoice was raised from (`Contains`, so `REG/QTN/2026-27/00065`, `QTN/2026-27/00065` and `00065`
all work; `00065` also still finds invoice `.../00065`). Checked on Dev: QTN 66 -> invoice 30344
REG/INV/2026-27/00383. Not changed: the customer portal list (`/invoices/my` has no server search; its rows
already carry `quotationNumber`), and the Payments register, which already searched the quotation number
(quotation-invoice kit). Advance invoices have no `QuotationId` (linked only through their notes), so the
quotation number does not find them. Screen: the Invoices search placeholder reads "Invoice, quotation or
reference no., customer..." and the column that already showed the quotation number is headed
"Quotation / source".

## Dev backfill (through the app, 27 Nov 2026)

| Quotation | Result |
|---|---|
| QTN 5 REG/QTN/2026-27/00004 (customer 10032) | *Convert to invoice* -> **REG/INV/2026-27/00394** (invoice 30355). Again -> "Already invoiced". |
| QTN 63 REG/QTN/2026-27/00062 (Rajkumar, kausika21@gmail.com, no mobile) | **Not invoiced**: no account has that email and the quotation has no mobile. Timeline: "No invoice raised: No customer account has the email ..., and a new account needs a mobile number ...". Needs Rajkumar's real mobile in the Convert dialog (not invented). |
| QTN 64 REG/QTN/2026-27/00063 (Akalya, enquiry@amstag.in) | Not invoiced: the email is the Regalis Administrator's. Timeline and page: "No invoice raised: The email enquiry@amstag.in belongs to a Regalis staff account (Regalis Administrator) ...". Needs Akalya's own email in the Convert dialog. |

## UAT / production repair (when asked; not done)

1. Deploy this kit (below).
2. Run `repair/approved_without_invoice.sql` (read-only, idempotent). It lists every approved quotation with no
   invoice and what Convert will do (raise, link by email / mobile, create, or what must be corrected).
3. For each row, open the quotation in Billing > Quotations and use **Convert to invoice** (correct the email /
   mobile in the dialog where the list says so). Running it twice is harmless: the second time answers
   "Already invoiced as ...". Do not repair with SQL: the app links the customer, creates the account and
   numbers the invoice.
4. Run the script again: only quotations still waiting for a correct email / mobile remain.

## API

| Call | Change |
|---|---|
| `POST /api/quotations/{id}/record-decision`, `/approve` | A quotation without an account is linked / gets one, then its invoice. Refused -> approval stands, no invoice, timeline note. |
| `POST /api/invoices/from-quotation/{id}` | Same linking; refused -> 422 `QUOTATION_NO_ACCOUNT` with the reason. |
| `POST /api/quotations/{id}/raise-invoice` | **New**, `INVOICE.CREATE` (+ `QUOTATION.EDIT` to change the email / mobile). Body `{ customerEmail?, customerMobile? }`. 201 raised / 200 already invoiced; reply: invoice id / number, customer id / name, `customerLink` (Email, Mobile, Created), message. |
| `GET /api/quotations/{id}` | `actions.canConvertToInvoice` true for an approved quotation without an invoice, account or not. |
| `GET /api/invoices?search=` | Also matches quotation number and reference number. |

No database change, no new permission, no menu row.

## Files

- Backend new (`backend/src`): Application `Finance/QuotationAccount/QuotationAccountContracts.cs`;
  Infrastructure `Services/QuotationAccount/{QuotationCustomerAccounts, QuotationAccountService}.cs`;
  API `Controllers/QuotationAccountController.cs`.
- Backend existing, `backend/existing-files.diff` (apply by hand):
  `DependencyInjection.cs` (2 registrations); `QuotationService.cs` (`DecideAsync` both branches, `ActionsFor`
  one condition); `InvoiceService.cs` (admin list search); and the quotation-invoice kit's
  `QuotationInvoicingService.cs` (edited in place in `deploy/quotation-invoice`: one constructor parameter,
  the row lock, the account step instead of the refusal).
- Frontend `frontend/apply.mjs` (idempotent, LF / CRLF, stops on a missing anchor; already run on the repo
  `frontend/`): `AdminQuotationPages-COWOrOkL.js`, `BillingMoneyIn-BF1.js`, `sw.js` revision
  `quotation-account-1` (both chunks and index.html).
- `repair/approved_without_invoice.sql` (read-only).
- Tests `tests/e2e_quotation_account.py`, `tests/ui_quotation_account.py`.

## Tests (local Dev, mail to the SMTP sink on 127.0.0.1:2525)

| Script | Result |
|---|---|
| `tests/e2e_quotation_account.py` | 48 / 48 (new email -> account + invoice; existing email (other case) -> linked, one account; same mobile -> linked; staff email -> approved, no invoice, reason, Convert 422, corrected email -> invoice, again -> same; no mobile -> reason, then invoice; concurrent double approval -> one invoice, one account; rejected -> nothing; customer quotation unchanged; advance invoice; invoice search by full / partial quotation number, reference, invoice number, name; Payments register by quotation; portal own list) |
| `tests/ui_quotation_account.py` (1366 and 390 px) | 18 / 18 |
| `node --test frontend/tests/*.test.mjs` | 24 / 24 |
| `deploy/quotation-invoice/tests/*` | **not run**: they sign in as the Dev test customer and need `QI_CUSTOMER_PASSWORD`, which was not available. The linked-customer approval path they cover is checked by e2e case 8 above. |

Test data: customers `qa-<run>-*@example.test` are soft-deleted by the scripts, invoices cancelled; the approved
/ rejected test quotations stay (numbers are never reused, approved quotations cannot be cancelled).

```
set REGALIS_ADMIN_EMAIL=...& set REGALIS_ADMIN_PASSWORD=...& set QA_MAIL_DIR=<sink folder>& set PYLIB=<websocket-client>
python -I deploy\quotation-account\tests\e2e_quotation_account.py
python -I deploy\quotation-account\tests\ui_quotation_account.py
```

## Deploy order (when asked; not done)

1. quotation-invoice kit first, if it is not on the server.
2. Backend source (Regalis-live): copy `backend/src/**`, apply `existing-files.diff` (including the
   `QuotationInvoicingService.cs` hunk), build.
3. Frontend: `node deploy/quotation-account/frontend/apply.mjs <site frontend folder>`.
4. Check: approve a test lead quotation with an internal test email -> invoice named; then the repair steps above.

## Rollback

- Backend: restore the previous DLLs (Dev copy: scratchpad `replaced-dlls/before-quotation-account`), or revert
  the diff and remove the four new files. No data migration to undo. Accounts and invoices created meanwhile
  stay (they are real records; cancel an invoice from Invoices if needed).
- Frontend: restore the two chunks and `sw.js` from git (`git checkout <previous> -- frontend/assets/AdminQuotationPages-COWOrOkL.js frontend/assets/BillingMoneyIn-BF1.js frontend/sw.js`)
  and bump the `sw.js` revision again so browsers reload.
- Dev database backup taken first: `RegalisWorkSpace_Dev_20261127_before_quotation_account.bak`.
