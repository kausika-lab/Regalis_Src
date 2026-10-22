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
   a Cloudflare Tunnel is the planned route instead.
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
