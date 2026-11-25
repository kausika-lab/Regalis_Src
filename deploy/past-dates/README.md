# Past dates in the customer portal (25 Nov 2026)

Request: "In the live Customer Portal, enable the option to select previous dates across all screens
wherever date selection is available ... without affecting the existing date-selection functionality
or business logic."

Product-owner decision (25 Nov 2026): **view only**. Customers can pick a past date and look at
that day, but cannot book, buy or start anything on it. The server rules are unchanged.

Frontend only. Live on production (myregalis.com) on 25 Nov 2026.

## Every date field in the customer portal

| Screen | Field | Before | Now |
|---|---|---|---|
| Book a Workspace (Meeting Room, hourly) | From date, To date | Earliest date: the first open day. An earlier date was moved forward every minute. | No earliest date. A past date the customer picks stays. The day's slots are shown, all greyed out (past slots were already greyed out). |
| Book a Workspace (Desk / Cabin, monthly) | Starts on | Earliest date: the first open day | No earliest date. |
| Book a Workspace (Virtual Office) | Starts on | Earliest date: the first open day | No earliest date. |
| Book a Workspace (Customisation) | From date, To date | Earliest date: the first open day | No earliest date. A request can name past dates, since it is a message to the team. |
| Passes (Day pass / Monthly pass) | First visit date / Starts on | Earliest date: today | No earliest date. |
| My Bookings, Reschedule | New date | No restriction | Unchanged. |
| Profile | Date of Birth | No restriction | Unchanged. |
| Register | Date of Birth | No restriction | Unchanged. |

Membership, Wallet, Invoices, Payments, Support, Notifications, My QR, Home and Wi-Fi have no date
field for the customer. The Wi-Fi page's `min` / `max` values are on number fields.

## What a past date does

- **Book a Workspace:** a note under the dates says the date has passed and a booking can only
  start today or later. *Find available workspaces* and *Continue to payment* stay disabled while
  the start date is in the past. Customisation requests are not affected.
- **Passes:** the availability line says the date has passed and a pass can only start today or
  later. No availability lookup is made for a past date. *Wallet*, *Pay online* and
  *Pay later at the front desk* stay disabled, and paying is also refused in code.
- **Unchanged:** the default dates are still today, or the next open day once today's last slot has
  started (`deploy/next-open-day`). The server still refuses a booking or pass that starts in the
  past (`BookingService`, `MonthlyTerm`, `PassService`), so nothing can be bought for a past date
  even outside the portal.

## Files

`frontend/apply.mjs` (idempotent string edits; expects the offers kit's patches). It has been run
on the repo `frontend/`.

- `BookWorkspacePage-MB2.js`
- `PassesPage-CxAkHUg-.js`
- `sw.js`: revision `past-dates-1` for those two files and `index.html`

## Tested (25 Nov 2026)

On a local copy of the live site (production frontend config) against `RegalisWorkSpace_PRODTEST`:

- `C:\RegalisBuild\testrun\ui-past-dates.mjs`, headless Chrome: **19/19**. It covers:
  - Meeting Room: a past day is kept, its slots are shown and all greyed out, the note is shown, and
    *Find* stays disabled. The picked past date survives the once-a-minute next-open-day tick.
  - A future date still works: a slot can be picked and workspaces are listed.
  - Desk monthly: a past start shows the note with *Find* disabled; a future start works as before.
  - Passes: no earliest date; a past date shows the message and every pay button is disabled; a
    future date runs the desk check as before.
  - No JavaScript errors.
- Page tour of 13 customer and 70 admin pages: no problems.

## Deploy and rollback

The three files were copied to `C:\inetpub\wwwroot\Regalis_Prod\frontend`. Backup of the previous
versions: `C:\inetpub\wwwroot\Regalis_Prod_backup_20261125_2341_past_dates_frontend`. To roll
back, copy those three files back.
