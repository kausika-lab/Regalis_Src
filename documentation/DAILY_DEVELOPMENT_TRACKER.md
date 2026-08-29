# DAILY_DEVELOPMENT_TRACKER.md
**Regalis WorkSpace — Phase 1**

Maintained by the Senior Developer. Updated at the end of every working day, immediately
after stand-up notes are collected. A machine-readable copy for import into Jira or Azure
DevOps is at `documentation/Development_Daily_Tracker.csv`.

## Columns

`Date · Developer · Module · Screen · Task · Task Type · Status · Priority ·
Estimated Hours · Actual Hours · Completion % · Blocker · Dependency · QA Status ·
Code Review Status · Remarks`

## Allowed values

**Task Type** — `Analysis` `Design` `Database` `Backend API` `Frontend` `Integration`
`Unit Test` `Integration Test` `Bug Fix` `Code Review` `Documentation` `Deployment` `QA`

**Status** — `Not Started` `In Progress` `Code Review` `Ready for QA` `QA Testing`
`QA Failed` `Bug Fixing` `Completed` `Blocked`

**Priority** — `Critical` `High` `Medium` `Low`

**QA Status** — `Not Started` `In Progress` `Passed` `Failed` `Blocked` `N/A`

**Code Review Status** — `Not Required` `Pending` `In Review` `Changes Requested` `Approved`

---

## Day 1 — Monday 31 August 2026

| Date | Dev | Module | Screen | Task | Type | Status | Pri | Est | Act | % | Blocker | Dependency | QA | Review | Remarks |
|------|-----|--------|--------|------|------|--------|-----|-----|-----|---|---------|-----------|----|--------|---------|
| 31-Aug | Senior | M01 | — | Requirement analysis across all 9 source documents | Analysis | Completed | Critical | 3 | 3 | 100 | — | — | N/A | Not Required | Doc-2 confirmed as governing baseline |
| 31-Aug | Senior | M01 | — | Architecture, layering, adapter seams | Design | Completed | Critical | 2 | 2 | 100 | — | — | N/A | Not Required | `ARCHITECTURE.md` |
| 31-Aug | Senior | M01 | — | ERD and full table design (45 tables) | Database | Completed | Critical | 3 | 3 | 100 | — | — | N/A | Not Required | `DATABASE_DESIGN.md` |
| 31-Aug | Senior | M04 | — | Permission catalogue and role matrix | Design | Completed | High | 1 | 1 | 100 | — | — | N/A | Not Required | 118 permissions, 6 roles |
| 31-Aug | Junior 1 | M06–M07 | — | Group A screen list and UI flows | Analysis | Completed | High | 4 | 4 | 100 | — | — | N/A | Not Required | 44 screens |
| 31-Aug | Junior 2 | M12–M24 | — | Group B screen list and UI flows | Analysis | Completed | High | 4 | 4 | 100 | — | — | N/A | Not Required | 47 screens |
| 31-Aug | QA | — | — | Test strategy, case template, acceptance criteria | QA | Completed | High | 6 | 6 | 100 | — | — | N/A | Not Required | `Testing.md` |
| 31-Aug | All | — | — | **Design freeze — API contract v1 agreed** | Design | Completed | Critical | 1 | 1 | 100 | — | — | N/A | N/A | Gate M1 passed |

## Day 2 — Tuesday 1 September 2026

| Date | Dev | Module | Screen | Task | Type | Status | Pri | Est | Act | % | Blocker | Dependency | QA | Review | Remarks |
|------|-----|--------|--------|------|------|--------|-----|-----|-----|---|---------|-----------|----|--------|---------|
| 01-Sep | Senior | M01 | — | Repo + branch strategy (main/develop/feature), .gitignore, solution scaffold: Domain/Shared/Application/Infrastructure/API | Backend API | Completed | Critical | 2 | 2 | 100 | — | — | N/A | Approved | Clean architecture, one-way references, net8.0 LTS |
| 01-Sep | Senior | M01 | — | EF Core DbContext, 44 entity configurations, InitialCreate migration, dev database created | Database | Completed | Critical | 3 | 3 | 100 | — | Solution scaffold | N/A | Approved | 44 tables + 8 check constraints verified in RegalisWorkSpace_Dev |
| 01-Sep | Senior | M01 | — | `ApiResponse`, exception middleware, correlation id, Serilog, audit interceptor | Backend API | Completed | Critical | 2 | 2 | 100 | — | — | Not Started | Approved | Audit rows with sensitive-field redaction |
| 01-Sep | Senior | M01 | — | Swagger, CORS, rate limiting, security headers, health checks, JWT + dynamic permission policy provider | Backend API | Completed | High | 2 | 2 | 100 | — | — | Not Started | Approved | /health 200 verified; `[HasPermission]` writes AUTHORIZATION_FAILED audit rows |
| 01-Sep | Junior 1 | M01 | Shell | Vite + React + TS PWA project, layouts, routing, theme tokens | Frontend | Completed | Critical | 4 | 4 | 100 | — | — | Not Started | Approved | Black/Gold/White tokens; PWA manifest + service worker |
| 01-Sep | Junior 1 | M01 | Components | Button, Input, Card, Modal, Toast, Spinner, Badge, EmptyState | Frontend | Completed | Critical | 4 | 4 | 100 | — | Shell | Not Started | Approved | DataTable, PageHeader, Pagination follow with the first list screens (Day 3–4) |
| 01-Sep | Junior 2 | M01 | — | Axios instance, interceptors, refresh queue, auth store, route guards | Frontend | Completed | Critical | 3 | 3 | 100 | — | Shell | Not Started | Approved | Single-flight refresh; zustand persisted session |
| 01-Sep | Junior 2 | M01 | Components | Form inputs + zod/react-hook-form validation, login form | Frontend | Completed | High | 3 | 3 | 100 | — | Shell | Not Started | Approved | DatePicker, Dropdown, FileUpload follow with the screens that need them |
| 01-Sep | QA | M01 | — | Foundation test cases; Postman collection; env files | QA | In Progress | High | 8 | 2 | 25 | — | — | N/A | Not Required | Smoke verified: build clean, API starts, /health 200, swagger.json serves, tsc + vite build pass |

## Day 3 — Wednesday 2 September 2026 · RELEASE 1

| Date | Dev | Module | Screen | Task | Type | Status | Pri | Est | Act | % | Blocker | Dependency | QA | Review | Remarks |
|------|-----|--------|--------|------|------|--------|-----|-----|-----|---|---------|-----------|----|--------|---------|
| 02-Sep | Senior | M02 | — | Login (dual portal), register + OTP (hashed, cooldown, attempts), forgot/reset (stateless replay-blocked token), refresh rotation + family revocation, lockout, password policy + TRN_PasswordHistory | Backend API | Completed | Critical | 4 | 4 | 100 | — | Foundation | Passed | Approved | Console OTP sender until SMTP creds arrive (exec-plan risk #3) |
| 02-Sep | Senior | M04/M05 | — | Idempotent seeder: 6 roles, 103 permissions, matrix, 36 menus + role maps, settings, masters, sequences, admin user | Database | Completed | Critical | 3 | 3 | 100 | — | — | Passed | Approved | Admin password via user-secrets only |
| 02-Sep | Senior | M03–M05 | — | Users/Roles/Menus/Permissions APIs with [HasPermission] on every action; GET /menus/my | Backend API | Completed | Critical | 3 | 3 | 100 | — | Seeder | Passed | Approved | /customers read views land with M03 remainder (Day 4) |
| 02-Sep | Junior 1 | M02 | /register /verify-otp /forgot-password /reset-password | Auth screens with mirrored zod password policy | Frontend | Completed | Critical | 4 | 4 | 100 | — | Auth API | Passed | Approved | |
| 02-Sep | Junior 1 | M03 | /admin/users | User management: search/filter/paging, create, status actions, admin reset, soft delete | Frontend | Completed | High | 4 | 4 | 100 | — | Users API | Passed | Approved | MustChangePassword forced-change screen lands with Profile (M03 remainder) |
| 02-Sep | Junior 2 | M04 | /admin/roles | Role list, create, permission-matrix editor, role-menu editor | Frontend | Completed | High | 4 | 4 | 100 | — | Roles API | Passed | Approved | |
| 02-Sep | Junior 2 | M05 | /admin/menus | Menu management: tree list, create/edit, delete with child guard | Frontend | Completed | High | 3 | 3 | 100 | — | Menus API | Passed | Approved | Dynamic sidebar shipped Day 2 |
| 02-Sep | QA | M02–M05 | — | 14 unit + 15 integration tests: lockout, refresh reuse kills family, OTP limits, reset revokes sessions, 403 + audit row, direct-API role tests, menu trees | Integration Test | Completed | Critical | 6 | 6 | 100 | — | — | Passed | Approved | Runs against RegalisWorkSpace_Test through the real pipeline |

**RELEASE 1 tagged — Security + Administration (gate M2 passed).**

## Day 4 — Thursday 3 September 2026

| Date | Dev | Module | Screen | Task | Type | Status | Pri | Est | Act | % | Blocker | Dependency | QA | Review | Remarks |
|------|-----|--------|--------|------|------|--------|-----|-----|-----|---|---------|-----------|----|--------|---------|
| 03-Sep | Senior | M08 | — | Central availability & booking engine: IAvailabilityService (slots/free-workspaces/check), quote with GST, CreateHold under Serializable + UPDLOCK/HOLDLOCK range locks, deadlock retry with tracker reset, gap-free REG- numbers in-transaction, configurable rules, hold sweeper | Backend API | Completed | Critical | 6 | 6 | 100 | — | Masters | Passed | Approved | Confirm transaction (invoice+QR+notification) is Day 5 |
| 03-Sep | Senior | M08 | — | Concurrency proof: 20 parallel holds on one slot → exactly 1 occupancy row | Integration Test | Completed | Critical | 1 | 1 | 100 | — | Engine | Passed | Approved | Gate M3 evidence, a day early |
| 03-Sep | Junior 1 | M06 | /admin/locations | Locations CRUD API + UI (working hours/days, default flag, delete guards) | Backend API | Completed | High | 3 | 3 | 100 | — | — | Passed | Approved | |
| 03-Sep | Junior 1 | M07 | /admin/workspaces (+desks/cabins/meeting-rooms) | Workspaces CRUD API + UI: filters, amenity mapping, status patch, future-occupancy delete guard; workspace types + amenities endpoints | Backend API | Completed | High | 5 | 5 | 100 | — | M06 | Passed | Approved | Type sub-menus pre-filter the same screen |
| 03-Sep | Junior 2 | M12 | — | IPaymentGateway seam: Razorpay adapter (REST orders + server-side HMAC verify, keys from config) + Sandbox adapter (same HMAC scheme), provider selected by configuration | Backend API | Completed | High | 3 | 3 | 100 | — | — | Passed | Approved | Payments controller lands with confirm flow (Day 5) |
| 03-Sep | Junior 2 | M13 | — | Wallet balance + self-checking ledger (BalanceAfter, WTX numbers), credit/debit primitives, GET /wallet/me (+transactions) | Backend API | Completed | High | 3 | 3 | 100 | — | — | Passed | Approved | Recharge UI Day 6 |
| 03-Sep | QA | M06–M08 | — | 17 new integration tests: quote maths, 409 overlap, half-open adjacency, rule violations (5 variants), block conflict, availability reflects holds, row scoping, masters CRUD/RBAC/duplicates, wallet zero-state, concurrency test | Integration Test | Completed | Critical | 6 | 6 | 100 | — | — | Passed | Approved | Suite total: 45 tests green |

## Day 5 — Friday 4 September 2026 · Gate M3

| Date | Dev | Module | Screen | Task | Type | Status | Pri | Est | Act | % | Blocker | Dependency | QA | Review | Remarks |
|------|-----|--------|--------|------|------|--------|-----|-----|-----|---|---------|-----------|----|--------|---------|
| 04-Sep | Senior | M08 | — | Atomic confirm transaction: range-locked re-verify (excl. own hold), wallet/gateway/cash settlement, GST invoice with snapshots, signed QR token, notification, history — all or nothing (Invariant 4) | Backend API | Completed | Critical | 5 | 5 | 100 | — | Engine | Passed | Approved | Tampered signature leaves zero side effects (tested) |
| 04-Sep | Senior | M09 | — | Calendar API day/week/month for both portals (admin detail vs anonymized customer view); block/unblock with confirmed-booking guard | Backend API | Completed | Critical | 3 | 3 | 100 | — | Engine | Passed | Approved | Calendar UI grids land Day 6 |
| 04-Sep | Senior | M11 | — | Admin booking (hold + immediate cash/wallet settlement); booking lists (my-tabs + admin filters) | Backend API | Completed | High | 2 | 2 | 100 | — | Confirm | Passed | Approved | |
| 04-Sep | Junior 1 | M10 | /book | Customer booking wizard: type → date → slots (IST) → duration → workspace cards → GST summary → wallet/online pay → confirmation with invoice download; 409 race handling refreshes availability | Frontend | Completed | Critical | 6 | 6 | 100 | — | APIs | Passed | Approved | |
| 04-Sep | Junior 1 | M11 | /admin/bookings | Admin booking list (filters/search), detail modal, front-desk booking modal (customer search, live slots, cash/wallet) | Frontend | Completed | High | 4 | 4 | 100 | — | APIs | Passed | Approved | |
| 04-Sep | Junior 1 | M02 | /login /register /verify-otp /forgot-password /reset-password | Auth screens rebuilt to the client's "Regalis Login" design canvas: parchment ground, Cormorant Garamond/Lora, crown + wordmark, iconed cream inputs, gold-gradient actions, medallions, ornaments | Frontend | Completed | High | 4 | 4 | 100 | — | Design zip | Passed | Approved | Portal reskin to the same design language (WorkSpace Admin UI artboard) is queued |
| 04-Sep | Junior 2 | M12/M14 | — | Payment orders + sandbox checkout simulation + signature-verified idempotent webhook; invoice reads + QuestPDF GST invoice on demand | Backend API | Completed | Critical | 4 | 4 | 100 | — | Gateway seam | Passed | Approved | |
| 04-Sep | QA | M08–M14 | — | 12 confirm-path + 4 calendar integration tests; manual end-to-end on dev: register → OTP → book → sandbox pay → confirm → PDF → admin calendar | Integration Test | Completed | Critical | 6 | 6 | 100 | — | — | Passed | Approved | Suite: 57 automated tests green |

**Gate M3 passed:** booking engine demo end-to-end, one calendar serving both portals, overlap prevention proven (Day 4 concurrency test).

## Day 3 onward — template

Copy the block below for each day. **Every row must be filled — an empty Actual Hours or
QA Status column at the end of a day is itself a tracking defect.**

```
| DD-MMM | <Senior|Junior 1|Junior 2|QA> | <Mxx> | <screen or —> | <task> | <type> | <status> | <priority> | <est> | <act> | <%> | <blocker or —> | <dependency or —> | <qa status> | <review status> | <remarks> |
```

---

## Daily stand-up record

Held every working day at 09:30 for 15 minutes. Any blocker older than one day is
escalated to the Project Lead the same morning.

```
DATE:
────────────────────────────────────────────
SENIOR
  Yesterday:
  Today:
  Blockers:
  Risk / dependency:

JUNIOR 1
  Yesterday:
  Today:
  Blockers:
  Risk / dependency:

JUNIOR 2
  Yesterday:
  Today:
  Blockers:
  Risk / dependency:

QA
  Tested:
  Passed:
  Failed:
  Blocked:
  Critical bugs:
  Regression status:
────────────────────────────────────────────
TRACKER UPDATED BY:            AT:
ESCALATIONS RAISED:
```

## End-of-session report

Produced at the end of every development session, per master prompt §42.

```
DATE:
DEVELOPER:
MODULE:
TASK:
COMPLETED:
IN PROGRESS:
BLOCKERS:
BUGS:
TEST STATUS:
CODE REVIEW:
NEXT ACTION:
ESTIMATED COMPLETION:
```

## Burndown

| Day | Planned cumulative % | Actual cumulative % | Variance | Note |
|-----|---------------------|--------------------|---------|------|
| 1 | 5 | 5 | 0 | Design freeze on time |
| 2 | 12 | 12 | 0 | Foundation merged to develop; dev DB created from migration |
| 3 | 22 | 22 | 0 | Release 1 tagged; auth + RBAC live with automated test proof |
| 4 | 30 | 30 | 0 | Booking engine live with concurrency proof (M3 evidence early) |
| 5 | 40 | 40 | 0 | Booking engine gate M3 passed; auth screens on the client design |
| 6 | 50 | | | |
| 7 | 60 | | | Release 2 |
| 8 | 70 | | | |
| 9 | 78 | | | |
| 10 | 86 | | | Feature complete target |
| 11 | 92 | | | Hardening |
| 12 | 100 | | | Release 3, code freeze |
| 13 | buffer | | | Defect fixing |
| 14 | buffer | | | UAT |
| 15 | buffer | | | Go-live |

**Escalation rule:** variance worse than −10 % on any day triggers the de-scope ladder in
`RISK_REGISTER.md` R02 rather than overtime.
