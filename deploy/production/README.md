# Production site (myregalis.com)

Production runs on the same server as UAT, fully separate from it.

| | UAT | Production |
|---|---|---|
| UI site | `regalis-ui`, http :8080 | `regalis-prod-ui`, http :80 `myregalis.com` + `www.` (443 after `06-https.ps1`) |
| API site | `regalis-api`, http :81 | `regalis-prod-api`, http `127.0.0.1:82` (local only) |
| Files | `C:\inetpub\wwwroot\Regalis_Src` | `C:\inetpub\wwwroot\Regalis_Prod` |
| Database | `RegalisWorkSpace_UAT` | `RegalisWorkSpace_PROD` |
| Uploads | `C:\ProgramData\Regalis\uploads` | `C:\ProgramData\Regalis\uploads_prod` |
| Pool recycle | every 29 h | daily 03:00 |
| DB backup | per deploy | nightly 02:00, 14 days (task "Regalis PROD nightly backup") |

Production has its own JWT signing key, so a UAT sign-in does not work on production.

**No online payments on production.** Customers pay at the front desk (cash,
UPI or card); staff credit their wallet with Admin > Wallet > "Add money";
customers then pay bookings, passes and memberships from the wallet. Staff can
also settle desk bookings and passes in cash. Production's `env.js` hides every
"Pay online" / "Pay now" / "Add funds" option (`deploy/offline-payments`), and
its web.config refuses the online-payment API calls, because the API's sandbox
gateway would otherwise accept a payment without any money. UAT keeps the
sandbox checkout for testing. Gap: there is no admin screen to record a desk
payment against an existing unpaid invoice (for example one raised from a
quotation); customers see "Pay at the front desk" on it.

## Setup (done 14 Oct 2026, kept for a rebuild)

1. `01-create-database.sql`: copy UAT to `RegalisWorkSpace_PROD` (refuses to overwrite).
2. `02-clean-data.sql`: keep staff + master data, remove customers and all transactions.
3. `04-create-sites.ps1`: folders, pools, sites, API web.config (from UAT's, new key).
4. `03-database-access.sql`: only `IIS APPPOOL\regalis-prod-api` can use the database (needs the pool from step 3).
5. `05-backup-task.ps1`: nightly backup.
6. `06-https.ps1 -Email <address>`: Let's Encrypt certificate once public DNS points here.
   The server sits behind Jio CGNAT, so a plain A record likely can't reach it;
   a Cloudflare Tunnel is the route instead: `09-go-public-cloudflare.ps1`
   (browser steps at the top of the script). `08-go-public-tailscale.ps1` is the
   fallback address on `*.ts.net`.
7. `07-move-to-sqlexpress.ps1`: moves the production database off the Developer
   edition (not licensed for production) onto SQL Server 2025 Express,
   `localhost\SQLEXPRESS`. Because this server is a domain controller, it first
   needs one AD group created by hand (the command is at the top of the script).
   `05-backup-task.ps1` and `promote-uat-to-prod.cmd` read the instance from the
   production API's connection string, so they follow the move.

## Shipping a change

Deploy and test on UAT as usual (`deploy-*.cmd`), then from an elevated prompt:

```
deploy\production\promote-uat-to-prod.cmd db\migrations\<the change's migration>.sql
```

It backs up production, applies the listed migrations, copies UAT's API and
frontend, checks `/health`, and rebuilds the frontend web.config
(`sync-frontend-config.ps1`). Never pass `_Rollback` or `_UAT_ONLY` scripts.

## 16 Nov 2026: all kits deployed straight to production (UAT skipped)

UAT could not be updated: `REGALIS\Administrator` has no login on the default SQL instance
that holds `RegalisWorkSpace_UAT`. Production was therefore released directly, after testing
on a restored copy of the production database.

- **API**: rebuilt on this server from `Downloads\Regalis-live\backend` plus the kits, in
  this order: profile-email, customer-portal, billing-finance, customer-ux, admin-customer,
  customer-10. The source with one git commit per kit is in `C:\RegalisBuild` (`git log`);
  the published build is `C:\RegalisBuild\publish`.
- **Database**: the 8 migrations from `20261101165945_BillingFinance` to
  `20261114150000_CustomerTenItems` (oldest first), plus
  `Notifications.CustomerEmailPortalUrl = https://myregalis.com`.
- **Frontend**: repo `frontend/` plus `deploy\billing-finance\frontend\apply.mjs`.
- **Tests** (on the copy): API smoke tests (Role on the profile, dashboard by location,
  payment history, delete-account check, waiting list, pay-later pass → Awaiting payment →
  desk payment → Active, a pay-later booking, double booking refused, billing endpoints), and
  a headless Chrome tour of 13 customer and 50 admin pages with no errors.
- **Backups**: `C:\inetpub\wwwroot\Regalis_Prod_backup_20261116_1308_all_kits` and
  `RegalisWorkSpace_PROD_20261116_1308_all_kits.bak` (SQLEXPRESS Backup folder). To roll back,
  run the eight `_Rollback.sql` scripts newest first, then restore both folders.

## 20 Nov 2026: CRM and Wi-Fi kits deployed straight to production (UAT skipped)

- **API**: `C:\RegalisBuild` commit `kit: crm + wifi` (the two kits' `backend/src` plus their
  DI lines), published to `C:\RegalisBuild\publish_crm_wifi` and copied to `Regalis_Prod\api`.
- **Database**: `20261119120000_Crm.sql`, then `20261119180000_Wifi.sql`.
- **Config**: production API web.config now has `Wifi__EncryptionKey` (new random key). Keep a
  copy of that value somewhere safe: without it the stored Wi-Fi passwords cannot be read.
- **Frontend**: repo `frontend/` (CRM + Wi-Fi pages, `crm-track.js`), then `sync-frontend-config.ps1`.
- **Wi-Fi runs on the simulated controller** (no hardware yet). Paying customers are issued
  access and emailed from now on (only payments after the migration, `issue.sinceUtc`).
- **Tests** (on `RegalisWorkSpace_PRODTEST`, a fresh copy of production): `testrun\smoke.mjs`
  36/36, `testrun\crm-wifi.mjs` 21/21; a desk-paid day pass was issued Wi-Fi within seconds.
- **Backups**: `C:\inetpub\wwwroot\Regalis_Prod_backup_20261120_1041_crm_wifi` and
  `RegalisWorkSpace_PROD_20261120_1041_crm_wifi.bak`. To roll back: run `20261119180000_Wifi_Rollback.sql`
  then `20261119120000_Crm_Rollback.sql`, and restore both folders.

**Until UAT is brought up to the same build, `promote-uat-to-prod.cmd` refuses to run**
(UAT's API is older). To bring UAT level: give an admin a login on the default instance,
apply the same 8 migrations to `RegalisWorkSpace_UAT`, then copy `C:\RegalisBuild\publish`
(without web.config) to `Regalis_Src\api` and the production frontend files to
`Regalis_Src\frontend`.

## 25 Nov 2026: Offers kit deployed straight to production (UAT skipped)

- **API**: `C:\RegalisBuild` commit `kit: offers` (the kit's `backend/src` plus the edits in
  `deploy\offers\backend\existing-files.diff`, applied by hand to the real source), published to
  `C:\RegalisBuild\publish_offers`; the `Regalis.*` files were copied to `Regalis_Prod\api`.
- **Database**: `20261123120000_Offers.sql`.
- **Frontend**: the two new chunks (`OffersKit-OF1.js`, `OffersAdminPage-OF1.js`), the four patched
  ones (`index`, `BookWorkspacePage-MB2`, `PassesPage`, `MembershipPage`) and `sw.js` (`offers-4`).
  `env.js` and `web.config` were not touched.
- **Nothing changes for customers until an admin creates and activates an offer** (Sales > Offers).
  Production takes no online payments, so offers apply to wallet and pay-later purchases.
- **Tests** (on `RegalisWorkSpace_PRODTEST` with the migration, new build on port 5099):
  `testrun\offers.mjs` 26/26 (admin CRUD, permissions, portal lists, preview arithmetic, a pay-later
  day pass with an offer, delete refused once used), `testrun\offers-booking.mjs` (a booking with a
  20% offer: 200.00 - 40.00, GST on 160.00, total 188.80, invoice names the offer; a plain booking
  still 236.00; the use is given back on cancel), `smoke.mjs` 35/36 (the one miss was "open cash
  register": a session was already open from an earlier run that day), `crm-wifi.mjs` 21/21, and
  the headless Chrome tour of 13 customer and 70 admin pages with no errors.
- **Backups**: `C:\inetpub\wwwroot\Regalis_Prod_backup_20261125_1204_offers` and
  `RegalisWorkSpace_PROD_20261125_1204_offers.bak`. To roll back: restore both folders, then run
  `20261123120000_Offers_Rollback.sql` (it drops the offer tables).

## 25 Nov 2026: past dates in the customer portal (frontend only)

`deploy/past-dates`: customers can pick past dates in Book a Workspace and Passes and see that day,
but cannot book or buy on it (view only; server rules unchanged). Files: `BookWorkspacePage-MB2.js`,
`PassesPage-CxAkHUg-.js`, `sw.js` (`past-dates-1`). Backup:
`C:\inetpub\wwwroot\Regalis_Prod_backup_20261125_2341_past_dates_frontend`.
