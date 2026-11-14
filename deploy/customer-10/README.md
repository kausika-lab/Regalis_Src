# Customer Portal: 10-item brief (14 Nov 2026)

Installed on the **local Dev stack only**. Not on UAT or production. Not committed.

Before building, every unclear point was put to the product owner. Their answers are in the "Decision" column.

## What changed

| # | Requirement | Decision | What was done |
|---|---|---|---|
| 1 | Virtual Office: Pay Later | Already built on 13–14 Nov (Membership and Book Workspace). | Verified. A pay-later VO is now **Awaiting payment** (item 2). Membership shows it as the current plan, with an amber "Awaiting payment: pay ₹X at the front desk" box and "Total to pay at the front desk". The pay-later notes on Membership and Book Workspace now say it "becomes active once it is paid". |
| 2 | Passes: no Per Day / Consecutive Days; inactive until paid | Remove the **wording** only; all 4 day-pass plans stay. Pay-later passes **and** memberships (VO included) wait for the desk payment. | **Wording:** the "₹X per day" line and every "consecutive days" text are gone. Database plan descriptions are cleaned for display only ("consecutive", "the best per-day rate", "at N% off"). The single day reads "/ 1 day". **Status:** new pass status **5 = Awaiting payment**. Pay later creates the pass in 5. It becomes Active (1) only when it is **paid in full**, through a desk receipt, an online invoice payment or a finance receipt. A void that undoes full payment puts it back to 5. All of this is in one EF save interceptor, so no payment path can skip it. The customer is notified "Pass activated". An awaiting pass cannot check in (app check-in uses Active only, and QR entry already refused unpaid passes) and gives no day credits. **Online:** a pass was already created only after the server verifies the Razorpay signature (bad signature or cancelled checkout = no pass); unchanged. |
| 3 | Membership price/benefits, no % | Remove (a) the term-card "% off" badges, (b) "%" in benefit lines, (c) "% off" in database plan descriptions (display only). **Keep** "CGST + SGST (18%)". | Prices on plan and term cards are larger, bold and gold, with "/ month" kept beside them. Benefits ("What's included", "Amenities") are highlighted chips. "10%/15% off standard" now reads "Discounted rate" / "Best discounted rate". "10% off extra meeting room bookings" now reads "Discount on extra meeting room bookings". "15% off extra bookings" now reads "Bigger discount on extra bookings". |
| 4 | Hide QR / Access | Already done on 13 Nov (`MST_Menu` CUST_QR hidden). | Verified: not in the customer menu. The `/qr` page, API and admin access screens are untouched. |
| 5 | Ticket Back + heading | — | **Back:** a boxed, gold-bordered "← Back" button on Raise Ticket (both steps). A new "← Back to tickets" button on the ticket detail page, which had none. **Heading:** a gold band with a 5 px gold left edge on the Support & Contact list, Raise Ticket and ticket detail headers. Responsive at 390 px. The admin ticket pages are separate and unchanged. |
| 6 | 10-minute slot hold | The hold **already existed**: `Booking.PendingHoldMinutes`=10, a serializable transaction with UPDLOCK/HOLDLOCK, expiry re-checked at confirm, and a sweeper every 60 s. Late payment: re-check the slot. If it is free, confirm. If it is taken, credit the full amount to the wallet. | **Gaps fixed:** (1) the payment **webhook** only marked the payment Success. It never confirmed the booking. If it came before the browser, the browser's confirm then failed with "already settled". Now the webhook confirms the booking server-side (invoice, QR, notification). The browser's confirm afterwards returns that same confirmation. (2) `payment.failed` webhooks were recorded as **Success**. They are now Failed. Webhooks with a wrong amount are ignored. (3) A hold past 10 minutes still blocked the slot for up to 60 s, until the sweep. It now stops counting at exactly 10 minutes: the locked slot check expires it inside the same transaction, and the availability grid ignores it. (4) Paying after the hold lapsed: the slot is re-checked under the locks. Free → Confirmed (history: Expired → Pending → Confirmed). Taken → the payment goes to the **wallet**, it is marked Refunded (never credited twice), the booking is Expired and the customer is notified. (5) The same webhook-first race existed for pass, invoice and wallet top-up payments. A webhook-only Success is now still accepted by the browser's signature-checked verify. |
| 7 | Crown logo → Dashboard | Already done on 13 Nov. | Verified on 4 pages, at desktop and 390 px. |
| 8 | Automatic logout | **Customers only**, warning at 9 minutes, sign-out at 10. Closing the tab already ended the session (sign-in kept in `sessionStorage`). | **Browser:** activity (pointer, keys, touch, scroll, wheel) is tracked. At 9 idle minutes, an "Are you still there?" box with a countdown, **Stay signed in** and **Sign out**. At 10: sign-out, back to the login page, and "You were signed out after 10 minutes without activity". It also checks when a tab or phone app comes back to the front, and after a reload (last activity is kept in `sessionStorage`). While active, the session is renewed quietly (at most once a minute). Once idle, the auto-refresh is refused, so background polling cannot keep it alive. **Server:** customer access tokens live **5 min** (staff: 15, unchanged). The customer refresh token now expires after **11 minutes** without a refresh, sliding (staff: 7 days, unchanged). A session whose page was closed, or whose app was killed, therefore dies on the server within 11 minutes, with no JavaScript. Why 11: 10 minutes plus the warning minute, so "Stay signed in" still works at second 59. |
| 9 | Delete Account | Password (emailed 6-digit code for Google-only accounts). Anonymise; keep the financial records. Refuse while anything is open. | Profile → **Delete Account** (customers only). The popup warns that bookings, memberships, passes and payment history become inaccessible. It lists any **blockers**, each with a link: unpaid dues/invoices, an upcoming or running booking, an active or awaiting pass/membership, wallet money. Then the customer enters the password (or code), ticks "I understand" and confirms. **Server:** the user row is kept but closed (`Status`=Inactive). Name → "Deleted customer", email → `deleted-<id>@deleted.invalid`, mobile → empty, and password, Google link, address, company, GSTIN, DOB, gender, role and photo are cleared (the photo file is deleted). All sessions and QR tokens are revoked. Waiting-list entries are closed. An audit row `ACCOUNT_DELETED` is written. Bookings, passes, invoices (which keep their own billing snapshot) and payments stay. The email and mobile are free to register again. The row is **not** soft-deleted (`IsDeleted`), because staff screens join bookings and payments to it. Rate-limited like sign-in. |
| 10 | Payment History | Pending older than 30 min shows "Not completed" (display only). | New sidebar entry **Payment History** (`/payments`). It is a list of cards, newest first: what the payment was for and its reference (booking no., pass/membership no., invoice no., or wallet top-up), details, amount, date (IST), method, transaction ID (gateway payment ID or payment no.) and a status badge. Filters: All, Successful, Pending, Failed / not completed, Refunded. Tap a card for the details (gateway order, invoice, refund note, failure reason) and a **Download receipt** button. The data is `TRN_Payment` only, always filtered to the signed-in customer (another customer's ID returns 404; staff get 403). |

## Database

`db/migrations/20261114150000_CustomerTenItems.sql` (rollback `..._Rollback.sql`). Idempotent. **No table or column changes.**

- New menu `CUST_PAYMENTS` → `/payments`, icon Banknote, order 75 (after Invoices), permission `PAYMENT.VIEW`, granted to CUSTOMER.
- Unpaid pay-later passes that are `Active` move to `5` (Awaiting payment). On Dev this was 1 row.
- The rollback hides the menu and turns every 5 back into 1. **Run it before going back to the old API**, which does not know status 5.

New enum values in code only: `CustomerPassStatus.AwaitingPayment = 5`, `OtpPurpose.AccountDeletion = 4` (both are tinyint columns with no check constraint).

## API

| Method | Route | Who | Notes |
|---|---|---|---|
| GET | `/api/account/payments?page&pageSize&status` | PAYMENT.VIEW, customers | status: success, pending, failed, refunded |
| GET | `/api/account/payments/{id}` | same | own rows only |
| GET | `/api/account/deletion` | signed-in customer | canDelete, method (password/otp), masked email, blockers |
| POST | `/api/account/deletion/code` | same | emails a code; 1 per minute; "auth" rate limit |
| POST | `/api/account/deletion` `{password}` or `{code}` | same | "auth" rate limit |

Changed behaviour of existing endpoints: `POST /payments/webhook` (items 2 and 6), `POST /bookings/{id}/confirm` (late or repeated gateway confirms), `POST /bookings` (stale holds released), `POST /passes/purchase` (pay later → 5; webhook-first verify accepted), the wallet top-up verify (`WalletAdminService.VerifyRechargeAsync`) and `POST /payments/invoice-verify` (webhook-first verify accepted), `POST /auth/login|google|refresh` (customer lifetimes).

Configuration (optional, `Jwt` section): `CustomerIdleMinutes` (default 11), `CustomerAccessTokenMinutes` (default 5).

## Files

**New (backend, `backend/src`):** `Persistence/Interceptors/PassActivationInterceptor.cs`, `Services/AccountDeletionService.cs`, `Services/MyPaymentsService.cs`, `Application/Account/AccountDeletionDtos.cs`, `Application/Account/MyPaymentDtos.cs`, `API/Controllers/CustomerAccountController.cs`.

**Existing (backend):** `backend/existing-files.diff`, 18 files. It was made against the decompiled DLLs, so apply it by hand, matching the lines shown: `CustomerPassStatus`, `OtpPurpose`, `IBookingService`, `BookingService`, `BookingEngineLocks`, `AvailabilityService`, `BookingHoldSweeper`, `PayLaterSweeper`, `PayLaterPolicy`, `PassService`, `CashCollectionService`, `PaymentService`, `WalletAdminService`, `DayPassSalesReport`, `JwtOptions`, `JwtTokenService`, `AuthService`, `DependencyInjection` (the interceptor goes **first** in `AddInterceptors`, plus two service registrations). Also a one-line change in the admin-customer kit: `deploy/admin-customer/.../SeatWaitlistJobs.cs` (an awaiting day pass counts as "bought").

**Frontend:** `frontend/apply.mjs` patches, idempotently: `index-DI5X4c9f.js` (idle watcher, `/payments` route), `ProfilePage-DRLk3DkQ.js`, `PassesPage-CxAkHUg-.js`, `MembershipPage-Bk9mQr2v.js`, `BookWorkspacePage-MB2.js`, `SupportPages-Cf5pV8yQ.js`, `finance-C0-kLKkN.js` (status label 5 = "Awaiting payment", which admin pass screens show too) and `sw.js` (revision `customer-10-1`). It copies the new `DeleteAccount-DA1.js` and `PaymentHistoryPage-PY1.js`. It has already been run on the repo `frontend/` and on `local-dev\frontend`.

## Deploy order (when asked; not done)

1. Backend source (Regalis-live): copy `backend/src/**`, apply `existing-files.diff` and the SeatWaitlistJobs line, then build.
2. Run the SQL migration together with the new API.
3. Front end: promote the changed `frontend/assets` files and `sw.js`.
4. Production must have `Payments:Razorpay:WebhookSecret` set and the Razorpay webhook enabled for `payment.captured` and `payment.failed`. Without it, the webhook fixes in item 6 never run (the browser path still works).

## Tested (local Dev stack, 14 Nov 2026)

Install: `local-dev\api` Regalis.*.dll rebuilt from the decompiled tree with this kit linked; migration run on Dev; `apply.mjs` run on `local-dev\frontend`. Backup: `D:\Regalis\local-dev-backup-20261114-before-customer-10` plus `RegalisWorkSpace_Dev_20261114_before_customer_10.bak` (instance Backup folder).

- `tests/e2e_customer10.py` (API + SQL, against a second API instance on :82 with a test webhook secret): **82/82**. It covers:
  - menus
  - VO and day-pass pay later → status 5
  - check-in refused while awaiting
  - part payment stays 5, full payment → 1 and notified, void → 5, paid again → 1
  - bad signature / cancelled checkout / failed webhook → no pass
  - webhook-first pass verify
  - hold: Pending, another customer refused (409), free at once after 10 min (history row), expired hold refused for wallet, sweep within a minute
  - paid in time → Confirmed
  - late payment with the slot free → Confirmed; replay is safe
  - late payment with the slot taken → 409, wallet credit, Refunded, notified, no double credit
  - webhook: wrong amount ignored, bad signature 401, confirms alone (invoice + QR), browser confirm afterwards OK, repeat ignored
  - token lifetimes (customer 5 min / 11 min sliding, staff unchanged); idle refresh refused
  - payment history: all rows, newest first, own only, all statuses, detail, 404 for someone else's payment, 403 for staff, "Not completed", filter
  - delete account: blockers, refused with open items, wrong password, success, anonymised, sessions revoked, audit, cannot sign in; Google-only by code (wrong code refused); staff refused
  - wallet pass, admin pass list, outstanding list and payments grid still work
- `tests/ui_customer10.py` (headless Chrome, 1366 px and 390 px): **66/66**. It covers:
  - no QR, Payment History in the menu
  - crown → dashboard from 4 pages at both widths
  - passes wording, plans still listed, "Awaiting payment" + "TO PAY"
  - membership: no %, prices ≥ 24 px gold, "/ month" kept beside them, highlighted benefit chips, GST kept, notes, no sideways scroll, awaiting VO shown as the current plan
  - tickets: highlighted heading and boxed Back on list, raise and detail at both widths; Back works
  - delete account: blocked popup → confirm needs the tick → deleted → login page; cannot sign in
  - payment history at both widths, statuses, detail, filter
  - idle: warning at 9 min, Stay signed in renews the session, sign-out at 10 with the note, protected page → login, nothing in `localStorage`
  - no JS errors
- `frontend/tests/*.test.mjs`: 24/24.
- `deploy/admin-customer/tests/ui_vo_paylater.py` expects a pay-later VO to be **active**. It is now Awaiting payment (5) by design, so that check needs updating if it is re-run.

## Open points

- **Captured online payment with no browser return, for a pass, invoice or wallet top-up.** The webhook records it as Success, but nothing is created, because the order does not store which plan or date was being bought. It shows in Payment History and in Finance → Payments. Booking payments are fully handled. A follow-up could store the purchase details on the order and finish it from the webhook.
- **Logout after 10 idle minutes.** The browser drops its copy at once, but the server-side revoke call usually fails (the 5-minute access token has expired). The refresh token then simply runs out at 11 minutes. It cannot be reused in the meantime, because the browser no longer has it.
- **Customers cannot cancel passes on Dev** (no PASS.EDIT for the CUSTOMER role). This predates the kit. The delete-account blocker links to Passes, but there the customer can only ask the desk.
- **Pre-existing:** on a bad signature, pass purchase marks the payment Failed, but that change is rolled back with the transaction, so the order stays Pending. No pass is created, which is what matters. Not changed.
- **Test data is still in Dev.** Someone was signed in on the local stack while the tests ran, so Dev was **not** restored. Test rows: customers `c10-*@example.test` (some now "Deleted customer"), bookings on the Board Room on 16 Nov 2026, desk receipts for them, and one ticket. Restore `RegalisWorkSpace_Dev_20261114_before_customer_10.bak`, then re-run the migration, when convenient.
