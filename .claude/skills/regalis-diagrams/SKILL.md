---
name: regalis-diagrams
description: Generate and maintain the Regalis WorkSpace Mermaid (.mmd) diagrams — layered architecture / dependency direction, database ERD, auth-JWT sequence, and booking lifecycle state machine. Use when asked to create, update, add or refresh a diagram, .mmd file, ERD, sequence diagram, state machine, architecture picture or flow chart for this project; or after a change that alters layer references, the EF Core model, the auth flow, or BookingStatus, since the diagrams then need regenerating.
---

# Regalis WorkSpace diagrams

Diagrams are **generated from code and schema, never hand-invented**. Every diagram
below names its source of truth — read that first, and let it override anything
stale in `documentation/*.md` (those files carry older ASCII-art versions of the
same pictures).

## Where they live

```
documentation/diagrams/
├── architecture-layers.mmd
├── database-erd-<cluster>.mmd     # one per cluster, see ERD section
├── auth-sequence.mmd
└── booking-lifecycle.mmd
```

One diagram per file, `.mmd` extension, no Markdown fence inside the file — the
file *is* the diagram body, starting with the type keyword (`flowchart`,
`erDiagram`, `sequenceDiagram`, `stateDiagram-v2`).

To embed one in a `.md` doc, copy it into a mermaid-tagged fence; keep the `.mmd`
file as the authoritative copy and note in the doc where it came from.

## Conventions

- **No hardcoded colors.** These render on light and dark backgrounds. Rely on
  Mermaid's default theme; if emphasis is genuinely needed use `classDef` with a
  stroke, not a fill.
- **Direction:** `TB` for layering and lifecycles, `LR` for pipelines and ERD
  clusters. Never leave it implicit.
- Node ids are terse and stable (`api`, `app`, `dom`); labels carry the real
  project names (`Regalis.API`).
- Label every edge that expresses a rule ("depends on", "implements"). An
  unlabeled arrow in an architecture diagram is a bug.
- Keep any single diagram under ~25 nodes. Past that, split it — a diagram nobody
  can read is worse than no diagram.
- Comment the provenance at the top of each file, e.g.
  `%% generated from backend/src/*/Regalis.*.csproj`.

## 1. architecture-layers.mmd

**Source of truth:** the `<ProjectReference>` elements in
`backend/src/*/Regalis.*.csproj`. Do not copy the ASCII art in
`documentation/ARCHITECTURE.md` §2 — verify against the csproj files, then fix the
doc if it disagrees.

The dependency rule is **strictly one-way**: `Domain` references nothing;
`Application` defines the interfaces (`IBookingService`, `IPaymentGateway`,
`IAccessControlAdapter`, `IUnitOfWork`); `Infrastructure` implements them; `API`
references Infrastructure *only* to register DI.

    flowchart TB
      api[Regalis.API] -->|depends on| app[Regalis.Application]
      app -->|depends on| dom[Regalis.Domain]
      app -->|depends on| shared[Regalis.Shared]
      infra[Regalis.Infrastructure] -->|implements interfaces from| app
      api -.->|DI registration only| infra

Use a dotted edge for the DI-only reference — it is a composition-root wiring
concern, not a logical dependency, and drawing it solid implies API may call
Infrastructure directly.

## 2. database-erd-<cluster>.mmd

**Source of truth:** the `DbSet<>` properties and `IEntityTypeConfiguration<>`
classes under `backend/src/Regalis.Infrastructure`. Cross-check physical table
names against `database/scripts/Database_Create.sql`.

There are **45 entities** — far too many for one diagram. Emit one file per
cluster (these clusters partition all 45; every entity appears in exactly one):

| Cluster | Entities |
|---|---|
| `identity` | User, Role, Permission, UserRole, RolePermission, Menu, RoleMenu, RefreshToken, Otp, PasswordHistory, LoginHistory |
| `workspace` | Location, Workspace, WorkspaceType, Amenity, WorkspaceAmenity, WorkspaceBlock, Booking, BookingStatusHistory, Attendance |
| `money` | Wallet, WalletTransaction, Payment, Invoice, InvoiceLine, PassPlan, CustomerPass |
| `services` | ServiceItem, ServiceOrder, ServiceOrderLine, InventoryItem, InventoryTransaction, HousekeepingSchedule |
| `support` | Ticket, TicketMessage, TicketAttachment, TicketCategory, TicketIssueType |
| `access` | AccessToken, AccessLog |
| `platform` | AuditLog, Notification, Banner, AppSetting, NumberSequence |

Table naming follows `MST_` (master), `TRN_` (transaction), `MAP_`
(mapping/junction). Use the **physical** table name as the entity label so the
diagram matches what a DBA sees, and keep the C# class name in a comment.

Include only keys and discriminating columns — not every column. Mark the
`ROWVERSION` optimistic-concurrency columns present on `TRN_Booking`,
`MST_Wallet` and `MST_Workspace`.

    erDiagram
      MST_User ||--o{ MAP_UserRole : has
      MST_Role ||--o{ MAP_UserRole : grants
      MST_User {
        bigint UserId PK
        nvarchar Email UK
        tinyint UserType
        bit IsActive
      }

Cross-cluster foreign keys: draw the foreign entity as a stub (key columns only)
and comment `%% detailed in database-erd-<other>.mmd`. Never duplicate a full
entity definition across files.

## 3. auth-sequence.mmd

**Source of truth:** `backend/src/Regalis.API/Controllers/AuthController.cs` and
the token services in `Regalis.Infrastructure`.

Endpoints: `login`, `register`, `verify-otp`, `resend-otp`, `forgot-password`,
`reset-password`, `change-password`, `refresh`, `logout`, `me`.

Draw the **happy path plus the refresh rotation**, with participants Browser,
API, AuthService, DB. The details that must be visible:

- Registration issues an **OTP** (channel configurable via `Otp:Channel`) before
  the account is usable.
- `login` returns a short-lived **JWT access token** plus a **rotating refresh
  token** — on `refresh` the old refresh token is invalidated and a new one
  issued. Show the invalidation; it is the part people get wrong.
- `MustChangePassword` forces a redirect to `change-password`.
- Reset signs the user out of all other devices.

Use `alt`/`else` for the lockout branch (`FailedLoginCount`, `LockoutEndUtc` on
`MST_User`), and `Note over` for token lifetimes rather than inventing numbers —
read them from `Jwt:AccessTokenMinutes` / `Jwt:RefreshTokenDays` in
`api/appsettings.json`.

## 4. booking-lifecycle.mmd

**Source of truth:** `enum BookingStatus` in
`backend/src/Regalis.Domain/Enums`, plus the transition guards in the booking
service.

All **seven** states must appear — a diagram showing only the three the customer
dashboard renders is incomplete:

`Pending(1) → Confirmed(2) → CheckedIn(3) → Completed(4)`, plus `Cancelled(5)`,
`Expired(6)`, `NoShow(7)`.

    stateDiagram-v2
      [*] --> Pending : booking created
      Pending --> Confirmed : payment captured
      Pending --> Expired : PendingHoldMinutes elapsed
      Confirmed --> CheckedIn : QR scanned
      CheckedIn --> Completed : slot ended
      Confirmed --> NoShow : slot ended, never checked in
      Pending --> Cancelled : cancelled
      Confirmed --> Cancelled : cancelled
      Completed --> [*]

`Pending` holds the slot for `Booking.PendingHoldMinutes` — label that edge with
the setting name, not a literal. Treat `BookingPaymentStatus`
(Pending/Paid/Failed/Refunded/PartiallyRefunded) as a **separate** concern:
either a second small state diagram or notes, never conflated into the booking
states. Every transition should correspond to a row in
`TRN_BookingStatusHistory`.

## Verifying

Mermaid syntax fails silently in some renderers, so check before declaring done:

1. **By inspection, always:** type keyword on line 1, direction declared, every
   node id referenced by an edge is defined, no unbalanced `[]` / `{}` / `()`,
   no stray Markdown fence.
2. **Re-read the source** you generated from and confirm every member appears. A
   missing enum member is the most common defect in these diagrams.
3. **Optional CLI render.** No Mermaid tooling is part of this repo's toolchain.
   If you want a real parse, render to a throwaway file — not a null device, the
   CLI infers format from the output extension and this is Windows:

       npx -y @mermaid-js/mermaid-cli -i documentation/diagrams/<file>.mmd -o %TEMP%\mmd-check.svg

   It pulls Puppeteer/Chromium on first run, so confirm that is acceptable before
   relying on it, and do not add it to `package.json` without being asked.

## Regenerating

When code changes, regenerate rather than patch: re-read the source of truth,
rewrite the file, then diff. If a diagram and the `documentation/*.md` prose
disagree, the code wins — update the prose and say so.
