# MODULE_PLAN.md
**Regalis WorkSpace — module inventory and team allocation**

Team model (master prompt §16): **Developer 1 (Senior / Tech Lead)**, **Developer 2
(Junior 1)**, **Developer 3 (Junior 2)**, **QA**.

---

## 1. Module inventory

24 modules. "Screens" counts distinct routes, not components.

| # | Module | Portal | Screens | Owner | Depends on |
|---|--------|--------|---------|-------|-----------|
| M01 | Platform Foundation | — | 0 | Senior | — |
| M02 | Authentication & OTP | Both | 6 | Senior | M01 |
| M03 | Users & Customers | Admin | 4 | Senior | M02 |
| M04 | Roles & Permissions | Admin | 3 | Senior | M02 |
| M05 | Menu Management | Admin | 2 | Senior | M04 |
| M06 | Locations | Admin | 2 | Junior 1 | M01 |
| M07 | Workspaces (Desk/Cabin/Meeting Room) | Admin | 4 | Junior 1 | M06 |
| M08 | **Central Availability & Booking Engine** | — | 0 | **Senior** | M07 |
| M09 | Central Calendar | Both | 3 | Senior | M08 |
| M10 | Customer Booking Flow | Customer | 5 | Junior 1 | M08 |
| M11 | Booking Management | Admin | 3 | Junior 1 | M08 |
| M12 | Payments | Both | 3 | Junior 2 | M08 |
| M13 | Wallet | Both | 3 | Junior 2 | M12 |
| M14 | Invoices (+ GST PDF) | Both | 4 | Junior 2 | M12 |
| M15 | Day Pass & Monthly Pass | Both | 5 | Junior 2 | M12 |
| M16 | QR & Access Control | Both | 3 | Senior | M08 |
| M17 | Attendance & Payroll Export | Admin | 3 | Junior 2 | M16 |
| M18 | Housekeeping / Maintenance | Admin | 3 | Senior | M08, M03 |
| M19 | Inventory | Admin | 4 | Junior 2 | M06 |
| M20 | Notifications | Both | 3 | Junior 1 | M02 |
| M21 | Support / Tickets | Both | 5 | Junior 1 | M02 |
| M22 | Services & Coffee | Both | 3 | Junior 1 | M06 |
| M23 | Banners & Settings | Admin | 3 | Junior 1 | M01 |
| M24 | Reports & Analysis (16 reports) | Admin | 17 | Junior 2 | all |

**Screen total: 91.**

---

## 2. Allocation per master prompt §17

### Developer 1 — Senior / Tech Lead
**Foundation and everything the other two build on top of.**

- M01 Platform Foundation — solution structure, EF Core, DbContext, audit interceptor,
  Serilog, `ApiResponse`, exception middleware, correlation id, Swagger, CORS, rate
  limiting, security headers, health checks
- M02 Authentication — JWT, refresh rotation, password hashing/policy, lockout, OTP
- M03 Users & Customers
- M04 Roles & Permissions — the dynamic policy provider
- M05 Menu Management
- **M08 Central Availability & Booking Engine** — the highest-risk component in the
  project; the Senior owns it outright
- M09 Central Calendar
- M16 QR & Access Control + the `IAccessControlAdapter` seam
- M18 Housekeeping (because it writes into the calendar)
- Frontend: app shell, layouts, routing, `ProtectedRoute`/`PermissionRoute`, axios
  interceptors, auth store, **the reusable component library**
- Database architecture, integration, deployment architecture, security
- **Reviews and approves every pull request from Junior 1 and Junior 2**

### Developer 2 — Junior 1 · Module Group A ("workspace & engagement")
- M06 Locations
- M07 Workspaces — Desks, Cabins, Meeting Rooms
- M10 Customer Booking Flow — type → date → time → duration → workspace → summary
- M11 Admin Booking Management
- M20 Notifications
- M21 Support / Tickets — raise, categories, issue types, attachments, thread
- M22 Services & Coffee
- M23 Banners & Settings
- Unit tests for the above

### Developer 3 — Junior 2 · Module Group B ("money & operations")
- M12 Payments — Razorpay adapter, server-side signature verification, webhook
- M13 Wallet — balance, recharge, ledger, refunds
- M14 Invoices — numbering, CGST/SGST, PDF, pending/paid
- M15 Day Pass & Monthly Pass
- M17 Attendance & payroll export
- M19 Inventory — item master, In/Out/Damage ledger, derived stock
- M24 Reports & Analysis — all 16 reports + Excel/PDF export
- Unit tests for the above

### QA
Test plan and cases from Day 1; tests each module the day it lands, never at the end.
Owns: functional, API, UI, regression, **role/permission testing including direct URL and
direct API calls with a valid token but the wrong permission**, negative testing,
concurrency testing of the booking engine, cross-browser (Chrome, Edge, Firefox),
responsive verification at the seven required widths, defect triage, release sign-off.

---

## 3. Screen list

### Customer portal (28)
`/login` · `/register` · `/verify-otp` · `/forgot-password` · `/reset-password` ·
`/` (Dashboard/Home) · `/book` (Calendar / Book Workspace) · `/book/summary` ·
`/book/confirmation/:id` · `/bookings` · `/bookings/:id` · `/day-pass` ·
`/day-pass/:planId` · `/monthly-pass` · `/passes` · `/wallet` · `/wallet/add-funds` ·
`/invoices` · `/invoices/:id` · `/qr` · `/work-status` · `/services` · `/notifications` ·
`/support` · `/support/raise` · `/support/tickets` · `/support/tickets/:id` · `/profile`

### Admin portal (46)
`/admin` (Dashboard) · `/admin/customers` · `/admin/customers/:id` ·
`/admin/users` · `/admin/users/:id` · `/admin/roles` · `/admin/roles/:id/permissions` ·
`/admin/menus` · `/admin/locations` · `/admin/locations/:id` ·
`/admin/workspaces` · `/admin/workspaces/desks` · `/admin/workspaces/cabins` ·
`/admin/workspaces/meeting-rooms` · `/admin/calendar` · `/admin/bookings` ·
`/admin/bookings/:id` · `/admin/payments` · `/admin/invoices` · `/admin/invoices/:id` ·
`/admin/wallet` · `/admin/passes/day` · `/admin/passes/monthly` ·
`/admin/passes/purchases` · `/admin/access` · `/admin/access/logs` ·
`/admin/attendance` · `/admin/attendance/payroll` · `/admin/inventory` ·
`/admin/inventory/items` · `/admin/inventory/transactions` ·
`/admin/housekeeping` · `/admin/housekeeping/:id` · `/admin/services` ·
`/admin/banners` · `/admin/notifications` · `/admin/tickets` · `/admin/tickets/:id` ·
`/admin/settings` · `/admin/audit` · `/admin/profile` ·
`/admin/reports` + 16 report routes under `/admin/reports/:reportKey`

---

## 4. Per-module definition of done

A module is not "done" until **all** of the following exist. This is the master prompt
§15 checklist and QA verifies it item by item.

- [ ] Database tables, FKs, indexes, audit columns, seed data
- [ ] Domain entities + EF configuration
- [ ] DTOs (request + response) with no entity leakage
- [ ] FluentValidation validator for every request DTO
- [ ] Service with the business logic (never in the controller)
- [ ] Repository / data access with server-side pagination, sorting, filtering, search
- [ ] Controller with `[HasPermission(...)]` on every action
- [ ] `ApiResponse` envelope on every response
- [ ] Swagger description, request/response examples, auth requirement
- [ ] Audit logging on create/update/delete/approve/export
- [ ] Structured logging on the key business operation
- [ ] Frontend screens using **only** the shared component library
- [ ] Frontend validation mirroring the backend rules
- [ ] Loading, empty, error and forbidden states
- [ ] Responsive at 1920 / 1600 / 1366 / 1280 / 1024 / 768 / 390
- [ ] Unit tests for the service
- [ ] Integration test for the primary endpoint
- [ ] QA functional + negative + role test passed
- [ ] Senior code review approved and merged
- [ ] **No `TODO`, `Coming Soon`, placeholder, dummy data or mock API left**
