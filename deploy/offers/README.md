# Offers (23 Nov 2026)

Built from the brief "Admin-Controlled Offers & Customer Portal Integration" (23 Nov 2026). Before this kit the app had no offers. Bookings and invoices already had a `DiscountAmount` column, but nothing ever filled it.

Installed on the **local Dev stack only**. Not on UAT or production.

Product-owner decisions taken before building:

| Question | Decision |
|---|---|
| What the discount comes off | **The price before GST.** GST (CGST + SGST) is then charged on what is left. The invoice shows *Discount* and GST on the lower amount. The minimum purchase and the maximum discount are also checked before GST. |
| Combining offers | **A "Can be combined" tick on each offer.** A customer can put several offers on one purchase only if every one of them allows it. Otherwise it is one offer per purchase. |
| Who applies offers | **Customers, in the customer portal.** This covers their own hourly and monthly bookings, day passes and memberships (Virtual Office included), with every payment method: wallet, online and pay later. Staff bookings and front-desk pass sales are unchanged and never take an offer. |
| Install | Local Dev, plus a commit on the new branch `DHK/Offers`. No push, and no UAT or production deploy. |

**Existing behaviour is unchanged when no offer is chosen.** A booking, pass or membership bought without an offer costs exactly what it did before: same totals, same invoice, same wallet debit, and the tests check this. The edits to existing files are listed under **Files**.

## Coverage, section by section

| § | What the brief asks | Built |
|---|---|---|
| 1 | Offers Management module in the admin portal: create, edit, activate / deactivate, delete when safe, dates, discount, modules, minimum, maximum, code, description, terms | **Sales → Offers** (`/admin/offers`). The editor has every field in the brief. *Delete* is refused once a customer has used the offer ("Deactivate it instead"). |
| 1 | Fields | Name (internal), title, description, banner picture, code, percentage / fixed, value, maximum discount, minimum purchase, modules, start and end dates, active, terms, display priority. Added: *Show on the customer portal* (off = the offer works only when its code is typed), *Can be combined*, *Uses per customer*, *Total uses*. |
| 2 | Module targeting with checkboxes; several offers per module | One tick per module: My Bookings, Passes, Membership. Inside a module the admin can narrow the offer to specific **workspace types** (bookings) or **plans** (passes, memberships). With none ticked, it applies to all. |
| 3 | My Bookings: offer cards; apply at booking / payment; original, discount and final amount; validate before confirming | Offer cards at the top of **My Bookings**, with a *Book a workspace* button. At **Book a Workspace → Review & pay** an offer picker covers hourly, multi-day and monthly bookings. The chosen offers are put on the held booking before paying. The server prices them, and the payment then charges the discounted total. |
| 4 | Passes: the same; excluded passes refused | Cards on **Book Pass**, and a picker under the price. The offer ids go on the existing `/passes/order` and `/passes/purchase` calls. A plan outside the offer's list is refused on the server. |
| 5 | Membership: the same | Cards on **Membership**, and a picker at *3 · Payment*. The order summary shows the offer row and the new total. A Virtual Office bought from *Book a Workspace* is a membership plan, so it gets the Membership picker. |
| 6 | Cards / badges, discount prominent, validity, terms, code with Copy, design system, responsive, no clutter | The cards use the app's tokens and components: a black-and-gold deal badge, validity, rules, a code chip with *Copy*, and expandable terms. They scroll sideways on a phone. If no offer is live in a module, nothing is shown. The picker sits above the pay buttons, and *Have an offer code?* is a single link until it is opened. |
| 7 | Validation and business rules | Every rule is enforced in one place, `OfferEngine`, and on the server. See **Rules**. |
| 8 | Overview: total / active / inactive / expired; per offer name, discount, modules, validity, status, created; search, filter, sort, pagination | Cards for Total, Active, Inactive, Expired and **Scheduled**; each card is also a filter. Search covers name, title and code. Filters for status and module. Sorting by priority, newest, ending soonest or name. 20 offers per page. Each row also shows how often the offer was used and the discount given. **Uses** lists every booking, pass or order that used the offer. |
| 9 | Tables, CRUD APIs, customer APIs by module, server-side validation and calculation, one central service | 5 tables (see **Database**). `OfferEngine` is the only code that validates offers or calculates discounts. Bookings, passes and memberships, the preview and the charge all go through it. |
| 10 | Admin and customer workflows | Both run end to end. The tests drive the screens with a browser. |

## Rules

- **Applied automatically** (user request, 23 Nov 2026). At *Review & pay* the server picks the eligible listed offer that saves the customer the most. If several combinable offers together save more, it picks those instead (`POST /api/offers/preview` with `auto: true`). The panel only shows the applied offer: there is nothing to tap or remove (user request, 23 Nov 2026). A typed code replaces it ("Have an offer code?"). Code-only offers are never applied automatically.
- **Shown** only when the offer is active and today (IST) is inside its start and end dates, inclusive. It must be ticked for the module and have *Show on the customer portal* on. A code-only offer is applied by typing its code.
- **Discount**:
  - A percentage is rounded to the paisa and capped at the maximum.
  - A fixed amount never takes the price below zero.
  - Several offers are each worked out on the full price, and together never exceed it.
  - GST is then charged on the rest, each half rounded half away from zero, exactly as the booking and pass engines do.
- **Eligible** only when all of these hold:
  - The offer is in the right module.
  - The plan or workspace type is in its list (if it has one).
  - The price before GST reaches the minimum.
  - The customer's own uses and the total uses are under their limits.
- **One purchase, one use.** Every day of a multi-day booking counts as one purchase. A fixed discount is spread across the days in proportion to their price.
- **Uses are given back** when a booking is cancelled or expires, a pass is cancelled, or an online pass order is abandoned (after 30 minutes). Nothing has to run to make this happen: the count is worked out from the booking, pass or payment the use points to.
- **Checked again before money moves.** The server re-checks the offer when it is applied, when the online order is created, and when a wallet or pay-later payment is confirmed. If an offer was switched off or ended meanwhile, the payment is refused with `OFFER_NOT_AVAILABLE` ("Please start the booking again"). Once an online payment has been taken, the price the order was made at is honoured.
- **No changes once payment has started.** Offers on a held booking cannot be changed after its online order exists.
- **Duplicates.** The same offer twice on one purchase is refused. The preview counts it once.

## Where it differs from the brief, and why

- **Customers only, not the front desk** (product-owner decision). Admin "book for a customer" and front-desk pass sales ignore offer ids.
- **Bookings get the offer after the hold.** A booking only exists once its 10-minute hold is taken, so the portal takes the hold, puts the offers on it, then pays, all in one click. If the server refuses the offer, the hold is released at once, so the slot is free again.
- **Display priority** sorts the cards: lower numbers show first.
- **Scheduled** (active, but not started yet) is a fifth overview card. An offer is exactly one of Active, Inactive, Scheduled or Expired. *Expired* means past its end date, whether or not it is switched on.
- **The banner picture is kept in the database** (`TRN_OfferImage`, PNG / JPG / WebP, under 2 MB, magic bytes checked). No file storage has to be set up on the server. It is served without sign-in at `/api/offers/{id}/image`, so the portal can show it in an `<img>`. Offers are public promotions.

## Database

`db/migrations/20261123120000_Offers.sql` (rollback: `..._Rollback.sql`, which **drops the offer tables**; bookings, passes and invoices keep their discounted amounts). Idempotent, one transaction.

- **New tables:**
  - `MST_Offer`: audited and soft-deleted like other master tables. The code is unique while the offer is not deleted (filtered index).
  - `MST_OfferModule`: which modules an offer applies to.
  - `MST_OfferScope`: optional narrowing to workspace types or plans.
  - `TRN_OfferImage`: the banner picture.
  - `TRN_OfferRedemption`: one row per offer per booking or pass. It has `PurchaseKey`, the base amount and the discount.
- **No existing table changes.** Bookings use their existing `DiscountAmount` column. Invoices use their existing `DiscountAmount` and `SubTotal` / `TaxableAmount` columns.
- **Permissions:**
  - `OFFER.VIEW` → Administrator, Operations Manager, Sales / Front desk.
  - `OFFER.MANAGE` → Administrator, Operations Manager.
- **Menu:** `ADM_OFFERS` is *Offers* under *Sales* (after CRM), route `/admin/offers`, icon `BadgePercent`. It goes to every staff role with `OFFER.VIEW`. Customers get no new menu row: the offers appear inside their existing pages.

## API

Admin (`api/admin/offers`, read needs `OFFER.VIEW`, changes need `OFFER.MANAGE`):

- `GET` (list + overview; `search`, `status`, `module`, `sort`, `page`, `pageSize`)
- `GET options`
- `GET {id}`
- `GET {id}/redemptions`
- `POST`, `PUT {id}`
- `POST {id}/activate|deactivate`
- `DELETE {id}`
- `POST|DELETE {id}/image`

Customer (`api/offers`, signed in; preview and apply are for customers only):

- `GET ?module=booking|pass|membership`: the offers for a module.
- `POST preview`: the module's offers with whether each fits this purchase, the selected offers priced exactly as checkout will charge, and any typed code.
- `POST bookings/apply`: put offers on (or, with none, take them off) the customer's own held bookings.
- `GET {id}/image`: anonymous.
- **Existing calls with a new optional field:** `POST /api/passes/order` and `POST /api/passes/purchase` take `offerIds`. Without it they behave as before.

## Files

- **Backend, new** (`backend/src`):
  - Domain: `Offers/OfferEntities.cs`
  - Application: `Offers/OfferDtos.cs`, `Offers/IOfferServices.cs`
  - Infrastructure: `Persistence/Configurations/OfferConfigurations.cs`, `Services/Offers/{OfferEngine, OfferAdminService, OfferPortalService}.cs`
  - API: `Controllers/OffersController.cs`
- **Backend, existing:** `backend/existing-files.diff` (63 changed lines; apply by hand):
  - `DependencyInjection.cs`: 3 registrations, next to the Wi-Fi kit's lines.
  - `PassOrderRequest` / `PurchasePassRequest`: optional `OfferIds`.
  - `PassService`: price the chosen offers; reserve them on the online order; tie them to the pass; invoice SubTotal / Discount / line amount = the price before the offer.
  - `BookingService`: re-check before a wallet / pay-later / cash confirmation. The invoice's CGST is on the price after the discount (identical when the discount is 0), and the invoice line names the offer.
  - `PaymentService.CreateOrderAsync`: re-check before an online order.
- **Frontend:** `frontend/apply.mjs`. It is idempotent and expects the wifi kit's patches, which are in the repo. It has already been run on the repo `frontend/`.
  - Copies `assets/OffersKit-OF1.js` (cards, picker, apply call) and `assets/OffersAdminPage-OF1.js`.
  - Patches `index-DI5X4c9f.js` (route, icon) and `MyBookingsPage`, `BookWorkspacePage`, `PassesPage`, `MembershipPage` (cards, picker, totals, offer ids).
  - Patches `sw.js` (revision `offers-1`).
- **Tests:** `tests/e2e_offers.py`, `tests/ui_offers.py`.

## Deploy order (when asked; not done)

1. Backend source (Regalis-live): copy `backend/src/**`, apply `existing-files.diff`, and build.
2. Run `20261123120000_Offers.sql` together with the new API.
3. Front end: promote the two new chunks and the five patched ones, plus `sw.js`.
4. Nothing is live until an admin creates and activates an offer.

## Tested (local Dev stack, 23 Nov 2026)

How it was installed:

- `local-dev\api` Regalis.*.dll were rebuilt from the decompiled tree with this kit linked.
- The migration was run on Dev twice (it is idempotent).
- `apply.mjs` was run on the repo `frontend/` (and run again: everything "already done").
- Backups: `D:\Regalis\local-dev-backup-20261123-before-offers` (the API) and `RegalisWorkSpace_Dev_20261123_before_offers.bak` (in the instance Backup folder).
- During the tests, mail went to a local SMTP sink. Purchases also trigger the Wi-Fi kit's emails.

Results:

- `tests/e2e_offers.py` (API + SQL): **110/110**. It covers:
  - Permissions and menus (Sales can view but not change; customers kept apart; staff cannot apply customer offers).
  - Validation, including a duplicate code in any case.
  - Overview counts, search, filters, sort and pages.
  - What each module lists: scheduled, inactive and code-only offers are hidden.
  - The arithmetic: cap, minimum, fixed amount larger than the price, combining refused or allowed, typed and unknown codes.
  - An hourly booking paid from the wallet (wallet, invoice and history checked).
  - One use per customer, and the use given back on cancellation.
  - A cabin with two combined offers paid online (the order amount checked).
  - The offer switched off between apply and pay → wallet and online refused; removing offers restores the price.
  - Multi-day split counted as one use; a monthly booking on pay later.
  - Day pass by wallet and online (the order-time price honoured); an excluded plan refused.
  - A code-only membership on pay later.
  - Delete refused when used and allowed when unused; code reuse after delete.
  - The picture: upload, anonymous download, a renamed file refused.
  - Purchases **without** offers unchanged (booking 236.00, pass 944.00, invoices with discount 0), and existing endpoints still answer.
- `tests/ui_offers.py` (headless Chrome, 1366 px and 390 px): **44/44**. It covers:
  - Admin: create an offer through the editor (field messages, picture), deactivate / activate, the Uses dialog, card filters, the phone layout.
  - Customer: cards on My Bookings / Passes / Membership (picture, code, terms).
  - A Meeting Room booked through the wizard with the offer: ₹944.00 → ₹849.60, paid from the wallet, checked in SQL.
  - A day pass: ₹944.00 → ₹849.60.
  - A membership with a typed code-only offer (summary, total payable, wallet).
  - No sideways scroll on a phone; no JS errors; existing customer pages unchanged.
- Regression:
  - Frontend unit tests 24/24.
  - The Wi-Fi kit's `e2e_wifi.py`, which buys passes and bookings through the changed services: **135/135**.

All test scripts delete the offers and redemptions they create and soft-delete their users. The test customers' passes, invoices and cancelled bookings stay on Dev; they belong to soft-deleted users.
