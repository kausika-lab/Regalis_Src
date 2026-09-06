# Regalis — architecture reference

Contents:
1. Recovered folder layout
2. Request pipeline and middleware
3. Authentication and tokens
4. Permission authorization
5. Rate limiting
6. Vendor adapters (the configuration seams)
7. Background jobs
8. Persistence details
9. Reporting and PDF
10. Deployment topology
11. Configuration surface
12. Migrations

Folder layout is recovered from PDB document tables, so file names are exact.
Interfaces with no method bodies produce no sequence points and therefore do
not appear in a PDB — where a file is listed as `<Module>Dtos.cs`, the module's
service interface is most likely declared in that same file or a sibling.

---

## 1. Recovered folder layout

### Regalis.Domain
```
Common/          AuditableEntity.cs, IAuditableEntity, ISoftDeletableEntity
Entities/
  Access/        AccessLog.cs  AccessToken.cs  Attendance.cs
  Booking/       Booking.cs  BookingStatusHistory.cs  WorkspaceBlock.cs
  Finance/       CustomerPass.cs  Invoice.cs  InvoiceLine.cs  Payment.cs
                 ServiceOrder.cs  ServiceOrderLine.cs  Wallet.cs
                 WalletTransaction.cs
  Identity/      AuditLog.cs  LoginHistory.cs  Menu.cs  Otp.cs
                 PasswordHistory.cs  Permission.cs  RefreshToken.cs  Role.cs
                 RoleMenu.cs  RolePermission.cs  User.cs  UserRole.cs
  Masters/       Amenity.cs  AppSetting.cs  Banner.cs  Location.cs
                 NumberSequence.cs  PassPlan.cs  ServiceItem.cs
                 TicketCategory.cs  TicketIssueType.cs  Workspace.cs
                 WorkspaceAmenity.cs  WorkspaceType.cs
  Operations/    HousekeepingSchedule.cs  InventoryItem.cs
                 InventoryTransaction.cs  Notification.cs  Ticket.cs
                 TicketAttachment.cs  TicketMessage.cs
Enums/           (35 enums, all 1-based)
```

### Regalis.Shared
```
IstClock.cs                     ToIst, ToUtc, IstDateOf
Api/    ApiResponse.cs (+ ApiResponse<T>), ApiError, PagedRequest, PagedResult<T>
Constants/  PermissionCodes.cs  AppSettingKeys.cs  ErrorCodes.cs
            RoleCodes.cs  AuditActions.cs
```

`PagedRequest`: `Page`, `PageSize` (default 20, max 100), `Search`, `SortBy`,
`SortDir` (`asc`/`desc`), derived `IsDescending`.
`PagedResult<T>`: `Items`, `Page`, `PageSize`, `TotalCount`, derived
`TotalPages`, `HasNext`, `HasPrevious`, plus a static `Create`.

### Regalis.Application
```
Abstractions/    IAccessControlAdapter.cs  ICurrentUser.cs  IDateTimeProvider.cs
                 INumberSequenceService.cs  IOtpSender.cs  IPasswordHasher.cs
                 IPaymentGateway.cs  IPayrollExporter.cs  ISettingService.cs
                 ITokenService.cs
Access/          AccessDtos.cs  AttendanceDtos.cs
Administration/  AuditDtos.cs  MenuAdminDtos.cs  RoleAdminDtos.cs
                 UserAdminDtos.cs
Auth/            AuthDtos.cs  AuthValidators.cs  IAuthService.cs
                 IMenuQueryService.cs  ProfileDtos.cs
Bookings/        BookingConfirmDtos.cs  BookingDtos.cs  BookingMutationDtos.cs
                 CalendarDtos.cs
Common/Exceptions/  AppExceptions.cs
Finance/         InvoiceDtos.cs  PassDtos.cs  PaymentDtos.cs
                 WalletAdminDtos.cs  WalletDtos.cs
Masters/         CatalogueDtos.cs  LocationDtos.cs  WorkspaceDtos.cs
Operations/      HousekeepingDtos.cs  InventoryDtos.cs  NotificationDtos.cs
                 TicketDtos.cs
Reporting/       DashboardDtos.cs  ReportDtos.cs
DependencyInjection.cs
```

`AddApplication()` registers application services **and every
FluentValidation validator in the assembly** by scan — a new validator needs
no registration.

### Regalis.Infrastructure
```
Adapters/        ConsoleOtpSender.cs  CsvPayrollExporter.cs
                 MockAccessControlAdapter.cs  RazorpayPaymentGateway.cs
                 SandboxPaymentGateway.cs  SmtpOtpSender.cs
BackgroundJobs/  BookingHoldSweeper.cs
Persistence/
  RegalisDbContext.cs
  Configurations/  AccessConfigurations.cs  BookingConfigurations.cs
                   FinanceConfigurations.cs  IdentityConfigurations.cs
                   MastersConfigurations.cs  OperationsConfigurations.cs
  Interceptors/    AuditSaveChangesInterceptor.cs
  Migrations/      InitialCreate, 20260828210450_AddPasswordHistory,
                   20260829180229_SignedAdjustmentQuantity,
                   RegalisDbContextModelSnapshot.cs
  Seed/            DatabaseSeeder.cs  SeedCatalogue.cs
QueryableExtensions.cs
Reporting/       InvoicePdfGenerator.cs  ReportExporters.cs  ReportService.cs
                 (ReportCellFormatter, ReportExcelExporter, ReportPdfGenerator)
Security/        JwtOptions.cs  JwtTokenService.cs  PasswordHasher.cs
                 QrPayloadSigner.cs
Services/        AccessService.cs  AttendanceService.cs  AttendanceRecorder
                 AuditService.cs  AuthService.cs  AvailabilityService.cs
                 BannerService.cs  BookingEngineLocks.cs
                 BookingMutationService.cs  BookingService.cs
                 CalendarService.cs  DashboardService.cs  FileTypeSniffer.cs
                 HousekeepingService.cs  InventoryService.cs
                 InvoiceService.cs  LocationService.cs  MenuAdminService.cs
                 MenuQueryService.cs  NotificationService.cs
                 NumberSequenceService.cs  PassService.cs  PaymentService.cs
                 ProfileService.cs  RoleAdminService.cs
                 ServiceCatalogueService.cs  SettingAdminService.cs
                 SettingService.cs  SystemDateTimeProvider.cs
                 TicketService.cs  UserAdminService.cs  WalletAdminService.cs
                 WalletService.cs  WorkspaceService.cs
                 (StorageOptions)
DependencyInjection.cs
```

Note `QueryableExtensions.cs`: EF Core 8 has no `ToHashSetAsync` (it arrives in
EF 9); this fills the gap and is in scope everywhere under
`Regalis.Infrastructure`.

### Regalis.API
```
Authorization/   HasPermissionAttribute.cs  PermissionAuthorizationHandler.cs
                 PermissionPolicyProvider.cs  PermissionRequirement.cs
Controllers/     AccessController  ApiControllerBase  AttendanceController
                 AuditController  AuthController  AvailabilityController
                 BannersController  BookingsController  CalendarController
                 CustomersController  DashboardController
                 HousekeepingController  InventoryController
                 InvoicesController  LocationsController  MenusController
                 NotificationsController  PassesController  PaymentsController
                 ProfileController  ReportsController  RolesController
                 ServicesController  SettingsController  TicketsController
                 UsersController  WalletController  WorkspacesController
                 (+ PayrollController, PassPlansController,
                    TicketCategoriesController, WorkspaceTypesController,
                    AmenitiesController, PermissionsController)
Filters/         ValidationActionFilter.cs
Middleware/      CorrelationIdMiddleware.cs  ExceptionHandlingMiddleware.cs
                 RequestLoggingMiddleware.cs  SecurityHeadersMiddleware.cs
Services/        CurrentUser.cs  FrontendDevLauncher.cs
                 UtcDateTimeConverter.cs
Program.cs
```

`FrontendDevLauncher` starts the Vite dev server from the API in development.
`UtcDateTimeConverter` is a JSON converter — it is why the SPA can rely on
timestamps being UTC-marked.

---

## 2. Request pipeline and middleware

Four custom middlewares, in this conceptual order:

1. `CorrelationIdMiddleware` — assigns/propagates the correlation id that
   appears in every log line (`[{CorrelationId}]`) and in every
   `ApiResponse.CorrelationId`. This is the primary debugging handle: given a
   user-reported failure, grep the log for its correlation id.
2. `SecurityHeadersMiddleware`
3. `RequestLoggingMiddleware` — emits
   `HTTP {verb} {path} responded {status} in {ms} ms (user: {id|anonymous})`.
4. `ExceptionHandlingMiddleware` — maps the `AppException` hierarchy to status
   codes and the error envelope; logs handled exceptions at `WRN` as
   `Handled {ExceptionType} for {verb} {path}: {message}`.

`ValidationActionFilter` turns model/FluentValidation failures into
`AppValidationException` (400 with field-level errors) before the action runs.

Program.cs also wires: CORS (`Cors:AllowedOrigins`), JWT bearer auth,
`AddRateLimiter` with fixed-window partitioned limiters, Swagger (gated by
`Swagger:EnableInProduction`), health checks (including an EF Core check), and
Serilog.

---

## 3. Authentication and tokens

Per `ARCHITECTURE §4`:

- **Passwords**: PBKDF2-HMAC-SHA256, **210 000 iterations**, 128-bit salt,
  256-bit subkey, stored as one versioned string
  `v1.{iterations}.{salt}.{subkey}` so parameters can be raised later without
  invalidating existing hashes. Verification is constant-time.
- **Password policy**: minimum length is dynamic from
  `Security.PasswordMinLength`; composition (upper + lower + digit + symbol)
  is enforced by `PasswordRules.MustBeWellComposed`. New password must not
  equal the last 3 hashes — that is what `TRN_PasswordHistory` exists for.
- **Access token**: 15-minute JWT with claims `sub`, `email`, `user_type`,
  `name`, `role` (multi), `perm` (multi), `loc`, `jti`. Roles and permissions
  are resolved from the database at login and stamped in — never hard-coded.
- **Refresh token**: 7-day, 256-bit random, opaque. Raw value returned to the
  client exactly once; only the SHA-256 hash is persisted. **Single-use and
  rotated on every refresh. Reuse of a consumed token revokes the whole
  `FamilyId`** — that is token-theft detection.
- **Lockout**: `Security.MaxFailedLogins` / `Security.LockoutMinutes`.
- **OTP**: 6 digits, hashed at rest, 10-minute TTL, max 5 attempts, 60-second
  resend cooldown. Purposes: Registration, ForgotPassword,
  MobileVerification.
- **Forgot password never enumerates accounts** — always returns 200.
- **Reset token** is short-lived and stateless; replay is blocked by comparing
  its issue time against `User.LastPasswordChangeUtc`.
- **Changing a password revokes every refresh token.**
- Every login attempt writes `TRN_LoginHistory`; sensitive actions write
  `TRN_AuditLog`.
- **Login portal constrains user type**: `customer` → Customer;
  `admin` → Admin/Staff.

### QR payloads
`QrPayloadSigner` produces `RGL1.{token}.{hmac}`. The reader posts it to
`POST /api/access/validate`, which re-verifies signature *and* token window.
A payload with a bad HMAC never reaches the database lookup. One
implementation signs booking, pass and housekeeping tokens alike.
`AccessToken.TokenValue` is cryptographically random and opaque — never a
guessable booking id.

---

## 4. Permission authorization

- `[HasPermission("MODULE.ACTION")]` on the action.
- `PermissionPolicyProvider` creates the `AuthorizationPolicy` **on demand**
  from the attribute — no policy is registered by hand. Non-permission
  policies fall back to the default provider.
- `PermissionAuthorizationHandler` grants when the JWT carries the matching
  `perm` claim. A valid token lacking the permission gets **403 plus an
  `AUTHORIZATION_FAILED` audit row**, and logs
  `Authorization failed: user {id} lacks permission {CODE}`.
- Permission codes are `MODULE.ACTION`. `MST_Permission` carries
  `ModuleName`, `ActionName`, `PermissionCode`, `DisplayOrder`.
- Menus are data: `MST_Menu` self-references four levels
  (`Module → Menu → SubMenu → Screen` = `MenuLevel` 1–4), scoped by
  `PortalType`, mapped to roles via `MAP_RoleMenu`. `GET /api/menus/my` builds
  the caller's sidebar. **Adding a screen is a data insert, not a redeploy.**
- Roles carry `PortalType` and `IsSystemRole`. `UserAdminService` enforces
  that a role matches the user's portal type — a Customer cannot receive an
  admin-portal role.
- Row-level scoping is *not* done by permissions. It is done in services via
  `ICurrentUser` (`ROLE_PERMISSION_MATRIX §6`). Housekeeping staff see only
  their own schedules; customers see only their own bookings, invoices,
  tickets, passes and wallet.

---

## 5. Rate limiting

Fixed-window partitioned limiters, configured under `RateLimiting`:

| Policy | Default | Applied to |
|---|---|---|
| `auth` | 5 / minute / IP | login, register, verify-otp, resend-otp, forgot-password, reset-password |
| `write` | 60 / minute | mutating endpoints |
| `read` | 300 / minute | read endpoints |
| `webhook` | 120 / minute | `POST /api/payments/webhook` |

Exceeding a limit surfaces `RATE_LIMIT_EXCEEDED`.

---

## 6. Vendor adapters (Invariant 5)

Every external vendor is an interface selected by configuration, so an absent
vendor never blocks development.

| Seam | Interface | Implementations | Selector |
|---|---|---|---|
| Payments | `IPaymentGateway` | `RazorpayPaymentGateway`, `SandboxPaymentGateway` | `Payments:Provider` |
| OTP | `IOtpSender` | `SmtpOtpSender`, `ConsoleOtpSender` | `Otp:Channel` (`Smtp` / `Console`) |
| Access hardware | `IAccessControlAdapter` | `MockAccessControlAdapter` | `AccessControl:Provider` |
| Payroll | `IPayrollExporter` | `CsvPayrollExporter` | `Payroll:Format` |

Important details:

- **`SandboxPaymentGateway` uses the same HMAC-SHA256 scheme as Razorpay**,
  keyed with a local secret, so the verification code path under test is
  identical to production. `POST /api/payments/sandbox-complete` exchanges an
  order for the payment id + signature the real gateway would return, and
  **answers only while `Payments:Provider = Sandbox`** (422 otherwise).
- `GatewayOrder.KeyId` is the **public** key the client checkout needs — never
  the secret. Razorpay keys come from configuration/user-secrets only.
- **`MockAccessControlAdapter` QA contract: any `deviceId` starting with
  `OFFLINE` reports the device down**, which exercises the `DeviceError` path
  end to end. Use this to test Invariant 9.
- `ConsoleOtpSender` writes the OTP to the log. Never selected in Production.
- `SmtpOtpSender` falls back to the log for SMS destinations — no SMS vendor
  exists yet. `SmtpOptions.FromAddress` defaults to `Username` when empty.
- `IAccessControlAdapter` only carries the door command and reports device
  health. **All access rules are evaluated by `AccessService` against the
  database.** Swapping in a real vendor is an adapter implementation, nothing
  more.
- `CsvPayrollExporter` exports times in **IST** because the payroll team works
  in local time; the UTC source in `TRN_Attendance` stays untouched.

---

## 7. Background jobs

`BookingHoldSweeper` — sweeps `Pending` bookings whose payment window
(`Booking.PendingHoldMinutes`, default 10) has lapsed to `Expired`, releasing
the slot. This is the code path of the planned
`usp_ExpireStaleBookingHolds` (`DATABASE_DESIGN §10`).
**`ChangedByUserId = 0` means "system".**

---

## 8. Persistence details

- `RegalisDbContext`, SQL Server, EF Core 8.
- Six configuration files, one per entity area.
- `AuditSaveChangesInterceptor` sets audit columns on insert/update, converts
  hard deletes of `ISoftDeletableEntity` into soft deletes, and writes a
  `TRN_AuditLog` row for every change to an auditable entity. **Sensitive
  fields are redacted before serialisation.** A global query filter hides
  soft-deleted rows.
- **Nothing in Phase 1 issues a hard DELETE against a business table.**
- Concurrency: `RowVersion` on `Booking`, `Workspace`, `Wallet`.
- **`UX_Booking_Workspace_Start`** is a filtered unique index — the second
  line of defence behind the range locks.
- `BookingEngineLocks.CountConflictsAsync` is *the* locking conflict check
  (`DATABASE_DESIGN §5`), shared by every path that writes occupancy: holds,
  confirmations, reschedules, extensions. It runs the documented availability
  predicate under `UPDLOCK/HOLDLOCK` inside the caller's serializable
  transaction. On confirmation, the booking's own `Pending` row is excluded.
- A retrying execution strategy replays deadlock victims (SQL error 1205).
  **See the MARS caveat in SKILL.md — this is currently compromised.**
- `NumberSequenceService` uses a single atomic
  `UPDATE … SET CurrentValue += 1 OUTPUT`; the row lock serializes concurrent
  callers.
- `MST_Wallet` has a **CHECK constraint refusing overdraft at the database
  level**, independent of the service-layer `INSUFFICIENT_WALLET_BALANCE`
  check. `WalletTransaction.BalanceAfter` is written in the same transaction
  that moves `Wallet.Balance`, so the ledger is self-checking:
  `SUM(credits) − SUM(debits) = Balance`.
- `WalletService.CreditAsync/DebitAsync` run **inside the caller's ambient
  transaction**; the caller owns `SaveChanges`/commit.
- The wallet is **non-withdrawable** (`Wallet.Withdrawable` setting,
  `Doc-1 §22`).
- `SettingService` caches `MST_AppSetting` reads briefly so hot paths (booking
  rules, security policy) don't hit the database per request.
  `SettingAdminService` **evicts that cache** on write, masks secret values on
  read, locks them on write, and validates against the row's declared
  `DataType`.
- Seeding is idempotent (`DatabaseSeeder` + `SeedCatalogue`), producing the
  state defined by `ROLE_PERMISSION_MATRIX.md` and `DATABASE_DESIGN.md`. The
  DBA route produces the same state via `Database_Seed.sql`. **Nothing in
  `SeedCatalogue` is consulted at request time — the engine reads only the
  database.**
- `Database:MigrateOnStartup` / `Database:SeedOnStartup` gate startup
  behaviour. Production runs `MigrateOnStartup: false`, `SeedOnStartup: true`.

---

## 9. Reporting and PDF

- **One tabular shape drives three renderings.** `ReportResult` carries
  `ReportColumn[]` + row dictionaries + summary items; the same payload feeds
  the grid (`ReportShell`), the Excel export (ClosedXML) and the PDF export
  (QuestPDF). A report is defined once, rendered three ways.
- `ReportColumn.Type` drives both frontend rendering and export formatting:
  `string | number | money | percent | date | datetime`.
- `ReportCellFormatter` is shared so grid, Excel and PDF show the same value
  for the same cell (UTC → IST at the edge).
- `ReportFilterRequest` is the common filter set every report accepts; a
  report simply ignores filters that do not apply.
- `GetCatalogue` lists all 16 reports; not-yet-built ones are listed but
  flagged unavailable. Export format is `xlsx | pdf`.
- `InvoicePdfGenerator` renders the GST tax invoice from the **snapshotted**
  invoice (QuestPDF community license), so later profile or settings edits
  never change what a customer already received. Same house style as reports.
- Fonts: Lato TTFs ship in `api/LatoFont/` for QuestPDF.
- QuestPDF needs native libs — `api/runtimes/*/native/libQuestPdfSkia.*` and
  `libqpdf.*` are published for win/linux/osx/musl × x64/x86/arm64.

---

## 10. Deployment topology

Two IIS sites, from `deploy/DEPLOYMENT.md §5.3`:

- **API site** on `http://127.0.0.1:81` — the published `api/` folder.
  Observed content root: `C:\inetpub\wwwroot\Regalis_Src\api`.
- **SPA site** — the built `frontend/` folder, with `web.config` (from
  `public/`, so `vite build` copies it into `dist/`). Requires **URL Rewrite
  2.1 + ARR 3.0** with server-level proxy enabled.

`web.config` rules:
- `^(api/.*|health.*|swagger.*)$` → rewrite to `http://127.0.0.1:81/{R:1}`
- everything else that is not a real file or directory → `/index.html`
- `.webmanifest` MIME map added
- `assets/` cached 365 days (hashed, immutable); **`sw.js` cache disabled** —
  a stale service worker would pin clients to an old build.

Hosting environments seen: `Development`, `UAT`, `Production`.
PWA: `manifest.webmanifest`, `registerSW.js`, Workbox `sw.js`.

---

## 11. Configuration surface

From `appsettings.json` (committed as a template with empty secrets):

```
Serilog                    Console + rolling daily File sink, 30 files retained,
                           template includes [{CorrelationId}]
ConnectionStrings:Default
Jwt:Issuer                 RegalisWorkSpace
Jwt:Audience               RegalisWorkSpace.Client
Jwt:SigningKey             min 32 bytes; empty in committed files
Jwt:AccessTokenMinutes     15
Jwt:RefreshTokenDays       7
Cors:AllowedOrigins        []
RateLimiting               AuthPerMinute 5, WritePerMinute 60,
                           ReadPerMinute 300, WebhookPerMinute 120
Payments:Provider          Sandbox | Razorpay
Payments:Razorpay:KeyId / :KeySecret
Otp:Channel                Console | Smtp
Smtp                       Host, Port 587, UseStartTls, Username, Password,
                           FromAddress, FromName
Swagger:EnableInProduction false
Storage:UploadRoot         App_Data/uploads
Database:MigrateOnStartup / :SeedOnStartup
Seed:AdminPassword
```

**Secrets rule.** `appsettings.json` and `appsettings.UAT.json` are committed
as templates with empty placeholders. `appsettings.Production.json` carries
live values and is **gitignored** — real values belong in environment
variables or the host secret store. Also gitignored: `api/logs/`, `*.log`,
`*.pdb`, `api/App_Data/uploads/`.

Uploads live under `Storage:UploadRoot` — **outside the web root**, streamed
back only through the authorized download endpoint.

---

## 12. Migrations

Three, in order:
1. `InitialCreate`
2. `20260828210450_AddPasswordHistory`
3. `20260829180229_SignedAdjustmentQuantity`

Naming: descriptive PascalCase, no ticket numbers.
