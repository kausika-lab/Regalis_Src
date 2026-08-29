# DATABASE_DESIGN.md
**Regalis WorkSpace — Microsoft SQL Server**
Schema: `dbo` · Collation: `SQL_Latin1_General_CP1_CI_AS` · 45 tables

---

## 1. Conventions

| Rule | Convention |
|------|-----------|
| Master tables | `MST_` prefix |
| Transaction tables | `TRN_` prefix |
| Mapping / junction tables | `MAP_` prefix |
| Primary key | `<TableRoot>Id`, `BIGINT IDENTITY(1,1)` (`INT` for small masters) |
| Foreign key | `FK_<Child>_<Parent>` |
| Unique constraint | `UX_<Table>_<Columns>` |
| Index | `IX_<Table>_<Columns>` |
| Check constraint | `CK_<Table>_<Rule>` |
| Money | `DECIMAL(18,2)` |
| Percent / rate | `DECIMAL(5,2)` |
| Timestamps | `DATETIME2(3)`, **always UTC**, suffix `Utc` |
| Dates without time | `DATE` |
| Booleans | `BIT NOT NULL DEFAULT 0/1` |
| Enums | `TINYINT` in the DB, C# `enum` in code, decoded by `vw_*` views for reporting |
| Free text | `NVARCHAR(n)`; long text `NVARCHAR(MAX)` |

**Audit columns — present on every business table:**

```sql
CreatedBy      BIGINT        NULL,
CreatedDate    DATETIME2(3)  NOT NULL DEFAULT SYSUTCDATETIME(),
ModifiedBy     BIGINT        NULL,
ModifiedDate   DATETIME2(3)  NULL,
IsActive       BIT           NOT NULL DEFAULT 1,
IsDeleted      BIT           NOT NULL DEFAULT 0,
DeletedBy      BIGINT        NULL,
DeletedDate    DATETIME2(3)  NULL
```

**Soft delete** — `IsDeleted = 1`. EF Core applies a global query filter; the SQL views
apply `WHERE IsDeleted = 0`. Nothing in Phase 1 issues a hard `DELETE` against a
business table.

**`RowVersion ROWVERSION`** is present on `TRN_Booking`, `MST_Wallet` and
`MST_Workspace` for optimistic concurrency.

---

## 2. ERD — logical overview

```
                          ┌──────────────┐
                          │  MST_Role    │
                          └──────┬───────┘
              ┌──────────────────┼──────────────────┐
              │                  │                  │
     ┌────────▼──────┐  ┌────────▼────────┐ ┌───────▼────────┐
     │ MAP_UserRole  │  │MAP_RolePermission│ │  MAP_RoleMenu  │
     └────────┬──────┘  └────────┬────────┘ └───────┬────────┘
              │                  │                  │
      ┌───────▼───────┐  ┌───────▼────────┐  ┌──────▼──────┐
      │   MST_User    │  │ MST_Permission │  │  MST_Menu   │──┐ self-FK
      └───┬───┬───┬───┘  └────────────────┘  └─────────────┘◄─┘
          │   │   │
          │   │   └──────────────┐
          │   └──────────┐       │
          ▼              ▼       ▼
   ┌────────────┐  ┌──────────┐ ┌────────────┐
   │ MST_Wallet │  │TRN_Ticket│ │TRN_Attend. │
   └─────┬──────┘  └────┬─────┘ └────────────┘
         │              │
   ┌─────▼──────────┐ ┌─▼──────────────┐
   │TRN_WalletTxn   │ │TRN_TicketMsg   │
   └────────────────┘ └────────────────┘

   ┌──────────────┐      ┌──────────────────┐      ┌────────────────┐
   │ MST_Location │─────<│  MST_Workspace   │>─────│MST_WorkspaceType│
   └──────┬───────┘      └────────┬─────────┘      └────────────────┘
          │                       │
          │              ┌────────┴──────────┬──────────────────┐
          │              ▼                   ▼                  ▼
          │      ┌───────────────┐  ┌──────────────────┐ ┌──────────────────────┐
          └─────<│  TRN_Booking  │  │TRN_WorkspaceBlock│ │TRN_HousekeepingSched.│
                 └───┬───┬───┬───┘  └──────────────────┘ └──────────┬───────────┘
                     │   │   │              ▲                       │
                     │   │   │              └───────────────────────┘  writes a block
                     │   │   │
        ┌────────────┘   │   └─────────────┐
        ▼                ▼                 ▼
 ┌─────────────┐  ┌─────────────┐  ┌────────────────┐
 │ TRN_Invoice │  │ TRN_Payment │  │TRN_AccessToken │
 └──────┬──────┘  └─────────────┘  └───────┬────────┘
        │                                  │
 ┌──────▼────────┐                  ┌──────▼──────┐
 │TRN_InvoiceLine│                  │TRN_AccessLog│
 └───────────────┘                  └─────────────┘
```

---

## 3. Group A — Identity & Access (11 tables)

### MST_User
Single user table for every principal. `UserType` separates the portals.

| Column | Type | Null | Notes |
|--------|------|------|-------|
| UserId | BIGINT IDENTITY | PK | |
| UserType | TINYINT | NO | 1 Customer, 2 Admin, 3 Staff |
| FullName | NVARCHAR(150) | NO | |
| Email | NVARCHAR(256) | NO | `UX_User_Email` on `(Email)` where `IsDeleted=0` |
| MobileNumber | NVARCHAR(20) | NO | `UX_User_Mobile` filtered |
| PasswordHash | NVARCHAR(400) | NO | PBKDF2, versioned string. **Never returned by any API.** |
| CompanyName | NVARCHAR(200) | YES | A11: optional |
| Address | NVARCHAR(500) | YES | |
| PhotoPath | NVARCHAR(500) | YES | optional |
| DefaultLocationId | INT | YES | FK → MST_Location |
| IsEmailVerified | BIT | NO | default 0 |
| IsMobileVerified | BIT | NO | default 0 |
| Status | TINYINT | NO | 1 Active, 2 Inactive, 3 Locked, 4 PendingVerification |
| FailedLoginCount | INT | NO | default 0 |
| LockoutEndUtc | DATETIME2(3) | YES | |
| LastLoginUtc | DATETIME2(3) | YES | |
| LastPasswordChangeUtc | DATETIME2(3) | YES | |
| MustChangePassword | BIT | NO | default 0 |
| + audit columns | | | |

`CK_User_UserType CHECK (UserType IN (1,2,3))`

### MST_Role
`RoleId INT IDENTITY PK`, `RoleCode NVARCHAR(50)` (`UX_Role_Code`), `RoleName NVARCHAR(100)`,
`Description NVARCHAR(300)`, `IsSystemRole BIT` (system roles cannot be deleted),
`PortalType TINYINT` (1 Customer, 2 Admin, 3 Both), + audit.
**Seeded:** `CUSTOMER`, `ADMIN`, `MANAGER`, `HOUSEKEEPING`, `FINANCE`, `SUPPORT`.

### MST_Permission
`PermissionId INT IDENTITY PK`, `PermissionCode NVARCHAR(80)` (`UX_Permission_Code`,
format `MODULE.ACTION`), `ModuleName NVARCHAR(60)`, `ActionName NVARCHAR(30)`,
`Description NVARCHAR(200)`, `DisplayOrder INT`, + audit.
Actions: `VIEW CREATE EDIT DELETE APPROVE EXPORT IMPORT PRINT`.

### MST_Menu
Dynamic navigation. Four levels via self-reference: Module → Menu → Sub-menu → Screen.

| Column | Type | Notes |
|--------|------|-------|
| MenuId | INT IDENTITY PK | |
| ParentMenuId | INT NULL | FK → MST_Menu (`NO ACTION`, cycles prevented in code) |
| MenuCode | NVARCHAR(60) | `UX_Menu_Code` |
| MenuName | NVARCHAR(100) | internal name |
| DisplayName | NVARCHAR(100) | shown in the sidebar |
| Route | NVARCHAR(200) NULL | null for a pure grouping node |
| Icon | NVARCHAR(60) NULL | lucide-react icon name |
| DisplayOrder | INT | |
| PermissionId | INT NULL | FK → MST_Permission; the permission that reveals it |
| PortalType | TINYINT | 1 Customer, 2 Admin |
| MenuLevel | TINYINT | 1 Module … 4 Screen |
| IsVisible | BIT | hide without deleting |
| + audit | | |

### MAP_UserRole
`UserRoleId BIGINT IDENTITY PK`, `UserId`, `RoleId`, `AssignedDateUtc`, `AssignedBy`,
`UX_UserRole (UserId, RoleId)`.

### MAP_RolePermission
`RolePermissionId BIGINT IDENTITY PK`, `RoleId`, `PermissionId`,
`UX_RolePermission (RoleId, PermissionId)`.

### MAP_RoleMenu
`RoleMenuId BIGINT IDENTITY PK`, `RoleId`, `MenuId`, `UX_RoleMenu (RoleId, MenuId)`.

### TRN_RefreshToken
`RefreshTokenId BIGINT IDENTITY PK`, `UserId`, `TokenHash NVARCHAR(128)`
(`UX_RefreshToken_Hash`), `FamilyId UNIQUEIDENTIFIER`, `ExpiresUtc`, `CreatedUtc`,
`CreatedByIp NVARCHAR(64)`, `ConsumedUtc NULL`, `RevokedUtc NULL`,
`RevokedReason NVARCHAR(200) NULL`, `ReplacedByTokenHash NVARCHAR(128) NULL`.
Rotation + family revocation on reuse.

### TRN_Otp
`OtpId BIGINT IDENTITY PK`, `UserId NULL`, `Destination NVARCHAR(256)` (email or mobile),
`Channel TINYINT` (1 Email, 2 Sms), `Purpose TINYINT` (1 Registration, 2 ForgotPassword,
3 MobileVerification), `CodeHash NVARCHAR(128)`, `ExpiresUtc`, `AttemptCount INT`,
`MaxAttempts INT`, `ConsumedUtc NULL`, `CreatedUtc`.
`IX_Otp_Destination_Purpose`.

### TRN_AuditLog
`AuditLogId BIGINT IDENTITY PK`, `UserId NULL`, `UserName NVARCHAR(150) NULL`,
`Action NVARCHAR(40)` (`LOGIN LOGOUT CREATE UPDATE DELETE APPROVE REJECT EXPORT IMPORT
PASSWORD_CHANGE ACCESS_OVERRIDE AUTHORIZATION_FAILED`), `ModuleName NVARCHAR(60)`,
`EntityName NVARCHAR(100) NULL`, `EntityId NVARCHAR(60) NULL`,
`OldValue NVARCHAR(MAX) NULL`, `NewValue NVARCHAR(MAX) NULL`,
`IpAddress NVARCHAR(64) NULL`, `UserAgent NVARCHAR(300) NULL`,
`CorrelationId NVARCHAR(64) NULL`, `TimestampUtc DATETIME2(3)`.
`IX_AuditLog_Timestamp`, `IX_AuditLog_User_Timestamp`, `IX_AuditLog_Entity`.
**Append-only** — no update or delete path exists.

### TRN_LoginHistory
`LoginHistoryId BIGINT IDENTITY PK`, `UserId NULL`, `AttemptedEmail NVARCHAR(256)`,
`IsSuccess BIT`, `FailureReason NVARCHAR(200) NULL`, `IpAddress`, `UserAgent`,
`AttemptedUtc`.

---

## 4. Group B — Masters (11 tables)

### MST_Location
`LocationId INT IDENTITY PK`, `LocationCode NVARCHAR(30)` (`UX`), `LocationName
NVARCHAR(150)`, `AddressLine1/2 NVARCHAR(200)`, `City NVARCHAR(100)`, `State
NVARCHAR(100)`, `PostalCode NVARCHAR(20)`, `ContactNumber NVARCHAR(20)`, `Email
NVARCHAR(256)`, `WorkingHoursStart TIME(0)`, `WorkingHoursEnd TIME(0)`,
`WorkingDays NVARCHAR(30)` (`MON,TUE,…`), `Latitude/Longitude DECIMAL(9,6) NULL`,
`ImagePath NVARCHAR(500) NULL`, `Description NVARCHAR(MAX) NULL`,
`IsDefault BIT`, + audit.
Invariant 7: every bookable/schedulable table carries `LocationId`.

### MST_WorkspaceType
`WorkspaceTypeId INT IDENTITY PK`, `TypeCode NVARCHAR(30)` (`DESK`, `CABIN`,
`MEETING_ROOM`), `TypeName NVARCHAR(100)`, `Description`, `IconName NVARCHAR(60)`,
`DisplayOrder INT`, `RequiresCapacity BIT`, + audit.
Extensible: adding `CONFERENCE_ROOM` or `EVENT_SPACE` is a data insert.

### MST_Workspace
| Column | Type | Notes |
|--------|------|-------|
| WorkspaceId | INT IDENTITY PK | |
| LocationId | INT | FK |
| WorkspaceTypeId | INT | FK |
| WorkspaceCode | NVARCHAR(40) | `D-001`, `C-001`, `MR-001`. `UX_Workspace_Location_Code (LocationId, WorkspaceCode)` |
| WorkspaceName | NVARCHAR(120) | "Conference Room A" |
| FloorNumber | NVARCHAR(20) NULL | |
| Capacity | INT | ≥ 1 |
| PricePerHour | DECIMAL(18,2) | |
| PricePerDay | DECIMAL(18,2) NULL | |
| Equipment | NVARCHAR(500) NULL | comma list, e.g. "Projector,Whiteboard,VC" |
| Description | NVARCHAR(MAX) NULL | |
| ImagePath | NVARCHAR(500) NULL | |
| Status | TINYINT | 1 Available, 2 Occupied, 3 Maintenance, 4 Disabled |
| IsBookable | BIT | false ⇒ excluded from customer availability |
| RowVersion | ROWVERSION | |
| + audit | | |

`CK_Workspace_Capacity CHECK (Capacity >= 1)`, `CK_Workspace_Price CHECK (PricePerHour >= 0)`

### MST_Amenity / MAP_WorkspaceAmenity
`AmenityId INT`, `AmenityName`, `IconName`, `DisplayOrder`. Junction with
`UX_WorkspaceAmenity (WorkspaceId, AmenityId)`.

### MST_PassPlan
`PassPlanId INT IDENTITY PK`, `PlanCode NVARCHAR(40)` (`UX`), `PlanName NVARCHAR(120)`,
`PassType TINYINT` (1 DayPass, 2 MonthlyPass), `LocationId INT NULL` (null = all),
`Price DECIMAL(18,2)`, `ValidityDays INT`, `MaxGuests INT NULL`,
`Description NVARCHAR(MAX)`, `TermsAndConditions NVARCHAR(MAX)`,
`ImagePath NVARCHAR(500) NULL`, `DisplayOrder INT`, + audit.

### MST_ServiceItem
Coffee and featured services (§ "Featured Services", Doc-1 §26 Coffee).
`ServiceItemId INT IDENTITY PK`, `ItemCode NVARCHAR(40)` (`UX`), `ItemName NVARCHAR(120)`,
`Category NVARCHAR(60)` (`Coffee`, `Tea`, `Snacks`, `Printing`, `Scanning`, `Locker`),
`Price DECIMAL(18,2)`, `Unit NVARCHAR(20)`, `ImagePath`, `Description`,
`IsAvailable BIT`, `DisplayOrder INT`, `LocationId INT NULL`, + audit.

### MST_Banner
`BannerId INT IDENTITY PK`, `Title NVARCHAR(150)`, `Subtitle NVARCHAR(300) NULL`,
`ImagePath NVARCHAR(500)`, `LinkUrl NVARCHAR(500) NULL`, `PromotionalPrice
DECIMAL(18,2) NULL`, `DisplayOrder INT`, `ValidFromUtc`, `ValidToUtc`,
`LocationId INT NULL`, `PortalType TINYINT`, + audit.

### MST_AppSetting
The "no hard-coding" table (Invariant 6).
`AppSettingId INT IDENTITY PK`, `SettingKey NVARCHAR(120)` (`UX`), `SettingValue
NVARCHAR(MAX)`, `SettingGroup NVARCHAR(60)`, `DataType NVARCHAR(20)`
(`string int decimal bool json time`), `Description NVARCHAR(300)`,
`IsEditableByAdmin BIT`, `IsSecret BIT` (secret values are never returned by the API), + audit.

Seeded groups and keys:

| Group | Keys |
|-------|------|
| Company | `Company.Name`, `Company.Gstin`, `Company.Address`, `Company.Email`, `Company.Phone`, `Company.LogoPath` |
| Tax | `Tax.CgstPercent` (9), `Tax.SgstPercent` (9), `Tax.IgstPercent` (18), `Tax.PlaceOfSupply` |
| Invoice | `Invoice.NumberPrefix` (`REG/INV`), `Invoice.DueDays` (7) |
| Booking | `Booking.NumberPrefix` (`REG-`), `Booking.SlotGranularityMinutes` (30), `Booking.MinMinutes` (30), `Booking.MaxMinutes` (480), `Booking.AdvanceDays` (60), `Booking.PendingHoldMinutes` (10) |
| Cancellation | `Cancellation.CutoffHours` (24), `Cancellation.RefundPercentBeforeCutoff` (100), `Cancellation.RefundPercentAfterCutoff` (0) |
| Wallet | `Wallet.MinRecharge` (100), `Wallet.MaxRecharge` (50000), `Wallet.Enabled` (true), `Wallet.Withdrawable` (false) |
| Support | `Support.ContactNumber`, `Support.Hours`, `Support.Message`, `Support.Email` |
| Security | `Security.MaxFailedLogins` (5), `Security.LockoutMinutes` (15), `Security.PasswordMinLength` (8), `Security.OtpTtlMinutes` (10), `Security.OtpMaxAttempts` (5) |
| Attendance | `Attendance.AllowMultipleCheckInPerDay` (false), `Attendance.AutoCheckoutHour` (23) |

### MST_TicketCategory / MST_TicketIssueType
Category: `Booking Related`, `Building Management`, `Electricity`, `Food & Beverage
(F&B)`, `Housekeeping & Pantry`, `Infrastructure & Design Issue`, `Meeting Room`,
`Parking`, `Procurement`, `Product / Technical Support`, `Project Delivery`,
`Sales Related`.
IssueType has `TicketCategoryId` FK and `IssueTypeName` (e.g. under Booking Related:
`Password Reset / Login Issue`, `Billing / Invoice Issue`, `Booking Cancellation`,
`Booking Modification`, `Booking Confirmation Not Received`, `Booking Payment Failed`,
`Booking Extension Request`, `Other Booking Issues`).

---

## 5. Group C — Booking & Calendar (3 tables) — the core

### TRN_Booking

| Column | Type | Notes |
|--------|------|-------|
| BookingId | BIGINT IDENTITY PK | |
| BookingNumber | NVARCHAR(30) | `REG-000123`. `UX_Booking_Number` |
| CustomerId | BIGINT | FK → MST_User |
| LocationId | INT | FK |
| WorkspaceId | INT | FK |
| WorkspaceTypeId | INT | FK (denormalised for reporting/filtering) |
| BookingDate | DATE | local booking date, for grouping |
| StartUtc | DATETIME2(3) | inclusive |
| EndUtc | DATETIME2(3) | **exclusive** |
| DurationMinutes | INT | |
| BaseAmount | DECIMAL(18,2) | |
| DiscountAmount | DECIMAL(18,2) | default 0 |
| TaxAmount | DECIMAL(18,2) | |
| TotalAmount | DECIMAL(18,2) | |
| Status | TINYINT | 1 Pending, 2 Confirmed, 3 CheckedIn, 4 Completed, 5 Cancelled, 6 Expired, 7 NoShow |
| PaymentStatus | TINYINT | 1 Pending, 2 Paid, 3 Failed, 4 Refunded, 5 PartiallyRefunded |
| BookingSource | TINYINT | 1 Customer, 2 Admin, 3 Pass |
| CustomerPassId | BIGINT NULL | FK — set when a Day/Monthly pass covers it |
| CreatedByUserId | BIGINT | who actually created it (admin bookings) |
| CheckInUtc | DATETIME2(3) NULL | |
| CheckOutUtc | DATETIME2(3) NULL | |
| CancelledUtc | DATETIME2(3) NULL | |
| CancelledByUserId | BIGINT NULL | |
| CancellationReason | NVARCHAR(500) NULL | |
| RefundAmount | DECIMAL(18,2) NULL | |
| Notes | NVARCHAR(500) NULL | |
| RowVersion | ROWVERSION | |
| + audit | | |

Constraints and indexes:

```sql
CK_Booking_Period   CHECK (EndUtc > StartUtc)
CK_Booking_Amounts  CHECK (BaseAmount >= 0 AND TaxAmount >= 0 AND TotalAmount >= 0)

-- the overlap-detection index; leading columns match the conflict predicate
IX_Booking_Workspace_Period  ON (WorkspaceId, StartUtc, EndUtc)
       INCLUDE (Status, BookingId) WHERE IsDeleted = 0

-- duplicate-submission guard for occupancy statuses
UX_Booking_Workspace_Start   ON (WorkspaceId, StartUtc)
       WHERE Status IN (1,2,3) AND IsDeleted = 0

IX_Booking_Customer_Start    ON (CustomerId, StartUtc DESC)
IX_Booking_Date_Status       ON (BookingDate, Status) INCLUDE (LocationId, TotalAmount)
IX_Booking_Location_Start    ON (LocationId, StartUtc)
```

**Occupancy statuses** = `Pending (held)`, `Confirmed`, `CheckedIn`. A `Pending` booking
holds the slot for `Booking.PendingHoldMinutes` (default 10) while payment completes; a
background sweep expires stale holds to `Expired`.

### TRN_BookingStatusHistory
`BookingStatusHistoryId BIGINT IDENTITY PK`, `BookingId`, `FromStatus TINYINT NULL`,
`ToStatus TINYINT`, `ChangedByUserId`, `ChangedUtc`, `Reason NVARCHAR(500) NULL`.
Append-only. This is why bookings never need deleting.

### TRN_WorkspaceBlock
Everything that occupies a workspace but is not a customer booking.

| Column | Type | Notes |
|--------|------|-------|
| WorkspaceBlockId | BIGINT IDENTITY PK | |
| WorkspaceId | INT | FK |
| LocationId | INT | FK |
| StartUtc / EndUtc | DATETIME2(3) | half-open |
| BlockReason | TINYINT | 1 Maintenance, 2 InternalUse, 3 Cleaning, 4 Repair, 5 PrivateEvent, 6 Housekeeping, 7 Other |
| ReasonNotes | NVARCHAR(500) NULL | |
| HousekeepingScheduleId | BIGINT NULL | FK — set when the block came from a schedule |
| BlockedByUserId | BIGINT | |
| ReleasedUtc | DATETIME2(3) NULL | unblock = set this + IsActive 0 |
| ReleasedByUserId | BIGINT NULL | |
| + audit | | |

`CK_Block_Period CHECK (EndUtc > StartUtc)`,
`IX_Block_Workspace_Period ON (WorkspaceId, StartUtc, EndUtc) WHERE IsActive = 1 AND IsDeleted = 0`

**The availability predicate — one query, used everywhere:**

```sql
-- a workspace is free for [@Start, @End) when neither of these returns a row
SELECT 1 FROM dbo.TRN_Booking WITH (UPDLOCK, HOLDLOCK)
 WHERE WorkspaceId = @WorkspaceId AND IsDeleted = 0
   AND Status IN (1,2,3)
   AND StartUtc < @End AND @Start < EndUtc;

SELECT 1 FROM dbo.TRN_WorkspaceBlock WITH (UPDLOCK, HOLDLOCK)
 WHERE WorkspaceId = @WorkspaceId AND IsDeleted = 0 AND IsActive = 1
   AND StartUtc < @End AND @Start < EndUtc;
```

---

## 6. Group D — Finance (8 tables)

### MST_Wallet
One row per customer. `WalletId BIGINT IDENTITY PK`, `CustomerId BIGINT`
(`UX_Wallet_Customer`), `Balance DECIMAL(18,2) NOT NULL DEFAULT 0`,
`TotalCredited DECIMAL(18,2)`, `TotalDebited DECIMAL(18,2)`,
`LastTransactionUtc DATETIME2(3) NULL`, `RowVersion ROWVERSION`, + audit.
`CK_Wallet_Balance CHECK (Balance >= 0)` — the database itself refuses an overdraft.

### TRN_WalletTransaction
Append-only ledger. `WalletTransactionId BIGINT IDENTITY PK`, `WalletId`, `CustomerId`,
`TransactionNumber NVARCHAR(30)` (`UX`), `Direction TINYINT` (1 Credit, 2 Debit),
`Category TINYINT` (1 Recharge, 2 Booking, 3 Refund, 4 DayPass, 5 MonthlyPass,
6 ServiceOrder, 7 SecurityDeposit, 8 AdminAdjustment, 9 Cancellation),
`Amount DECIMAL(18,2)`, `BalanceAfter DECIMAL(18,2)`,
`ReferenceType NVARCHAR(40) NULL`, `ReferenceId BIGINT NULL`,
`PaymentId BIGINT NULL`, `Narration NVARCHAR(300)`, `TransactionUtc DATETIME2(3)`,
`PerformedByUserId BIGINT`, + audit.
`IX_WalletTxn_Customer_Date ON (CustomerId, TransactionUtc DESC)`,
`IX_WalletTxn_Category`.
`BalanceAfter` is written inside the same transaction that updates `MST_Wallet.Balance`,
so the ledger is self-checking: `SUM(credits) - SUM(debits) = Balance`.

### TRN_Payment
`PaymentId BIGINT IDENTITY PK`, `PaymentNumber NVARCHAR(30)` (`UX`), `CustomerId`,
`BookingId BIGINT NULL`, `InvoiceId BIGINT NULL`, `CustomerPassId BIGINT NULL`,
`ServiceOrderId BIGINT NULL`, `Amount DECIMAL(18,2)`,
`PaymentMethod TINYINT` (1 Wallet, 2 Upi, 3 CreditCard, 4 DebitCard, 5 NetBanking,
6 Cash, 7 Other), `Gateway NVARCHAR(40) NULL` (`Razorpay`/`Sandbox`),
`GatewayOrderId NVARCHAR(120) NULL`, `GatewayPaymentId NVARCHAR(120) NULL`,
`GatewaySignature NVARCHAR(256) NULL`, `IsSignatureVerified BIT`,
`Status TINYINT` (1 Pending, 2 Success, 3 Failed, 4 Refunded, 5 Cancelled),
`PaidUtc DATETIME2(3) NULL`, `FailureReason NVARCHAR(500) NULL`,
`RefundedAmount DECIMAL(18,2) NULL`, `RefundReference NVARCHAR(120) NULL`,
`RawGatewayResponse NVARCHAR(MAX) NULL`, + audit.
`IX_Payment_Customer_Date`, `IX_Payment_Status_Date`, `UX_Payment_GatewayPaymentId`
(filtered, non-null) — makes gateway webhooks idempotent.

### TRN_Invoice
`InvoiceId BIGINT IDENTITY PK`, `InvoiceNumber NVARCHAR(40)` (`UX`), `CustomerId`,
`BookingId NULL`, `CustomerPassId NULL`, `ServiceOrderId NULL`, `LocationId`,
`InvoiceDate DATE`, `DueDate DATE`, `SubTotal DECIMAL(18,2)`,
`DiscountAmount DECIMAL(18,2)`, `TaxableAmount DECIMAL(18,2)`,
`CgstPercent/SgstPercent/IgstPercent DECIMAL(5,2)`,
`CgstAmount/SgstAmount/IgstAmount DECIMAL(18,2)`, `TotalAmount DECIMAL(18,2)`,
`PaidAmount DECIMAL(18,2)`, `OutstandingAmount AS (TotalAmount - PaidAmount) PERSISTED`,
`Status TINYINT` (1 Pending, 2 Paid, 3 PartiallyPaid, 4 Cancelled, 5 Refunded),
`PaymentId BIGINT NULL`, `PaidUtc NULL`,
`CustomerName/CustomerEmail/CustomerMobile/CustomerGstin/BillingAddress` — **snapshotted
at issue time** so a later profile edit never rewrites history,
`CompanyName/CompanyGstin/CompanyAddress` — likewise snapshotted,
`PdfPath NVARCHAR(500) NULL`, `Notes`, `CancellationReason`, + audit.
`IX_Invoice_Customer_Date`, `IX_Invoice_Status_Date`.

### TRN_InvoiceLine
`InvoiceLineId BIGINT IDENTITY PK`, `InvoiceId`, `LineNumber INT`,
`Description NVARCHAR(300)`, `HsnSacCode NVARCHAR(20) NULL`, `Quantity DECIMAL(18,3)`,
`UnitPrice DECIMAL(18,2)`, `LineAmount DECIMAL(18,2)`, `TaxPercent DECIMAL(5,2)`,
`TaxAmount DECIMAL(18,2)`, `TotalAmount DECIMAL(18,2)`.

### TRN_CustomerPass
`CustomerPassId BIGINT IDENTITY PK`, `PassNumber NVARCHAR(30)` (`UX`), `CustomerId`,
`PassPlanId`, `LocationId`, `PassType TINYINT`, `PurchaseUtc`, `ValidFrom DATE`,
`ValidTo DATE`, `GuestCount INT`, `Price/TaxAmount/TotalAmount DECIMAL(18,2)`,
`Status TINYINT` (1 Active, 2 Used, 3 Expired, 4 Cancelled),
`UsedUtc NULL`, `InvoiceId NULL`, `PaymentId NULL`, + audit.
`IX_CustomerPass_Customer_Status`, `IX_CustomerPass_ValidTo_Status` (drives the expiry job).

### TRN_ServiceOrder / TRN_ServiceOrderLine
Coffee / featured-service purchases. Order: `ServiceOrderId`, `OrderNumber` (`UX`),
`CustomerId`, `LocationId`, `OrderUtc`, `SubTotal/TaxAmount/TotalAmount`,
`Status TINYINT` (1 Placed, 2 Confirmed, 3 Ready, 4 Delivered, 5 Cancelled),
`PaymentId NULL`, `InvoiceId NULL`, `Notes`.
Line: `ServiceOrderLineId`, `ServiceOrderId`, `ServiceItemId`, `ItemName` (snapshot),
`Quantity`, `UnitPrice`, `LineAmount`.

---

## 7. Group E — Access & Attendance (3 tables)

### TRN_AccessToken
`AccessTokenId BIGINT IDENTITY PK`, `TokenValue NVARCHAR(64)` (`UX_AccessToken_Value`,
cryptographically random, opaque — **not** a guessable booking id),
`QrPayload NVARCHAR(500)` (signed compact payload rendered as the QR),
`UserId BIGINT`, `BookingId NULL`, `CustomerPassId NULL`,
`HousekeepingScheduleId NULL`, `WorkspaceId INT NULL`, `LocationId INT`,
`TokenPurpose TINYINT` (1 Booking, 2 DayPass, 3 MonthlyPass, 4 Housekeeping,
5 VisitorFuture), `ValidFromUtc`, `ValidToUtc`,
`Status TINYINT` (1 Active, 2 Used, 3 Expired, 4 Revoked),
`UsageCount INT`, `MaxUsageCount INT NULL` (null = unlimited within the window),
`FirstUsedUtc NULL`, `LastUsedUtc NULL`, `RevokedReason NVARCHAR(300) NULL`, + audit.
`IX_AccessToken_User_Status`, `IX_AccessToken_Booking`.

### TRN_AccessLog
Append-only. Records **every** attempt, granted or denied.
`AccessLogId BIGINT IDENTITY PK`, `AccessTokenId NULL`, `UserId NULL`,
`ScannedTokenValue NVARCHAR(64) NULL` (what was actually presented, even if invalid),
`WorkspaceId NULL`, `LocationId`, `Direction TINYINT` (1 In, 2 Out),
`AccessMethod TINYINT` (1 Qr, 2 Card, 3 Manual, 4 Nfc, 5 App),
`Result TINYINT` (1 Granted, 2 Denied, 3 DeviceError),
`DenyReason NVARCHAR(200) NULL` (`TokenNotFound`, `TokenRevoked`, `OutsideValidWindow`,
`BookingCancelled`, `WrongWorkspace`, `AlreadyCheckedOut`, `DeviceOffline`),
`DeviceId NVARCHAR(80) NULL`, `DeviceStatus NVARCHAR(60) NULL`,
`IsManualOverride BIT`, `OverrideByUserId BIGINT NULL`,
`OverrideReason NVARCHAR(500) NULL`, `IpAddress`, `LoggedUtc DATETIME2(3)`.
`IX_AccessLog_Logged`, `IX_AccessLog_User_Logged`, `IX_AccessLog_Result_Logged`.
Invariant 9: a `DeviceError` result never implies a grant. Only an explicit override with
`ACCESS.OVERRIDE` produces `Granted` + `IsManualOverride = 1`.

### TRN_Attendance
`AttendanceId BIGINT IDENTITY PK`, `UserId`, `SubjectType TINYINT` (1 Customer, 2 Staff),
`LocationId`, `WorkspaceId NULL`, `BookingId NULL`, `HousekeepingScheduleId NULL`,
`AttendanceDate DATE`, `CheckInUtc`, `CheckOutUtc NULL`,
`TotalMinutes INT NULL`, `Status TINYINT` (1 CheckedIn, 2 CheckedOut, 3 AutoClosed),
`CheckInMethod TINYINT`, `CheckOutMethod TINYINT NULL`,
`CheckInAccessLogId NULL`, `CheckOutAccessLogId NULL`,
`IsException BIT`, `ExceptionNotes NVARCHAR(500) NULL`,
`IsCorrected BIT`, `CorrectedByUserId NULL`, `CorrectionReason NVARCHAR(500) NULL`,
`OriginalCheckInUtc NULL`, `OriginalCheckOutUtc NULL`, + audit.
`UX_Attendance_User_Date` filtered on `Attendance.AllowMultipleCheckInPerDay = false`
is enforced in the service layer (the setting is configurable), backed by
`IX_Attendance_User_Date` and `IX_Attendance_Date_Location`.
Corrections keep the original values — the payroll hand-off is auditable.

---

## 8. Group F — Operations (7 tables)

### MST_InventoryItem
`InventoryItemId INT IDENTITY PK`, `ItemCode NVARCHAR(40)` (`UX`), `ItemName
NVARCHAR(150)`, `Category NVARCHAR(60)`, `Unit NVARCHAR(20)` (`Nos`, `Kg`, `Ltr`,
`Box`), `OpeningQuantity DECIMAL(18,3)`, `ReorderLevel DECIMAL(18,3)`,
`UnitCost DECIMAL(18,2) NULL`, `LocationId INT`, `Description`, `ImagePath`, + audit.

**Current stock is never stored as a mutable column** (Invariant 11). It is derived:

```sql
OpeningQuantity
  + SUM(Quantity WHERE TransactionType = 1 /*In*/)
  - SUM(Quantity WHERE TransactionType = 2 /*Out*/)
  - SUM(Quantity WHERE TransactionType = 3 /*Damage*/)
```
exposed by `vw_InventoryStock` and, for performance, by the indexed helper
`fn_GetCurrentStock(@ItemId)`.

### TRN_InventoryTransaction
Append-only. `InventoryTransactionId BIGINT IDENTITY PK`, `InventoryItemId`,
`TransactionNumber NVARCHAR(30)` (`UX`),
`TransactionType TINYINT` (1 In, 2 Out, 3 Damage, 4 OpeningBalance, 5 Adjustment),
`Quantity DECIMAL(18,3)`, `TransactionDate DATE`,
`SourceOrDestination NVARCHAR(200) NULL` (supplier for In, department/use for Out),
`ReferenceNumber NVARCHAR(80) NULL`, `UnitCost DECIMAL(18,2) NULL`,
`DamageDescription NVARCHAR(500) NULL`, `DamageCause NVARCHAR(300) NULL`,
`PhotoPath NVARCHAR(500) NULL`, `LocationId`, `EnteredByUserId`, `Notes`, + audit.
`CK_InvTxn_Quantity CHECK (Quantity > 0 OR (TransactionType = 5 AND Quantity <> 0))`
(amended Day 8: an `Adjustment` is the one signed row — a compensating entry must be
able to move stock down as well as up; every other type stays strictly positive),
`IX_InvTxn_Item_Date`, `IX_InvTxn_Type_Date`.
There is **no update or delete endpoint**; a mistake is corrected with a compensating
`Adjustment` row, so the audit history is complete.

### TRN_HousekeepingSchedule
`HousekeepingScheduleId BIGINT IDENTITY PK`, `ScheduleNumber NVARCHAR(30)` (`UX`),
`StaffUserId BIGINT` (FK → MST_User where UserType = 3),
`LocationId`, `WorkspaceId INT NULL`, `AreaName NVARCHAR(150)`,
`ScheduledDate DATE`, `StartUtc`, `EndUtc`,
`TaskType TINYINT` (1 Housekeeping, 2 Maintenance, 3 Cleaning, 4 Repair, 5 Inspection),
`TaskDescription NVARCHAR(500)`, `Notes NVARCHAR(500) NULL`,
`Status TINYINT` (1 Scheduled, 2 InProgress, 3 Completed, 4 Cancelled, 5 Missed),
`BlocksWorkspace BIT` (true ⇒ a `TRN_WorkspaceBlock` row is written),
`WorkspaceBlockId BIGINT NULL`,
`CheckInUtc NULL`, `CheckOutUtc NULL`, `CompletionNotes NVARCHAR(500) NULL`,
`AssignedByUserId`, + audit.
`IX_HkSchedule_Staff_Date`, `IX_HkSchedule_Date_Status`, `IX_HkSchedule_Workspace_Period`.

### TRN_Notification
`NotificationId BIGINT IDENTITY PK`, `UserId BIGINT NULL` (null ⇒ broadcast),
`Audience TINYINT` (1 SingleUser, 2 AllCustomers, 3 AllAdmins, 4 ByLocation, 5 ByRole),
`AudienceRefId BIGINT NULL`, `Title NVARCHAR(200)`, `Message NVARCHAR(MAX)`,
`Category TINYINT` (1 Booking, 2 MeetingRoom, 3 CheckInOut, 4 Payment, 5 Invoice,
6 ServiceOrder, 7 Event, 8 SupportTicket, 9 Announcement, 10 Registration, 11 Pass,
12 Housekeeping), `Priority TINYINT`,
`ReferenceType NVARCHAR(40) NULL`, `ReferenceId BIGINT NULL`,
`ActionLabel NVARCHAR(60) NULL`, `ActionRoute NVARCHAR(200) NULL`,
`IsRead BIT`, `ReadUtc NULL`, `ScheduledForUtc NULL`, `SentUtc NULL`,
`CreatedByUserId NULL`, + audit.
`IX_Notification_User_Created`, `IX_Notification_User_IsRead`, `IX_Notification_Scheduled`.

### TRN_Ticket
`TicketId BIGINT IDENTITY PK`, `TicketNumber NVARCHAR(30)` (`UX`), `CustomerId`,
`TicketCategoryId`, `TicketIssueTypeId`, `LocationId`,
`Subject NVARCHAR(200)`, `Description NVARCHAR(MAX)`,
`Priority TINYINT` (1 Low, 2 Medium, 3 High, 4 Critical),
`Status TINYINT` (1 Open, 2 Assigned, 3 InProgress, 4 WaitingForCustomer, 5 Resolved,
6 Closed, 7 Reopened), `AssignedToUserId BIGINT NULL`, `AssignedUtc NULL`,
`FirstResponseUtc NULL`, `ResolvedUtc NULL`, `ClosedUtc NULL`,
`ResolutionNotes NVARCHAR(MAX) NULL`, `ReopenCount INT`, + audit.
`IX_Ticket_Customer_Created`, `IX_Ticket_Status_Priority`, `IX_Ticket_Assigned_Status`.

### TRN_TicketMessage
`TicketMessageId BIGINT IDENTITY PK`, `TicketId`, `SenderUserId`,
`SenderType TINYINT` (1 Customer, 2 Admin), `Message NVARCHAR(MAX)`,
`IsInternalNote BIT` (never returned to the customer), `SentUtc`, + audit.

### TRN_TicketAttachment
`TicketAttachmentId BIGINT IDENTITY PK`, `TicketId`, `TicketMessageId NULL`,
`FileName NVARCHAR(255)`, `StoredFileName NVARCHAR(255)`, `FilePath NVARCHAR(500)`,
`ContentType NVARCHAR(120)`, `FileSizeBytes BIGINT`, `UploadedByUserId`, `UploadedUtc`.
Allowed types (validated server-side by extension **and** content sniffing):
`.png .jpg .jpeg .webp .pdf`. Max 10 MB per file, 5 files per ticket.

---

## 9. Views (`database/views/Views.sql`)

| View | Purpose |
|------|---------|
| `vw_BookingDetail` | Booking joined to customer, workspace, location, type, invoice, payment with **decoded status text** — the Booking Report |
| `vw_WorkspaceAvailabilityToday` | Per workspace: today's booked minutes, blocked minutes, free minutes |
| `vw_WorkspaceOccupancy` | Totals/available/occupied/occupancy % per workspace type per location |
| `vw_RevenueDaily` | Date, location, type: bookings, gross, tax, net |
| `vw_CustomerSummary` | Customer with booking count, lifetime value, wallet balance, open tickets |
| `vw_InvoiceReport` | Invoice + customer + booking + payment status |
| `vw_PaymentReport` | Payment + method + status + linked booking |
| `vw_WalletStatement` | Wallet ledger with running balance |
| `vw_AttendanceReport` | Attendance + user + location + worked hours |
| `vw_AccessAudit` | Access log joined to token, user, workspace with decoded result/reason |
| `vw_InventoryStock` | Derived current stock, reorder flag |
| `vw_TicketReport` | Ticket + category + issue type + assignee + resolution hours |
| `vw_PassReport` | Day/Monthly pass sales and status |
| `vw_ServiceOrderReport` | Coffee/service sales |

## 10. Stored procedures (`database/stored-procedures/StoredProcedures.sql`)

| Procedure | Purpose |
|-----------|---------|
| `usp_CheckWorkspaceAvailability` | The locking availability check, exactly as the engine uses it |
| `usp_GetAvailableWorkspaces` | List free workspaces for location/type/window |
| `usp_GetCalendarGrid` | Day/week/month calendar cells for the admin calendar |
| `usp_GetNextSequence` | Gap-free `REG-000123` / invoice numbers via an atomic counter table |
| `usp_ExpireStaleBookingHolds` | Sweeps `Pending` past the hold window → `Expired` |
| `usp_ExpirePasses` | Day/Monthly passes past `ValidTo` → `Expired` |
| `usp_AutoCloseAttendance` | Closes `CheckedIn` rows left open past the configured hour |
| `usp_GetDashboardSummary` | The admin dashboard cards in one round trip |
| `usp_GetRevenueReport` | Parameterised revenue report |
| `usp_GetInventoryStock` | Derived stock with reorder flags |

Sequence numbers come from `MST_NumberSequence (SequenceKey, Prefix, CurrentValue,
FinancialYear)` updated with `UPDATE … SET @next = CurrentValue += 1` under a
transaction, which is atomic and gap-free — `IDENTITY` is not used for
customer-visible numbers because it leaks volume and skips on rollback.

---

## 11. Referential integrity policy

| Relationship | On delete |
|--------------|-----------|
| Any `MAP_*` → parent master | `CASCADE` (junction rows are meaningless alone) |
| `TRN_InvoiceLine` → `TRN_Invoice` | `CASCADE` |
| `TRN_TicketMessage/Attachment` → `TRN_Ticket` | `CASCADE` |
| `TRN_BookingStatusHistory` → `TRN_Booking` | `CASCADE` |
| Everything else | `NO ACTION` — soft delete only |

Multiple `NO ACTION` paths to `MST_User` and `MST_Location` are deliberate; SQL Server
would reject overlapping cascade paths, and business records must survive their parents.

---

## 12. Deployment

Both routes produce the identical schema:

1. **Developer route** — `dotnet ef database update` (EF Core migrations,
   `backend/src/Regalis.Infrastructure/Persistence/Migrations`).
2. **DBA route** — execute in order, no EF Core required (master prompt §29):
   `Database_Create.sql` → `Indexes.sql` → `Views.sql` → `StoredProcedures.sql` →
   `Database_Seed.sql`. `Database_Migration.sql` carries incremental changes after the
   first release.

Every script is idempotent (`IF NOT EXISTS` guards) and safe to re-run.
