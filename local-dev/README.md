# local-dev — running the stack on your own machine

`start-local.cmd` brings up the two halves of Regalis and leaves each in its own window:

| Part | URL | What runs |
|---|---|---|
| API | http://localhost:81 | `api\Regalis.API.exe`, the published build in this repo |
| Website | http://localhost:8080 | `web-server.mjs`, a dependency-free Node server over `frontend\` |

Open **http://localhost:8080**. The VS Code entries in `.vscode/launch.json` run the script
first, so F5 is enough.

It is safe to run twice: a part that is already listening is left alone. The script waits for
the API to open its port, then prints the result of `/health`, and exits non-zero if the API
never came up — which is what stops VS Code from opening a browser onto a dead page.

## Why the script is here and not on a D: drive

The task used to run `D:\Regalis\local-dev\start-local.cmd`, a path that existed on one
developer's machine. Anywhere else the task failed with "The system cannot find the path
specified". The script now lives in the repo and derives every path from its own location, so a
fresh clone can start the stack. Machine-specific values go in `local.env.cmd`, which git ignores.

## Settings

Copy `local.env.cmd.sample` to `local.env.cmd` and edit it. The script applies its own defaults
first and then calls that file, so anything you set there wins.

| Variable | Default | Notes |
|---|---|---|
| `REGALIS_SQL_INSTANCE` | `localhost\SQLEXPRESS` | The instance holding the databases on this machine |
| `REGALIS_DB` | `RegalisWorkSpace_Dev` | |
| `REGALIS_API_PORT` | `81` | Change it together with the URLs in `.vscode/launch.json` |
| `REGALIS_WEB_PORT` | `8080` | |
| `REGALIS_SEED` | `false` | Startup seeding, see below |
| `REGALIS_JWT_KEY` | a fixed local string | Local only; UAT and production take theirs from the host secret store |

The script passes these to the API as `ConnectionStrings__Default`, `Jwt__SigningKey` and
`Database__SeedOnStartup`. Environment beats the JSON files, so nothing in
`api\appsettings.Development.json` needs editing — which matters, because the connection string
committed there points at another developer's instance (`localhost\MSSQLSERVER02`).

Any other API setting can be overridden the same way: `Section__Key=value` in `local.env.cmd`.

## Two things that will bite you

**Startup seeding is off by default.** The build in `api\` is older than the Dev database
snapshots we restore. Its seeder lists the menus that already exist with `WHERE IsDeleted = 0`,
while `UX_Menu_Code` is a unique index over *every* row, soft-deleted ones included. So it
re-inserts a menu that a later migration soft-deleted — `ADM_FINANCE_DASHBOARD` and
`ADM_FINANCE_BANK` in the 14 Nov snapshot — and dies in `DatabaseSeeder.SeedMenusAsync` with SQL
error 2601 before Kestrel binds. A restored snapshot is already seeded, so nothing is lost. Set
`REGALIS_SEED=true` when you point the stack at an empty database.

**Both windows run their process through `powershell -NoExit`, not `cmd /k`.** With
`start ... cmd /k Regalis.API.exe` the window opens and the API never binds, every time; through
PowerShell it is listening in about 14 seconds. `-NoExit` also keeps a crash message on screen,
which is the only place to read a failure that happens before Serilog starts.

## The website server

`web-server.mjs` does what `frontend\web.config` does on IIS, because the SPA is built with
axios `baseURL: "/api"` and only works from an origin that proxies the API onto itself:

- `/api/...`, `/health...`, `/swagger...` are proxied to the API;
- anything else is served from `frontend\`, falling back to `index.html` for SPA routes.

Two deliberate differences from the IIS rules: nothing is cached (several bundles are patched in
place without a new file name, so a cached copy is always the wrong copy), and a missing file
*with* an extension returns 404 instead of `index.html`, so a wrong chunk name shows up in the
Network tab as a 404 rather than as HTML being parsed as JavaScript.

### The service worker is disabled locally

`frontend/sw.js` is a Workbox worker that precaches every bundle. On a real site that is the
point. On localhost it means the browser serves the copy it cached earlier and your edit never
appears, no matter how often you reload — bumping the `revision` in `sw.js` only helps once the
new worker installs *and* you reload again.

So the dev server answers `/sw.js` with a small stub that deletes every cache, unregisters itself
and reloads the open tabs once. After that first load nothing is cached at all, because every
response here carries `no-store`. The file in the repo is untouched; this only affects localhost.

Set `REGALIS_SERVE_SW=1` in `local.env.cmd` when you need the real worker, to test offline
behaviour or the install prompt. The website window says which mode it is in at startup.

## Stopping and troubleshooting

Close the two windows, or Ctrl+C in each.

- **"the API never opened port 81"** — read the API window, then
  `api\logs\regalis-api-<date>.log`. Usual causes: the wrong SQL instance, a database that is not
  restored, or `REGALIS_SEED=true` against a snapshot (see above).
- **The website answers but every call fails with a 502** — the API is down; its window says why.
- **A port is already taken by something else** — the script names the process and pid and leaves
  it alone, so change the port in `local.env.cmd` or stop that process.
- **Billing & Finance screens 404** — the build in `api\` predates those endpoints, even though
  the restored database has their tables. Nothing to do with the launcher.

## Verified on this machine, 15 Nov 2026

ASP.NET Core 8.0.31 runtime, Node 24, SQL Server 2025 Express (`localhost\SQLEXPRESS`),
`RegalisWorkSpace_Dev` restored from the 14 Nov snapshot.

- Cold start: API listening in ~14s, `/health` 200 Healthy; second run reports both ports already
  serving and exits 0.
- Through http://localhost:8080 — `/`, `/login`, `/admin/users` serve `index.html`; `/assets/...`,
  `/env.js`, `/sw.js`, `/manifest.webmanifest` and `/icons/...` serve with the right content type;
  `/health` 200, `/swagger/index.html` 200, `/api/locations` 401 (no token, as expected),
  `/assets/nope.js` 404.
- No migrations were applied: the database is ahead of the build in `api\`, so there is nothing
  pending for `MigrateOnStartup` to do.
