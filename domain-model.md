# Regalis — domain model

48 entities and 35 enums, extracted from IL metadata in `Regalis.Domain.dll`.
Regenerate with:

```bash
python3 scripts/dump2.py api/Regalis.Domain.dll Regalis.Domain.Entities props
python3 scripts/dump2.py api/Regalis.Domain.dll Regalis.Domain.Enums
```

## Reading notes

- **Every enum starts at 1.** There is no zero member anywhere. A `0` in an
  enum column means the value was never set.
- `: AuditableEntity` marks entities carrying the standard audit columns, set
  by `AuditSaveChangesInterceptor` — **never by hand**.
- Entities *without* `: AuditableEntity` are the append-only log/history/join
  tables. They have no audit columns because they are never updated.
- Navigation properties appear alongside scalars (`Customer` next to
  `CustomerId`). Presence of a navigation property tells you the
  configuration defines that relationship.
- `RowVersion` marks optimistic-concurrency entities: `Booking`, `Workspace`,
  `Wallet`.
- Table names are `MST_` / `TRN_` / `MAP_` prefixed; the entity name is not.

## Table-name mapping

| Entity | Table | Notes |
|---|---|---|
| AccessLog | TRN_AccessLog | append-only; every attempt, granted or denied |
| AccessToken | TRN_AccessToken | `TokenValue` is opaque + random |
| Attendance | TRN_Attendance | customers **and** staff; corrections keep originals |
| Booking | TRN_Booking | core occupancy record; never deleted |
| BookingStatusHistory | TRN_BookingStatusHistory | append-only |
| WorkspaceBlock | TRN_WorkspaceBlock | non-booking occupancy |
| CustomerPass | TRN_CustomerPass | purchased day/monthly pass |
| Invoice | TRN_Invoice | customer + company details snapshotted at issue |
| InvoiceLine | TRN_InvoiceLine | |
| Payment | TRN_Payment | gateway signature verified server-side |
| ServiceOrder | TRN_ServiceOrder | coffee / featured services |
| ServiceOrderLine | TRN_ServiceOrderLine | `ItemName` snapshotted |
| Wallet | MST_Wallet | one per customer; CHECK constraint blocks overdraft |
| WalletTransaction | TRN_WalletTransaction | append-only, self-checking ledger |
| AuditLog | TRN_AuditLog | append-only; no update/delete path exists |
| LoginHistory | TRN_LoginHistory | every attempt |
| Menu | MST_Menu | 4-level self-reference; adding a screen = data insert |
| Otp | TRN_Otp | 6 digits, hashed, 10-min TTL, 5 attempts |
| PasswordHistory | TRN_PasswordHistory | backs the "not last 3 hashes" rule |
| Permission | MST_Permission | code format `MODULE.ACTION` |
| RefreshToken | TRN_RefreshToken | hashed, single-use, family revocation |
| Role | MST_Role | |
| RoleMenu | MAP_RoleMenu | |
| RolePermission | MAP_RolePermission | |
| User | MST_User | single table for every principal; `UserType` splits portals |
| UserRole | MAP_UserRole | |
| Amenity | MST_Amenity | |
| AppSetting | MST_AppSetting | the "no hard-coding" table |
| Banner | MST_Banner | home-screen promos |
| Location | MST_Location | Invariant 7 anchor |
| NumberSequence | MST_NumberSequence | atomic gap-free counters |
| PassPlan | MST_PassPlan | purchasable pass definition |
| ServiceItem | MST_ServiceItem | coffee + featured services catalogue |
| TicketCategory | MST_TicketCategory | |
| TicketIssueType | MST_TicketIssueType | |
| Workspace | MST_Workspace | bookable desk, cabin or meeting room |
| WorkspaceAmenity | MAP_WorkspaceAmenity | |
| WorkspaceType | MST_WorkspaceType | DESK, CABIN, MEETING_ROOM — extensible by insert |
| HousekeepingSchedule | TRN_HousekeepingSchedule | writes a WorkspaceBlock when blocking |
| InventoryItem | MST_InventoryItem | stock never stored |
| InventoryTransaction | TRN_InventoryTransaction | append-only |
| Notification | TRN_Notification | in-app notification centre |
| Ticket | TRN_Ticket | |
| TicketAttachment | TRN_TicketAttachment | .png .jpg .jpeg .webp .pdf, ≤10 MB, 5/ticket |
| TicketMessage | TRN_TicketMessage | `IsInternalNote` hidden from customers |

## Attachment rules

- **Ticket attachments**: `.png .jpg .jpeg .webp .pdf`, max 10 MB per file,
  5 per ticket. Validated by extension, size **and** leading magic bytes.
- **Profile photo**: `.png .jpg .jpeg .webp`, max 2 MB, content-sniffed the
  same way.
- Both stored outside the web root under `Storage:UploadRoot`.

---

## Enums

```

## Regalis.Domain.Enums.AccessDirection  [enum] : Enum
   In=1, Out=2

## Regalis.Domain.Enums.AccessMethod  [enum] : Enum
   Qr=1, Card=2, Manual=3, Nfc=4, App=5

## Regalis.Domain.Enums.AccessResult  [enum] : Enum
   Granted=1, Denied=2, DeviceError=3

## Regalis.Domain.Enums.AccessTokenPurpose  [enum] : Enum
   Booking=1, DayPass=2, MonthlyPass=3, Housekeeping=4, VisitorFuture=5

## Regalis.Domain.Enums.AccessTokenStatus  [enum] : Enum
   Active=1, Used=2, Expired=3, Revoked=4

## Regalis.Domain.Enums.AttendanceStatus  [enum] : Enum
   CheckedIn=1, CheckedOut=2, AutoClosed=3

## Regalis.Domain.Enums.AttendanceSubjectType  [enum] : Enum
   Customer=1, Staff=2

## Regalis.Domain.Enums.BlockReason  [enum] : Enum
   Maintenance=1, InternalUse=2, Cleaning=3, Repair=4, PrivateEvent=5, Housekeeping=6, Other=7

## Regalis.Domain.Enums.BookingPaymentStatus  [enum] : Enum
   Pending=1, Paid=2, Failed=3, Refunded=4, PartiallyRefunded=5

## Regalis.Domain.Enums.BookingSource  [enum] : Enum
   Customer=1, Admin=2, Pass=3

## Regalis.Domain.Enums.BookingStatus  [enum] : Enum
   Pending=1, Confirmed=2, CheckedIn=3, Completed=4, Cancelled=5, Expired=6, NoShow=7

## Regalis.Domain.Enums.CustomerPassStatus  [enum] : Enum
   Active=1, Used=2, Expired=3, Cancelled=4

## Regalis.Domain.Enums.HousekeepingStatus  [enum] : Enum
   Scheduled=1, InProgress=2, Completed=3, Cancelled=4, Missed=5

## Regalis.Domain.Enums.HousekeepingTaskType  [enum] : Enum
   Housekeeping=1, Maintenance=2, Cleaning=3, Repair=4, Inspection=5

## Regalis.Domain.Enums.InventoryTransactionType  [enum] : Enum
   In=1, Out=2, Damage=3, OpeningBalance=4, Adjustment=5

## Regalis.Domain.Enums.InvoiceStatus  [enum] : Enum
   Pending=1, Paid=2, PartiallyPaid=3, Cancelled=4, Refunded=5

## Regalis.Domain.Enums.MenuLevel  [enum] : Enum
   Module=1, Menu=2, SubMenu=3, Screen=4

## Regalis.Domain.Enums.NotificationAudience  [enum] : Enum
   SingleUser=1, AllCustomers=2, AllAdmins=3, ByLocation=4, ByRole=5

## Regalis.Domain.Enums.NotificationCategory  [enum] : Enum
   Booking=1, MeetingRoom=2, CheckInOut=3, Payment=4, Invoice=5, ServiceOrder=6, Event=7, SupportTicket=8, Announcement=9, Registration=10, Pass=11, Housekeeping=12

## Regalis.Domain.Enums.NotificationPriority  [enum] : Enum
   Low=1, Normal=2, High=3

## Regalis.Domain.Enums.OtpChannel  [enum] : Enum
   Email=1, Sms=2

## Regalis.Domain.Enums.OtpPurpose  [enum] : Enum
   Registration=1, ForgotPassword=2, MobileVerification=3

## Regalis.Domain.Enums.PassType  [enum] : Enum
   DayPass=1, MonthlyPass=2

## Regalis.Domain.Enums.PaymentMethod  [enum] : Enum
   Wallet=1, Upi=2, CreditCard=3, DebitCard=4, NetBanking=5, Cash=6, Other=7

## Regalis.Domain.Enums.PaymentStatus  [enum] : Enum
   Pending=1, Success=2, Failed=3, Refunded=4, Cancelled=5

## Regalis.Domain.Enums.PortalType  [enum] : Enum
   Customer=1, Admin=2, Both=3

## Regalis.Domain.Enums.ServiceOrderStatus  [enum] : Enum
   Placed=1, Confirmed=2, Ready=3, Delivered=4, Cancelled=5

## Regalis.Domain.Enums.TicketPriority  [enum] : Enum
   Low=1, Medium=2, High=3, Critical=4

## Regalis.Domain.Enums.TicketSenderType  [enum] : Enum
   Customer=1, Admin=2

## Regalis.Domain.Enums.TicketStatus  [enum] : Enum
   Open=1, Assigned=2, InProgress=3, WaitingForCustomer=4, Resolved=5, Closed=6, Reopened=7

## Regalis.Domain.Enums.UserStatus  [enum] : Enum
   Active=1, Inactive=2, Locked=3, PendingVerification=4

## Regalis.Domain.Enums.UserType  [enum] : Enum
   Customer=1, Admin=2, Staff=3

## Regalis.Domain.Enums.WalletCategory  [enum] : Enum
   Recharge=1, Booking=2, Refund=3, DayPass=4, MonthlyPass=5, ServiceOrder=6, SecurityDeposit=7, AdminAdjustment=8, Cancellation=9

## Regalis.Domain.Enums.WalletDirection  [enum] : Enum
   Credit=1, Debit=2

## Regalis.Domain.Enums.WorkspaceStatus  [enum] : Enum
   Available=1, Occupied=2, Maintenance=3, Disabled=4
```

---

## Entities

```

## Regalis.Domain.Entities.Access.AccessLog  [class]
   PROPS: AccessLogId, AccessMethod, AccessTokenId, DenyReason, DeviceId, DeviceStatus, Direction, IpAddress, IsManualOverride, LocationId, LoggedUtc, OverrideByUserId, OverrideReason, Result, ScannedTokenValue, UserId, WorkspaceId

## Regalis.Domain.Entities.Access.AccessToken  [class] : AuditableEntity
   PROPS: AccessTokenId, BookingId, CustomerPassId, FirstUsedUtc, HousekeepingScheduleId, LastUsedUtc, LocationId, MaxUsageCount, QrPayload, RevokedReason, Status, TokenPurpose, TokenValue, UsageCount, UserId, ValidFromUtc, ValidToUtc, WorkspaceId

## Regalis.Domain.Entities.Access.Attendance  [class] : AuditableEntity
   PROPS: AttendanceDate, AttendanceId, BookingId, CheckInAccessLogId, CheckInMethod, CheckInUtc, CheckOutAccessLogId, CheckOutMethod, CheckOutUtc, CorrectedByUserId, CorrectionReason, ExceptionNotes, HousekeepingScheduleId, IsCorrected, IsException, LocationId, OriginalCheckInUtc, OriginalCheckOutUtc, Status, SubjectType, TotalMinutes, UserId, WorkspaceId

## Regalis.Domain.Entities.Booking.Booking  [class] : AuditableEntity
   PROPS: BaseAmount, BookingDate, BookingId, BookingNumber, BookingSource, CancellationReason, CancelledByUserId, CancelledUtc, CheckInUtc, CheckOutUtc, CreatedByUserId, Customer, CustomerId, CustomerPassId, DiscountAmount, DurationMinutes, EndUtc, Location, LocationId, Notes, PaymentStatus, RefundAmount, RowVersion, StartUtc, Status, StatusHistory, TaxAmount, TotalAmount, Workspace, WorkspaceId, WorkspaceType, WorkspaceTypeId

## Regalis.Domain.Entities.Booking.BookingStatusHistory  [class]
   PROPS: Booking, BookingId, BookingStatusHistoryId, ChangedByUserId, ChangedUtc, FromStatus, Reason, ToStatus

## Regalis.Domain.Entities.Booking.WorkspaceBlock  [class] : AuditableEntity
   PROPS: BlockReason, BlockedByUserId, EndUtc, HousekeepingScheduleId, Location, LocationId, ReasonNotes, ReleasedByUserId, ReleasedUtc, StartUtc, Workspace, WorkspaceBlockId, WorkspaceId

## Regalis.Domain.Entities.Finance.CustomerPass  [class] : AuditableEntity
   PROPS: CustomerId, CustomerPassId, GuestCount, InvoiceId, LocationId, PassNumber, PassPlan, PassPlanId, PassType, PaymentId, Price, PurchaseUtc, Status, TaxAmount, TotalAmount, UsedUtc, ValidFrom, ValidTo

## Regalis.Domain.Entities.Finance.Invoice  [class] : AuditableEntity
   PROPS: BillingAddress, BookingId, CancellationReason, CgstAmount, CgstPercent, CompanyAddress, CompanyGstin, CompanyName, CustomerEmail, CustomerGstin, CustomerId, CustomerMobile, CustomerName, CustomerPassId, DiscountAmount, DueDate, IgstAmount, IgstPercent, InvoiceDate, InvoiceId, InvoiceNumber, Lines, LocationId, Notes, OutstandingAmount, PaidAmount, PaidUtc, PaymentId, PdfPath, ServiceOrderId, SgstAmount, SgstPercent, Status, SubTotal, TaxableAmount, TotalAmount

## Regalis.Domain.Entities.Finance.InvoiceLine  [class]
   PROPS: Description, HsnSacCode, Invoice, InvoiceId, InvoiceLineId, LineAmount, LineNumber, Quantity, TaxAmount, TaxPercent, TotalAmount, UnitPrice

## Regalis.Domain.Entities.Finance.Payment  [class] : AuditableEntity
   PROPS: Amount, BookingId, CustomerId, CustomerPassId, FailureReason, Gateway, GatewayOrderId, GatewayPaymentId, GatewaySignature, InvoiceId, IsSignatureVerified, PaidUtc, PaymentId, PaymentMethod, PaymentNumber, RawGatewayResponse, RefundReference, RefundedAmount, ServiceOrderId, Status

## Regalis.Domain.Entities.Finance.ServiceOrder  [class] : AuditableEntity
   PROPS: CustomerId, InvoiceId, Lines, LocationId, Notes, OrderNumber, OrderUtc, PaymentId, ServiceOrderId, Status, SubTotal, TaxAmount, TotalAmount

## Regalis.Domain.Entities.Finance.ServiceOrderLine  [class]
   PROPS: ItemName, LineAmount, Quantity, ServiceItem, ServiceItemId, ServiceOrder, ServiceOrderId, ServiceOrderLineId, UnitPrice

## Regalis.Domain.Entities.Finance.Wallet  [class] : AuditableEntity
   PROPS: Balance, Customer, CustomerId, LastTransactionUtc, RowVersion, TotalCredited, TotalDebited, Transactions, WalletId

## Regalis.Domain.Entities.Finance.WalletTransaction  [class] : AuditableEntity
   PROPS: Amount, BalanceAfter, Category, CustomerId, Direction, Narration, PaymentId, PerformedByUserId, ReferenceId, ReferenceType, TransactionNumber, TransactionUtc, Wallet, WalletId, WalletTransactionId

## Regalis.Domain.Entities.Identity.AuditLog  [class]
   PROPS: Action, AuditLogId, CorrelationId, EntityId, EntityName, IpAddress, ModuleName, NewValue, OldValue, TimestampUtc, UserAgent, UserId, UserName

## Regalis.Domain.Entities.Identity.LoginHistory  [class]
   PROPS: AttemptedEmail, AttemptedUtc, FailureReason, IpAddress, IsSuccess, LoginHistoryId, UserAgent, UserId

## Regalis.Domain.Entities.Identity.Menu  [class] : AuditableEntity
   PROPS: Children, DisplayName, DisplayOrder, Icon, IsVisible, MenuCode, MenuId, MenuLevel, MenuName, ParentMenu, ParentMenuId, Permission, PermissionId, PortalType, RoleMenus, Route

## Regalis.Domain.Entities.Identity.Otp  [class]
   PROPS: AttemptCount, Channel, CodeHash, ConsumedUtc, CreatedUtc, Destination, ExpiresUtc, MaxAttempts, OtpId, Purpose, UserId

## Regalis.Domain.Entities.Identity.PasswordHistory  [class]
   PROPS: ChangedUtc, PasswordHash, PasswordHistoryId, User, UserId

## Regalis.Domain.Entities.Identity.Permission  [class] : AuditableEntity
   PROPS: ActionName, Description, DisplayOrder, ModuleName, PermissionCode, PermissionId, RolePermissions

## Regalis.Domain.Entities.Identity.RefreshToken  [class]
   PROPS: ConsumedUtc, CreatedByIp, CreatedUtc, ExpiresUtc, FamilyId, RefreshTokenId, ReplacedByTokenHash, RevokedReason, RevokedUtc, TokenHash, User, UserId

## Regalis.Domain.Entities.Identity.Role  [class] : AuditableEntity
   PROPS: Description, IsSystemRole, PortalType, RoleCode, RoleId, RoleMenus, RoleName, RolePermissions, UserRoles

## Regalis.Domain.Entities.Identity.RoleMenu  [class]
   PROPS: Menu, MenuId, Role, RoleId, RoleMenuId

## Regalis.Domain.Entities.Identity.RolePermission  [class]
   PROPS: Permission, PermissionId, Role, RoleId, RolePermissionId

## Regalis.Domain.Entities.Identity.User  [class] : AuditableEntity
   PROPS: Address, CompanyName, DefaultLocation, DefaultLocationId, Email, FailedLoginCount, FullName, IsEmailVerified, IsMobileVerified, LastLoginUtc, LastPasswordChangeUtc, LockoutEndUtc, MobileNumber, MustChangePassword, PasswordHash, PhotoPath, RefreshTokens, Status, UserId, UserRoles, UserType

## Regalis.Domain.Entities.Identity.UserRole  [class]
   PROPS: AssignedBy, AssignedDateUtc, Role, RoleId, User, UserId, UserRoleId

## Regalis.Domain.Entities.Masters.Amenity  [class] : AuditableEntity
   PROPS: AmenityId, AmenityName, DisplayOrder, IconName, WorkspaceAmenities

## Regalis.Domain.Entities.Masters.AppSetting  [class] : AuditableEntity
   PROPS: AppSettingId, DataType, Description, IsEditableByAdmin, IsSecret, SettingGroup, SettingKey, SettingValue

## Regalis.Domain.Entities.Masters.Banner  [class] : AuditableEntity
   PROPS: BannerId, DisplayOrder, ImagePath, LinkUrl, Location, LocationId, PortalType, PromotionalPrice, Subtitle, Title, ValidFromUtc, ValidToUtc

## Regalis.Domain.Entities.Masters.Location  [class] : AuditableEntity
   PROPS: AddressLine1, AddressLine2, City, ContactNumber, Description, Email, ImagePath, IsDefault, Latitude, LocationCode, LocationId, LocationName, Longitude, PostalCode, State, WorkingDays, WorkingHoursEnd, WorkingHoursStart, Workspaces

## Regalis.Domain.Entities.Masters.NumberSequence  [class]
   PROPS: CurrentValue, FinancialYear, NumberSequenceId, Prefix, SequenceKey

## Regalis.Domain.Entities.Masters.PassPlan  [class] : AuditableEntity
   PROPS: Description, DisplayOrder, ImagePath, Location, LocationId, MaxGuests, PassPlanId, PassType, PlanCode, PlanName, Price, TermsAndConditions, ValidityDays

## Regalis.Domain.Entities.Masters.ServiceItem  [class] : AuditableEntity
   PROPS: Category, Description, DisplayOrder, ImagePath, IsAvailable, ItemCode, ItemName, Location, LocationId, Price, ServiceItemId, Unit

## Regalis.Domain.Entities.Masters.TicketCategory  [class] : AuditableEntity
   PROPS: CategoryName, DisplayOrder, IssueTypes, TicketCategoryId

## Regalis.Domain.Entities.Masters.TicketIssueType  [class] : AuditableEntity
   PROPS: DisplayOrder, IssueTypeName, TicketCategory, TicketCategoryId, TicketIssueTypeId

## Regalis.Domain.Entities.Masters.Workspace  [class] : AuditableEntity
   PROPS: Capacity, Description, Equipment, FloorNumber, ImagePath, IsBookable, Location, LocationId, PricePerDay, PricePerHour, RowVersion, Status, WorkspaceAmenities, WorkspaceCode, WorkspaceId, WorkspaceName, WorkspaceType, WorkspaceTypeId

## Regalis.Domain.Entities.Masters.WorkspaceAmenity  [class]
   PROPS: Amenity, AmenityId, Workspace, WorkspaceAmenityId, WorkspaceId

## Regalis.Domain.Entities.Masters.WorkspaceType  [class] : AuditableEntity
   PROPS: Description, DisplayOrder, IconName, RequiresCapacity, TypeCode, TypeName, WorkspaceTypeId, Workspaces

## Regalis.Domain.Entities.Operations.HousekeepingSchedule  [class] : AuditableEntity
   PROPS: AreaName, AssignedByUserId, BlocksWorkspace, CheckInUtc, CheckOutUtc, CompletionNotes, EndUtc, HousekeepingScheduleId, LocationId, Notes, ScheduleNumber, ScheduledDate, StaffUserId, StartUtc, Status, TaskDescription, TaskType, WorkspaceBlockId, WorkspaceId

## Regalis.Domain.Entities.Operations.InventoryItem  [class] : AuditableEntity
   PROPS: Category, Description, ImagePath, InventoryItemId, ItemCode, ItemName, LocationId, OpeningQuantity, ReorderLevel, Transactions, Unit, UnitCost

## Regalis.Domain.Entities.Operations.InventoryTransaction  [class] : AuditableEntity
   PROPS: DamageCause, DamageDescription, EnteredByUserId, InventoryItem, InventoryItemId, InventoryTransactionId, LocationId, Notes, PhotoPath, Quantity, ReferenceNumber, SourceOrDestination, TransactionDate, TransactionNumber, TransactionType, UnitCost

## Regalis.Domain.Entities.Operations.Notification  [class] : AuditableEntity
   PROPS: ActionLabel, ActionRoute, Audience, AudienceRefId, Category, CreatedByUserId, IsRead, Message, NotificationId, Priority, ReadUtc, ReferenceId, ReferenceType, ScheduledForUtc, SentUtc, Title, UserId

## Regalis.Domain.Entities.Operations.Ticket  [class] : AuditableEntity
   PROPS: AssignedToUserId, AssignedUtc, Attachments, ClosedUtc, CustomerId, Description, FirstResponseUtc, LocationId, Messages, Priority, ReopenCount, ResolutionNotes, ResolvedUtc, Status, Subject, TicketCategory, TicketCategoryId, TicketId, TicketIssueType, TicketIssueTypeId, TicketNumber

## Regalis.Domain.Entities.Operations.TicketAttachment  [class]
   PROPS: ContentType, FileName, FilePath, FileSizeBytes, StoredFileName, Ticket, TicketAttachmentId, TicketId, TicketMessageId, UploadedByUserId, UploadedUtc

## Regalis.Domain.Entities.Operations.TicketMessage  [class] : AuditableEntity
   PROPS: IsInternalNote, Message, SenderType, SenderUserId, SentUtc, Ticket, TicketId, TicketMessageId
```
