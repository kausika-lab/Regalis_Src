# Assets module — staged release (16 Oct 2026)

Built from `Regalis_WorkSpace_Assets_Feature_Specification.docx`. **DEPLOYED to UAT on 23 Oct 2026**
with `deploy-assets.cmd`, built from `Downloads\Regalis-live` (branch `feature/assets-live`: the live
API plus Assets, without the Workspace types release, which stays on hold).

## What is where

| Part | Location |
|---|---|
| API source | `C:\Users\Administrator\Downloads\Regalis-live` (git worktree, branch `feature/assets-live`, committed: the live UAT API plus Assets). The original build in `Regalis-assets` (`feature/assets`) also carries Workspace types |
| Migration | `db\migrations\20261016030926_AssetsModule.sql` (+ `_Rollback.sql`), then `20261023070000_AssetRoomsLinkCabins.sql` |
| Frontend | `deploy\assets\frontend\assets\*-AS1.js` (5 chunks), installed by `apply.mjs` |
| Checks | `deploy\assets\verify.ps1` (run by the deploy) |
| Evidence | `deploy\assets\evidence\e2e-assets.mjs` (kept on the server, not in git) — the browser walkthrough script; its screenshots were cleared on 23 Oct 2026 |

## How the spec maps onto the existing app

- **Item master stays `MST_InventoryItem`** (quotations and purchase orders use it). It gains
  Kind (asset / consumable), category, brand, vendor, remarks and "kept in". Each asset unit
  is its own row plus a row in `MST_AssetDetail` (room, condition, status, purchase date,
  warranty, label printed).
- **The ledger stays `TRN_InventoryTransaction`** (append-only); rows now point at the
  receipt, issue or stock count behind them. On hand is always summed from it.
- New tables: categories, rooms, code sequences, goods receipts/lines, goods issues/lines,
  repairs, item history, item documents, stock counts/lines.
- Purchase order **Receive** now posts an Incoming Goods receipt (same posting code).
- Old `/admin/inventory/*` addresses redirect to `/admin/assets/*`. The old
  `/api/inventory/*` routes stay for quotations/POs but refuse assets and opening quantities.

## Decisions taken where the spec left a choice

- Money is `DECIMAL(18,2)` like the rest of the database (exact to the paisa), not integer paise.
- Open items 2/3: straight-line depreciation, 5% floor; useful life per category, editable
  (10 / 7 / 10 / 4 years). The spec's example (₹44,505) uses 4 weeks, not 6; the formula gives ₹44,260.
- Open item 4: rooms are their own list; a room that is a workspace links to it
  (Private Cabin 1–3 linked to Private Cabin 01–03 on UAT).
- Open item 6: plain-text QR, no link; scanning inside the app finds the item.
- Open item 7: roles that can add items today get RECEIVE / ISSUE / LABELS / COUNT;
  REVERSE is Administrator only. Adjust in Roles & Permissions.
- Open item 8: no "Assigned To" field. Open item 9: costs are entered before GST.
- Open item 10: the old Items page is replaced; services (sold on quotations) are kept as a
  third kind, shown only when "Services" is chosen in the Kind filter.
- The "+ New item" asset form posts an opening-balance (or supplier) receipt behind the scenes,
  so every asset is still born from Incoming Goods.
- Only an asset's latest movement can be reversed; a receipt can be reversed only while the
  assets it created have not moved.
- Assets with no purchase date (36 of 43 rows in the team's sheet) show their cost undepreciated.
- Scanning: Chrome on Android reads stickers with the camera (BarcodeDetector). Other browsers
  get a code box — a USB scanner types into it.
- "Save PDF" opens the browser's print dialogue; choose "Save as PDF" there.

## Verified

- Backend tests: 258 / 259 integration (the known `RbacTests` "Support" failure) and 70 / 70 unit,
  including 13 new Assets tests (codes per unit, concurrency, reversals, never below zero,
  stock count, the team-sheet import, permissions).
- Migration on a restored copy of UAT: up, rollback, up again — every stock figure unchanged.
- Browser walkthrough on that copy: 32 / 32 checks, no console errors (`evidence\e2e-assets.mjs`).
