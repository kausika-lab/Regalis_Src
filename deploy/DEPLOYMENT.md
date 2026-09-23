# Regalis WorkSpace — UAT deployment runbook (Release 3)

Release 3 is the feature-complete build (gate M7). This runbook takes a clean
Windows or Linux host from nothing to a running UAT instance. Production follows
the same steps with production values and `ASPNETCORE_ENVIRONMENT=Production`
(Swagger off, HSTS on).

## 1. Prerequisites

| Item | Requirement |
|------|-------------|
| .NET (Linux) | ASP.NET Core Runtime 8.0.x, or a self-contained publish |
| .NET (Windows + IIS) | **ASP.NET Core 8.0.x Hosting Bundle** — the downloaded file must be named `dotnet-hosting-8.0.xx-win.exe`. The plain runtime and the SDK do *not* work; see §5.1 |
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

### IIS (same origin)

Same shape as the Nginx sketch — a static site for `dist/`, a loopback site for
the API, and a rewrite rule joining them.

#### 5.1 The Hosting Bundle — the one download that matters

On <https://dotnet.microsoft.com/download/dotnet/8.0>, in the **Windows**
column, take **Hosting Bundle**. The file that lands must be named
`dotnet-hosting-8.0.xx-win.exe` — the word *hosting* in the filename is the
check.

`aspnetcore-runtime-…`, `dotnet-runtime-…` and `dotnet-sdk-…` are the wrong
file. They install the runtime but not the ASP.NET Core Module, and the module
is what the published `web.config` binds to
(`modules="AspNetCoreModuleV2"`, `hostingModel="inprocess"`), so IIS answers
every request with **HTTP 500.19 — module not recognised**.

Order matters: enable the IIS role **first**, then run the bundle. If IIS is
added afterwards the module is never registered — re-run the installer and
choose *Repair*. Finish with `iisreset`.

Verify all three before going further:

```powershell
Test-Path "$env:ProgramFiles\IIS\Asp.Net Core Module\V2\aspnetcorev2.dll"   # True
& "$env:SystemRoot\System32\inetsrv\appcmd.exe" list module /name:AspNetCoreModuleV2
dotnet --list-runtimes | Select-String 'Microsoft.AspNetCore.App 8\.'
```

Two further downloads are needed for the rules in §5.3 and are **not** in the
bundle: **URL Rewrite 2.1** and **Application Request Routing 3.0**. After
installing ARR, enable the proxy once at server level: IIS Manager → server
node → *Application Request Routing Cache* → *Server Proxy Settings* → tick
**Enable proxy**.

#### 5.2 Sites

| Site | Binding | Physical path | App pool |
|------|---------|---------------|----------|
| `regalis-api` | `http://127.0.0.1:5000` | published API (`artifacts/api`) | `regalis-api` — **No Managed Code** |
| `regalis` | `https://uat.regalis.example:443` | `frontend/dist` | `regalis-static` — **No Managed Code** |

Both pools are *No Managed Code*: ANCM hosts the app, the .NET Framework
pipeline is not involved.

Do **not** shortcut this by hosting the API as an IIS application at the `/api`
virtual path. ANCM makes that path the app's `PathBase` and strips it, so
`/api/auth/login` arrives as `/auth/login` and matches no controller — the
controllers already carry the `api/` prefix themselves.

`dotnet publish` regenerates the API's `web.config` on every deploy, so keep
the §4 environment variables out of it — set them on the app pool or as
machine-level variables, or re-apply them as a deploy step.

The app-pool identity needs read on both folders, and write on
`Storage__UploadRoot` and on the ANCM `logs\` folder if `stdoutLogEnabled` is
ever turned on.

#### 5.3 `web.config` for the static site

The rules live in **`frontend/public/web.config`**, not in `dist/` — `vite build`
copies `public/` into `dist/`, so every rebuild carries them and a redeploy of
`dist/` cannot silently lose them. They do three things:

- rewrite `^(api/.*|health.*|swagger.*)$` to the API site (ARR proxy). The port
  in that rule must match the API site's binding;
- SPA fallback: anything that is not an existing file or directory → `/index.html`;
- serve `.webmanifest` as `application/manifest+json` (IIS has no such mapping
  out of the box, so the PWA manifest would otherwise 404), cache `assets/`
  for a year (hashed filenames) and never cache `sw.js`.

`swagger` is UAT only — drop it from the alternation in Production.

#### 5.4 As deployed on the UAT box (2026-09-01)

| | |
|---|---|
| Hosting Bundle | 8.0.30 (`dotnet-hosting-8.0.30-win.exe`), ANCM V2 registered |
| IIS modules | URL Rewrite 2.1, ARR 3.0, server proxy `enabled="true"` |
| API site | `regalis-api`, `http://*:81`, `C:\inetpub\wwwroot\Regalis_Src\api` |
| SPA site | `regalis-ui`, `http://*:8080`, `C:\inetpub\wwwroot\Regalis_Src\frontend` |
| Database | `RegalisWorkSpace_UAT` on `localhost` (SQL Server 2025), 46 tables, 3 migrations |
| DB access | `IIS APPPOOL\regalis-api` — `db_datareader`, `db_datawriter`, `EXECUTE` on `dbo` |
| Uploads | `C:\ProgramData\Regalis\uploads` (Modify for the app-pool identity) |
| Node | 24.20.0 LTS / npm 11.19.0 (frontend build toolchain) |

Environment variables are set in the deployed API's `web.config`
`<environmentVariables>` — remember §5.2: `dotnet publish` overwrites that file,
so re-apply them after each deploy.

**Still open:** TLS. The SPA site is HTTP on `:8080` — bind 443 with a real
certificate for the UAT hostname before this is exposed beyond the host.

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
