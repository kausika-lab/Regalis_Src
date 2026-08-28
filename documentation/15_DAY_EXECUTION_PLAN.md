# 15_DAY_EXECUTION_PLAN.md
**Regalis WorkSpace — Phase 1 core**

**Team:** Developer 1 (Senior / Tech Lead) · Developer 2 (Junior 1) · Developer 3 (Junior 2) · QA
**Working days:** 15, Monday 31 August 2026 → Friday 18 September 2026
**Split:** 12 days planned delivery + **3 days contingency (20%)**
**Scope:** Phase-1 core as defined in `PROJECT_ANALYSIS.md` §7. Overflow items listed there are not in this plan.

> The buffer is **not** parked at the end. Days 13–15 carry named buffer work, and each of
> Days 4, 7 and 10 reserves half a day of unallocated capacity that rolls forward if unused.
> Master prompt §18 requires the buffer be used intelligently, not hoarded.

---

## Capacity

| Role | Days | Hours/day | Total hours |
|------|------|-----------|-------------|
| Senior | 15 | 8 | 120 |
| Junior 1 | 15 | 8 | 120 |
| Junior 2 | 15 | 8 | 120 |
| QA | 15 | 8 | 120 |
| **Total** | | | **480** |

Senior loses ~2 h/day to review and unblocking from Day 4, so ~96 h of Senior build
capacity is the planning figure.

---

## Day 1 — Monday 31 Aug · Requirements & Architecture

| Role | Work |
|------|------|
| Senior | Requirement analysis; module identification; architecture; ERD; API standards; auth & authorization design; git strategy; repo + branch protection |
| Junior 1 | Analyse Module Group A; screen list; UI flow for locations, workspaces, booking, tickets |
| Junior 2 | Analyse Module Group B; screen list; UI flow for payments, wallet, invoices, passes, inventory, reports |
| QA | Test strategy; test-case template; acceptance criteria per module; defect severity definitions |

**Deliverables:** `PROJECT_ANALYSIS.md` `ARCHITECTURE.md` `DATABASE_DESIGN.md` `API_PLAN.md` `UI_PLAN.md` `MODULE_PLAN.md` `ROLE_PERMISSION_MATRIX.md` `RISK_REGISTER.md`, this plan, tracker.
**Gate:** design freeze — no coding starts until the ERD and API contract v1 are agreed by all three developers. *(Maps to exec-plan gate **M1**.)*

## Day 2 — Tuesday 1 Sep · Project foundation

| Role | Work |
|------|------|
| Senior | .NET solution (Domain/Shared/Application/Infrastructure/API); EF Core + DbContext + configurations; `ApiResponse`; exception middleware; correlation id; Serilog; Swagger; CORS; rate limiting; security headers; audit interceptor; JWT scaffolding |
| Junior 1 | React + TS + Vite project; layouts; routing; theme tokens; first 8 shared components |
| Junior 2 | Axios instance + interceptors; API service layer; DataTable; form components; validation setup |
| QA | Foundation test cases; Postman collection skeleton; environment files |

**Exit:** solution builds; API runs; Swagger loads; frontend renders the shell; database created from migrations.

## Day 3 — Wednesday 2 Sep · Authentication + User / Role / Menu / Permission

| Role | Work |
|------|------|
| Senior | Login (customer + admin), register, OTP, forgot/reset password, refresh rotation, lockout, password policy; dynamic permission policy provider; `GET /menus/my` |
| Junior 1 | Login page (dual panel), register, OTP, forgot/reset screens; User management UI |
| Junior 2 | Role management UI; permission matrix editor; menu management UI; dynamic sidebar renderer |
| QA | Test auth, lockout, OTP expiry/attempts, token refresh, **role/menu/permission enforcement including direct API calls** |

**RELEASE 1 — Security + Administration.** *(Maps to **M2**.)*

## Day 4 — Thursday 3 Sep · Masters + the booking engine begins

| Role | Work |
|------|------|
| Senior | **Central availability & booking engine**: `IAvailabilityService`, overlap predicate, serializable + `UPDLOCK/HOLDLOCK` conflict check, retry policy, quote/hold/confirm skeleton |
| Junior 1 | Locations CRUD; Workspace types; Workspaces CRUD (Desk/Cabin/Meeting Room) API + UI |
| Junior 2 | Payment abstraction + Razorpay adapter + sandbox adapter; wallet schema, balance, ledger |
| QA | Test masters; write the booking concurrency test plan |

*Half-day float reserved.*

## Day 5 — Friday 4 Sep · Booking + calendar + payments

| Role | Work |
|------|------|
| Senior | Confirm transaction (booking + invoice + QR + notification + calendar, atomic); calendar API day/week/month; block/unblock; admin booking |
| Junior 1 | Customer booking flow screens (type → date → time → duration → workspace → summary); admin booking list/detail |
| Junior 2 | Invoice generation, numbering, CGST/SGST, PDF; payment verify + webhook |
| QA | Functional test of the booking flow; **concurrency test: 20 parallel confirms on one slot** |

**Gate:** central booking engine demo, one calendar serving both portals, backend overlap prevention proven. *(**M3**.)*

## Day 6 — Monday 7 Sep · Money complete

| Role | Work |
|------|------|
| Senior | Calendar UI (day/week/month grids, popups, block dialog); code review backlog |
| Junior 1 | My Bookings, booking detail, cancel/extend/reschedule UI; notifications module |
| Junior 2 | Wallet screens, recharge, transactions, refunds; invoice screens pending/paid; Day Pass + Monthly Pass |
| QA | Test payments, wallet, invoices, refunds, GST maths |

**Gate:** paid booking end to end produces booking id, invoice PDF, QR, notification, calendar update. *(**M4**.)*

## Day 7 — Tuesday 8 Sep · QR, access control, attendance

| Role | Work |
|------|------|
| Senior | Access token generation, QR payload signing, `/access/validate`, `IAccessControlAdapter` + mock, deny reasons, **manual override with `ACCESS.OVERRIDE`**, access log |
| Junior 1 | My QR / Access screen; check-in/check-out UI; My Work Status |
| Junior 2 | Attendance records, admin attendance grid, corrections keeping originals, payroll export |
| QA | Grant / deny / device-offline / override paths; attendance correctness |

**RELEASE 2 — Core business modules.** *Half-day float reserved.* *(**M5**.)*

## Day 8 — Wednesday 9 Sep · Operations

| Role | Work |
|------|------|
| Senior | Housekeeping scheduling **writing calendar blocks**; staff access tokens time-boxed to schedule; integration review |
| Junior 1 | Services & Coffee catalogue; banners; settings; contact us |
| Junior 2 | Inventory item master; In/Out/Damage ledger; derived stock; reorder flags; export |
| QA | Verify a housekeeping schedule removes the slot from customer availability; inventory audit history |

**Gate:** operations modules complete. *(**M6**.)*

## Day 9 — Thursday 10 Sep · Support, notifications, reports begin

| Role | Work |
|------|------|
| Senior | Cross-module integration; performance pass on the calendar and availability queries; N+1 elimination |
| Junior 1 | Support/ticket: categories, issue types, raise flow, attachments, thread, admin triage |
| Junior 2 | Reports 1–8 + `ReportShell` + Excel/PDF export |
| QA | End-to-end: register → book → pay → invoice → QR → check-in → check-out → report |

## Day 10 — Friday 11 Sep · Feature complete target

| Role | Work |
|------|------|
| Senior | Complete code review; security review; audit trail screen; dashboard aggregates |
| Junior 1 | Customer dashboard/home (banner, featured services, today's booking, QR button); profile |
| Junior 2 | Reports 9–16; admin dashboard cards |
| QA | Full regression pass 1 |

*Half-day float reserved.*

## Day 11 — Monday 14 Sep · Hardening

Security (authorization on every endpoint re-verified, secrets audit, input validation,
file-upload validation, CORS, headers, rate limits) · performance (indexes, pagination,
bundle size, lazy routes) · error handling · logging · audit completeness · responsive
verification at all seven widths · cross-browser Chrome / Edge / Firefox.

QA: negative and security-oriented functional tests; **role/permission matrix executed in full**.

## Day 12 — Tuesday 15 Sep · Release 3 — feature-complete build

Build, deploy to UAT, full regression, critical defect validation, release approval by
the Senior. **Code freeze: only defect fixes after this point.** *(**M7**.)*

---

## Buffer days (20% contingency)

## Day 13 — Wednesday 16 Sep · Buffer / defect fixing
Critical and high defects only; integration issues; requirement clarifications;
performance fixes; database issues. Any unused float from Days 4, 7 and 10 is consumed
here first. If the day is clear, it is spent on test-coverage gaps, not new features.

## Day 14 — Thursday 17 Sep · Buffer / UAT
UAT support with Regalis operations; regression on fixes; deployment rehearsal;
production configuration validation; backup and **rollback rehearsal**. *(**M8** — UAT sign-off.)*

## Day 15 — Friday 18 Sep · Final delivery
Final regression · production build · deployment · database scripts executed by the DBA ·
configuration · documentation · release notes · handover and training.
**Status target: production ready.** *(**M9**.)*

---

## Incremental release strategy

| Release | Day | Contents |
|---------|-----|----------|
| Release 1 | 3 | Foundation, authentication, users, roles, menus, permissions, dashboards |
| Release 2 | 7 | Locations, workspaces, booking engine, calendar, payments, wallet, invoices, passes, QR, access, attendance |
| Release 3 | 12 | Operations, support, notifications, services, banners, settings, all 16 reports — feature complete |
| Release 4 | 15 | Final production build |

Every release goes through build → deploy → test → fix → regression → approve.

## Git strategy

`main` (protected, release-only) · `develop` (integration) · `feature/*` · `release/*` ·
`hotfix/*`. No direct commits to `main` or `develop`. Every change arrives by pull
request, reviewed and approved by the Senior against the §25 checklist. Meaningful commit
messages. No secrets committed — a pre-commit secret scan runs in CI.

## Daily rhythm

15-minute stand-up each morning (yesterday / today / blockers / risks). Any blocker older
than one day escalates. The Senior updates the master tracker daily. QA reports tested /
passed / failed / blocked / critical / regression status. Defect triage runs daily from
Day 9.

## What would make this plan fail

Honestly stated, so it can be watched rather than discovered late:

1. Two junior developers producing 60 % of the screens in 12 days is the tightest
   constraint. If Day 6 velocity is below plan, the first de-scope is Reports 9–16 to a
   generic filterable grid without bespoke analytics.
2. The booking engine is the only truly hard component. If it is not demonstrable by end
   of Day 5, the buffer is spent there first and everything else slips behind it.
3. Razorpay keys, the seeded admin password and the SMTP/OTP channel must exist by Day 4.
   Without them the sandbox adapters carry the demo, but not UAT.
