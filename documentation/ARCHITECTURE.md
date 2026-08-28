# ARCHITECTURE.md
**Regalis WorkSpace — Phase 1**

---

## 1. Stack

| Layer | Technology | Version |
|-------|-----------|---------|
| Frontend | React + TypeScript + Vite | React 19, TS 5.9, Vite 7 |
| Routing | React Router | 7.x |
| HTTP | Axios (single instance, interceptors) | 1.x |
| Forms | React Hook Form + Zod | 7.x / 4.x |
| State | React Context + `zustand` for auth/session | 5.x |
| Icons | lucide-react | 0.5x |
| PWA | `vite-plugin-pwa` (Workbox) | 1.x |
| Backend | ASP.NET Core Web API, C# 12 | `net8.0` (LTS) |
| ORM | Entity Framework Core (SqlServer) | 8.0.x |
| Auth | JWT access + rotating refresh tokens | — |
| Validation | FluentValidation | 11.x |
| Logging | Serilog (console + rolling file + correlation id) | 8.x |
| Docs | Swashbuckle / OpenAPI | 6.x |
| Excel/PDF export | ClosedXML / QuestPDF | — |
| DB | Microsoft SQL Server | 2019+ |
| Tests | xUnit + FluentAssertions + Moq + WebApplicationFactory; Vitest + Testing Library | — |

---

## 2. Solution layout

```
RegalisWorkSpace/
├── backend/
│   ├── Regalis.sln
│   ├── src/
│   │   ├── Regalis.Domain/          # entities, enums, domain rules. No dependencies.
│   │   ├── Regalis.Shared/          # ApiResponse, PagedResult, constants, permission codes
│   │   ├── Regalis.Application/     # DTOs, validators, service interfaces + implementations
│   │   ├── Regalis.Infrastructure/  # EF Core, repositories, adapters, JWT, hashing, PDF/Excel
│   │   └── Regalis.API/             # controllers, middleware, DI composition root
├── frontend/                        # React + TS + Vite PWA
├── database/
│   ├── scripts/                     # Database_Create.sql, Indexes.sql, Database_Migration.sql
│   ├── seed-data/                   # Database_Seed.sql
│   ├── views/                       # Views.sql
│   └── stored-procedures/           # StoredProcedures.sql
├── tests/
│   ├── backend/Regalis.UnitTests, Regalis.IntegrationTests
│   └── frontend/                    # Vitest suites live beside the frontend src
└── documentation/
```

### Dependency direction (strictly one-way)

```
   Regalis.API
        │  depends on
        ▼
 Regalis.Application ──────┐
        │                  │
        ▼                  ▼
  Regalis.Domain     Regalis.Shared
        ▲
        │  implements interfaces from
 Regalis.Infrastructure ───┘   (referenced by API only, for DI registration)
```

`Domain` references nothing. `Application` defines the interfaces (`IBookingService`,
`IPaymentGateway`, `IAccessControlAdapter`, `IUnitOfWork`…); `Infrastructure` implements
them; `API` wires them together. This keeps business logic out of controllers and out of
EF Core.

---

## 3. Request pipeline

```
HTTP request
  │
  ├─ CorrelationIdMiddleware      adds/propagates X-Correlation-Id, pushes to Serilog scope
  ├─ RequestLoggingMiddleware     method, path, status, elapsed ms, user id
  ├─ ExceptionHandlingMiddleware  maps exceptions → ApiResponse, never leaks stack traces
  ├─ Security headers             HSTS, X-Content-Type-Options, X-Frame-Options, CSP, Referrer-Policy
  ├─ CORS                         origins from configuration only
  ├─ Rate limiter                 fixed window; strict policy on /auth/*
  ├─ Authentication               JWT bearer
  ├─ Authorization                permission policies (see §5)
  └─ Controller → Service → Repository → EF Core → SQL Server
```

### Standard response envelope

Every endpoint returns `ApiResponse<T>`:

```json
{ "success": true,  "message": "Operation completed successfully.", "data": {}, "errors": [] }
{ "success": false, "message": "Unable to process the request.",   "data": null,
  "errors": [ { "code": "VALIDATION_ERROR", "message": "Required field is missing.", "field": "email" } ],
  "correlationId": "5f3c…" }
```

Implemented once in `Regalis.Shared/ApiResponse.cs` and produced by
`ApiControllerBase`, the validation filter and the exception middleware — never
hand-rolled inside a controller.

---

## 4. Authentication

- **Password hashing:** PBKDF2-HMAC-SHA256, 210 000 iterations, 128-bit salt, 256-bit
  subkey, stored as a single versioned string. Constant-time comparison.
- **Access token:** JWT, 15-minute lifetime. Claims: `sub`, `email`, `user_type`,
  `name`, `role` (multi), `perm` (multi, permission codes), `loc` (location id), `jti`.
- **Refresh token:** 7 days, 256-bit random, **stored hashed** (SHA-256), single-use,
  **rotated** on every refresh. Reuse of a consumed token revokes the whole family
  (token-theft detection).
- **Lockout:** 5 consecutive failures → 15-minute lock (`MST_User.LockoutEndUtc`).
  Counter resets on success. Configurable.
- **Password policy:** ≥ 8 chars, upper + lower + digit + symbol, not equal to the last
  3 hashes (`TRN_PasswordHistory` inside `MST_User` JSON column is *not* used — a
  dedicated table is used).
- **OTP:** 6 digits, hashed at rest, 10-minute TTL, max 5 verification attempts,
  60-second resend cooldown. Purposes: `Registration`, `ForgotPassword`.
- **Logout:** refresh token revoked server-side; the client clears memory state. A
  revoked token cannot be replayed.
- **Secrets:** JWT signing key, DB connection string, Razorpay keys and the seeded admin
  password all come from environment variables / user-secrets. `appsettings.json` ships
  with empty placeholders and is safe to commit.

---

## 5. Authorization — dynamic, database-driven

Nothing about roles, menus or permissions is hard-coded in either tier.

```
MST_User ──< MAP_UserRole >── MST_Role ──< MAP_RolePermission >── MST_Permission
                                  │
                                  └──< MAP_RoleMenu >── MST_Menu ──> MST_Permission
```

- A **permission** is `MODULE.ACTION`, e.g. `BOOKING.CREATE`, `INVENTORY.DELETE`,
  `REPORT.EXPORT`. Actions: `VIEW CREATE EDIT DELETE APPROVE EXPORT IMPORT PRINT`.
- At login the user's effective permission set is resolved from the database and stamped
  into the JWT as `perm` claims.
- `PermissionPolicyProvider` + `PermissionAuthorizationHandler` create an
  `AuthorizationPolicy` on demand for any `[HasPermission("BOOKING.CREATE")]` attribute.
  No policy is registered by hand.
- **The API is the enforcement point.** The frontend hiding a button is cosmetic; every
  controller action carries `[HasPermission(...)]`. A user calling a restricted endpoint
  directly with a valid token but without the permission gets `403` and an
  `AUTHORIZATION_FAILED` audit row.
- **Menus** are served by `GET /api/menus/my` and rendered by the sidebar. Adding a
  screen is: insert a menu row, map it to a role. No redeploy.
- **Row-level scoping:** customers see only their own bookings/invoices/wallet/tickets.
  Enforced in the service layer via `ICurrentUser`, not by trusting a client-supplied
  customer id.

---

## 6. The central booking & availability engine

This is the heart of the system and the one place where correctness matters most.

```
                 IAvailabilityService
                          │
        ┌─────────────────┼─────────────────┐
        ▼                 ▼                 ▼
  TRN_Booking      TRN_WorkspaceBlock   MST_Workspace
  (Confirmed,      (Maintenance,        (Status must be
   CheckedIn,       Housekeeping,        Available and
   Pending-held)    Internal, Repair)    IsActive)
```

**Overlap rule.** Two occupancy intervals `[s1,e1)` and `[s2,e2)` conflict when
`s1 < e2 AND s2 < e1`. Half-open intervals mean a booking ending at 12:00 and one
starting at 12:00 do **not** conflict.

**Concurrency.** Two customers can hit "confirm" on the same slot in the same
millisecond. Frontend checks are advisory only. The engine therefore:

1. Opens a transaction at `IsolationLevel.Serializable`.
2. Runs the conflict query with `WITH (UPDLOCK, HOLDLOCK)` on `TRN_Booking` and
   `TRN_WorkspaceBlock`, which takes a **key-range lock** — this blocks a second
   transaction from inserting a conflicting row rather than merely reading a stale one.
3. Inserts the booking and commits.
4. A supporting filtered unique index (`UX_Booking_Workspace_Start`) is a second line of
   defence against exact-duplicate submissions.
5. `SqlException` 1205 (deadlock victim) is retried up to 3 times with jittered backoff;
   `EnableRetryOnFailure` is configured on the EF provider.

The integration test `Booking_ConcurrentRequestsForSameSlot_OnlyOneSucceeds` fires 20
parallel confirmations at one slot and asserts exactly one `Confirmed` row.

**Bookings are never deleted** (Design Invariant #3). Cancellation is a status change
plus a `TRN_BookingStatusHistory` row.

**Housekeeping blocks the same calendar** (Design Invariant #12): creating a schedule for
a bookable workspace writes a `TRN_WorkspaceBlock`, so customer availability drops it
automatically. There is no second code path.

---

## 7. The confirmed-booking transaction

Design Invariant #4: every paid booking produces a booking id, an invoice, a QR/access
token, a notification and a calendar update — **atomically**.

```
BEGIN TRANSACTION (Serializable)
  1. re-verify availability under range lock
  2. INSERT TRN_Booking            → BookingNumber REG-000123
  3. debit wallet  OR  attach verified gateway payment  → TRN_Payment
  4. INSERT TRN_Invoice + lines    → REG/INV/2026-27/00042 (CGST/SGST split)
  5. INSERT TRN_AccessToken        → opaque token + QR payload
  6. INSERT TRN_Notification       → "Booking Confirmed"
  7. INSERT TRN_BookingStatusHistory
COMMIT
  8. (post-commit, best effort) email/PDF generation, ZOAPI push
```

Steps 1–7 succeed or fail together. Step 8 is outside the transaction and is retried
independently — a failed email must never roll back a paid booking.

---

## 8. Integration adapters

Every external system sits behind an interface in `Regalis.Application/Abstractions`.
The concrete implementation is chosen by configuration, so a missing vendor never blocks
the build or the MVP (Design Invariant #5).

| Interface | Phase-1 implementations | Config key |
|-----------|------------------------|-----------|
| `IPaymentGateway` | `RazorpayPaymentGateway`, `SandboxPaymentGateway` | `Payments:Provider` |
| `IAccessControlAdapter` | `MockAccessControlAdapter` (logs + always evaluates against our own rules) | `AccessControl:Provider` |
| `IZoapiAdapter` | `NullZoapiAdapter` | `Zoapi:Enabled` |
| `IPayrollExporter` | `CsvPayrollExporter`, `XlsxPayrollExporter` | `Payroll:Format` |
| `INotificationSender` | `InAppNotificationSender`, `SmtpEmailSender` | `Notifications:Channels` |
| `IFileStorage` | `LocalFileStorage` (`/wwwroot/uploads`) | `Storage:Provider` |
| `IOtpSender` | `ConsoleOtpSender` (dev), `SmtpOtpSender` | `Otp:Channel` |

Razorpay payments are **verified server-side** by recomputing the
`HMAC-SHA256(order_id|payment_id, key_secret)` signature. A client-reported "payment
succeeded" is never trusted.

---

## 9. Audit, logging and error handling

- **Audit trail** — an EF Core `SaveChangesInterceptor` writes `TRN_AuditLog` rows
  (user, action, module, entity, entity id, old JSON, new JSON, IP, correlation id,
  timestamp) for every insert/update/soft-delete on an auditable entity, plus explicit
  rows for `LOGIN`, `LOGOUT`, `PASSWORD_CHANGE`, `EXPORT`, `APPROVE`, `REJECT`,
  `ACCESS_OVERRIDE`. Sensitive fields (`PasswordHash`, `TokenHash`, `CodeHash`) are
  redacted before serialisation.
- **Audit fields** — `CreatedBy CreatedDate ModifiedBy ModifiedDate IsActive IsDeleted
  DeletedBy DeletedDate` on every business table, set by the same interceptor. Soft
  delete is applied through a global query filter.
- **Logging** — Serilog, structured, with levels `Trace…Critical` from configuration.
  Every log line carries the correlation id. Request/response status, auth events,
  authorization failures and key business operations are logged.
- **Errors** — `ExceptionHandlingMiddleware` maps
  `ValidationException → 400`, `UnauthorizedException → 401`, `ForbiddenException → 403`,
  `NotFoundException → 404`, `ConflictException → 409` (used by the booking engine),
  `BusinessRuleException → 422`, everything else → `500` with a generic message and the
  correlation id. Stack traces reach the log, never the client.

---

## 10. Frontend architecture

```
frontend/src/
├── api/            axios instance, interceptors, one module per API area
├── components/     the reusable library (DataTable, Modal, Toast, …) — §9 of the prompt
├── contexts/       AuthContext, ToastContext, ThemeContext
├── hooks/          usePermission, useDebounce, usePagination, useApi
├── layouts/        AuthLayout, CustomerLayout, AdminLayout
├── pages/          customer/*, admin/*
├── routes/         AppRoutes, ProtectedRoute, PermissionRoute
├── models/         TypeScript types mirroring the DTOs
├── services/       business-facing wrappers over api/
├── store/          zustand auth store (token, user, permissions, menus)
├── utils/          formatters, date helpers, currency, constants
├── validations/    zod schemas, shared with react-hook-form
└── styles/         design tokens (Black / Gold / White), globals
```

- **No business logic in components.** Components render; `services/` and `hooks/` hold
  the logic.
- **Axios interceptors** attach the bearer token, refresh once on `401` (single-flight,
  queued retries), redirect on repeated failure, and surface `ApiResponse.errors` to the
  toast/form layer.
- **Dynamic sidebar** — built from `GET /api/menus/my`, nested Module → Menu → Sub-menu →
  Screen, ordered by `DisplayOrder`, icons resolved by name from `lucide-react`.
- **`<PermissionRoute permission="BOOKING.VIEW">`** guards routes; `usePermission()`
  guards buttons. Both are convenience only — the API is the real gate.
- **Responsive** at 1920/1600/1366/1280/1024/768/390 px. Tables scroll inside their own
  `overflow-x:auto` container; the page body never scrolls horizontally.
- **Theme** — CSS custom properties: `--regalis-black #0B0B0D`, `--regalis-gold #C9A227`,
  `--regalis-gold-light #E3C767`, `--regalis-white #FFFFFF`, plus a full neutral ramp.
  Dark surface for chrome, light surface for content.
- **PWA** — manifest + Workbox service worker, precached shell, network-first for API,
  installable, offline fallback page.

---

## 11. Environments

| Env | Frontend | Backend | Database |
|-----|----------|---------|----------|
| Development | `.env.development` | `appsettings.Development.json` + user-secrets | `RegalisWorkSpace_Dev` |
| Test | `.env.test` | `appsettings.Test.json` | `RegalisWorkSpace_Test` |
| UAT | `.env.uat` | `appsettings.UAT.json` | `RegalisWorkSpace_UAT` |
| Production | `.env.production` | `appsettings.Production.json` + env vars | `RegalisWorkSpace` |

No secret is committed in any of them. See `Configuration.md`.

---

## 12. How the design invariants are enforced

| # | Invariant | Enforced by |
|---|-----------|-------------|
| 1 | One booking/calendar engine | `IAvailabilityService` is the only availability source; both portals call `/api/calendar` |
| 2 | Backend overlap prevention | Serializable + `UPDLOCK, HOLDLOCK` + filtered unique index + concurrency test |
| 3 | Bookings never deleted | No delete endpoint; `Cancel` writes status + history; global soft-delete filter |
| 4 | Paid booking → id + invoice + QR + notification + calendar | One transaction in `BookingService.ConfirmAsync` |
| 5 | Vendor logic behind adapters | `IAccessControlAdapter`, `IZoapiAdapter`, `IPaymentGateway` |
| 6 | Nothing business-related hard-coded | `MST_AppSetting`, masters, `MST_Menu`, `MST_Banner`, `MST_ServiceItem` |
| 7 | Multi-location-ready schema | `LocationId` FK on workspaces, bookings, passes, schedules, blocks |
| 8 | Responsive PWA, no native app | Vite PWA plugin, responsive breakpoints |
| 9 | Access failure never grants free access | `ManualOverride` requires `ACCESS.OVERRIDE` + writes an exception log row |
| 10 | Attendance visible only to authorised users | `ATTENDANCE.VIEW` permission + service-layer scoping |
| 11 | Inventory keeps full audit history | Append-only `TRN_InventoryTransaction`; stock is derived, never overwritten |
| 12 | Housekeeping blocks the customer calendar | Schedule creation writes `TRN_WorkspaceBlock` in the same transaction |
