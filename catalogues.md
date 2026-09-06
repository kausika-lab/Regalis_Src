# Regalis — constant catalogues

All values below are compiled `const string` fields, extracted from the
Constant table of `Regalis.Shared.dll` and `Regalis.Application.dll`. They are
exact.

Contents:
1. Permission codes
2. App setting keys
3. Error codes
4. Role codes
5. Audit actions
6. Sequence keys
7. Access deny reasons
8. Payment methods and portals
9. The 16-report catalogue

---

## 1. Permission codes (`Regalis.Shared.Constants.PermissionCodes`)

Format is always `MODULE.ACTION`. Reference these through the constants, never
as string literals in an attribute.

| Group | Codes |
|---|---|
| `Access` | ACCESS.VIEW, ACCESS.CREATE, ACCESS.EDIT, **ACCESS.OVERRIDE**, ACCESS.EXPORT |
| `Attendance` | ATTENDANCE.VIEW, .CREATE, .EDIT, .APPROVE, .EXPORT |
| `Audit` | AUDIT.VIEW, AUDIT.EXPORT |
| `Banner` | BANNER.VIEW, .CREATE, .EDIT, .DELETE |
| `Booking` | BOOKING.VIEW, .CREATE, .EDIT, .DELETE, .APPROVE, .EXPORT, .PRINT |
| `Calendar` | CALENDAR.VIEW, .CREATE, .EDIT |
| `Customer` | CUSTOMER.VIEW, .CREATE, .EDIT, .DELETE, .EXPORT |
| `Dashboard` | DASHBOARD.VIEW |
| `Housekeeping` | HOUSEKEEPING.VIEW, .CREATE, .EDIT, .DELETE, .APPROVE |
| `Inventory` | INVENTORY.VIEW, .CREATE, .EDIT, .DELETE, .EXPORT |
| `Invoice` | INVOICE.VIEW, .CREATE, .EDIT, .EXPORT, .PRINT |
| `Location` | LOCATION.VIEW, .CREATE, .EDIT, .DELETE |
| `Menu` | MENU.VIEW, .CREATE, .EDIT, .DELETE |
| `Notification` | NOTIFICATION.VIEW, .CREATE, .EDIT, .DELETE |
| `Pass` | PASS.VIEW, .CREATE, .EDIT, .DELETE, .EXPORT |
| `Payment` | PAYMENT.VIEW, .CREATE, .EXPORT |
| `Payroll` | PAYROLL.VIEW, PAYROLL.EXPORT |
| `Permission` | PERMISSION.VIEW, PERMISSION.EDIT |
| `Report` | REPORT.VIEW, .EXPORT, .PRINT |
| `Role` | ROLE.VIEW, .CREATE, .EDIT, .DELETE |
| `Service` | SERVICE.VIEW, .CREATE, .EDIT, .DELETE |
| `Setting` | SETTING.VIEW, SETTING.EDIT |
| `Ticket` | TICKET.VIEW, .CREATE, .EDIT, .DELETE, .APPROVE, .EXPORT |
| `User` | USER.VIEW, .CREATE, .EDIT, .DELETE, .EXPORT |
| `Wallet` | WALLET.VIEW, .CREATE, .EDIT, .EXPORT |
| `Workspace` | WORKSPACE.VIEW, .CREATE, .EDIT, .DELETE |

**`ACCESS.OVERRIDE` is special** (Invariant 9 / A10): the device-down manual
fallback, a distinct, separately grantable, separately audited privilege. Do
not fold it into `ACCESS.EDIT`.

Note `SERVICE.CREATE` exists as a constant but `POST /api/services` is guarded
with `SERVICE.EDIT` — a small inconsistency in the compiled build, worth
checking against `ROLE_PERMISSION_MATRIX.md` before relying on either.

Adding a permission requires three things, not one: the constant, the
`MST_Permission` seed row, and the `MAP_RolePermission` grants in
`SeedCatalogue`.

---

## 2. App setting keys (`Regalis.Shared.Constants.AppSettingKeys`)

Format is `Group.Key`, matching `MST_AppSetting.SettingGroup` +
`SettingKey`. Read through `ISettingService`. **Never introduce a hard-coded
constant for anything on this list** (Invariant 6).

| Group | Keys |
|---|---|
| `Attendance` | AllowMultipleCheckInPerDay, AutoCheckoutHour |
| `Booking` | NumberPrefix, SlotGranularityMinutes, MinMinutes, MaxMinutes, AdvanceDays, PendingHoldMinutes |
| `Cancellation` | CutoffHours, RefundPercentBeforeCutoff, RefundPercentAfterCutoff |
| `Company` | Name, Gstin, Address, Email, Phone, LogoPath |
| `Invoice` | NumberPrefix, DueDays |
| `Security` | MaxFailedLogins, LockoutMinutes, PasswordMinLength, OtpTtlMinutes, OtpMaxAttempts |
| `Support` | ContactNumber, Hours, Message, Email |
| `Tax` | CgstPercent, SgstPercent, IgstPercent, PlaceOfSupply |
| `Wallet` | MinRecharge, MaxRecharge, Enabled, Withdrawable |

Rows carry `DataType`, `IsSecret` and `IsEditableByAdmin`.
`SettingAdminService` masks secrets on read, refuses to write them, validates
against `DataType`, and evicts the read cache so changes apply immediately.

`Booking.PendingHoldMinutes` defaults to 10 and is what `BookingHoldSweeper`
enforces. `Cancellation.*` drives the refund percent credited to the wallet.
`Company.*` and `Tax.*` are what get snapshotted onto an invoice.

---

## 3. Error codes (`Regalis.Shared.Constants.ErrorCodes`)

```
VALIDATION_ERROR              AUTHENTICATION_FAILED     TOKEN_EXPIRED
TOKEN_REVOKED                 ACCOUNT_LOCKED            ACCOUNT_INACTIVE
AUTHORIZATION_FAILED          NOT_FOUND                 SLOT_UNAVAILABLE
INSUFFICIENT_WALLET_BALANCE   PAYMENT_VERIFICATION_FAILED
BUSINESS_RULE_VIOLATION       DUPLICATE_RECORD          OTP_INVALID
OTP_EXPIRED                   OTP_MAX_ATTEMPTS          RATE_LIMIT_EXCEEDED
FILE_TYPE_NOT_ALLOWED         FILE_TOO_LARGE            INTERNAL_ERROR
```

The SPA branches on these via `ApiClientError.hasCode(code)`.
**`SLOT_UNAVAILABLE` (409) is expected, not exceptional** — the frontend
re-renders availability instead of showing a generic error.

---

## 4. Role codes (`Regalis.Shared.Constants.RoleCodes`)

`CUSTOMER`, `ADMIN`, `MANAGER`, `FINANCE`, `SUPPORT`, `HOUSEKEEPING`

Roles carry a `PortalType` (Customer / Admin / Both) and `IsSystemRole`.
A Customer user cannot be assigned an admin-portal role, and vice versa —
enforced by `UserAdminService.ValidateRoleAssignmentAsync`.

`HOUSEKEEPING` is the row-scoped case to remember: per the matrix, housekeeping
staff "see only their own schedule" — hence
`GET /api/housekeeping/my-schedules` alongside the full grid.

---

## 5. Audit actions (`Regalis.Shared.Constants.AuditActions`)

```
LOGIN     LOGOUT    CREATE    UPDATE    DELETE    APPROVE   REJECT
EXPORT    IMPORT    PASSWORD_CHANGE     ACCESS_OVERRIDE     AUTHORIZATION_FAILED
```

`TRN_AuditLog` rows come from two places: the save-changes interceptor
(automatic, for auditable entities) and explicit exception paths
(`ACCESS_OVERRIDE`, `AUTHORIZATION_FAILED`, `PASSWORD_CHANGE`). It is
read-only everywhere — `AuditService` only ever queries it.

---

## 6. Sequence keys (`Regalis.Application.Abstractions.SequenceKeys`)

```
Booking   Invoice   Payment   WalletTransaction   CustomerPass
ServiceOrder   Ticket   HousekeepingSchedule   InventoryTransaction
```

Seeded in `MST_NumberSequence` with `Prefix` and `FinancialYear`. Call
`INumberSequenceService` **inside** the business transaction.
Observed formats: `REG-000123` (booking),
`REG/INV/2026-27/00042` (invoice — note the Indian financial year).

---

## 7. Access deny reasons (`Regalis.Application.Access.DenyReasons`)

```
TokenNotFound   TokenRevoked   OutsideValidWindow   BookingCancelled
WrongWorkspace  AlreadyCheckedOut   DeviceOffline    UsageExceeded
```

Written to `TRN_AccessLog.DenyReason` (`DATABASE_DESIGN §7`).
`POST /api/access/validate` **returns 200 whether it grants or denies** — the
decision is in the response body, and a log row is always written.

`AccessResult` is `Granted=1, Denied=2, DeviceError=3`. `DeviceError` is never
a grant. To test that path, use a `deviceId` starting with `OFFLINE` against
`MockAccessControlAdapter`.

---

## 8. Payment methods and portals

`Regalis.Application.Bookings.PaymentMethods` — the string values accepted at
confirmation: `wallet`, `gateway`, `cash`. **`cash` is admin-only** — cash
collected at the desk.

`Regalis.Application.Auth.LoginPortal`: `Customer=1`, `Admin=2`. The portal
constrains user type: customer → Customer, admin → Admin/Staff.

`Regalis.Domain.Enums.PaymentMethod` (stored on `TRN_Payment`) is the wider
set: Wallet, Upi, CreditCard, DebitCard, NetBanking, Cash, Other.

---

## 9. The 16-report catalogue

Recovered report titles. `GET /api/reports` returns the catalogue with
not-yet-built keys flagged unavailable; `GET /api/reports/{key}` returns the
tabular payload; `GET /api/reports/{key}/export?format=xlsx|pdf` exports it.

```
Booking Report              Desk Booking Report
Cabin Booking Report        Meeting Room Report
Occupancy Report            Revenue Report
Payment Report              Invoice Report
Wallet Report               Day Pass Report
Monthly Pass Report         QR Access Report
Check-in/out Report         Customer Report
Services & Coffee Report    Support Ticket Report
```

Observed kebab-case key literals include `day-pass`, `monthly-pass`,
`meeting-room`, `qr-access`, `check-in-out` — keys follow the title in
kebab-case. Doc comments note reports 1–8 shipped first, with the remainder
catalogued but unavailable until later; check `GetCatalogue` output against
the current build rather than assuming all 16 are live.

Adding a report means adding a key + column set to `ReportService`. The grid,
Excel exporter and PDF exporter need **no** changes — that is the whole point
of the single tabular shape.
