# Quotations — build notes

Implements `Regalis_WorkSpace_Quotation_Feature_Specification.docx` (v1.0, Sept 2026)
against the existing Quotation module rather than replacing it: the tables, the
number sequence, the approve/reject routes and the invoice conversion were already
there and were extended, not rebuilt.

Deployed to UAT on 23 Sep 2026. Migration `20260922191326_AddQuotationBillingFields`.

## What a quotation now does

Admin raises it at **Billing & Finance → Quotations**, adds service lines
(Cabin / Desk / Meeting Room / Pass / custom) with Qty × Duration × Rate and a
per-line GST rate, applies a discount, and clicks **Save and send**. It goes out by
Email and/or WhatsApp with the PDF and a portal link, and is frozen from that moment.
The customer approves or rejects it in their portal; an approved quotation converts to
an invoice in one click.

Three rules the code enforces in both the API and the UI:

- **A sent quotation is never edited.** New prices go out through **Revise**, which
  opens a fresh draft numbered `…-R1` and marks the original `Revised`. The API
  returns 422 on an edit, a delete or a second revision.
- **Only the newest revision can be approved.** A decision on a replaced quotation is
  refused even if the customer still has the old link.
- **Money is computed server-side.** Totals sent from the browser are ignored; the
  form's totals box is a preview and says so.

## Calculation (§5.2)

Line value is `Qty × Duration × Rate`. The whole-quotation discount is split across
lines in proportion to line value, with the **last paying line absorbing the paisa
difference**, so the shares always add back to exactly the discount. GST is charged
per line on what is left, then split CGST/SGST inside `Quotation.HomeStateCode` or
charged as IGST outside it. TDS comes off the taxable value, then adjustment, then
rounding to the rupee.

The document's worked example is a test:
`tests/backend/Regalis.IntegrationTests/QuotationTests.cs` →
`WorkedExample_MatchesTheSpecificationToThePaisa`. It asserts ₹1,08,200 → ₹1,21,292.00
and the per-line discount shares, to the paisa.

## Settings to configure before go-live

All in `MST_AppSetting`, group `Quotation`. The migration inserts them with defaults;
these three need real values:

| Key | Set to | Why |
|---|---|---|
| `Quotation.PortalBaseUrl` | the public site URL | The approve link in the email and the WhatsApp message is built on it. UAT is set to `http://localhost:8080`, which is only reachable on the box. |
| `Quotation.TermsAndConditions` | the standard terms | Empty today, so new quotations start with a blank terms box. |
| `Quotation.HomeStateCode` | `29` (Karnataka) | A matching place of supply is intra-state (CGST+SGST); anything else is IGST. |

Also worth a look: `Quotation.ValidityDays` (15), `Quotation.DefaultGstPercent` (18),
`Quotation.TdsOptions`, `Quotation.ExpiryReminderDays` (2).

## Email and WhatsApp

Both sit behind `IQuotationDispatcher` (Invariant 5).

- **Email** reuses the `Smtp` section the OTP sender already uses. With no
  `Smtp:Host` configured the send is recorded as *"No mail server is configured —
  email was not sent"* rather than failing, so the flow is walkable on UAT.
- **WhatsApp** uses the Business API when `WhatsApp:ApiBaseUrl` and
  `WhatsApp:AccessToken` are set, with the pre-approved template
  `WhatsApp:TemplateName` (default `quotation_sent`). With neither set it returns a
  `wa.me` link and logs *"Shared via WhatsApp (manual)"* — the document's stated
  fallback. **No WhatsApp provider is configured yet** (open item 5 in the spec).

A send that reaches neither channel leaves the quotation a draft.

## Deploy order

The binaries read new columns and EF names every column explicitly, so a missing
column breaks *every* query on `TRN_Quotation`. **Migration first, binaries second** —
the same rule as `deploy-google-signin.cmd`:

1. `db/migrations/20260922191326_AddQuotationBillingFields.sql` (idempotent; also
   inserts the settings rows and backfills the enum columns on existing rows)
2. publish the API, stop the `regalis-api` pool, copy **excluding `web.config`**,
   start the pool
3. the SPA chunks are already in `frontend/assets` with bumped `sw.js` revisions

Rollback: `20260922191326_AddQuotationBillingFields_Rollback.sql` drops the two new
tables and every added column. It is **not loss-free** — attachments, the activity
timeline and the revision links go with it.

### Why the backfill matters

EF gives a new non-nullable `byte` column a default of `0`, which is not a member of
any of these enums. Left alone, a quotation raised before this migration reads as tax
mode 0 and the screen shows *"IGST @ 0%"* where it charged CGST and SGST. The totals
were always right; only the labelling was wrong. The forward script fixes it.

## Frontend

No frontend source exists in this repo, so the change is patched into the built
bundles (see the existing convention):

- `frontend/assets/AdminQuotationPages-COWOrOkL.js` — list, full-page form, detail,
  send popup, record-decision popup
- `frontend/assets/QuotationsPage-CKs53Gwr.js` — customer portal, mobile-first
- `frontend/assets/DataTable-7lkt-S8A.js` — optional `onRowClick`, additive
- `frontend/assets/index-DI5X4c9f.js` — routes `quotations/new` and
  `quotations/:id/edit`, both gated on `QUOTATION.CREATE`
- `frontend/assets/index-BgQ7mK4p.css` — the `qtn-*` styles
- `frontend/sw.js` — revision markers `quotation-feature-1`, `quotation-routes-1`,
  `quotation-css-1`, `quotation-rowclick-1`

**A real build from source will overwrite all of this.** The frontend source at
`…\Downloads\Regalis\frontend` is stale against what is deployed.

## Not built

Called out so nobody goes looking for them:

- **Drag-to-reorder lines.** Rows move with ↑ / ↓ buttons instead.
- **"Add services in bulk" and multi-row bulk actions** (§3.3). One row at a time.
- **PDF preview pane on the detail screen** (§3.6). The document is a download; the
  screen shows the same figures as HTML.
- **Delivery receipts** (Delivered / Read) on the activity timeline. The timeline
  records Sent or Failed; the providers' webhooks are not wired up.
- **Monthly rates for cabins and desks.** `MST_Workspace.PricePerMonth` was added and
  flows through the Workspaces API, but **no workspace has one set**, so a
  cabin or desk line auto-fills a rate of 0 and the admin types it. Set the monthly
  prices in Workspaces to get §2.1's auto-fill.
