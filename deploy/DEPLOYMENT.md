# Regalis WorkSpace — UAT deployment runbook (Release 3)

Release 3 is the feature-complete build (gate M7). This runbook takes a clean
Windows or Linux host from nothing to a running UAT instance. Production follows
the same steps with production values and `ASPNETCORE_ENVIRONMENT=Production`
(Swagger off, HSTS on).

## 1. Prerequisites

| Item | Requirement |
|------|-------------|
| .NET | ASP.NET Core Runtime 8.0.x (or self-contained publish) |
| Database | SQL Server 2019+ (any edition), a `RegalisWorkSpace_UAT` database and a login with `db_owner` on it during deployment, `db_datareader`/`db_datawriter` + `EXECUTE` at runtime |
| Web server | Anything that can reverse-proxy: IIS (ANCM), Nginx or YARP. The SPA and the API **should share one origin** — the frontend calls the relative path `/api` and the PWA service worker caches only same-origin API calls |
| TLS | A certificate for the UAT hostname; HTTPS end-to-end |

## 2. Build the artifacts (from tag `release-3`)

```bash
git checkout release-3

# API — framework-dependent publish
dotnet publish backend/src/Regalis.API -c Release -o artifacts/api

# Frontend — production bundle (dist/)
cd frontend && npm ci && npm run build && cd ..
# artifacts: frontend/dist  (~430 KB main JS + lazy chunks, PWA precache)
```

## 3. Database

Hand `deploy/sql/release-3-idempotent.sql` to the DBA. It is **idempotent** —
it creates `__EFMigrationsHistory` if missing and applies only migrations not
yet recorded, so it is safe on an empty database *and* on one carrying an
earlier release.

Alternative (operator-run):

```bash
ASPNETCORE_ENVIRONMENT=UAT ConnectionStrings__Default="<uat connection string>" \
  dotnet ef database update --project backend/src/Regalis.Infrastructure --startup-project backend/src/Regalis.API
```

Seeding (permissions, roles, menus, settings, ticket categories, number
sequences) runs automatically at first startup and is idempotent — keyed by
code, it inserts only what is missing. The admin account is created **only**
when `Seed__AdminPassword` is set.

## 4. Configure the API

`appsettings.UAT.json` ships with placeholders. Set the real values as
environment variables (or the host's secret store) — never in git:

| Variable | Value |
|----------|-------|
| `ASPNETCORE_ENVIRONMENT` | `UAT` |
| `ASPNETCORE_URLS` | e.g. `http://127.0.0.1:5000` (behind the proxy) |
| `ConnectionStrings__Default` | UAT SQL Server connection string |
| `Jwt__SigningKey` | ≥ 32 random bytes, e.g. `openssl rand -base64 64`. **Outside Development the app refuses to start without it** |
| `Seed__AdminPassword` | First-boot admin password for `enquiry@amstag.in` (remove after first boot; the admin should change it) |
| `Cors__AllowedOrigins__0` | Only needed for a split-host UAT, e.g. `https://uat.regalis.example` |
| `Payments__Provider` | `Sandbox` for UAT (`Razorpay` + `Payments__Razorpay__KeyId/KeySecret` when gateway testing) |
| `Storage__UploadRoot` | A persistent path outside the web root for ticket attachments and profile photos |

## 5. Configure the frontend

Same-origin (recommended): no configuration — `dist/` is static; the proxy
routes `/api/*` to the API process and everything else to `index.html`
(SPA fallback).

Split-host UAT only: build with `VITE_API_BASE_URL=https://api-uat.regalis.example/api npm run build`
and set `Cors__AllowedOrigins__0` on the API.

### Nginx sketch (same origin)

```nginx
server {
    listen 443 ssl;
    server_name uat.regalis.example;

    root /srv/regalis/frontend;           # frontend/dist
    location /api/    { proxy_pass http://127.0.0.1:5000; proxy_set_header X-Forwarded-For $remote_addr; }
    location /health  { proxy_pass http://127.0.0.1:5000; }
    location /swagger { proxy_pass http://127.0.0.1:5000; }   # UAT only
    location /        { try_files $uri /index.html; }
}
```

IIS equivalent: site → `dist/`, ARR/ANCM reverse-proxy rule for `/api`,
URL-rewrite SPA fallback to `/index.html`.

## 6. Smoke checklist (10 minutes, in order)

1. `GET /health` → 200; `GET /health/ready` → 200 (DB reachable).
2. `GET /swagger` renders (UAT only).
3. Admin login (`enquiry@amstag.in`) → dashboard shows the aggregate cards.
4. Register a customer → OTP appears in the **API console log** (console OTP
   sender — no SMTP in UAT, execution-plan risk #3) → verify → login.
5. Customer: book a desk for tomorrow → pay via **sandbox** → invoice PDF
   downloads → QR appears under My QR.
6. Admin: Access → paste the QR payload → deny (outside window) proves rules
   run; Housekeeping → create a blocking schedule → the slot disappears from
   the customer's calendar → cancel → it returns.
7. Customer: raise a ticket with a photo → admin triage sees it; reply →
   customer notification bell.
8. Reports → Booking Report → rows appear → export Excel and PDF.
9. Responsive pass: 320 / 375 / 414 / 768 / 1024 / 1366 / 1920 px, and the
   Chrome / Edge / Firefox matrix (carried over from Day 11 — record results
   in the tracker).

## 7. Rollback

Redeploy the previous artifacts (tag `release-2`). Database: migrations up to
Release 3 are additive except `SignedAdjustmentQuantity` (a check-constraint
swap, backward-compatible) — no destructive change; a schema rollback is not
required for an application rollback.

## 8. Code freeze

After release approval, `release-3` is the frozen line: **only defect fixes**
merge until go-live, each cherry-picked to `develop` and re-tagged
(`release-3.1`, …).
