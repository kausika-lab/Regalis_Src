# Admin booking = the customer booking screen, without payment (27 Nov 2026)

Built from the brief "Update the Admin Booking Screen to function exactly like the Customer Booking
Screen, with one exception: remove the payment option" (27 Nov 2026).

Installed on the **local Dev stack only**. Not on UAT or production. **Needs the offers and coupons
kits** (backend and frontend) installed first.

Decisions taken before building:

| Question | Decision |
|---|---|
| How is an admin booking settled? | The existing **pay-later path**: the booking is Confirmed, its GST invoice is raised and stays due, and **no payment, receipt or wallet row** is written. The money is recorded later on **Finance → Payments**, against that invoice. |
| The customer's own pay-later rules (switch, account flag, open-items limit) | **Not applied to staff.** They still apply, unchanged, when the customer books in the portal. |
| Booking source | **Front desk** (`BookingSource` 2, the value the old modal used). `CreatedByUserId`, the status history and the audit trail record the staff user. |
| Reuse or copy the customer page | **Reuse.** The admin screen renders the customer page component itself, in an admin mode. |
| Which booking types | Single slot, multi-day range, monthly term (desk / cabin) and Virtual Office. The **Customisation** tile is hidden: it raises a support ticket, not a booking. |
| Offers and coupons | **Kept**, applied for the customer with the same rules as the portal (they are pricing, not payment). |
| The old "New booking (front desk)" modal | **Removed**, with its Settlement dropdown (cash / wallet / pay later). `POST /api/bookings/admin` stays in the API for compatibility; no screen uses it now. |
| Customer notifications | The same as for a customer pay-later booking. |
| Existing data | Untouched. No migration, no backfill. |

**Existing behaviour is unchanged.** The customer screen looks and works as before: every change in
the shared files only acts when the page is opened from the admin screen. Customer bookings, passes
and pay later cost and behave exactly as before. The tests check this (see **Tests**).

## Coverage, point by point

| Brief | Built |
|---|---|
| 1. Same flow, UI, fields, validation and functions as the customer screen | **Admin → Bookings → New booking** now opens the customer *Book a Workspace* page (the same component, `BookWorkspacePage-MB2.js`) in place of the bookings list: the same type cards, dates, duration, slot grid, workspace cards with *Booked* / *Not available*, monthly terms, Virtual Office terms, multi-day ranges, closed-day and past-date messages, GST breakdown and offer panel. On the server the same booking engine runs for the customer (see **How the reuse works**), so availability, closed days, working hours, slot size, the 409 on a lost slot, GST, monthly and Virtual Office rules are the customer's rules. |
| 2. Admin books for a customer through the same process | Two fields are added at the top: **Customer** (search by name, email or mobile, then choose; required) and **Location** (the customer page takes it from the header; staff can change it here). *Find available workspaces* stays off, with "Choose the customer to book for.", until a customer is chosen. |
| 3. Remove all payment options | The last step is **Review & confirm** (not *Review & pay*) with **one button**: *Confirm booking*, *Confirm N days* or *Confirm Virtual Office*. No Wallet, Pay online or Pay later button, no wallet balance, no "pay at the front desk" line. The Virtual Office button reads *Continue to review*. Error toasts say "Could not create the booking." instead of "Payment failed.". |
| 4. No payment collection, methods or processing | The admin screen never calls the wallet, the payment gateway or the pay-later checks. The server confirms with the pay-later settlement, which writes no `TRN_Payment`, `TRN_InvoicePayment`, `TRN_CashCollection` or `TRN_WalletTransaction` row. |
| 5. Saved without payment | Confirmed at once. Booking: `IsPayLater` = 1, `AmountPaid` = 0, payment status Pending, `PaymentDueUtc` = start. Invoice: Pending, nothing paid, no payment linked. Virtual Office: *Awaiting payment* (status 5), as for a customer pay-later Virtual Office. |
| 6. Payment entered on the Payment screen; no duplicates | Nothing is paid at booking time, so there is nothing to duplicate. **Finance → Payments** records the money against the invoice (`POST /api/invoices/{id}/payments`): one payment and one receipt; the booking becomes Paid (or the Virtual Office Active). A second payment on a paid invoice is refused. Cash Collections also lists the booking, as it lists any pay-later booking. |
| 7. Existing booking and payment data unchanged | No migration, no data change. Old rows are read as before. |
| 8. Shows correctly in the relevant screens | Checked in the browser: Admin Bookings list (*Pay later · ₹X due*) and detail (*Source: Front desk*), the customer's My Bookings. Checked through the screens' APIs: Calendar, the customer's Invoices, Cash Collections → outstanding, and recording the payment on Finance → Payments. |

## How the reuse works

**Front end.** `BookWorkspacePage` now takes an optional `admin` prop. The customer route renders it
without props, exactly as before. The new chunk `AdminNewBooking-AB1.js` renders it with
`admin = { customerId, header, onDone }`; `header` is the Customer field. With the prop set, and
only then, the page:

- shows the Customer and Location fields, hides the Customisation tile and the page title;
- skips the customer-only calls (`/wallet/me`, `/bookings/pay-later`, `/passes/pay-later`);
- sends the hold, monthly hold, confirm, offer / coupon apply and Virtual Office calls to
  `/api/admin/on-behalf/{customerId}/...` (quotes, slots and availability use the same calls as the
  customer page);
- gives the offer panel (`OfferPicker`) the on-behalf preview URLs and the staff wording
  (*Total after discount*, *Saving* instead of *You pay*, *You save*);
- replaces the payment buttons with one Confirm button, and the payment wording on the review and
  confirmation steps with "Nothing is collected here …" / "₹X is due from the customer …".

**Back end.** `OnBehalfBookingService` checks that the caller is staff and the customer is an active
customer, then builds the **customer booking engine** classes (`BookingService`, `PassService`,
`OfferPortalService`, `CouponPortalService`) for this one request with an `OnBehalfCurrentUser`: an
`ICurrentUser` whose user id and type are the customer's and whose `StaffUserId` is the signed-in
staff user. So every rule that works on "the caller's own rows" (holds, offers, coupons, per-customer
coupon limits, Virtual Office) works on the chosen customer, with no copy of the booking logic. The
DI `ICurrentUser` is not replaced, so the audit interceptors still record the staff user. Small edits
in the engine read the marker interface `IOnBehalfOfCustomer`:

- `BookingService` hold: source **Admin** and `CreatedByUserId` = staff; status history rows by the staff user.
- `BookingService.ConfirmAsync` / `PassService.PurchaseAsync`: the customer's pay-later eligibility is skipped.
- `OfferPortalService` / `CouponPortalService` apply: a hold of source Admin is accepted (only through the on-behalf identity).

## Rules

- **Staff only.** The controller has `[StaffOnly]`: a customer token is refused (403) before
  anything runs, even with the customer's own id. Bookings need `BOOKING.CREATE`; the Virtual Office
  needs `PASS.CREATE`. The customer endpoints did not change: a `customerId` sent to `POST /api/bookings`
  is ignored, and the booking is the caller's own.
- **Only an active customer** can be booked for (422 `CUSTOMER_NOT_FOUND` otherwise; a staff user's id is refused the same way).
- **Only holds made on this screen** (source Admin, same customer) can be confirmed or discounted
  through it. A hold the customer made in the portal cannot be confirmed without payment by staff (422).
- **Virtual Office only** on the pass endpoint: a plan whose code is `MEM-VIRTUAL-<n>M`, as the customer
  page reads them. Day passes and other memberships are refused (422).
- **Profile completeness is not required** of the customer (the customer endpoints check the caller's own
  profile; the old admin modal did not check it either).
- **Pay-later consequences apply as they do today**: check-in is blocked until paid
  (`PayLater.BlockCheckInUntilPaid`), a reminder goes out (`PayLater.ReminderHoursBefore`), and an unpaid
  booking is released `PayLater.AutoCancelAfterStartMinutes` (Dev: 30) after its start, as for any pay-later
  booking. See **Open questions**.
- **Offers and coupons** price exactly as in the portal: the automatic best offer applies by itself (so a
  live offer lowers the admin booking's price just as it would the customer's), a typed code is tried as a
  coupon first, a coupon use counts against the customer's per-customer limit.

## Where it differs from the brief, and why

- **Pay-later automation applies to admin bookings.** The brief says the booking is saved without
  payment and paid later on the Payment screen; it does not say what happens if it is never paid. The
  existing pay-later rules (blocked check-in, reminder, release after the start) now apply to admin
  bookings too. Nothing was invented: these are the rules of the settlement path chosen. Listed as an
  open question.
- **Release of a failed hold** uses the existing cancel call, as the customer page does. If an offer
  cannot be applied, or one day of a range cannot be held, the holds already taken are cancelled, and
  the customer gets the usual "Booking cancelled" notification for them. This needs `BOOKING.EDIT`; without
  it the holds expire by themselves after `Booking.PendingHoldMinutes` (10).
- **Multi-day on Dev.** A whole working day on Dev is 08:00–21:00 = 780 minutes, above
  `Booking.MaxMinutes` = 480, so a multi-day range is refused on Dev **for customers and staff alike**
  ("Duration must be between 30 and 480 minutes"). This is the existing rule, not this kit. The tests
  raise the setting for the multi-day part only and put it back.

## Open questions (for the product owner; nothing was built on a guess)

1. Should an unpaid admin booking be **released after its start** (`PayLater.AutoCancelAfterStartMinutes`) and
   have **check-in blocked** like a customer pay-later booking, or should front-desk bookings be exempt?
   Today they follow the pay-later rules.
2. `Booking.MaxMinutes` (480 on Dev) is shorter than a working day, so multi-day ranges cannot be booked on
   Dev by anyone. Is that the intended value?

## Database

**No migration.** No table, column, permission, menu or setting changes. The new endpoints use the existing
`BOOKING.CREATE` and `PASS.CREATE` permissions and the existing Bookings menu.

## API

New, `api/admin/on-behalf/{customerId}` (staff only):

| Method | Route | Permission | Same rules as |
|---|---|---|---|
| POST | `bookings` | BOOKING.CREATE | `POST /api/bookings` (hold) |
| POST | `bookings/monthly` | BOOKING.CREATE | `POST /api/bookings/monthly` |
| POST | `bookings/{bookingId}/confirm` | BOOKING.CREATE | `POST /api/bookings/{id}/confirm` with pay later, without the eligibility check |
| POST | `offers/preview` | BOOKING.CREATE | `POST /api/offers/preview` |
| POST | `offers/bookings/apply` | BOOKING.CREATE | `POST /api/offers/bookings/apply` |
| POST | `coupons/preview` | BOOKING.CREATE | `POST /api/coupons/preview` |
| POST | `coupons/bookings/apply` | BOOKING.CREATE | `POST /api/coupons/bookings/apply` |
| POST | `virtual-office` `{passPlanId, validFrom, offerIds?, couponCode?}` | PASS.CREATE | `POST /api/passes/purchase` with pay later, without the eligibility check |

The request bodies are the existing request types (`CreateBookingRequest`, `MonthlyBookingRequest`,
`OfferPreviewRequest`, …), so their validators run as before. No existing endpoint changed.

## Files

- **Backend, new** (`backend/src`):
  - Application: `Abstractions/IOnBehalfOfCustomer.cs`, `Bookings/OnBehalf/IOnBehalfBookingService.cs`, `Bookings/OnBehalf/OnBehalfDtos.cs`
  - Infrastructure: `Services/OnBehalf/OnBehalfCurrentUser.cs`, `Services/OnBehalf/OnBehalfBookingService.cs`
  - API: `Controllers/OnBehalfBookingsController.cs`
- **Backend, existing:** `backend/existing-files.diff` (apply by hand on the server source):
  - `DependencyInjection.cs`: 1 registration.
  - `BookingService.cs`: the `ActorUserId` helper; hold source and `CreatedByUserId`; the two status-history
    rows; the pay-later check skipped for staff.
  - `PassService.cs`: the pay-later check skipped for staff.
  - `OfferPortalService.cs` (offers kit file) and `CouponPortalService.cs` (coupons kit file): accept a hold
    of source Admin through the on-behalf identity. These two kit files were edited in place in the repo
    (`deploy/offers/...`, `deploy/coupons/...`).
- **Frontend:** `frontend/apply.mjs`. Idempotent, fails on a missing anchor, handles CRLF chunks; already run
  on the repo `frontend/`.
  - Copies `assets/AdminNewBooking-AB1.js` (the New booking page and the Customer field).
  - Patches `BookWorkspacePage-MB2.js` (admin mode), `OffersKit-OF1.js` (`api` and `staff` props),
    `AdminBookingsPage-BK2.js` (New booking opens the page; the old modal and its Settlement dropdown are
    removed) and `sw.js` (revision `admin-booking-1`).
- **Tests:** `tests/e2e_admin_booking.py`, `tests/ui_admin_booking.py`.

## Tests (local Dev, 27 Nov 2026)

| Script | Result |
|---|---|
| `tests/e2e_admin_booking.py`: API, money and database | **86 / 86** |
| `tests/ui_admin_booking.py`: headless Chrome, 1366 px and 390 px | **59 / 59** |
| `node --test frontend/tests/*.test.mjs` | 24 / 24 |
| Regression `deploy/coupons/tests/ui_coupons.py` (customer booking page with a coupon, wallet; passes; membership) | 93 / 93 |
| Regression `deploy/offers/tests/ui_offers.py` | 40 / 40 |
| Regression `deploy/offers/tests/e2e_offers.py` | 69 passed, 0 failed, then stopped: "no free slot" (see below) |
| Regression `deploy/coupons/tests/e2e_coupons.py` | 65 passed, 0 failed, then stopped: "no free slot" (see below) |

The two e2e regressions book hourly slots on desk DDK-D-11 about ten days ahead. On Dev that desk is
taken by a 6-month booking a Dev customer made on 25 Nov 2026 (REG-000105, before this kit), so both
scripts stop at their booking section with "no free slot". Every check before that point passes. The
booking page itself is covered by the two UI regressions, which pass in full.

`e2e_admin_booking.py` covers: who may book for a customer (customer, staff without `BOOKING.CREATE`,
unknown customer, staff id, `customerId` sent to the customer endpoint); a single slot with every
customer validation (409, past, closed day, working hours, slot size, validator); the confirmation
(no payment / receipt / cash / wallet row, invoice due, QR token, notification, status history and audit
by the staff user); the customer's own pay-later gate unchanged; the list, detail, My Bookings, Calendar,
Invoices and Cash Collections; a two-day range; a monthly desk; a Virtual Office; a coupon and offers for
the customer (per-customer limit counted, the customer's own holds refused); the Payments screen (one
payment, booking Paid, Virtual Office Active, second payment refused); and customer wallet, customer
pay later, the old `POST /bookings/admin` and a customer Virtual Office unchanged.

Run them with the API sending mail to a local SMTP sink (`127.0.0.1:2525`); the local Dev stack otherwise
sends real email:

```
set REGALIS_ADMIN_EMAIL=...& set REGALIS_ADMIN_PASSWORD=...
python -I deploy\admin-booking\tests\e2e_admin_booking.py
python -I deploy\admin-booking\tests\ui_admin_booking.py
```

## Test accounts left on Dev

From one `e2e_admin_booking.py --keep` run (27 Nov 2026), for a look in the browser. Their password is
printed by that run only; reset it from Admin → Users if needed.

| Account | What it has |
|---|---|
| `adm-bk-a-demo-12114@example.test` (customer, pay later off on the account) | Staff bookings, source Front desk: REG-000187 (meeting room, paid afterwards on the Payments screen), REG-000189 and REG-000190 (two whole days, due), REG-000191 (monthly desk, due), REG-000192 (meeting room with a ₹100 coupon, due); Virtual Office MEM-000154 (paid on the Payments screen, Active). |
| `adm-bk-b-demo-12114@example.test` (customer) | Customer-portal bookings for comparison: REG-000194 (wallet), REG-000195 (pay later), and REG-000196 made with the old `POST /bookings/admin` (cash); Virtual Office MEM-000155 (wallet). |
| `adm-bk-fin-demo-12114@example.test` (Finance staff) | No `BOOKING.CREATE`: the New booking endpoints refuse it. |

The run's test coupon `ADMBK12114` is deactivated. Every other test run removed its own data; the UI test's
customer (`abui-…`) and the e2e runs' customers (`adm-bk-…-e2e-…`) are soft-deleted, with their bookings cancelled.

The Dev database was backed up first: `RegalisWorkSpace_Dev_20261127_before_admin_booking.bak` (SQL Server
backup folder of `SQLEXPRESS2025`).

## Deploy order (when asked; not done)

1. The offers and coupons kits first, if they are not on the server yet.
2. Backend source (Regalis-live): copy `backend/src/**`, apply `existing-files.diff`, build.
3. No database step.
4. Frontend: `node deploy/admin-booking/frontend/apply.mjs <site frontend folder>`, after the offers and coupons kits' `apply.mjs`.
5. Check: Bookings → New booking, choose a test customer, book a meeting room, see it Confirmed with the amount
   due, record the payment on Finance → Payments, then cancel the test booking.
