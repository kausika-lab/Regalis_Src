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

## Day 6 — Monday 7 September 2026 · Gate M4

| Date | Dev | Module | Screen | Task | Type | Status | Pri | Est | Act | % | Blocker | Dependency | QA | Review | Remarks |
|------|-----|--------|--------|------|------|--------|-----|-----|-----|---|---------|-----------|----|--------|---------|
| 07-Sep | Senior | M09 | /admin/calendar | Calendar day-timeline UI (workspaces × IST hours), booking/block popups, block dialog with confirmed-booking guard, unblock | Frontend | Completed | Critical | 5 | 5 | 100 | — | Calendar API | Passed | Approved | Week/month API live; grid views iterate with the design reskin |
| 07-Sep | Senior | M08 | — | Cancel/reschedule/extend engine: policy refunds to wallet, locked availability re-check, QR window follows, supplementary GST invoice for extensions; shared BookingEngineLocks | Backend API | Completed | Critical | 5 | 5 | 100 | — | Engine | Passed | Approved | UTC JSON converter fixed browser-local date parsing |
| 07-Sep | Junior 1 | M10/M11 | /bookings | My Bookings tabs + cancel (policy messaging) / reschedule / extend dialogs | Frontend | Completed | Critical | 4 | 4 | 100 | — | Mutation APIs | Passed | Approved | |
| 07-Sep | Junior 1 | M20 | /notifications | Notification centre (read/read-all/delete, action links) + topbar bell with unread badge; broadcast API | Frontend | Completed | High | 3 | 3 | 100 | — | — | Passed | Approved | |
| 07-Sep | Junior 2 | M13 | /wallet /admin/wallet | Wallet screens: balance tiles, sandbox recharge (limits enforced), running-balance ledger; admin wallet search + audited adjust | Frontend | Completed | Critical | 5 | 5 | 100 | — | Recharge API | Passed | Approved | |
| 07-Sep | Junior 2 | M14 | /invoices /admin/invoices /admin/payments | Invoice tabs + PDF download, admin invoice grid with GST columns, payments grid | Frontend | Completed | High | 4 | 4 | 100 | — | — | Passed | Approved | |
| 07-Sep | Junior 2 | M15 | /day-pass /monthly-pass /passes /admin/passes/* | Pass plans browse + wallet/gateway purchase, my passes, admin plan editor + purchases with cancel/refund | Frontend | Completed | Critical | 5 | 5 | 100 | — | Pass APIs | Passed | Approved | Pass expiry in the background sweeper |
| 07-Sep | QA | M08–M15 | — | 11 new integration tests + dev smoke (recharge WTX-000001, 100% refund ₹708, notifications) | Integration Test | Completed | Critical | 6 | 6 | 100 | — | — | Passed | Approved | Suite: 68 automated tests green |

**Gate M4 passed** (proven Day 5 and regression-held): paid booking → id + invoice PDF + QR + notification + calendar, and the full money cycle now closes with refunds.

## Day 7 — Tuesday 8 September 2026 · RELEASE 2 (Gate M5)

| Date | Dev | Module | Screen | Task | Type | Status | Pri | Est | Act | % | Blocker | Dependency | QA | Review | Remarks |
|------|-----|--------|--------|------|------|--------|-----|-----|-----|---|---------|-----------|----|--------|---------|
| 08-Sep | Senior | M16 | — | /access/validate: HMAC-first payload check, token/window/usage/booking/workspace rules, attendance gate, door via IAccessControlAdapter (Mock, A01), append-only access log for every attempt; grants bump usage + attendance + booking transitions | Backend API | Completed | Critical | 6 | 6 | 100 | — | QR tokens | Passed | Approved | Invariant 9: device offline = DeviceError, never a grant |
| 08-Sep | Senior | M16 | — | Manual override behind distinct ACCESS.OVERRIDE: mandatory reason, exception log row + ACCESS_OVERRIDE audit entry (A10); token revoke endpoint | Backend API | Completed | Critical | 2 | 2 | 100 | — | — | Passed | Approved | |
| 08-Sep | Junior 1 | M16 | /qr /work-status | My QR (client-rendered QR codes per active token) and My Work Status (self check-in/out with live-basis rule, history) | Frontend | Completed | Critical | 4 | 4 | 100 | — | Access APIs | Passed | Approved | qrcode npm package |
| 08-Sep | Junior 1 | M16 | /admin/access /admin/access/logs | Scan/validate console with grant/deny panel + override dialog; access log grid with result filter and override badges | Frontend | Completed | High | 4 | 4 | 100 | — | — | Passed | Approved | OFFLINE-* device ids simulate the downed reader |
| 08-Sep | Junior 2 | M17 | /admin/attendance /admin/attendance/payroll | Attendance grid (original times shown next to corrections), correction dialog with mandatory reason, payroll CSV export page; CsvPayrollExporter behind IPayrollExporter (A03); auto-close in the sweeper | Backend API | Completed | Critical | 6 | 6 | 100 | — | Recorder | Passed | Approved | XLSX variant joins with the reporting toolkit (Day 9) |
| 08-Sep | QA | M16–M17 | — | 9 access/attendance integration tests + live smoke: pass PASS-000001 → gate in → offline DeviceError → gate out → attendance closed → payroll CSV | Integration Test | Completed | Critical | 6 | 6 | 100 | — | — | Passed | Approved | Suite: 77 automated tests green |

**RELEASE 2 tagged — core business modules (gate M5 passed).**

## Day 8 — Wednesday 9 September 2026 · Gate M6

| Date | Dev | Module | Screen | Task | Type | Status | Pri | Est | Act | % | Blocker | Dependency | QA | Review | Remarks |
|------|-----|--------|--------|------|------|--------|-----|-----|-----|---|---------|-----------|----|--------|---------|
| 09-Sep | Senior | M18 | /admin/housekeeping | Housekeeping engine: schedule create/edit writes TRN_WorkspaceBlock in the same serializable transaction (Invariant 12) with booking/block conflict guards; staff overlap check; cancel/complete releases the block; staff access token time-boxed to exactly the schedule window, revoked on cancel/reschedule; assignment notifications; HKS- numbers | Backend API | Completed | Critical | 6 | 6 | 100 | — | Calendar blocks, access tokens | Passed | Approved | QR signing deduplicated into QrPayloadSigner |
| 09-Sep | Senior | M18 | /admin/housekeeping | Housekeeping grid + editor (staff picker, IST window, block toggle), start/complete/cancel; staff scoped server-side to own rows and given the menu | Frontend | Completed | High | 3 | 3 | 100 | — | — | Passed | Approved | |
| 09-Sep | Junior 1 | M22 | /admin/services /services | Services & Coffee catalogue CRUD (catalogue writes = SERVICE.EDIT; SERVICE.CREATE stays reserved for customer orders) + customer browse page with category chips | Full stack | Completed | High | 4 | 4 | 100 | — | — | Passed | Approved | Ordering flow lands with the Day 10 home revamp |
| 09-Sep | Junior 1 | M23 | /admin/banners /admin/settings /contact | Banners CRUD + windowed/portal-filtered /banners/active; Settings screen grouped with masked secrets, DataType validation and cache eviction on save; anonymous /settings/public feeding the new Contact Us page; Contact Us menu seeded | Full stack | Completed | High | 4 | 4 | 100 | — | — | Passed | Approved | |
| 09-Sep | Junior 2 | M19 | /admin/inventory/* | Inventory item master (code frozen once ledgered), append-only In/Out/Damage/Adjustment ledger with ITX- numbers, derived stock + reorder flags (Invariant 11), negative-stock guard under a serializable transaction, CSV export; entry modal + grids | Full stack | Completed | Critical | 6 | 6 | 100 | — | — | Passed | Approved | CK_InvTxn_Quantity amended (migration) so a compensating Adjustment can be signed; DATABASE_DESIGN updated |
| 09-Sep | QA | M18–M23 | — | 9 operations integration tests: block drops availability + cancel restores it, block refuses paid bookings, staff token time-boxed + revoked, stock derivation + overdraw refused + signed adjustment, catalogue visibility, banner windows, settings public/update/type-guard | Integration Test | Completed | Critical | 5 | 5 | 100 | — | — | Passed | Approved | Suite: 86 automated tests green (72 integration + 14 unit) |

**Gate M6 passed — operations modules complete:** a housekeeping schedule removes the
slot from customer availability through the one calendar path and gives the staff QR
exactly the schedule window; the inventory ledger carries a complete audit history.

## Day 9 — Thursday 10 September 2026

| Date | Dev | Module | Screen | Task | Type | Status | Pri | Est | Act | % | Blocker | Dependency | QA | Review | Remarks |
|------|-----|--------|--------|------|------|--------|-----|-----|-----|---|---------|-----------|----|--------|---------|
| 10-Sep | Senior | — | — | Cross-module integration review + performance pass: availability slot loop regrouped per workspace (was O(slots × spaces × rows)), workspace+amenity include split (AsSplitQuery) to stop join row multiplication; booking/calendar index coverage reviewed — sufficient, no schema change | Backend | Completed | High | 4 | 4 | 100 | — | — | Passed | Approved | Calendar queries already single-round-trip |
| 10-Sep | Junior 1 | M21 | /support /support/raise /support/tickets/:id | Ticket engine + UI: guided category→issue-type→details raise flow (TKT- numbers), customer row-scoping (404, no existence leak), threaded replies, admin-only internal notes, assign/status/priority/close/reopen (customer reply on Resolved reopens), notifications both ways, CSV export | Full stack | Completed | Critical | 6 | 6 | 100 | — | Seeded categories | Passed | Approved | |
| 10-Sep | Junior 1 | M21 | — | Attachments: multipart ≤10 MB, png/jpg/jpeg/webp/pdf validated by extension AND magic bytes (spoofed extension refused), ≤5 per ticket, stored under Storage:UploadRoot outside the web root, streamed back only through the authorized endpoint | Backend API | Completed | Critical | 3 | 3 | 100 | — | — | Passed | Approved | |
| 10-Sep | Junior 2 | M24 | /admin/reports /admin/reports/:key | Report engine + ReportShell: one typed tabular contract drives the grid, the ClosedXML Excel export and the QuestPDF landscape PDF for all 16 catalogued reports; reports 1–8 live (booking, revenue, customer, occupancy, desk, cabin, meeting-room, day-pass); landing grid shows the Day 10 set disabled | Full stack | Completed | Critical | 7 | 7 | 100 | — | — | Passed | Approved | ClosedXML 0.105.1 added |
| 10-Sep | QA | all | — | End-to-end proof: register → book → wallet pay → invoice PDF → QR → gate in → gate out → attendance closed → booking + revenue reports carry the row. Plus ticket scoping/notes/attachment-sniff tests and report catalogue/export tests (5 new) | Integration Test | Completed | Critical | 6 | 6 | 100 | — | — | Passed | Approved | Suite: 91 automated tests green (77 integration + 14 unit) |

## Day 10 — Friday 11 September 2026 · FEATURE COMPLETE

| Date | Dev | Module | Screen | Task | Type | Status | Pri | Est | Act | % | Blocker | Dependency | QA | Review | Remarks |
|------|-----|--------|--------|------|------|--------|-----|-----|-----|---|---------|-----------|----|--------|---------|
| 11-Sep | Senior | M25 | /admin/audit | Audit trail: filtered read + module list + CSV export over the append-only TRN_AuditLog; grid with action badges and change preview | Full stack | Completed | High | 3 | 3 | 100 | — | Audit interceptor | Passed | Approved | Read-only by design |
| 11-Sep | Senior | — | /admin /(home) | Dashboard aggregates in one round trip: /dashboard/admin card row (bookings, occupancy, revenue today/month, checked-in, passes, tickets, housekeeping) and /dashboard/customer (wallet, passes, notifications, tickets, today's + next booking) | Full stack | Completed | High | 4 | 4 | 100 | — | — | Passed | Approved | EF aggregates instead of usp_GetDashboardSummary — same shape, no proc deployment |
| 11-Sep | Senior | — | — | Code + security review of Days 8–9: two findings fixed — includeUnavailable on /services now needs SERVICE.EDIT (hidden items no longer leak to customers holding SERVICE.VIEW); the full /banners grid scopes customers to live customer-portal banners. Regression tests added for both | Security | Completed | Critical | 3 | 3 | 100 | — | — | Passed | Approved | |
| 11-Sep | Junior 1 | M02 | / /profile /admin/profile | Customer home per UI_PLAN: banner carousel, stat chips, Today's Booking with View QR / Book Now, featured services strip. Own-record profile: GET/PUT /profile (email immutable, mobile uniqueness), content-sniffed photo ≤2 MB, change-password card | Full stack | Completed | Critical | 6 | 6 | 100 | — | Dashboard API | Passed | Approved | |
| 11-Sep | Junior 2 | M24 | /admin/reports/* | Reports 9–16: monthly-pass, payment, invoice, wallet, coffee, ticket (first-response hours), check-in-out, qr-access — all through the shared contract and exporters; admin dashboard card row wired | Full stack | Completed | Critical | 6 | 6 | 100 | — | Report engine | Passed | Approved | All 16 verified to run by test |
| 11-Sep | QA | all | — | Full regression pass 1: entire suite re-run (one stale Day 9 catalogue assertion updated); 5 new tests for reports 9–16, dashboards, audit, profile, security fixes | Integration Test | Completed | Critical | 5 | 5 | 100 | — | — | Passed | Approved | Suite: 96 automated tests green (82 integration + 14 unit) |

**FEATURE COMPLETE on target:** every Phase-1 module has its backend and screens;
remaining days are hardening (Day 11) and Release 3 / code freeze (Day 12).

## Day 11 — Monday 14 September 2026 · Hardening

| Date | Dev | Module | Screen | Task | Type | Status | Pri | Est | Act | % | Blocker | Dependency | QA | Review | Remarks |
|------|-----|--------|--------|------|------|--------|-----|-----|-----|---|---------|-----------|----|--------|---------|
| 14-Sep | Senior | — | — | Authorization hardening: fallback policy (auth is the default; health probes anonymous by declaration); reflection test proving every action declares [Authorize]/[HasPermission]/[AllowAnonymous]; anonymous auth POSTs verified behind the "auth" rate-limit policy | Security | Completed | Critical | 3 | 3 | 100 | — | — | Passed | Approved | An unannotated endpoint can no longer be public |
| 14-Sep | Senior | — | — | Finding fixed: /dashboard/admin was reachable with the customer's DASHBOARD.VIEW — operational aggregates now admin-portal only. Secrets audit clean (no credentials outside tests; committed appsettings empty; dev key ephemeral). Security headers/CORS/rate limits/401-403 envelopes re-verified, headers asserted by test | Security | Completed | Critical | 3 | 3 | 100 | — | — | Passed | Approved | Found by the new negative sweep |
| 14-Sep | Senior | — | — | Performance: all routes converted to lazy chunks (59) and the full lucide icon map evicted from the eager layout for a seeded-menu icon registry — first-paint JS 1,079 KB → 430 KB (−60%). Pagination clamps and error/logging middleware re-verified; audit interceptor redaction list reviewed | Performance | Completed | High | 4 | 4 | 100 | — | — | Passed | Approved | |
| 14-Sep | QA | all | — | Role/permission matrix executed in full: Manager/Finance/Support/Housekeeping users provisioned live and probed against 15 endpoints from matrix §4 + ACCESS.OVERRIDE as a distinct privilege; customer negative sweep across all administration surfaces; security-header and health-probe tests | Security Test | Completed | Critical | 6 | 6 | 100 | — | — | Passed | Approved | Suite: 101 automated tests green (87 integration + 14 unit) |
| 14-Sep | QA | all | — | Responsive: breakpoints verified in code (sidebar → bottom nav < 768 px); the seven-width visual pass and Chrome/Edge/Firefox matrix are manual and scheduled with the Day 12 UAT deployment | Manual QA | In progress | High | 2 | 1 | 50 | Needs deployed UAT | Day 12 deploy | Pending | — | Honest carry-over, not a defect |

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
| 6 | 50 | 50 | 0 | Money complete: refunds, recharge, passes, invoices, calendar UI |
| 7 | 60 | 60 | 0 | Release 2 tagged: QR access, attendance, payroll export live |
| 8 | 70 | 70 | 0 | Gate M6: housekeeping blocks the real calendar; inventory ledger live |
| 9 | 78 | 78 | 0 | Tickets + reports 1–8 live; e2e journey proven by automated test |
| 10 | 86 | 86 | 0 | Feature complete on target; regression pass 1 green |
| 11 | 92 | 92 | 0 | Hardening: matrix proven by test, bundle −60%, 2 authz findings fixed |
| 12 | 100 | | | Release 3, code freeze |
| 13 | buffer | | | Defect fixing |
| 14 | buffer | | | UAT |
| 15 | buffer | | | Go-live |

**Escalation rule:** variance worse than −10 % on any day triggers the de-scope ladder in
`RISK_REGISTER.md` R02 rather than overtime.
