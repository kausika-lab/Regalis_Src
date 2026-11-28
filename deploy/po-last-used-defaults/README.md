# Purchase orders: last-used defaults and the "Shipping to" name (28 Nov 2026)

Local Dev only. Not on UAT or production. Not committed.

## What was asked

1. **Terms & conditions.** "What we have done as last changes needs to be the default." Meaning confirmed by
   the user: when someone saves a purchase order with edited terms, those terms become the default for the
   next new PO.
2. **Shipping (delivery) address.** The last one saved on a PO becomes the default for the next new PO.
3. **"Rupesh" in place of "RR Nagar", on purchase orders only.** The location (MST_Location id 2) keeps the
   name RR Nagar everywhere else: bookings, invoices, admin screens.

## Decisions

- Terms and address are saved by the **backend**, inside the PO create / update transaction. A frontend call
  to `PUT /api/settings` would fail for Finance and Operations Manager, who have PURCHASE.CREATE/EDIT but not
  SETTING.EDIT.
- The defaults are ordinary `MST_AppSetting` rows (group Billing), so they stay visible and editable under
  Settings → Billing:

  | Key | Written when | Value |
  |---|---|---|
  | `Purchase.TermsAndConditions` (exists) | a PO is saved with non-blank terms that differ from it | the PO's terms, exactly as stored on the PO (trimmed by the existing PO code) |
  | `Purchase.LastDeliveryLocationId` (new, int) | a PO is saved with a non-blank delivery address | the PO's LocationId |
  | `Purchase.LastDeliveryAddress` (new, string) | same | the PO's delivery address text |
  | `Purchase.ShipToName` (new, string, migration) | only by an admin, through Settings | `Rupesh` |

- A missing row is created on the first save (shape copied from migration `20260925103627`). A soft-deleted row
  is left alone. A value equal to the stored one writes nothing, so ModifiedDate does not move.
- ModifiedBy / ModifiedDate and the generic `UPDATE AppSetting` audit row come from the save-changes
  interceptor (same as the Settings screen). The PO service also writes named audit rows
  `PO_DEFAULT_TERMS_UPDATED` / `PO_DEFAULT_DELIVERY_UPDATED` against the PO, following its existing
  `AddAudit` pattern.
- The settings read cache (60 s, key `appsetting:<key>`) is evicted after the commit, so the next
  form-options call returns the new defaults at once.
- **Ship-to name:** the PDF "Shipping to" box prints `Purchase.ShipToName` in place of the location-name line
  (company name and the address lines are unchanged). Blank setting = location name, as before. Nothing is
  renamed in MST_Location, and the PO APIs still return the real `locationName`. The screens swap the name.

## Coverage

| Item | Where | What it does |
|---|---|---|
| Terms default | backend | create + update move `Purchase.TermsAndConditions`; blank never overwrites |
| Terms hint | PO form | "Pre-filled with the terms saved on the last purchase order. The terms you save here become the default for the next new PO." |
| Address default | backend | create + update store LocationId + DeliveryAddress as a pair, only when the address is non-blank |
| form-options | API | new fields `lastDeliveryLocationId`, `lastDeliveryAddress`, `shipToName` (null when unset/blank) |
| New-PO pre-fill | PO form | uses the last location + address when that location is in the active location list; otherwise the old behaviour (default location + its address) |
| Edit PO | PO form | unchanged (pre-filled from the PO itself) |
| Ship-to name | PDF, PO detail "Shipping to" block, PO form "Shipping to:" line (shown when the address is empty), "Change delivery address" picker labels | `shipToName` in place of the location name |
| Not changed | PO list (location under the supplier name), list filter, Locations, Bookings, invoices, the header location switcher | keep the location name |

## Rules and behaviour worth knowing

- "Last saved" is literal: re-saving an older draft whose terms differ from the current default makes **its**
  terms the default again. The same applies to the address.
- A PO sent without an address gets the location's address (existing behaviour), and that becomes the last
  address. The stored address is never blank.
- `Purchase.ShipToName` is one value for the whole app. With more than one location, every location would show
  that name in the PO screens and picker. Dev has one location. If more branches are added, this needs a
  per-location value (open question, not built).
- The last location id is passed through as stored. The **screen** checks it against active locations.

## Where it differs from the brief

- The ship-to name goes to the PDF through the existing `PurchaseOrderPdfContext.LocationName` (set in
  `PurchaseOrderService.GetPdfAsync`), not by editing `PurchaseOrderPdfGenerator`. That field is printed only
  in the "Shipping to" box, so the result is the same with a smaller edit.
- When a location has no address at all, the screens fall back to the ship-to name (before: the location name)
  as the address text. Dev's location has an address, so this path is not hit on Dev.

## Database

- `db/migrations/20261128100000_PurchaseShipToName.sql`: inserts `Purchase.ShipToName` = `Rupesh` if missing
  (idempotent, one transaction). Applied on Dev.
- Rollback `20261128100000_PurchaseShipToName_Rollback.sql`: deletes `Purchase.ShipToName`,
  `Purchase.LastDeliveryLocationId` and `Purchase.LastDeliveryAddress`. `Purchase.TermsAndConditions` is kept.
- No table or column changes.

## API

`GET /api/purchase-orders/form-options` gains `lastDeliveryLocationId` (int?), `lastDeliveryAddress` (string?) and
`shipToName` (string?). `POST /api/purchase-orders` and `PUT /api/purchase-orders/{id}` keep their contracts and
now also move the defaults. No new endpoint and no new permission.

## Files

- `backend/src/Regalis.Infrastructure/Services/PurchaseOrderDefaults/PurchaseOrderLastUsed.cs` (new)
- `backend/existing-files.diff`: `PurchaseOrderService.cs` (constructor gets `IMemoryCache`, already registered
  by `AddMemoryCache`; create / update / form-options / PDF), `PurchaseOrderFormOptionsDto.cs` (3 optional
  parameters at the end), plus the po-shipping-label "Shipping to" caption for completeness.
- `frontend/apply.mjs`: patches `AdminPurchaseOrderPages-C1FTHC0p.js` (9 edits, idempotent, CRLF-safe) and sets
  the `sw.js` revision `po-last-used-defaults-1`. Run it after po-shipping-label's apply.mjs.
- `tests/e2e_po_last_used_defaults.py`, `tests/ui_po_last_used_defaults.py`

## Tests (28 Nov 2026, local Dev)

| Suite | Result |
|---|---|
| `python -I tests/e2e_po_last_used_defaults.py` (needs `PDFLIB` = a folder with pypdf) | 60/60 |
| `python -I tests/ui_po_last_used_defaults.py` (needs `PYLIB` = websocket-client), 1366 + 390 px | 17/17 |
| `node --test frontend/tests/` | 24/24 |

The e2e covers create/update for admin and Finance (Finance is refused on `PUT /api/settings` but still moves
the defaults), blank and whitespace values, equal values (row untouched), exact text and a 4000-character value,
refused saves (unknown supplier, no lines, non-draft PO), a missing terms row re-created, a last location
that is not active, the PDF ("Shipping to", "Rupesh", header email info@regalis.co.in; blank ship-to name
gives "RR Nagar" again), and that the location and the PO API still say "RR Nagar". Both suites put every
setting back exactly as found (value, ModifiedDate, ModifiedBy) and cancel + soft-delete their POs. PO numbers
used by the tests stay used.

## Deploy to UAT / production (when asked; not done)

1. Database: run `20261128100000_PurchaseShipToName.sql`.
2. Server source: copy the new file, apply `backend/existing-files.diff` by hand, build, deploy the API.
3. Frontend: `node deploy/po-shipping-label/frontend/apply.mjs <site frontend>`, then
   `node deploy/po-last-used-defaults/frontend/apply.mjs <site frontend>`.
4. Check: new PO → terms and address pre-filled from the last PO; detail and PDF show "Rupesh"; Locations still
   shows the location name.

## Rollback

Backend: previous build. Frontend: restore the chunk + `sw.js` from git and bump the `sw.js` revision. Database:
the rollback script.
