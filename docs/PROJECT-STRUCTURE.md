# Regalis WorkSpace — Project Code Structure

How the Regalis WorkSpace project is organised today. This describes the
existing structure only; nothing here is a proposed change.

| | |
|---|---|
| Backend | ASP.NET Core 8 Web API, Clean Architecture (5 projects) |
| Database | SQL Server (`RegalisWorkSpace_UAT`), Entity Framework Core |
| Frontend | React 19 + TypeScript 5.9, built with Vite 7 (PWA via Workbox) |
| Hosting | IIS on the UAT server — API on port **81**, SPA on port **8080** |

The project lives in **two places**:

1. **Source repo** — `C:\Users\Administrator\Downloads\Regalis`
   The C# and TypeScript source. The API is built and published from here.
2. **Deployment repo** — `C:\inetpub\wwwroot\Regalis_Src` (this repo)
   The published output that IIS actually serves, plus SQL migrations,
   deploy scripts and documentation.

> The frontend source in the source repo is older than what is deployed.
> Recent UI changes were made directly in the built bundles under
> `Regalis_Src\frontend\assets`, so that folder is the current UI.

---

## 1. Source repo — `Downloads\Regalis`

```
Regalis/
├── Regalis.sln                    Visual Studio solution (API + frontend)
├── Directory.Build.props          Shared .NET build settings (net8.0)
├── README.md, SECURITY.md, CLAUDE.md
├── backend/
│   └── src/
│       ├── Regalis.Domain/        Entities and enums — no dependencies
│       ├── Regalis.Shared/        Cross-cutting helpers (money, IST clock, API envelope)
│       ├── Regalis.Application/   DTOs, interfaces, exceptions (the contracts)
│       ├── Regalis.Infrastructure/ EF Core, services, adapters, background jobs
│       └── Regalis.API/           Controllers, middleware, auth, Program.cs
├── frontend/                      React + Vite SPA (Regalis.Frontend.esproj)
│   ├── src/
│   ├── e2e/                       Playwright end-to-end tests
│   ├── public/
│   ├── package.json, vite.config.ts, tsconfig*.json, playwright.config.ts
├── deploy/
│   ├── DEPLOYMENT.md
│   └── sql/                       release-N-idempotent.sql scripts
├── scripts/                       run-tests.ps1, run-e2e.ps1
├── tests/
└── documentation/
```

### 1.1 Backend layers

Dependencies point inward only:

```
Regalis.API ──► Regalis.Infrastructure ──► Regalis.Application ──► Regalis.Domain
      │                    │                       │
      └────────────────────┴───────────────────────┴──► Regalis.Shared
```

#### `Regalis.Domain` — business entities
```
Regalis.Domain/
├── Common/            Base entity types (Id, audit columns, soft delete)
├── Enums/             Byte-backed status / type enums
└── Entities/
    ├── Access/        QR passes, access logs, attendance
    ├── Booking/       Bookings, workspaces, availability
    ├── Finance/       Invoices, payments, wallet, expenses, purchase orders
    ├── Identity/      Users, roles, permissions, menus
    ├── Masters/       Locations, pass plans, services, suppliers
    └── Operations/    Tickets, maintenance, housekeeping, inventory
```
Tables follow the prefixes `MST_` (master data), `TRN_` (transactions) and
`MAP_` (link tables, e.g. `MAP_RoleMenu`).

#### `Regalis.Shared` — cross-cutting helpers
```
Regalis.Shared/
├── Api/               ApiResponse, PagedRequest, PagedResult (response envelope)
├── Constants/         Permission codes, setting keys, etc.
├── IndianMoney.cs     INR formatting and rounding
└── IstClock.cs        UTC ⇄ IST conversion
```

#### `Regalis.Application` — contracts
```
Regalis.Application/
├── Abstractions/      Interfaces implemented in Infrastructure
│                      (ICurrentUser, IPaymentGateway, IOtpSender,
│                       ISettingService, INumberSequenceService, ...)
├── Common/Exceptions/ Business exceptions mapped to HTTP status codes
├── Access/  Administration/  Auth/  Bookings/
├── Finance/ Masters/  Operations/  Reporting/
│                      Request/response DTOs and validators, one folder per area
└── DependencyInjection.cs
```

#### `Regalis.Infrastructure` — implementations
```
Regalis.Infrastructure/
├── Persistence/
│   ├── RegalisDbContext.cs
│   ├── Configurations/   EF Core entity configurations
│   ├── Interceptors/     Audit + admin-notification interceptors
│   ├── Migrations/       EF Core migrations
│   └── Seed/             Startup seed data
├── Services/          One service per feature: BookingService, WalletService,
│                      InvoiceService, PurchaseOrderService, MaintenanceService,
│                      QuotationService, AuthService, ... (~50 files)
├── Adapters/          External systems behind interfaces:
│                      Razorpay / Sandbox payment gateway, SMTP / Graph / Console
│                      OTP sender, CSV payroll exporter, mock access control,
│                      quotation dispatcher
├── BackgroundJobs/    BookingHoldSweeper, MaintenanceOverdueSweeper,
│                      AdminNotificationWorker
├── Excel/             ClosedXML workbook helper (import / export)
├── Reporting/         QuestPDF documents (invoices, quotations, POs, reports)
├── Security/          Password hashing, JWT / token handling
├── QueryableExtensions.cs
└── DependencyInjection.cs
```

#### `Regalis.API` — HTTP layer
```
Regalis.API/
├── Program.cs         Startup: DI, auth, Swagger, Serilog, middleware order
├── Controllers/       37 controllers, all deriving from ApiControllerBase
│                      (Auth, Bookings, Wallet, Invoices, PurchaseOrders,
│                       Maintenance, Quotations, Reports, Users, Roles, Menus, ...)
├── Middleware/        CorrelationId, RequestLogging, SecurityHeaders,
│                      ExceptionHandling
├── Authorization/     [HasPermission], [StaffOnly], [RequireCompleteProfile],
│                      permission policy provider + handler
├── Auditing/          Audit trail capture
├── Filters/           MVC filters
├── Services/          HTTP-specific services (current user, etc.)
├── UtcDateTimeConverter.cs
└── appsettings.json / appsettings.Development.json / appsettings.UAT.json
```

**Request flow:**
`Controller → [HasPermission]/[StaffOnly] → Service (Infrastructure) → RegalisDbContext → SQL Server`,
with every response wrapped in `ApiResponse<T>` and errors converted by
`ExceptionHandlingMiddleware`.

### 1.2 Frontend (`frontend/src`)

| Concern | Library |
|---|---|
| UI | React 19 |
| Routing | react-router-dom 7 |
| State | zustand 5 (with `persist`) |
| HTTP | axios |
| Validation | zod 4 |
| Icons | lucide-react |
| Styling | Hand-written CSS with custom properties (no Tailwind / CSS-in-JS) |
| Unit tests | Vitest |
| E2E tests | Playwright |

```
src/
├── main.tsx               Entry point — mounts <App/>, registers the service worker
├── App.tsx                Root component
├── routes/
│   ├── AppRoutes.tsx      All routes, lazy-loaded per page
│   ├── ProtectedRoute.tsx Requires sign-in
│   ├── PortalRoute.tsx    Customer vs admin portal
│   └── PermissionRoute.tsx Requires a permission code
├── layouts/
│   ├── AuthLayout.tsx     Sign-in / register screens
│   └── PortalLayout.tsx   Sidebar + header shell (menu comes from the API)
├── pages/
│   ├── auth/              Login, Register, VerifyOtp, ForgotPassword, ResetPassword
│   ├── customer/          Home, BookWorkspace, MyBookings, Passes, Wallet,
│   │                      Invoices, Quotations, Services, Support, MyQr, ...
│   ├── admin/             Dashboard, Bookings, Calendar, Customers, Finance,
│   │                      Expenses, PurchaseOrders, Inventory, Maintenance,
│   │                      Reports, Users, Roles, Menus, Workspaces, ...
│   ├── errors/            NotFound, Forbidden
│   └── ProfilePage.tsx
├── components/
│   ├── ui/                Shared primitives: Button, Card, Badge, Modal, DataTable,
│   │                      Input, Select, Checkbox, ConfirmDialog, EmptyState,
│   │                      PageHeader, Spinner, Toast (+ index.ts barrel)
│   ├── auth/              Auth form pieces
│   └── AccountMenu, LocationSwitcher, ThemeToggle
├── api/
│   ├── client.ts          axios instance, JWT header, 401 → refresh → retry
│   └── access, admin, auth, banking, bookings, finance, masters,
│       operations, procurement, quotations, reports, support .ts
├── models/                TypeScript types for each API module (same names as api/)
├── store/                 authStore, themeStore, uiStore (zustand)
├── hooks/                 useDebounce, useFieldErrors, usePermission
├── utils/                 formatters (INR, IST dates), greeting, menuIcons (+ tests)
├── styles/                tokens.css, globals.css, layouts.css, components.css, auth.css
└── test/setup.ts          Vitest setup
```

**Data flow:** `page → api/<module>.ts → api/client.ts (axios) → Regalis API`;
session and UI preferences live in zustand stores; permissions from the
signed-in user drive `PermissionRoute`, `usePermission` and the sidebar menu.

---

## 2. Deployment repo — `inetpub\wwwroot\Regalis_Src` (this repo)

```
Regalis_Src/
├── api/                   ► IIS site "regalis-api"  (http :81)
│   ├── Regalis.API.dll / .exe          Published API
│   ├── Regalis.Application/Domain/Infrastructure/Shared.dll
│   ├── *.dll                           NuGet dependencies (EF Core, Serilog,
│   │                                   QuestPDF, ClosedXML, MailKit, Graph, ...)
│   ├── runtimes/                       Native runtime files
│   ├── LatoFont/, brand/               Fonts and logos used by PDFs
│   ├── appsettings*.json               Config templates (Production is git-ignored)
│   ├── web.config                      Live config — git-ignored (secrets)
│   └── logs/                           Serilog output — git-ignored
│
├── frontend/              ► IIS site "regalis-ui"  (http :8080)
│   ├── index.html                      SPA entry
│   ├── assets/                         Built JS/CSS chunks (see 2.1)
│   ├── sw.js, workbox-*.js, registerSW.js   PWA service worker + precache list
│   ├── manifest.webmanifest, icons/, brand/, samples/
│   ├── tests/availability.test.mjs     Node test for the availability chunk
│   └── web.config                      SPA rewrite rules
│
├── db/
│   ├── migrations/                     Dated SQL migrations
│   │                                   YYYYMMDDhhmmss_Name.sql + _Rollback.sql
│   │                                   (idempotent; README.md explains them)
│   └── apply-*.cmd                     sqlcmd runners for one-off data scripts
│
├── deploy/<feature>/                   Payload for each release:
│   ├── deploy-steps.ps1                PowerShell steps (API publish / copy)
│   ├── apply.mjs                       Node script that patches built chunks + sw.js
│   ├── frontend/assets/                New/updated chunks to install
│   └── *.sql                           Feature data scripts
├── deploy-<feature>.cmd                Launcher for each release (run elevated);
│                                       backs up, applies SQL, deploys, verifies
│
├── docs/                               Feature notes (Google login, M365 OTP,
│                                       quotations, staff-screen spec) + this file
├── *.docx                              Feature specifications (Maintenance, PO)
├── architecture.md, domain-model.md,   Developer reference, reverse-engineered
│   endpoints.md, frontend.md,          from the build (also packaged in
│   catalogues.md, SKILL.md             regalis-dev.skill)
├── .vscode/launch.json                 Chrome debug against :8080
└── .gitignore
```

### 2.1 Built frontend chunks (`frontend/assets`)

- `index-<hash>.js` — vendor libraries, app shell, routes, stores, API client.
- `index-<hash>.css` — the whole stylesheet.
- `<Page>-<hash>.js` — one lazy chunk per page or page group
  (`AdminFinancePages`, `AdminInventoryPages`, `SupportPages`, ...).
- `<module>-<hash>.js` — API modules (`bookings`, `finance`, `procurement`, ...)
  and shared UI primitives (`Card`, `Modal`, `DataTable`, ...).

Chunks are loaded **by exact file name** from the index chunk (`import` +
`mapDeps`) and listed in `sw.js` with a revision. Patched chunks use short
suffixes such as `-BK2`, `-EX2`, `-IX1`, `-MT4`. When a chunk changes, its
`sw.js` revision must change too, or browsers keep the cached copy.

---

## 3. How a change reaches UAT

```
Backend change
  Downloads\Regalis\backend  ──dotnet publish──►  staging  ──copy (except web.config)──►  Regalis_Src\api
                                                                                       (app pool restart)
Database change
  Regalis_Src\db\migrations\<date>_<Name>.sql  ──sqlcmd -I──►  RegalisWorkSpace_UAT

Frontend change
  deploy\<feature>\apply.mjs / frontend\assets  ──►  Regalis_Src\frontend\assets + sw.js revision

All three are run together by  deploy-<feature>.cmd
```

## 4. Quick map: where to find things

| Looking for | Location |
|---|---|
| An API endpoint | `backend/src/Regalis.API/Controllers/<Area>Controller.cs` |
| Business logic | `backend/src/Regalis.Infrastructure/Services/<Area>Service.cs` |
| Request / response shapes | `backend/src/Regalis.Application/<Area>/` |
| Table definitions | `Regalis.Domain/Entities/` + `Infrastructure/Persistence/Configurations/` |
| Permissions / access rules | `Regalis.API/Authorization/` |
| Background jobs | `Regalis.Infrastructure/BackgroundJobs/` |
| PDF layouts | `Regalis.Infrastructure/Reporting/` |
| A screen (source) | `frontend/src/pages/{customer,admin,auth}/` |
| A screen (live) | `Regalis_Src/frontend/assets/<Page>-*.js` |
| Routes | `frontend/src/routes/AppRoutes.tsx` |
| Sidebar menu items | Database: `MST_Menu` + `MAP_RoleMenu` |
| Schema changes | `Regalis_Src/db/migrations/` |
| Live API contract | Swagger at `http://localhost:81/swagger` |
