# Coupon codes (25 Nov 2026)

Built from the brief "Add a Coupon Code option to the Offers screen" (25 Nov 2026). Before this kit,
an offer could carry an optional code (offers kit, 23 Nov 2026). There were no coupons of their own.

Installed on the **local Dev stack only**. Not on UAT or production. **Needs the offers kit** (its
migration, backend and frontend) installed first.

Product-owner decisions taken before building:

| Question | Decision |
|---|---|
| How coupons are stored | **Separate coupon tables**, not offers with a code. Coupons have their own tables, admin tab, rules service and redemptions. |
| A coupon and the automatic offer | **The coupon replaces the offer.** They are never combined on one purchase. *Remove coupon* brings the automatic offer back. |
| Usage limit | **Both**: a total limit (everyone together) and a limit per customer. Both are optional. |
| Showing coupons to customers | **Never** (user request, 25 Nov 2026, after the first build). No cards, no chips, no list endpoint. A coupon applies only when the customer types a code that matches one. |
| Install | Local Dev, plus a commit on the new branch `DHK/Coupons`. No push, and no UAT or production deploy. |

**Existing behaviour is unchanged when no coupon is used.** A booking, pass or membership bought
without a coupon costs exactly what it did before, with or without an offer: same totals, same
invoice, same wallet debit. The tests check this, and the offers kit's own tests still pass. The
edits to existing files are listed under **Files**.

## Coverage, point by point

| Brief | Built |
|---|---|
| Create and manage coupon codes in the Offers screen | **Sales → Offers** now has two tabs: *Offers* (unchanged) and *Coupon codes* (`/admin/offers?tab=coupons`). |
| Create, edit, activate, deactivate, delete | *New coupon*, *Edit*, *Activate* / *Deactivate* and *Delete* on each row. Delete is refused once a customer has used the coupon ("Deactivate it instead"). The code of a used coupon cannot change; its other fields can. |
| Fields: code, discount type, value, valid from / until, usage limit, minimum booking amount | All of them. Added: *Description* (shown after the code is applied), *Maximum discount* (for a percentage), *Uses per customer*. |
| Which services it applies to | Tick boxes for **My Bookings**, **Passes** and **Membership** (Virtual Office included). |
| Show available coupons in the Customer Portal | **Changed at the user's request: coupons are never shown.** The first build had coupon cards and *Available coupons* chips; both were removed, with the customer list API. The admin shares codes outside the app. |
| Enter a coupon code at checkout | The existing *Have an offer code?* link is now *Have a coupon or offer code?*. A typed code is tried as a coupon first, then as an offer code (as before). |
| Validate before applying | The server checks every coupon twice: when it is previewed or applied, and again before money moves. See **Rules**. |
| Calculate and show the discount and final amount | The panel shows the original amount, the coupon discount (before GST), the price after the discount, CGST + SGST on it, and *You pay* with the old total struck through. The page's own total and pay buttons use the new amount. |
| Messages for invalid, expired, inactive and already-used codes | One message each, plus *not started yet*, *not for this service*, *below the minimum* and *usage limit reached*. See **Messages**. |
| Usage limits and validity dates enforced | On the server, under a lock per coupon, so two customers cannot both take the last use. |
| Database relationships, APIs, frontend validation | 3 tables with foreign keys (see **Database**). Admin and customer APIs (see **API**). The coupon editor checks every field in the browser before saving, and the server checks them again. |

## Rules

- **Discount**: off the price before GST. A percentage is rounded to the paisa and capped at the
  maximum. A fixed amount never takes the price below zero. GST is then charged on what is left,
  each half rounded half away from zero, exactly as the booking and pass engines do. The coupon uses
  the offers kit's arithmetic (`OfferEngine.Price`), so coupon and offer prices cannot drift apart.
- **Valid** only when all of these hold, checked in this order:
  1. The code exists.
  2. Today (IST) is on or before *Valid until*.
  3. The coupon is active.
  4. Today is on or after *Valid from*.
  5. It is ticked for this service.
  6. The price before GST reaches the minimum booking amount.
  7. This customer's uses are under *Uses per customer*.
  8. All uses are under the total limit.
- **Code**: upper case, 3 to 30 letters, digits, `-` or `_`. It is unique among coupons that are not
  deleted. A coupon cannot take a code an offer already uses. If an offer is later given a coupon's
  code, the coupon wins at checkout and the admin list flags the clash in red.
- **One purchase, one use.** Every day of a multi-day booking counts as one purchase. The
  discount is spread across the days in proportion to their price.
- **A coupon replaces offers.** At payment, entering a coupon removes the automatic offer. On the
  server, `/passes/order` and `/passes/purchase` ignore offer ids when a coupon code is sent. Putting a
  coupon on a held booking removes the offers on it. If an offer is later put on the same hold
  through the offers API, the coupon stops counting as used and the admin *Uses* list shows it as
  *Released*.
- **Uses are given back** when a booking is cancelled or expires, a pass is cancelled, or an online
  pass order is abandoned (after 30 minutes). Nothing has to run to make this happen: the count is
  worked out from the booking, pass or payment the use points to, the same way offers do it.
- **Limits under a lock.** Putting a coupon on a hold, creating an online pass order and buying a
  pass from the wallet / pay later / cash each take an exclusive lock on that coupon
  (`sp_getapplock`) and count again inside the transaction. Two customers racing for the last
  use: one gets it, the other gets *reached its usage limit*. The tests check this race.
- **Checked again before money moves.** A coupon switched off or ended after it was put on a hold
  is refused at wallet / pay-later confirmation and at online-order creation, with
  `COUPON_NOT_AVAILABLE` ("Please start the booking again"). Once an online payment has been
  taken, the price the order was made at is honoured.
- **Never listed.** There is no customer API that lists coupons (`GET /api/coupons` does not exist), the offers list carries no coupon codes, and a wrong code reveals no other code. The tests check all three.
- **Customers only.** Staff cannot preview or apply coupons. Admin "book for a customer" and
  front-desk pass sales ignore the coupon code, as they ignore offers.

## Messages

| Code | When | Message (example) |
|---|---|---|
| `COUPON_INVALID` | No such code, or deleted | The coupon code ABC is not valid. Check the code and try again. |
| `COUPON_EXPIRED` | Past *Valid until* | The coupon ABC expired on 30 Nov 2026. |
| `COUPON_INACTIVE` | Switched off | The coupon ABC is not active. |
| `COUPON_NOT_STARTED` | Before *Valid from* | The coupon ABC can be used from 1 Dec 2026. |
| `COUPON_NOT_APPLICABLE` | Not ticked for this service | The coupon ABC cannot be used on passes. |
| `COUPON_MIN_AMOUNT` | Below the minimum | Spend at least ₹300.00 (before GST) to use the coupon ABC. |
| `COUPON_ALREADY_USED` | This customer's limit reached | You have already used the coupon ABC. |
| `COUPON_LIMIT_REACHED` | Total limit reached | The coupon ABC has reached its usage limit. |
| `COUPON_NOT_AVAILABLE` | Switched off after it was put on a hold | The coupon ABC is no longer available, so this discounted price cannot be charged. Please start the booking again. |

The preview answers 200 with `valid: false` and the message, so the panel can show it. The
purchase and apply calls answer 422 with the same message and code.

## Database

`db/migrations/20261125120000_Coupons.sql`. It stops without changing anything if the offers
migration is missing. Idempotent, one transaction. Rollback: `..._Rollback.sql`, which **drops the
coupon tables**. Bookings, passes and invoices bought with a coupon keep their discounted amounts.

- `MST_Coupon`: audited and soft-deleted like other master tables. Checks: discount type 1 or 2,
  value above 0 and a percentage at most 100, *Valid until* on or after *Valid from*, limits above
  0. Unique filtered index on `Code` while not deleted.
- `MST_CouponModule`: the services (1 My Bookings, 2 Passes, 3 Membership). Foreign key to the
  coupon, unique per coupon and service.
- `TRN_CouponRedemption`: one row per booking (each day of a multi-day booking) or pass purchase.
  It has `PurchaseKey`, the price before GST, the discount and the code at the time. Foreign keys to
  `MST_Coupon`, `MST_User`, `TRN_Booking`, `TRN_CustomerPass` and `TRN_Payment`.
- **No existing table changes.** Bookings and invoices use their existing `DiscountAmount` columns, as
  the offers kit does.
- **No new permission or menu row.** Coupons live in the Offers screen and use `OFFER.VIEW` (see
  them) and `OFFER.MANAGE` (change them).

## API

Admin (`api/admin/coupons`; reading needs `OFFER.VIEW`, changes need `OFFER.MANAGE`):

- `GET` (list + overview; `search`, `status`, `module`, `sort`, `page`, `pageSize`)
- `GET {id}`
- `GET {id}/redemptions`
- `POST`, `PUT {id}`
- `POST {id}/activate|deactivate`
- `DELETE {id}`

Customer (`api/coupons`, signed in; preview and apply are for customers only):

- `POST preview`: checks a code against the purchase and prices it exactly as checkout will.
- `POST bookings/apply`: puts a coupon on the customer's own held bookings (or, with an empty code,
  takes it off). It replaces any offer on them.
- **Existing calls with a new optional field:** `POST /api/passes/order` and `POST /api/passes/purchase`
  take `couponCode`. Without it they behave as before.

## Files

- **Backend, new** (`backend/src`):
  - Domain: `Coupons/CouponEntities.cs`
  - Application: `Coupons/CouponDtos.cs`, `Coupons/ICouponServices.cs`
  - Infrastructure: `Persistence/Configurations/CouponConfigurations.cs`, `Services/Coupons/{CouponEngine, CouponAdminService, CouponPortalService}.cs`
  - API: `Controllers/CouponsController.cs`
- **Backend, existing:** `backend/existing-files.diff` (31 changed lines, on top of the offers kit's
  diff; apply by hand):
  - `DependencyInjection.cs`: 3 registrations, next to the offers kit's lines.
  - `PassOrderRequest` / `PurchasePassRequest`: optional `CouponCode`.
  - `PassService`: the price, order reservation, purchase record and invoice note go through
    `CouponEngine`, which hands over to `OfferEngine` unchanged when no coupon is given.
  - `BookingService`: re-check the coupon before a wallet / pay-later / cash confirmation; the
    invoice line names the coupon.
  - `PaymentService.CreateOrderAsync`: re-check the coupon before an online order.
- **The offers kit's files are not changed.** Coupons sit on top of them: the backend calls
  `OfferEngine` and the frontend script patches the offers chunks.
- **Frontend:** `frontend/apply.mjs`. It is idempotent, refuses to run without the offers kit's
  frontend, and has already been run on the repo `frontend/`.
  - Copies `assets/CouponsKit-CP1.js` (preview and apply calls) and `assets/CouponsAdminPage-CP1.js` (the admin tab).
  - Patches `OffersAdminPage-OF1.js` (tabs) and `OffersKit-OF1.js` (the payment panel).
  - Patches `BookWorkspacePage-MB2.js`, `PassesPage-CxAkHUg-.js` and `MembershipPage-Bk9mQr2v.js` (coupon code on the purchase calls).
  - Patches `sw.js` (revision `coupons-1`).
- **A display fix that also affects offers.** On **Book Pass**, the order summary card showed the
  price and the GST before any discount, next to the discounted total. Its lines did not add up
  (this was already so with an offer). It now has a *Coupon / Offer discount (before GST)* row and
  shows the GST actually charged. The amounts charged were always right.
- **One line of the offers kit's UI test changed** (`deploy/offers/tests/ui_offers.py`): it typed
  into the field by its old label *Offer code*. It now accepts *Coupon or offer code* as well.
- **Tests:** `tests/e2e_coupons.py`, `tests/ui_coupons.py`.

## Tests (local Dev, 25 Nov 2026)

| Script | Result |
|---|---|
| `tests/e2e_coupons.py`: API, money and database | **96 / 96** |
| `tests/ui_coupons.py`: headless Chrome, 1366 px and 390 px | **93 / 93** |
| `node --test frontend/tests/` | 24 / 24 |
| Regression `deploy/offers/tests/e2e_offers.py` | 110 / 110 |
| Regression `deploy/wifi/tests/e2e_wifi.py` | 135 / 135 |
| Regression `deploy/offers/tests/ui_offers.py` | 40 / 40 (after the label change under **Files**) |

`e2e_coupons.py` covers:

- Permissions.
- Every validation rule.
- Every message above.
- The arithmetic, including the maximum cap.
- A day pass from the wallet, with the invoice checked in SQL.
- Per-customer and total limits, and the two-customer race for the last use.
- An online pass order through the sandbox gateway.
- A coupon sent together with offers.
- An hourly booking with a coupon, paid from the wallet.
- A coupon replacing an offer on a hold, and the reverse.
- A coupon switched off while on a hold.
- A hidden Virtual Office coupon on pay later.
- Delete, edit and the *Uses* list.
- Uses given back on cancellation.
- Offers unchanged: typed offer code, automatic offer, offer invoice text, no-discount prices.

Run them with the API sending mail to a local SMTP sink. Purchases trigger the Wi-Fi kit's emails,
and the local Dev stack otherwise sends real email:

```
set REGALIS_ADMIN_EMAIL=...& set REGALIS_ADMIN_PASSWORD=...
python -I deploy\coupons\tests\e2e_coupons.py
```

## Deploy order (when asked; not done)

1. The offers kit first, if it is not on the server yet.
2. Backend source (Regalis-live): copy `backend/src/**`, apply `existing-files.diff`, and build.
3. Database: `db/migrations/20261125120000_Coupons.sql`.
4. Frontend: `node deploy/coupons/frontend/apply.mjs <site frontend folder>`, after the offers kit's `apply.mjs`.
5. Check: *Sales → Offers → Coupon codes* opens, create a test coupon, preview it on a day pass, then delete it.
