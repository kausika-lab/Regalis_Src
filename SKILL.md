---
name: regalis-dev
description: "Project conventions, architecture and design invariants for the Regalis WorkSpace coworking platform (ASP.NET Core 8 clean-architecture API + React/Vite SPA, SQL Server, INR/GST, IST). Use this skill for ANY work on the Regalis codebase — adding or changing an endpoint, entity, migration, service, permission, report, setting or SPA page; reviewing a Regalis pull request; debugging booking conflicts, wallet balances, access/QR denials, attendance, invoices or the 401-refresh loop; or answering 'how does Regalis do X'. Trigger this whenever Regalis, Regalis WorkSpace, RegalisDbContext, Regalis.API/Application/Domain/Infrastructure/Shared, MST_/TRN_/MAP_ tables, or files under api/ or frontend/ come up, even if the user does not name the architecture or ask for conventions explicitly."
---

# Regalis WorkSpace — developer guide

Regalis WorkSpace is a premium coworking management platform. Customers book
desks, cabins and meeting rooms, buy day/monthly passes, top up a wallet, order
services, raise tickets and enter the building with a QR pass. Admin and staff
manage the same data through a second portal over the same API.

Two portals, **one** API, **one** database. Money is INR with Indian GST;
timestamps are stored UTC and rendered IST.

## Reconstruction provenance — read this first

This skill was reverse-engineered from a **published deployment snapshot**
(compiled DLLs + XML doc files + PDBs + minified SPA bundles), not from source.
That has two consequences you must respect:

- **Everything here about intent, structure and rules is high-confidence** —
  it comes from the project's own XML doc comments, IL metadata and attribute
  blobs, which are authoritative.
- **Method bodies were never read.** Exact algorithms, edge-case branches and
  validation ordering are *not* in this skill. Where behaviour matters, read
  the real source.

The doc comments repeatedly cite design documents that are **not** in the
snapshot: `ARCHITECTURE.md`, `DATABASE_DESIGN.md`, `API_PLAN.md`,
`ROLE_PERMISSION_MATRIX.md`, `Doc-1`, and `deploy/DEPLOYMENT.md`. They are the
real source of truth. When a task turns on a rule this skill states only in
summary, ask for the relevant document rather than guessing.

If you have the actual repository, prefer it over this skill for any specific
line of code. Use this skill for orientation, conventions and invariants.

## The design invariants

The codebase is organised around a numbered invariant system, cited in doc
comments as `Invariant N` / `Design Invariant N`. **These are the rules of the
project.** Violating one is a design bug, not a style preference.

| # | Invariant |
|---|---|
| 1 | **One calendar.** Both portals read the same bookings + blocks through the same half-open overlap predicate `s1 < e2 AND s2 < e1`. Never add a second availability source. |
| 2 | **Holds take range locks.** A `Pending` hold is created in a serializable transaction whose conflict query holds `UPDLOCK/HOLDLOCK`. |
| 3 | **Bookings are never deleted.** Cancellation = status change + `TRN_BookingStatusHistory` row + policy refund to wallet. |
| 4 | **Atomic confirmation.** One serializable transaction produces confirmed booking + verified payment + GST invoice + QR access token + notification. All or nothing. |
| 5 | **Vendor seams are adapters.** Payments, OTP delivery, access hardware and payroll sit behind interfaces selected by configuration, so no vendor blocks the build. |
| 6 | **No hard-coding.** Operational values live in `MST_AppSetting`, read through `ISettingService`. |
| 7 | **Everything is location-scoped.** Every bookable/schedulable table carries `LocationId`. |
| 8 | *(not cited in any doc comment — recover from `ARCHITECTURE.md`)* |
| 9 | **A device failure never grants.** Hardware trouble is `DeviceError` + deny. The only path to entry while hardware is down is the separately-granted `ACCESS.OVERRIDE` manual fallback, which writes an exception log row and an audit entry. |
| 10 | *(not cited in any doc comment — recover from `ARCHITECTURE.md`)* |
| 11 | **Stock is derived, never stored.** `opening + in + adjustments − out − damage`, from the append-only ledger. No update/delete on a transaction; correct mistakes with a compensating `Adjustment` row. |
| 12 | **Housekeeping blocks the calendar.** A schedule with `BlocksWorkspace` writes its `TRN_WorkspaceBlock` in the same transaction, so customer availability drops the slot through the one existing calendar path. No second code path. |

Append-only tables — never write an update or delete path for these:
`TRN_AuditLog`, `TRN_AccessLog`, `TRN_LoginHistory`, `TRN_PasswordHistory`,
`TRN_BookingStatusHistory`, `TRN_WalletTransaction`, `TRN_InventoryTransaction`.

## Layers

```
Regalis.Domain          entities, enums, audit/soft-delete markers. No dependencies.
Regalis.Shared          ApiResponse envelope, PagedRequest/PagedResult, PermissionCodes,
                        AppSettingKeys, ErrorCodes, RoleCodes, AuditActions, IstClock.
Regalis.Application     service interfaces, DTOs, FluentValidation validators,
                        vendor-seam abstractions, AppException hierarchy.
Regalis.Infrastructure  EF Core, RegalisDbContext, configurations, migrations, seeding,
                        service implementations, adapters, security, reporting, jobs.
Regalis.API             controllers, permission authorization, middleware, Program.cs.
```

Dependencies point inward only. Concretely:

- **Interfaces and DTOs live in `Application`; implementations live in
  `Infrastructure/Services/`.** `IBookingService` is in
  `Application/Bookings/`; `BookingService` is in `Infrastructure/Services/`.
- DTOs are grouped per module in `<Module>Dtos.cs` files, as `record` types.
- **The Application layer never sees `IFormFile`.** Controllers wrap uploads in
  `AttachmentUpload(fileName, contentType, sizeBytes, stream)`.
- **Row scoping goes through `ICurrentUser`, never a client-supplied id.**
  Customers read their own rows whatever id they pass. This is load-bearing
  security, not a convenience.
- Clock access goes through `IDateTimeProvider` so services stay testable.

Full recovered folder layout: `references/architecture.md`.

## Conventions that will bite you

- **Enums are 1-based.** Every enum starts at 1, never 0. A `0` in an enum
  column means "nobody set it".
- **Tables are prefixed** `MST_` (master), `TRN_` (transaction), `MAP_`
  (mapping). Entity names carry no prefix; the prefix is in the doc comment
  and the EF configuration.
- **Intervals are half-open** `[StartUtc, EndUtc)`. A booking ending 12:00 and
  one starting 12:00 do **not** conflict.
- **Customer-visible numbers come from `INumberSequenceService`**, never
  IDENTITY — IDENTITY leaks volume and skips on rollback. Call it *inside* the
  business transaction so the number commits or rolls back with the record.
  Formats: `REG-000123`, `REG/INV/2026-27/00042`.
- **Invoices snapshot** customer and company details at issue time. A later
  profile or settings edit must never rewrite an issued invoice.
- **Payments are verified server-side** by recomputing
  `HMAC-SHA256(orderId|paymentId, secret)`. A client-reported "payment
  succeeded" is never trusted.
- **Audit columns are set by the save-changes interceptor**, never by hand.
  The same interceptor converts hard deletes of soft-deletable entities into
  soft deletes and writes the `TRN_AuditLog` row.
- **Uploads are validated three ways** — extension, size, *and* leading magic
  bytes (`FileTypeSniffer`) — and stored outside the web root, streamed back
  only through the authorized download endpoint.
- **UTC in, IST out.** Store UTC. The SPA renders `Asia/Kolkata`. Exports that
  humans read (payroll CSV, reports) convert to IST at the edge.

## Adding a feature — the path through the codebase

Work outward from the domain. A typical vertical slice:

1. **Domain** — entity in `Entities/<Area>/`, inherit `AuditableEntity` if it
   needs audit columns; enums in `Enums/`, starting at 1.
2. **Infrastructure/Persistence/Configurations** — add to the existing
   `<Area>Configurations.cs`; set the `MST_`/`TRN_`/`MAP_` table name,
   precision on money, filtered/unique indexes, `RowVersion` where concurrent
   movement matters.
3. **Migration** — `dotnet ef migrations add <Name>`. Note the two existing
   named migrations follow `AddPasswordHistory` / `SignedAdjustmentQuantity`
   style: descriptive PascalCase, no ticket numbers.
4. **Application** — `I<Thing>Service` + DTO records in
   `<Module>Dtos.cs` + a FluentValidation validator. Validators are
   auto-registered by assembly scan in `AddApplication()`.
5. **Infrastructure/Services** — the implementation. Register in
   `Infrastructure/DependencyInjection.cs`.
6. **API** — controller inheriting `ApiControllerBase` (it produces the
   `ApiResponse` envelope; never hand-roll one). Route
   `[Route("api/<kebab-plural>")]`. Guard every action with
   `[HasPermission(PermissionCodes.<Module>.<Action>)]`.
7. **Permission + menu** — if the action needs a new permission code, add it to
   `PermissionCodes` **and** seed the `MST_Permission` row and the
   `MAP_RolePermission` grants in `SeedCatalogue`. Screens are `MST_Menu` rows:
   **adding a screen is a data insert, not a redeploy.**
8. **Settings** — anything tunable goes in `MST_AppSetting` via
   `AppSettingKeys`, read through `ISettingService`. Do not add a constant.
9. **Frontend** — API module function, page component, route. See
   `references/frontend.md`.

Seeding is **idempotent**: existing rows are never overwritten, only missing
rows inserted. Safe on every startup.

## Error handling

Throw the typed exception; middleware maps it. Never return a bare status.

| Exception | Status |
|---|---|
| `AppValidationException` | 400 + field-level errors |
| `UnauthorizedException` | 401 |
| `ForbiddenException` | 403 |
| `NotFoundException` | 404 |
| `ConflictException` | 409 |
| `BusinessRuleException` | 422 |

Every response is the `ApiResponse` / `ApiResponse<T>` envelope carrying
`Success`, `Message`, `Errors[]` and `CorrelationId`. Stack traces reach the
log, never the client.

`409 SLOT_UNAVAILABLE` is a **documented, expected** outcome of losing a
booking race — the SPA re-renders availability rather than showing an error.
Do not treat it as a failure to be logged loudly.

Error codes, deny reasons, roles and audit actions: `references/catalogues.md`.

## Known live issues

- **MARS disables EF savepoints.** The connection string sets
  `MultipleActiveResultSets=True`, which makes EF Core log:
  *"Savepoints are disabled because Multiple Active Result Sets (MARS) is
  enabled."* This directly undercuts the retrying execution strategy the
  booking engine relies on to replay deadlock victims (error 1205) — a failed
  `SaveChanges` inside a serializable transaction cannot roll back to a clean
  state automatically. Anyone touching the booking or wallet transaction paths
  should treat this as a real defect, not log noise. Fix is to drop MARS from
  the connection string, or roll back explicitly before retrying.
- `Failed to determine the https port for redirect` on startup — HTTPS
  redirection middleware is active without a configured HTTPS port behind the
  IIS reverse proxy.

## Reference files

Read the one you need; they are independent.

- **`references/architecture.md`** — recovered folder layout of all five
  projects, request pipeline, middleware order, auth/token design, rate-limit
  policies, background jobs, vendor adapters, deployment topology.
- **`references/endpoints.md`** — every controller, route, HTTP verb and its
  required permission code, plus anonymous and rate-limited endpoints.
  Machine-extracted from the attribute blobs; treat as authoritative.
- **`references/domain-model.md`** — all 48 entities with their properties, all
  35 enums with their integer values.
- **`references/catalogues.md`** — permission codes, app-setting keys, error
  codes, role codes, audit actions, sequence keys, access deny reasons, the
  16-report catalogue.
- **`references/frontend.md`** — SPA stack, API client and refresh flow, state
  stores, routing, CSS token system and theming, formatter conventions.

## Re-extracting from a build

`scripts/` holds the tools used to build this skill. They need only Python and
`pip install dnfile` — no .NET SDK, no decompiler. Run them against a published
`api/` folder to refresh any reference file after a release:

```bash
python3 scripts/routes.py  api/Regalis.API.dll                  # endpoint → permission map
python3 scripts/dump2.py   api/Regalis.Domain.dll Enums          # enums with values
python3 scripts/dump2.py   api/Regalis.Domain.dll Entities props # entity properties
python3 scripts/xdoc.py    api/Regalis.Application.xml           # doc comments per layer
```
