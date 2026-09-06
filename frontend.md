# Regalis — frontend reference

Reconstructed from minified production bundles, so **names of internal
variables are lost** but structure, behaviour, routes, tokens and library
choices are exact.

Contents:
1. Stack
2. Build output and code splitting
3. API client and the refresh flow
4. State stores
5. Routes
6. API modules
7. Formatting (money, dates, the UTC gotcha)
8. CSS token system and theming
9. Component inventory
10. PWA

---

## 1. Stack

| Concern | Choice |
|---|---|
| Framework | React |
| Build | Vite (hashed ESM chunks, `crossorigin`) |
| Routing | react-router |
| State | zustand, with `persist` |
| HTTP | axios |
| Validation | zod |
| Icons | lucide-react |
| Styling | **hand-written CSS with custom properties — no Tailwind, no CSS-in-JS** |
| PWA | vite-plugin-pwa + Workbox |
| Fonts | Cormorant Garamond (display) + Lora (body), from Google Fonts |

There is no component library. Primitives (`Card`, `Badge`, `Modal`,
`Checkbox`, `DataTable`, `EmptyState`, `ConfirmDialog`, `PageHeader`) are
local components in their own chunks.

---

## 2. Build output and code splitting

Published `frontend/`:

```
index.html
assets/index-<hash>.js      ~434 KB — vendor + shell + stores + api client
assets/index-<hash>.css     ~31 KB  — the whole stylesheet, one file
assets/<Page>-<hash>.js      route-level lazy chunks
assets/<module>-<hash>.js    API modules and shared primitives
brand/regalis-crown.png      icons/ manifest.webmanifest registerSW.js
sw.js  workbox-<hash>.js  web.config
```

Pages are lazy-loaded per route and grouped where they share a domain —
`AdminCataloguePages`, `AdminFinancePages`, `AdminAccessPages`,
`AdminInventoryPages`, `AdminReportsPages`, `SupportPages` are multi-page
chunks. Keep that grouping when adding an admin screen: put it in the
existing domain chunk rather than creating a new one.

`assets/` is cached immutably for a year; `sw.js` is explicitly
cache-disabled. Never hand-edit files in `assets/` — they are build output.

---

## 3. API client and the refresh flow

One axios instance, `baseURL: "/api"`, with a timeout. In production IIS
rewrites `/api/*` to the API site, so the SPA never needs an absolute URL and
there is **no `VITE_API_URL` env var** — do not add one.

**Request interceptor** attaches `Authorization: Bearer <accessToken>` from
the auth store when a token is present.

**Response interceptor** on 401:

1. Skips refresh entirely when the failing request URL starts with `/auth/`
   — this prevents an infinite loop when login itself 401s.
2. Otherwise, if the request has not already been retried, marks it
   `_retried` and calls the refresh routine.
3. The refresh routine is **single-flight**: a module-level promise is
   memoised so ten concurrent 401s trigger exactly one
   `POST /api/auth/refresh`. It is cleared in a `finally`.
4. On success it stores the rotated token pair and replays the original
   request with the new bearer.
5. On failure it calls `clearSession()`.

This matters because refresh tokens are **single-use with family revocation**
(see architecture.md §3): two parallel refreshes would revoke the family and
log the user out. **Do not add a second refresh path or bypass the
single-flight guard.**

### Error type

```js
class ApiClientError extends Error {
  response  // the ApiResponse envelope, or null
  status    // HTTP status, or null
  get errors()        // response.errors ?? []
  hasCode(code)       // true if any error carries that code
}
```

Default message when the server is unreachable: *"Unable to reach the server.
Please check your connection."*

Branch on `err.hasCode(ErrorCodes.X)`, not on message text. The canonical case
is `SLOT_UNAVAILABLE` — re-render availability rather than surfacing an error.

### Envelope unwrapping

A helper wraps every call: it awaits the response, throws `ApiClientError`
when `success` is false or `data` is null, and otherwise returns `data`.
So API module functions resolve to the **payload**, not the envelope.

Blob endpoints bypass the helper — see the invoice PDF pattern in §6.

---

## 4. State stores

Two persisted zustand stores.

### `regalis-auth`
```
accessToken, refreshToken, user, roles[], permissions[], menus[]
isAuthenticated()      -> accessToken !== null
hasPermission(code)    -> permissions.includes(code)
setSession({...})      set after login
setTokens(a, r)        set after refresh
setMenus(m)            set from GET /menus/my
clearSession()         wipes everything
```

**`hasPermission` is the UI gate and mirrors the server's `perm` claim
exactly.** Gate a button or route with the same code the endpoint requires —
but remember the UI gate is cosmetic; the server enforces it. Never rely on
hiding a control as the security boundary.

The sidebar is **data-driven** from `menus`, populated by
`GET /api/menus/my`. Adding a screen means inserting `MST_Menu` +
`MAP_RoleMenu` rows and adding the route — not editing a nav array.

### `regalis-appearance`
```
mode: "light" | "dark" | "system"    accent: "gold" | ...
setMode(m)   setAccent(a)
```

Subscribes to itself to reapply on change, and listens to the
`prefers-color-scheme` media query while `mode === "system"`.

---

## 5. Routes

### Customer portal
```
/                    /login              /register
/verify-otp          /forgot-password    /reset-password
/book                /bookings           /work-status
/day-pass            /monthly-pass       /qr
/wallet              /invoices           /notifications
/services            /support            /support/raise
/support/tickets     /support/tickets/:id
/profile             /contact
/forbidden           (404 fallback)
```

### Admin portal (all under `/admin`)
```
/admin                        /admin/calendar        /admin/bookings
/admin/workspaces             /admin/workspaces/desks
/admin/workspaces/cabins      /admin/workspaces/meeting-rooms
/admin/locations              /admin/services        /admin/banners
/admin/passes/day             /admin/passes/monthly
/admin/invoices               /admin/payments        /admin/wallet
/admin/housekeeping           /admin/inventory/items
/admin/inventory/transactions
/admin/access                 /admin/access/logs
/admin/attendance             /admin/attendance/payroll
/admin/tickets                /admin/tickets/:id
/admin/users                  /admin/roles           /admin/menus
/admin/settings               /admin/audit
/admin/reports                /admin/reports/:key
/admin/profile
```

`/forbidden` is where a failed permission check lands. `/admin/reports/:key`
is the single generic report screen — one shell renders all 16.

---

## 6. API modules

Grouped by domain into chunks (`bookings`, `finance`, `masters`,
`operations`, `access`, `admin`, `reports`), each exporting an object of
functions. Actual shape from the bookings module:

```js
availabilityApi = {
  slots:      (locationId, workspaceTypeId, date) => …GET /availability/slots
  workspaces: (locationId, workspaceTypeId, startUtc, durationMinutes)
                                                  => …GET /availability/workspaces
}
bookingsApi = {
  quote:       (workspaceId, startUtc, durationMinutes) => POST /bookings/quote
  hold:        (workspaceId, startUtc, durationMinutes, notes) => POST /bookings
  confirm:     (id, body)      => POST /bookings/{id}/confirm
  get:         (id)            => GET  /bookings/{id}
  my:          (tab, params)   => GET  /bookings/my?tab=…
  list:        (params)        => GET  /bookings
  adminCreate: (body)          => POST /bookings/admin
}
calendarApi = { get(view, date, params), block(body), unblock(blockId) }
paymentsApi = { createOrder(bookingId), sandboxComplete(gatewayOrderId) }
```

Conventions to follow:

- **Query parameters go through axios `params`**, never string-concatenated.
- Path parameters use template literals.
- One function per endpoint, named after the action not the verb.
- Add a function to the existing domain module; do not create a per-page
  module.

### Blob downloads

The invoice PDF pattern — `responseType: "blob"`, object URL, synthetic
anchor click, then revoke:

```js
downloadPdf: async (id, invoiceNumber) => {
  const res = await api.get(`/invoices/${id}/pdf`, { responseType: "blob" });
  const url = URL.createObjectURL(res.data);
  const a = document.createElement("a");
  a.href = url;
  a.download = `${invoiceNumber.replaceAll("/", "-")}.pdf`;
  a.click();
  URL.revokeObjectURL(url);
}
```

Note the `replaceAll("/", "-")` — invoice numbers contain slashes
(`REG/INV/2026-27/00042`) which are illegal in filenames. Reuse this for
report and CSV exports.

---

## 7. Formatting

A single shared formatters module, all pinned to India:

```js
const TZ = "Asia/Kolkata";
currency  : Intl.NumberFormat("en-IN", { style:"currency", currency:"INR",
                                         maximumFractionDigits: 2 })
dateTime  : Intl.DateTimeFormat("en-IN", { timeZone: TZ, day:"2-digit",
              month:"short", year:"numeric", hour:"2-digit", minute:"2-digit",
              hour12: true })
date      : same, date parts only
time      : same, time parts only
```

### The UTC gotcha — important

The module contains a parse helper that **appends `Z` when a timestamp string
carries no timezone marker**:

```js
function parseUtc(v) {
  if (v instanceof Date) return v;
  const naive = !/[zZ]|[+-]\d{2}:?\d{2}$/.test(v);
  return new Date(naive ? `${v}Z` : v);
}
```

Every formatter runs input through this first. The reason: the API stores and
returns UTC, but a serialized `DateTime` without an offset would otherwise be
parsed as **local** time by `new Date()`, silently shifting every timestamp by
5½ hours. `UtcDateTimeConverter` on the server is the other half of this
contract.

**Never call `new Date(apiValue)` directly.** Always go through the
formatters, or through `parseUtc` if you need a `Date` object.

Conversely, when *sending* a time, send UTC. `IstClock` on the server
(`ToIst`, `ToUtc`, `IstDateOf`) handles the boundary server-side.

---

## 8. CSS token system and theming

One stylesheet, ~100 custom properties on `:root`. No utility framework —
**write semantic classes and consume tokens.**

### Token families

```
Brand      --regalis-black --regalis-white --regalis-gold
           --regalis-gold-light --regalis-gold-dark
Accent     --accent --accent-strong --accent-bright --accent-contrast
           --accent-rgb          (space-separated triple, for rgb(… / alpha))
Neutral    --neutral-50 … --neutral-900
Surface    --surface-page --surface-card --surface-inset --surface-hover
           --surface-chrome --surface-chrome-raised
Text       --text-primary --text-secondary --text-muted
           --text-on-chrome --text-on-chrome-muted --text-on-gold
Border     --border-subtle --border-strong --chrome-border
Status     --color-success --color-warning --color-danger --color-info
Shadow     --shadow-sm --shadow-md --shadow-lg --shadow-raised
           --shadow-float --shadow-color --card-sheen
Radius     --radius-sm --radius-md --radius-lg --radius-full
Space      --space-1 … --space-12
Type       --font-display --font-sans
           --font-size-xs --font-size-sm --font-size-md
           --font-size-lg --font-size-xl --font-size-2xl
Motion     --transition-fast --transition-base
Layout     --sidebar-width --topbar-height
Misc       --focus-ring --glass-bg --primary --secondary --outline --ghost --active
Auth       --auth-* (22 tokens — see below)
```

### Theming

Applied as attributes on the document root:

```
[data-theme=dark]
[data-accent=emerald]   [data-accent=ruby]
[data-accent=sapphire]  [data-accent=violet]
```

Gold is the default accent (no attribute). Each accent block redefines
`--accent`, `--accent-strong`, `--accent-bright`, `--accent-contrast` and
`--accent-rgb`:

| Accent | `--accent` |
|---|---|
| gold (default) | `#c9a227` |
| emerald | `#0e9f6e` |
| ruby | `#e11d48` |
| sapphire | `#2563eb` |
| violet | `#7c3aed` |

**A new component must use `--accent*`, never a literal brand colour**, or it
will ignore four of the five themes.

### The auth surface is a separate design system

The 22 `--auth-*` tokens define a warm cream-and-gold palette
(`--auth-bg: #faf6ec`, `--auth-gold: #e0ab3f`, serif display type) used only
by the login/register/OTP/reset screens. It is deliberately **not** derived
from the main tokens and does **not** respond to `data-theme` or
`data-accent`. Do not "fix" that; keep auth screens on `--auth-*` and app
screens on the main tokens.

### Class naming

BEM: `block__element--modifier`, e.g. `auth__divider`,
`auth__seg-opt--active`, `appearance__modes`.

Existing families, by size — reuse before inventing:
```
auth (57)   portal (27)   btn (15)   appearance (15)   cal (11)
field (9)   data-table (8)   choice-card (7)   badge (7)
auth-layout (7)   toast (6)   perm-matrix (5)   modal (5)
spinner (4)   slot-chip (4)   page-header (4)   dash-tile (4)   card (4)
wizard-step (3)   summary-rows (3)   empty-state (3)
toolbar (2)   error-page (2)   checkbox (2)
plus: slot-grid, choice-grid, dash-grid, form-grid, form-stack,
      pay-options, row-actions, table-scroll, summary-total,
      modal-overlay, toast-container, wizard-steps, spinner-center
```

`cal-*` is the shared calendar (Invariant 1 — one calendar, both portals).
`slot-chip` / `slot-grid` are the availability grid. `wizard-step*` is the
booking flow. `perm-matrix` is the role→permission editor.

---

## 9. Component inventory

Primitives: `Card`, `Badge`, `Modal`, `ConfirmDialog`, `Checkbox`,
`DataTable`, `EmptyState`, `PageHeader`, plus a `useDebounce` hook and the
`formatters` module.

Notable pages: `BookWorkspacePage` (the booking wizard),
`AdminCalendarPage`, `MyQrPage` (largest page chunk at ~27 KB — it carries QR
rendering), `WorkStatusPage`, `WalletPage`, `AdminReportsPages`,
`RolesPage` (permission matrix), `AdminAuditPage`.

`DataTable` + `PageHeader` + `EmptyState` + `useDebounce` is the standard
admin-grid composition; every admin list screen should use it rather than a
bespoke table.

---

## 10. PWA

`manifest.webmanifest`: name "Regalis WorkSpace", short name "Regalis",
`standalone`, `theme_color`/`background_color` `#0B0B0D`, icons at 192/512
plus a maskable 512. Service worker via Workbox, registered by
`registerSW.js`.

Because a stale service worker pins clients to an old build, `web.config`
disables caching for `sw.js` specifically. If you change caching behaviour,
preserve that.
