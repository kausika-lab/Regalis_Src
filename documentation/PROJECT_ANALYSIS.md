# PROJECT_ANALYSIS.md
**Project:** Regalis WorkSpace — Coworking Space Management Platform
**Client:** Regalis WorkSpace (delivery by Amstag Tech Pvt Ltd)
**Phase:** Phase 1 — Live Release
**Document version:** 1.0
**Date:** 28 August 2026

---

## 1. Purpose of this document

This is STEP 1/STEP 2 output of the master development prompt. It records what was
found in the supplied source documents, what the system must actually do, what is
ambiguous, and what assumptions have been made so that development can start without
blocking.

No code was written before this analysis was completed.

---

## 2. Source documents analysed

| # | Document | Type | What it contributes |
|---|----------|------|--------------------|
| 1 | `Regalis WorkingSpace.docx` | Requirements | Customer portal: login/registration, home screen, navigation, booking, wallet, invoices, notifications, support, day pass, packages. Admin portal: full functional requirements, calendar/booking management, 16 reports. **Primary functional baseline.** |
| 2 | `Regalis_Phase_1_Live_Operations_Requirements_Customer_Admin.docx` | Requirements | Adds Access Control, Attendance, Billing & Finance, Inventory, Housekeeping/Maintenance to live Phase 1. Declares itself the **governing scope baseline (Doc-2)**, superseding Doc-1's out-of-scope list. 12 developer rules, 10 open confirmations. |
| 3 | `Regalis_Phase1_Execution_Plan.xlsx` / `.pdf` | Plan | 10-week plan, 79 tasks, 9 milestone gates, 6 roles, 12 design invariants, 17 open tooling decisions, out-of-scope register. |
| 4 | `Regalis_Phase1_Delivery_Proposal.pdf` | Commercial | Phase 1 delivery proposal. |
| 5 | `Regalis_WorkSpace_Phase1_Delivery_Proposal.pdf` | Commercial | Workspace-specific delivery proposal. |
| 6 | `Regalis_Proposal_3Dev_1QA.xlsx` | Commercial | 3 Dev + 1 QA capacity model, effort estimate, de-scope ladder, commercials. **Matches the team model in the master prompt.** |
| 7 | `Regalis_Development_Server_Setup.pdf` | Infra | Development server setup. |
| 8 | `Tracking SW.xlsx` | Tracking | Project cycle + epic tracker. |

---

## 3. Conflicts found between the master prompt and the source documents

These were escalated and decided before development started. Decisions are recorded
here as the binding position.

| # | Conflict | Master prompt says | Documents say | **DECISION** |
|---|----------|--------------------|---------------|--------------|
| C1 | Backend stack | ASP.NET Core + C# + EF Core | Not fixed | **ASP.NET Core + EF Core** (master prompt wins) |
| C2 | Database | Microsoft SQL Server | Tooling sheet: "PostgreSQL / MySQL — decision needed" | **Microsoft SQL Server** (master prompt wins). Closes tooling decision #5. |
| C3 | Schedule | 15 days (12 + 3 buffer) | 10 weeks, 24 Aug – 30 Oct 2026, gates M1–M9 | **15-day plan against Phase-1 core only.** Operational modules that cannot fit are explicitly listed as overflow (§7). |
| C4 | Team | Senior + 2 Junior + 1 QA | 6 roles (2 BE, 1 FE, 1 UX, 1 QA, 1 DevOps, PL) | **Senior + Junior 1 + Junior 2 + QA** (master prompt wins). Senior absorbs architecture, DevOps and code review. |
| C5 | Roles/permissions | Full dynamic RBAC, menu management, permission matrix, policy authorization | "Phase 1 ships Customer and Admin roles only… no custom permission builder" | **Build the dynamic RBAC engine** (master prompt wins — it is explicitly a no-hard-coding requirement). **Seed only Customer, Admin, Staff roles.** The engine is generic; the seeded data is minimal. This satisfies both. |
| C6 | Client type | React + Vite responsive web app | Progressive Web Application | **React + TS + Vite, installable PWA** (service worker + manifest). Both satisfied. |
| C7 | .NET version | "latest stable" | — | **`net8.0` (LTS).** Retargeting to net9/net10 is a one-line `TargetFramework` change; see `documentation/Configuration.md`. |

---

## 4. What the system is

Regalis WorkSpace is a **premium coworking-space management platform** delivered as an
installable PWA with two portals over **one shared backend and one shared database**.

```
                    REGALIS WORKSPACE
                            |
        +-------------------+-------------------+
        |                                       |
  CUSTOMER PORTAL                          ADMIN PORTAL
  book / pay / access                 operate / configure / report
        |                                       |
        +-------------------+-------------------+
                            |
              ONE BACKEND API  (ASP.NET Core)
                            |
              ONE DATABASE   (SQL Server)
                            |
        +-----------+-------+-------+-----------+
        |           |               |           |
   Payment GW   ZOAPI          Access Ctrl   Payroll
   (Razorpay)   (meeting rm)   (door/QR)     (export)
        ^           ^               ^           ^
        |           |               |           |
        +----- all behind ADAPTER interfaces ---+
```

**The single most important architectural fact:** there is exactly one booking and
availability engine and exactly one calendar data source. The customer calendar and the
admin calendar are two views of the same data. This is Design Invariant #1 and is
non-negotiable.

---

## 5. Entity inventory derived from requirements

45 tables in 6 groups. Full design in `DATABASE_DESIGN.md`.

| Group | Tables |
|-------|--------|
| Identity & Access | MST_User, MST_Role, MST_Permission, MST_Menu, MAP_UserRole, MAP_RolePermission, MAP_RoleMenu, TRN_RefreshToken, TRN_Otp, TRN_AuditLog, TRN_LoginHistory |
| Masters | MST_Location, MST_WorkspaceType, MST_Workspace, MST_Amenity, MAP_WorkspaceAmenity, MST_PassPlan, MST_ServiceItem, MST_Banner, MST_AppSetting, MST_TicketCategory, MST_TicketIssueType |
| Booking & Calendar | TRN_Booking, TRN_BookingStatusHistory, TRN_WorkspaceBlock |
| Finance | MST_Wallet, TRN_WalletTransaction, TRN_Payment, TRN_Invoice, TRN_InvoiceLine, TRN_CustomerPass, TRN_ServiceOrder, TRN_ServiceOrderLine |
| Access & Attendance | TRN_AccessToken, TRN_AccessLog, TRN_Attendance |
| Operations | MST_InventoryItem, TRN_InventoryTransaction, TRN_HousekeepingSchedule, TRN_Notification, TRN_Ticket, TRN_TicketMessage, TRN_TicketAttachment |

---

## 6. Assumptions register

Per master prompt §35, ambiguity is resolved by the safest reasonable assumption,
documented, and development continues. Each assumption maps to an open item that
Regalis Operations must confirm.

| ID | Ambiguity | Assumption taken | Risk if wrong | Confirm by |
|----|-----------|------------------|---------------|-----------|
| A01 | Access-control vendor/device API unknown (Doc-2 §21.1) | `IAccessControlAdapter` interface + `MockAccessControlAdapter` registered by configuration. No vendor logic in booking. | Low — swap the adapter implementation only. | Week 1 |
| A02 | ZOAPI meeting-room device API unknown | `IZoapiAdapter` interface + no-op implementation. Regalis calendar remains source of truth. | Low | Week 1 |
| A03 | Payroll product unknown (Doc-2 §21.3) | Attendance is stored centrally; hand-off is a CSV/XLSX **export** through `IPayrollExporter`. No direct integration. | Low | Week 2 |
| A04 | Is attendance for employees, members, or both? (Doc-2 §21.4) | **Both.** `TRN_Attendance.UserId` points at `MST_User`; `SubjectType` distinguishes Customer / Staff. | Low | Week 1 |
| A05 | Do housekeeping staff get application accounts? (Doc-2 §21.5) | **Yes** — `MST_User.UserType = Staff` with the `Housekeeping` role. No portal screens in Phase 1; they exist to be scheduled and to own access tokens. | Medium — adds a staff portal if wrong. | Week 1 |
| A06 | Housekeeping QR, card, or both? | `TRN_AccessToken.Method` supports `QR`, `Card`, `Manual`. Both work. | Low | Week 2 |
| A07 | Maintenance and housekeeping — one module or two? | **One module**, `TRN_HousekeepingSchedule.TaskType` distinguishes `Housekeeping` / `Maintenance` / `Repair` / `Cleaning` / `Inspection`. | Low | Week 1 |
| A08 | Inventory: In/Out/Damage only, or suppliers/POs too? | **In / Out / Damage only**, with a free-text `SourceOrDestination` and `ReferenceNumber`. No supplier master, no purchase orders. | Medium — supplier master is a Phase-2 change request. | Week 1 |
| A09 | GST fields, GSTIN, tax break-up, invoice numbering | Intra-state **CGST 9% + SGST 9% = 18%**, configurable in `MST_AppSetting`. Invoice number `REG/INV/{FY}/{00001}`. Booking number `REG-000001`. Company GSTIN held in settings, not hard-coded. | Medium — number format change is data-only. | Week 1 |
| A10 | Manual access procedure when access control is down | Admin-only `POST /access/manual-override` requiring `ACCESS.OVERRIDE` permission; every override writes an `AccessLog` row with `IsManualOverride = 1` and the approver's user id. Never grants unrestricted access. | Low | Week 2 |
| A11 | "Company Name — Yes / Confirm business rule" on registration | **Optional.** Individual freelancers are a core coworking segment. | Low | Week 1 |
| A12 | Payment gateway | **Razorpay** behind `IPaymentGateway`. A `SandboxPaymentGateway` lets the whole flow be demoed before keys arrive. | Low | Week 1 |
| A13 | Currency / timezone | **INR**, **Asia/Kolkata**. All timestamps stored **UTC**, rendered in IST. | Low | — |
| A14 | Booking durations | 30 min / 1 h / 2 h selectable, plus admin-configurable `MinBookingMinutes` / `MaxBookingMinutes` / `SlotGranularityMinutes` settings. | Low | Week 1 |
| A15 | Cancellation / refund policy | Configurable `CancellationCutoffHours` (default 24) and `RefundPercentBeforeCutoff` (default 100) / `RefundPercentAfterCutoff` (default 0). Refunds credit the **Personal Wallet**. | Medium | Week 1 |
| A16 | Wallet withdrawal | **Non-withdrawable** (Doc-1 §22 states this explicitly). | Low | — |

---

## 7. Scope position for the 15-day plan

**In scope (must be complete and production-ready):**

Foundation · Authentication & OTP · Dynamic RBAC + menus + permissions · Locations ·
Workspaces (Desk/Cabin/Meeting Room) · **Central availability & booking engine** ·
Central calendar (day/week/month, block/unblock, admin booking) · Payments (Razorpay
adapter + wallet) · Wallet · Invoices with GST PDF · Day Pass · Monthly Pass ·
QR / access tokens · Access logs + manual override · Check-in/check-out & attendance ·
Housekeeping scheduling **with calendar blocking** · Inventory In/Out/Damage ledger ·
Notifications · Support tickets · Coffee/service catalogue · Banners · Settings ·
Reports (16) with Excel/PDF export · Audit trail · Admin portal · Customer portal.

**Overflow — will not fit 15 days at this team size; scheduled first in the follow-on sprint:**

| Item | Why it is overflow | Interim position |
|------|--------------------|------------------|
| Real access-control device integration | Vendor API not supplied (A01) | Adapter + mock device console; swap when vendor delivers |
| Real ZOAPI integration | Vendor API not supplied (A02) | Adapter stub; Regalis calendar authoritative |
| Direct payroll integration | Product unknown (A03) | Export file |
| Regalis Now packages (credit bundles) | Large sub-system: credits, redemption, expiry | Day Pass + Monthly Pass ship; packages deferred |
| Full F&B ordering | Doc-1 §26 explicitly excludes it | Coffee/service catalogue only |
| Web push notifications | Requires VAPID + service-worker push infra | In-app notification centre ships; push is a Phase-2 switch |

**Explicitly out of Phase 1** (per the Out of Scope sheet, unchanged): multiple live
locations, parking, visitor management, corporate accounts, membership
freeze/proration/carry-forward/overage, predictive analytics, native apps.

---

## 8. Existing code

`~/source/repos` contained no prior Regalis application code at analysis time. This is
therefore a greenfield build; the master prompt's "preserve and improve existing code"
rule has nothing to preserve. Every subsequent change must respect it.

---

## 9. Exit criteria for this analysis

- [x] All 9 supplied documents parsed
- [x] Conflicts between master prompt and documents identified and decided
- [x] Complete entity inventory derived
- [x] 16 assumptions documented with owners and confirm-by dates
- [x] Scope split into in-scope / overflow / out-of-scope
- [x] No development started before this document existed
