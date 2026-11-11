# Customer UX changes (6 Nov 2026)

Nine changes to the customer portal: dashboard, Membership, bookings, Book a Workspace and passes.
`frontend/sw.js` revisions for the changed files are `customer-ux-1`.

## What changed

| # | Change | Where |
|---|---|---|
| 1 | Dashboard: the "Customisation available" banner is gone. **Custom** is now a quick tile right after Membership, the same tile as the others. It opens Book Workspace with the Customisation card already chosen (`/book?type=custom`). | `CustomerHomePage`, `BookWorkspacePage-MB2` |
| 2 | Membership: type one step larger. Card headings 22 px, plan titles 16 px, small details 14 px, plan rows 16 px, section headings `--font-size-xl`. Wrapped in `.mem-page`, so the rest of the portal is unchanged. | `MembershipPage`, `index-BgQ7mK4p.css` |
| 3 | My Bookings, Amount column: a **PAID** or **TO PAY** tag (14 px, bold, green or amber pill) under the amount. TO PAY covers an unpaid pay-later booking, with "₹X due at the front desk" under it, and a booking still waiting for online payment. Cancelled and expired bookings show the amount only. | `MyBookingsPage`, `.pay-tag` in CSS |
| 4 | Book a Workspace: the workspace cards come first ("1 · Choose your workspace"). The dates, duration, start times and the Find button only appear after one is picked ("2 · Choose your date and time"). The choice stays selected while the dates and time change. The server flow is unchanged. | `BookWorkspacePage-MB2` |
| 5 | Taken workspace cards say **Booked** once, as the badge. Two repeats are gone: the hover tooltip, and the price line that repeated "Booked" on a monthly desk or cabin with no price. | `BookWorkspacePage-MB2` |
| 6 | Desk cards no longer show "· seats 1". Cabins and meeting rooms still show their seat count. | `BookWorkspacePage-MB2` |
| 7 | My Passes: a pay-later pass never shows "Expiring soon". It shows Active, plus its own TO PAY / PAID tag. Paid passes keep the warning. | `PassesPage` |
| 8 | My Passes card: plan, pass number, status, then **Purchase date** and **Due date**, nothing else. Due date is the pass's last valid day (`validTo`). The credit circles and the "x of n used · y remaining" line are gone. | `PassesPage` |
| 9 | Several passes at once. A customer can buy another day pass for any dates while one is running. Each pass is its own sale at full price, with its own number, dates, invoice, QR access and payment state, and each shows as its own card. | `PassService` (API), migration below |

## 9: the API change

`PassService.LiveDayPassAsync` returns no pass unless `MST_AppSetting Passes.OnePassAtATime`
is `true`. That method was the only source of the old "one pass at a time, upgrade only"
rule. It fed purchase, payment order and `GET /passes/purchase-options`. So with the
setting `false`:

- every pack is allowed;
- nothing counts as an upgrade, so there is no credit for unused days;
- `purchase-options.current` is null, so Book Pass hides its "You're on …" banner and greys out nothing.

The upgrade code is still there. Set the value to `true` on the Settings screen to bring the
old rule back without a deploy.

Unchanged and still applied: the pay-later cap `PayLater.MaxOpenBookings` (2 on Dev). It counts
unpaid pay-later bookings and passes together, so a customer can hold at most 2 unpaid
pay-later passes. Paid passes have no limit.

Check-in with overlapping passes: each pass has its own QR token, and the door checks that
token's own payment state, so an unpaid pass never opens on a paid pass's behalf. App self
check-in takes any active pass valid today; it records attendance only.

## Deploy order

1. Backend (Regalis-live source): apply `backend/existing-files.diff` (`PassService.cs`, one
   method). It was made against the decompiled DLLs; match by the lines shown.
2. Run `db/migrations/20261106130000_CustomerUxPasses.sql`. It adds `Passes.OnePassAtATime = false`.
   It is idempotent.
   - Rollback: `..._Rollback.sql` sets it to `true`. It keeps the row, because a missing row
     means `false` in the new code.
3. Front end: the changed files are in `frontend/` (repo). Promote them as usual.

The front end works with the old API too. Changes 1–8 need no API change. Change 9 on the old
API only means Book Pass keeps its old banner and greyed-out packs.

## Tested (local Dev stack, 6 Nov 2026)

Install: `local-dev\api` Regalis.*.dll rebuilt from the decompiled tree with the diff applied.
`local-dev\frontend` got the 5 chunks, the CSS and the `sw.js` bumps. The SQL was run on
RegalisWorkSpace_Dev. Backup: `D:\Regalis\local-dev-backup-20261106-before-customer-ux`.

- `tests/e2e_multi_pass.py` (API): **13/13**.
  - Two pay-later day passes, same plan, 3 and 10 days ahead. The second was refused before this change.
  - Both are listed separately, each with its own dates, purchase time, amount, payment state and invoice. The first was not replaced.
  - `Passes.OnePassAtATime=true` brings back the current-pass banner and the blocked packs.
  - The test passes are cancelled at the end and the setting is restored. Customers on Dev lack the cancel permission, so set `REGALIS_ADMIN_EMAIL/PASSWORD` for the cleanup.
- `tests/ui_customer_ux.py` (headless Chrome, 1366 px and 390 px): **53/53**.
  - Bookings, live passes and a booked desk are served by intercepting the API call in Chrome, so nothing is written to the DB.
  - Checked: the Custom tile beside Membership, and it opening Customisation; no date inputs before a workspace is picked, and the pick kept after a date change; "Booked" once and no seat count on desks; Membership sizes; PAID / TO PAY; pass cards; no one-pass banner.
  - No sideways scroll on phone. No JS errors.
  - Screenshots are written to `tests/shots/`. Keep them out of git: they show the test account.

Not deployed to UAT or production.
