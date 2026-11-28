# Purchase order: "Deliver to" becomes "Shipping to" (28 Nov 2026)

The user asked for the purchase order PDF box headed "Deliver to" to read "Shipping to". The two admin
screen labels that mirror it were changed too, so screen and PDF match. "Deliver by" (the date) and the
"Change delivery address" button are unchanged. Local Dev only; not on UAT or production; not committed.

| Where | Change |
|---|---|
| PDF (`PurchaseOrderPdfGenerator.Generate`, Regalis.Infrastructure) | `Caption(ship, "Deliver to")` -> `"Shipping to"`, see `backend/PurchaseOrderPdfGenerator.diff` |
| Admin PO detail page | block title "Deliver to" -> "Shipping to" |
| Admin PO form | "Deliver to:" -> "Shipping to:" |

Related change the same day, data only: `db/migrations/20261127150000_CompanyEmail.sql` sets `Company.Email` to
info@regalis.co.in (the email under the company name on PO, invoice and quotation PDFs). Rollback:
`20261127150000_CompanyEmail_Rollback.sql`.

## Dev

- Frontend: `node deploy/po-shipping-label/frontend/apply.mjs frontend` (run on the repo `frontend\`).
- Backend: the C# source is not on this PC, so the one string in `D:\Regalis\local-dev\api\Regalis.Infrastructure.dll`
  was rewritten with Mono.Cecil (same IL, only the `ldstr` operand). Original DLL + PDB kept in the
  session scratchpad `replaced-dlls\before-po-shipping-label`. Takes effect when the Dev API restarts.
- Superseded the same day: that patched DLL was never installed. The Dev API now runs a build from the
  decompiled source with the caption changed in `PurchaseOrderPdfGenerator.cs` (installed with
  `deploy/po-last-used-defaults`, whose `existing-files.diff` repeats this one-line edit). Its `apply.mjs` runs
  after this kit's and sets the chunk's `sw.js` revision to `po-last-used-defaults-1`.

## UAT / production (when asked; not done)

1. Server source: apply `backend/PurchaseOrderPdfGenerator.diff`, build, deploy the API as usual.
2. Frontend: `node deploy/po-shipping-label/frontend/apply.mjs <site frontend folder>`.
3. Database: run `20261127150000_CompanyEmail.sql`.
4. Check: open any PO PDF; header shows info@regalis.co.in and the middle box says "Shipping to".

## Rollback

- Backend: revert the one line (Dev: copy the saved DLL back and restart the API).
- Frontend: `git checkout <previous> -- frontend/assets/AdminPurchaseOrderPages-C1FTHC0p.js frontend/sw.js`, then bump
  the `sw.js` revision so browsers reload.
