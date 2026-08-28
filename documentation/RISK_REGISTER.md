# RISK_REGISTER.md
**Regalis WorkSpace — Phase 1**

Severity = Likelihood × Impact. **H** high · **M** medium · **L** low.
Status is updated at the Friday stage gate.

| # | Risk | Like. | Impact | Sev | Mitigation | Trigger to act | Owner |
|---|------|-------|--------|-----|-----------|----------------|-------|
| R01 | **Concurrent double-booking of the same slot** — two customers confirm the same workspace/time | M | **H** | **H** | Serializable transaction + `UPDLOCK, HOLDLOCK` range lock + filtered unique index + deadlock retry. Automated 20-thread concurrency test in CI. Frontend checks treated as advisory only. | Any test run where two `Confirmed` rows overlap | Senior |
| R02 | **15 days is aggressive for 91 screens with 2 juniors** | **H** | **H** | **H** | 12+3 split with float on Days 4/7/10; incremental releases so value lands early; de-scope ladder agreed up front (Reports → generic grid; packages → Phase 2) | Day 6 burndown below 45 % | Senior / PL |
| R03 | **Access-control vendor API not supplied** | **H** | M | **H** | `IAccessControlAdapter` + mock. No vendor logic in booking. MVP never blocked. | Vendor docs absent on Day 4 | Regalis Ops |
| R04 | **ZOAPI API not supplied** | **H** | L | M | `IZoapiAdapter` no-op. Regalis calendar remains the source of truth. | Same | Regalis Ops |
| R05 | **Payroll system unknown** | **H** | L | M | Export-file hand-off through `IPayrollExporter`. No direct integration attempted. | Confirmation absent on Day 7 | Regalis Ops |
| R06 | **Razorpay credentials late** | M | **H** | **H** | `SandboxPaymentGateway` completes every flow end to end without real keys; swapping is configuration only | Keys absent on Day 4 | Regalis Ops |
| R07 | **GST/invoice rules wrong** (GSTIN, place of supply, numbering, CGST/SGST vs IGST) | M | **H** | **H** | All tax values in `MST_AppSetting`; invoice snapshots company + customer at issue; a change is data, not code | Finance review not done by Day 6 | Junior 2 / Finance |
| R08 | **Scope creep from the two conflicting requirement documents** | **H** | M | **H** | Doc-2 declared governing baseline; out-of-scope register published; written change request with schedule impact required | Any request outside `PROJECT_ANALYSIS.md` §7 | PL |
| R09 | **Junior code quality / inconsistent patterns** | **H** | M | **H** | Mandatory PR review by the Senior against the §25 checklist; shared component library built on Day 2 before feature work; no direct merge | Second PR rejected for the same reason | Senior |
| R10 | **Authorization enforced only in the UI** | M | **H** | **H** | `[HasPermission]` on every action; QA executes the role matrix with direct API calls carrying a valid token and the wrong permission | Any endpoint found without an attribute | Senior / QA |
| R11 | **Secrets committed to git** | M | **H** | **H** | `appsettings.json` ships with empty placeholders; env vars / user-secrets everywhere; `.gitignore` covers `.env*`; pre-commit secret scan in CI | Any scan hit | DevOps/Senior |
| R12 | **Performance collapse on calendar and availability queries** | M | M | M | Covering indexes on `(WorkspaceId, StartUtc, EndUtc)`; server-side pagination everywhere; no N+1 (explicit `Include`/projection); seeded 10 k-booking dataset used for the Day 11 performance pass | Any list endpoint > 800 ms on the seeded set | Junior 2 / Senior |
| R13 | **Attendance data exposed to the wrong users** | L | **H** | M | `ATTENDANCE.VIEW` permission + service-layer scoping; customers see only their own rows | Any cross-user read in QA | Senior |
| R14 | **Access-control outage turns into unrestricted entry** | L | **H** | M | Device error never grants. Only `ACCESS.OVERRIDE` grants, with a mandatory reason and an exception log row. | Any code path granting on `DeviceError` | Senior |
| R15 | **Refund / cancellation policy undefined** | M | M | M | Configurable cutoff and refund percentages, defaulted and documented (A15); refunds credit the wallet | Policy unconfirmed by Day 6 | Regalis Ops |
| R16 | **Housekeeping blocks not reaching the customer calendar** | L | **H** | M | Schedule creation writes `TRN_WorkspaceBlock` in the same transaction; one availability code path; explicit QA case | Any schedule without a block row | Senior / QA |
| R17 | **SQL Server licensing/hosting not provisioned** | M | M | M | Developer Edition for dev/test; production edition and sizing confirmed by Day 10; scripts are DBA-executable so infra does not block code | Environment absent on Day 10 | DevOps |
| R18 | **OTP delivery channel (SMTP/SMS) unavailable** | M | M | M | `ConsoleOtpSender` for dev; SMTP configured for UAT; SMS provider is a Phase-2 adapter | Channel absent on Day 3 | DevOps |
| R19 | **Wallet ledger drifts from balance** | L | **H** | M | Balance and ledger written in one transaction; `CK_Wallet_Balance >= 0`; `BalanceAfter` recorded per row; nightly reconciliation query in `Views.sql` | Any reconciliation mismatch | Junior 2 |
| R20 | **QA starts too late** | M | **H** | **H** | QA writes cases from Day 1 and tests each module the day it lands; daily triage from Day 9; QA has a hard veto on the go-live gate | Any module completed and untested for > 1 day | QA |

## Open items requiring a Regalis decision (Week 1)

Carried from `Regalis_Phase_1_Live_Operations_Requirements` §21. Each has a working
assumption in `PROJECT_ANALYSIS.md` §6, so none of them blocks development — but each
is a change request if answered differently.

| # | Question | Assumption in force | Blocks | Needed by |
|---|----------|---------------------|--------|-----------|
| O1 | Access-control device/vendor and API/SDK | Mock adapter (A01) | Real access control | Day 4 |
| O2 | How is the QR scanned, and which controller decides? | Our API decides; device calls `/access/validate` | Real access control | Day 4 |
| O3 | Payroll software and API/import capability | Export file (A03) | Payroll integration | Day 7 |
| O4 | Attendance for employees, members, or both? | Both (A04) | — | Day 2 |
| O5 | Do housekeeping staff get application accounts? | Yes, `UserType = Staff` (A05) | Staff portal scope | Day 2 |
| O6 | Housekeeping QR, card, or both? | Both supported (A06) | — | Day 7 |
| O7 | Maintenance and housekeeping — one module or two? | One (A07) | — | Day 2 |
| O8 | Inventory: In/Out/Damage only, or suppliers/POs? | In/Out/Damage only (A08) | Supplier master | Day 2 |
| O9 | GSTIN, tax break-up, invoice numbering format | 9+9 CGST/SGST, `REG/INV/{FY}/{n}` (A09) | Invoice sign-off | Day 5 |
| O10 | Approved manual access procedure when the device is down | `ACCESS.OVERRIDE` + mandatory reason + exception log (A10) | — | Day 7 |
| O11 | Is Company Name mandatory at registration? | Optional (A11) | — | Day 3 |
| O12 | Payment gateway and credentials | Razorpay, sandbox until keys arrive (A12) | UAT payments | Day 4 |
| O13 | Cancellation and refund policy | 24 h cutoff, 100 % / 0 %, refund to wallet (A15) | — | Day 6 |
| O14 | Production hosting, SQL Server edition, domain, TLS | Not assumed | Go-live | Day 10 |
| O15 | SMTP / SMS provider for OTP and invoice email | Console in dev, SMTP in UAT (A18) | UAT | Day 3 |
