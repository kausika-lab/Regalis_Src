# API_PLAN.md
**Regalis WorkSpace REST API — v1**

Base: `/api` · Auth: `Authorization: Bearer <jwt>` · Content: `application/json`
Every response is an `ApiResponse<T>` envelope. Every list endpoint is server-side
paged, sorted, filtered and searchable.

---

## 1. Conventions

### Query contract for every list endpoint
```
?page=1&pageSize=20&sortBy=createdDate&sortDir=desc&search=REG-000123
&from=2026-08-01&to=2026-08-31&status=2&locationId=1
```
`pageSize` is capped at 100. `PagedResult<T>` returns
`{ items, page, pageSize, totalCount, totalPages, hasNext, hasPrevious }`.

### Status codes
`200` ok · `201` created · `204` no content · `400` validation · `401` unauthenticated ·
`403` permission denied · `404` not found · `409` conflict (slot already taken) ·
`422` business rule violated · `429` rate limited · `500` unexpected.

### Error codes
`VALIDATION_ERROR` `AUTHENTICATION_FAILED` `TOKEN_EXPIRED` `TOKEN_REVOKED`
`ACCOUNT_LOCKED` `ACCOUNT_INACTIVE` `AUTHORIZATION_FAILED` `NOT_FOUND`
`SLOT_UNAVAILABLE` `INSUFFICIENT_WALLET_BALANCE` `PAYMENT_VERIFICATION_FAILED`
`BUSINESS_RULE_VIOLATION` `DUPLICATE_RECORD` `OTP_INVALID` `OTP_EXPIRED`
`OTP_MAX_ATTEMPTS` `RATE_LIMIT_EXCEEDED` `FILE_TYPE_NOT_ALLOWED` `FILE_TOO_LARGE`
`INTERNAL_ERROR`

---

## 2. Authentication — `/api/auth` (anonymous unless noted)

| Method | Route | Permission | Purpose |
|--------|-------|-----------|---------|
| POST | `/auth/login` | – | Customer or admin login. Body `{ email, password, portal }`. Returns access + refresh token, user, roles, permissions. |
| POST | `/auth/register` | – | Customer self-registration. Creates a `PendingVerification` user and sends an OTP. |
| POST | `/auth/verify-otp` | – | `{ destination, purpose, code }`. Activates the account or issues a reset token. |
| POST | `/auth/resend-otp` | – | 60-second cooldown, rate limited. |
| POST | `/auth/forgot-password` | – | Sends an OTP to the registered email/mobile. Always returns 200 (no account enumeration). |
| POST | `/auth/reset-password` | – | `{ resetToken, newPassword, confirmPassword }`. |
| POST | `/auth/change-password` | authenticated | `{ currentPassword, newPassword }`. Revokes all refresh tokens. |
| POST | `/auth/refresh` | – | Rotates the refresh token. Reuse revokes the family. |
| POST | `/auth/logout` | authenticated | Revokes the presented refresh token. |
| GET | `/auth/me` | authenticated | Current user, roles, permissions. |

## 3. Navigation & authorisation

| Method | Route | Permission |
|--------|-------|-----------|
| GET | `/menus/my` | authenticated — the sidebar tree for the caller |
| GET/POST/PUT/DELETE | `/menus` | `MENU.VIEW/CREATE/EDIT/DELETE` |
| GET/POST/PUT/DELETE | `/roles` | `ROLE.*` |
| GET | `/roles/{id}/permissions` · PUT `/roles/{id}/permissions` | `PERMISSION.VIEW` / `PERMISSION.EDIT` |
| GET | `/roles/{id}/menus` · PUT `/roles/{id}/menus` | `ROLE.VIEW` / `ROLE.EDIT` |
| GET | `/permissions` | `PERMISSION.VIEW` |

## 4. Users & customers

| Method | Route | Permission |
|--------|-------|-----------|
| GET | `/users` `/users/{id}` | `USER.VIEW` |
| POST | `/users` | `USER.CREATE` |
| PUT | `/users/{id}` | `USER.EDIT` |
| PATCH | `/users/{id}/activate` · `/deactivate` · `/lock` · `/unlock` | `USER.EDIT` |
| POST | `/users/{id}/reset-password` | `USER.EDIT` |
| GET | `/users/{id}/audit` | `AUDIT.VIEW` |
| DELETE | `/users/{id}` | `USER.DELETE` (soft) |
| GET | `/customers` `/customers/{id}` `/customers/{id}/summary` | `CUSTOMER.VIEW` |
| GET | `/customers/export` | `CUSTOMER.EXPORT` |
| GET/PUT | `/profile` | authenticated (own record only) |
| POST | `/profile/photo` | authenticated |

## 5. Masters

| Route | Permissions |
|-------|-------------|
| `/locations` CRUD | `LOCATION.*` (`GET` open to authenticated) |
| `/workspace-types` CRUD | `WORKSPACE.*` |
| `/workspaces` CRUD | `WORKSPACE.*` |
| `PATCH /workspaces/{id}/status` | `WORKSPACE.EDIT` |
| `/amenities` CRUD | `WORKSPACE.*` |
| `/services` CRUD | `SERVICE.*` |
| `/banners` CRUD | `BANNER.*` |
| `GET /banners/active` | authenticated |
| `/settings` GET/PUT | `SETTING.VIEW` / `SETTING.EDIT` |
| `GET /settings/public` | anonymous — support number, hours, company name only |
| `/ticket-categories`, `/ticket-categories/{id}/issue-types` | `TICKET.VIEW` |

## 6. Availability, calendar and booking — the core

| Method | Route | Permission | Notes |
|--------|-------|-----------|-------|
| GET | `/availability/slots` | `BOOKING.VIEW` | `?locationId&workspaceTypeId&date` → time slots with `isAvailable` |
| GET | `/availability/workspaces` | `BOOKING.VIEW` | `?locationId&workspaceTypeId&startUtc&durationMinutes` → free workspaces + price |
| POST | `/availability/check` | `BOOKING.VIEW` | `{ workspaceId, startUtc, endUtc }` → `{ isAvailable, conflictReason }` |
| GET | `/calendar` | `CALENDAR.VIEW` | `?view=day\|week\|month&date&locationId&workspaceTypeId&workspaceId` → the single calendar grid used by **both** portals |
| GET | `/calendar/workspace/{id}` | `CALENDAR.VIEW` | one workspace's timeline |
| POST | `/calendar/block` | `CALENDAR.CREATE` | `{ workspaceId, startUtc, endUtc, blockReason, reasonNotes }` |
| POST | `/calendar/unblock/{blockId}` | `CALENDAR.EDIT` | releases, keeps the row |
| POST | `/bookings/quote` | `BOOKING.VIEW` | price + tax breakdown before payment; no write |
| POST | `/bookings` | `BOOKING.CREATE` | creates a `Pending` hold, returns a payment intent |
| POST | `/bookings/{id}/confirm` | `BOOKING.CREATE` | **the atomic transaction** — verify payment → Confirmed + invoice + QR + notification |
| GET | `/bookings` | `BOOKING.VIEW` | customers get their own rows only |
| GET | `/bookings/{id}` | `BOOKING.VIEW` | |
| GET | `/bookings/my?tab=upcoming\|completed\|cancelled` | `BOOKING.VIEW` | |
| PUT | `/bookings/{id}` | `BOOKING.EDIT` | reschedule; re-runs the availability check |
| POST | `/bookings/{id}/cancel` | `BOOKING.EDIT` | status change + refund per policy. **Never deletes.** |
| POST | `/bookings/{id}/extend` | `BOOKING.EDIT` | availability-checked |
| POST | `/bookings/admin` | `BOOKING.CREATE` | admin books on a customer's behalf |
| GET | `/bookings/{id}/qr` | `BOOKING.VIEW` | PNG/base64 QR |
| GET | `/bookings/{id}/invoice` | `INVOICE.PRINT` | PDF |
| GET | `/bookings/export` | `BOOKING.EXPORT` | xlsx/pdf |

**`409 SLOT_UNAVAILABLE`** is the documented, expected outcome of losing a race. The
frontend catches it and re-renders availability rather than showing a generic error.

## 7. Payments, wallet, invoices

| Method | Route | Permission |
|--------|-------|-----------|
| POST | `/payments/order` | `PAYMENT.CREATE` — creates the gateway order |
| POST | `/payments/verify` | `PAYMENT.CREATE` — **server-side HMAC signature verification** |
| POST | `/payments/webhook` | anonymous, signature-verified, idempotent on `GatewayPaymentId` |
| GET | `/payments` `/payments/{id}` | `PAYMENT.VIEW` |
| GET | `/payments/export` | `PAYMENT.EXPORT` |
| GET | `/wallet/me` | `WALLET.VIEW` |
| GET | `/wallet/me/transactions` | `WALLET.VIEW` — `?month&category` |
| POST | `/wallet/recharge` | `WALLET.CREATE` |
| GET | `/wallet/{customerId}` | `WALLET.VIEW` (admin) |
| POST | `/wallet/{customerId}/adjust` | `WALLET.EDIT` — credit/debit with mandatory reason, fully audited |
| GET | `/invoices` `/invoices/{id}` | `INVOICE.VIEW` |
| GET | `/invoices/my?status=pending\|paid` | `INVOICE.VIEW` |
| GET | `/invoices/{id}/pdf` | `INVOICE.PRINT` |
| POST | `/invoices/{id}/pay` | `INVOICE.EDIT` |
| POST | `/invoices/manual` | `INVOICE.CREATE` |
| POST | `/invoices/{id}/cancel` | `INVOICE.EDIT` |
| POST | `/invoices/{id}/resend` | `INVOICE.EDIT` |

## 8. Passes

| Method | Route | Permission |
|--------|-------|-----------|
| GET/POST/PUT/DELETE | `/pass-plans` | `PASS.*` |
| GET | `/pass-plans/active?type=day\|monthly` | authenticated |
| POST | `/passes/purchase` | `PASS.CREATE` |
| GET | `/passes/my` | `PASS.VIEW` |
| GET | `/passes` | `PASS.VIEW` (admin) |
| POST | `/passes/{id}/cancel` | `PASS.EDIT` |

## 9. QR, access control, attendance

| Method | Route | Permission |
|--------|-------|-----------|
| GET | `/access/my-tokens` | `ACCESS.VIEW` |
| GET | `/access/token/{bookingId}/qr` | `ACCESS.VIEW` |
| POST | `/access/validate` | `ACCESS.CREATE` — the device/reader endpoint. `{ tokenValue, workspaceId, direction, deviceId }` → grant/deny + always writes `TRN_AccessLog` |
| POST | `/access/manual-override` | **`ACCESS.OVERRIDE`** — the device-down fallback. Mandatory reason. Writes an exception log row. |
| POST | `/access/revoke/{tokenId}` | `ACCESS.EDIT` |
| GET | `/access/logs` | `ACCESS.VIEW` |
| GET | `/access/logs/export` | `ACCESS.EXPORT` |
| POST | `/attendance/check-in` | authenticated — My Work Status |
| POST | `/attendance/check-out` | authenticated |
| GET | `/attendance/my` | authenticated |
| GET | `/attendance` | `ATTENDANCE.VIEW` — `?date&userId&locationId` |
| PUT | `/attendance/{id}` | `ATTENDANCE.EDIT` — correction, keeps original values |
| POST | `/attendance/{id}/finalize` | `ATTENDANCE.APPROVE` |
| GET | `/attendance/export` | `ATTENDANCE.EXPORT` |
| GET | `/payroll/export` | `PAYROLL.EXPORT` — CSV/XLSX hand-off |

## 10. Operations

| Method | Route | Permission |
|--------|-------|-----------|
| GET/POST/PUT/DELETE | `/inventory/items` | `INVENTORY.*` |
| GET | `/inventory/stock` | `INVENTORY.VIEW` — derived stock + reorder flag |
| POST | `/inventory/transactions` | `INVENTORY.CREATE` — In / Out / Damage / Adjustment |
| GET | `/inventory/transactions` | `INVENTORY.VIEW` — the audit ledger |
| GET | `/inventory/export` | `INVENTORY.EXPORT` |
| GET/POST/PUT | `/housekeeping/schedules` | `HOUSEKEEPING.*` |
| POST | `/housekeeping/schedules/{id}/cancel` | `HOUSEKEEPING.EDIT` — releases the calendar block |
| POST | `/housekeeping/schedules/{id}/check-in` · `/check-out` | `HOUSEKEEPING.EDIT` |
| GET | `/housekeeping/my-schedules` | `HOUSEKEEPING.VIEW` (staff, own rows) |
| GET/POST | `/service-orders` | `SERVICE.VIEW` / `SERVICE.CREATE` |

## 11. Notifications & support

| Method | Route | Permission |
|--------|-------|-----------|
| GET | `/notifications/my` | authenticated — `?category&isRead` |
| GET | `/notifications/my/unread-count` | authenticated |
| PATCH | `/notifications/{id}/read` · `POST /notifications/read-all` | authenticated |
| DELETE | `/notifications/{id}` | authenticated (own) |
| POST | `/notifications/broadcast` | `NOTIFICATION.CREATE` |
| GET | `/notifications` | `NOTIFICATION.VIEW` |
| POST | `/tickets` | `TICKET.CREATE` |
| GET | `/tickets/my` `/tickets` `/tickets/{id}` | `TICKET.VIEW` |
| POST | `/tickets/{id}/messages` | `TICKET.EDIT` |
| POST | `/tickets/{id}/attachments` | `TICKET.EDIT` — multipart, ≤10 MB, `.png .jpg .jpeg .webp .pdf` |
| PATCH | `/tickets/{id}/assign` · `/status` · `/priority` | `TICKET.EDIT` |
| POST | `/tickets/{id}/close` · `/reopen` | `TICKET.EDIT` |
| GET | `/tickets/export` | `TICKET.EXPORT` |

## 12. Reports — `/api/reports`

`GET /reports/{key}` with the common filter set, plus
`GET /reports/{key}/export?format=xlsx|pdf` guarded by `REPORT.EXPORT`.

Keys: `booking` `revenue` `customer` `occupancy` `desk` `cabin` `meeting-room`
`day-pass` `monthly-pass` `payment` `invoice` `wallet` `coffee` `ticket`
`check-in-out` `qr-access`

Common filters: `from` `to` `locationId` `workspaceTypeId` `workspaceId` `customerId`
`bookingStatus` `paymentStatus`.

`GET /dashboard/admin` and `GET /dashboard/customer` return the summary cards in one
round trip (`usp_GetDashboardSummary`).

## 13. Audit & health

| Method | Route | Permission |
|--------|-------|-----------|
| GET | `/audit` | `AUDIT.VIEW` — `?userId&module&action&entityName&from&to` |
| GET | `/audit/export` | `AUDIT.EXPORT` |
| GET | `/health` `/health/ready` | anonymous |

---

## 14. Swagger

Swashbuckle is configured with: JWT bearer security definition, XML comments from all
projects (`GenerateDocumentationFile`), `[ProducesResponseType]` on every action,
request/response examples, grouped tags per module, and the permission required by each
endpoint written into its description. Swagger UI is enabled in Development, Test and
UAT; **disabled in Production** unless `Swagger:EnableInProduction` is explicitly true.

## 15. Rate limits

| Policy | Applies to | Limit |
|--------|-----------|-------|
| `auth` | `/auth/login`, `/auth/register`, `/auth/forgot-password`, `/auth/resend-otp` | 5 requests / minute / IP |
| `write` | all POST/PUT/PATCH/DELETE | 60 / minute / user |
| `read` | everything else | 300 / minute / user |
| `webhook` | `/payments/webhook` | 120 / minute / IP |
