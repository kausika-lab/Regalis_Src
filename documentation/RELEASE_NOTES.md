# Regalis WorkSpace — Release Notes

## Release 3 · v1.0.0-rc.1 — feature-complete build (Day 12, gate M7)

Everything in Phase 1 scope is implemented, integration-tested and hardened.
This is the UAT candidate; only defect fixes merge after this tag.

### New since Release 2

**Operations (Day 8, gate M6)**
- Housekeeping scheduling: a blocking schedule writes the workspace calendar
  block in the same transaction, so customer availability drops the slot
  through the one calendar path; staff access QRs are time-boxed to exactly
  the schedule window and revoked on cancel/reschedule.
- Inventory: item master, append-only In/Out/Damage/Adjustment ledger with
  derived stock and reorder flags, CSV export. Signed adjustments enabled by
  the `SignedAdjustmentQuantity` migration (spec updated to match).
- Services & Coffee catalogue (admin CRUD + customer browse), promotional
  banners with validity windows, the admin Settings screen (secrets masked,
  values type-checked, applied immediately), anonymous Contact Us.

**Support & reports (Day 9)**
- Support tickets: guided raise flow, threaded conversations, admin-only
  internal notes, assign/status/priority/close/reopen, notifications both
  ways, CSV export. Attachments ≤ 10 MB validated by extension *and* magic
  bytes, stored outside the web root, streamed through an authorized endpoint.
- Report engine: 16 reports on one typed tabular contract with shared Excel
  (ClosedXML) and PDF (QuestPDF) exporters, behind one ReportShell screen.
- Performance pass on availability queries (grouped occupancy, split queries).

**Feature complete (Day 10)**
- Reports 9–16 (payments, invoices, wallet, service orders, tickets,
  attendance, QR access, monthly passes).
- Dashboard aggregates in one round trip for both portals; customer home with
  banner carousel, today's booking + QR, featured services.
- Audit trail screen over the append-only audit log, with CSV export.
- Own-record profile (email immutable) with content-sniffed photo upload.

**Hardening (Day 11)**
- Fallback authorization policy: authentication is the default; a reflection
  test proves every endpoint declares its authorization explicitly.
- The role/permission matrix is executed in full by automated test across
  Manager, Finance, Support and Housekeeping plus a customer negative sweep.
- Findings fixed: admin dashboard aggregates, hidden catalogue items and
  off-window banners are no longer reachable with customer-held permissions.
- First-paint JS reduced 60% (1,079 KB → 430 KB) via route-level code
  splitting and a bounded menu-icon registry.

### Quality evidence
- 101 automated tests green (87 integration — real API against real SQL
  Server — plus 14 unit), including the end-to-end journey: register → book →
  pay → invoice → QR → gate in/out → attendance → report.

### Known items for UAT
- The seven-width responsive pass and the Chrome/Edge/Firefox matrix are
  manual checks scheduled on the deployed UAT build (tracker, Day 11).
- OTP delivery uses the console sender until SMTP credentials arrive
  (execution-plan risk #3): UAT verification codes are read from the API log.
- In-app service *ordering* (quick-add, wallet payment) is out of Release 3
  scope: the catalogue is live, orders are taken at the counter. The order
  tables, payment seam and the coffee report are already in place.
- Payments run against the sandbox gateway in UAT; Razorpay goes live with
  production keys at go-live.

---

## Release 2 — core business modules (Day 7, gate M5)
Booking engine with atomic confirmation (id + payment + invoice + QR +
notification), calendar for both portals, cancel/reschedule/extend with policy
refunds, wallet + recharge, GST invoices with PDF, day/monthly passes, QR
access control with manual override, attendance with corrections, payroll CSV.

## Release 1 — foundation (Day 3)
Clean architecture skeleton, JWT auth with refresh rotation, OTP registration,
dynamic RBAC (roles, permissions, menus — all data), user/role/menu
administration, audit interceptor, Serilog + correlation ids, rate limiting.
