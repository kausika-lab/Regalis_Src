# Regalis — endpoint → permission map

Machine-extracted from `[Http*]`, `[Route]`, `[HasPermission]`,
`[AllowAnonymous]` and `[EnableRateLimiting]` attribute blobs in
`Regalis.API.dll`. **Treat as authoritative** — this is the compiled truth,
not a doc comment.

Regenerate after a release with:
`python3 scripts/routes.py api/Regalis.API.dll`

## How to read this

- `perm=CODE` — action requires that permission claim in the JWT. A valid
  token without it gets 403 + an `AUTHORIZATION_FAILED` audit row.
- `ANON` — `[AllowAnonymous]`.
- `rate=auth|webhook` — non-default rate-limit policy.
- **No `perm` and no `ANON`** means authenticated but ungated: these are
  own-record endpoints scoped in the service via `ICurrentUser`
  (profile, notifications, attendance self-service, `/menus/my`,
  `/dashboard/customer`, `/banners/active`, `/locations`, `/workspace-types`,
  `/amenities`, `/pass-plans/active`, `/settings/public`, auth session
  endpoints). Absence of a permission attribute is **not** absence of
  authorization — the scoping is in the service layer.

## Notable pairs

- `GET /api/bookings` (admin list, `BOOKING.VIEW`) vs
  `GET /api/bookings/my` (own rows, same permission — scoping is by route +
  service, not permission).
- `GET /api/wallet/me` vs `GET /api/wallet/{customerId}` — same
  `WALLET.VIEW`; the admin route takes an id, the customer route never does.
- `POST /api/access/manual-override` is the only `ACCESS.OVERRIDE` action —
  a distinct, separately grantable, separately audited privilege (Invariant 9).
- `POST /api/payments/webhook` is anonymous by necessity; it is
  signature-verified and idempotent on `GatewayPaymentId`.
- `POST /api/payments/sandbox-complete` requires `PAYMENT.CREATE` and returns
  422 unless `Payments:Provider = Sandbox`.

---

## Full map

```

### AccessController   [base: /api/access]
    GET    /api/access/my-tokens                                GetMyTokens                perm=ACCESS.VIEW
    POST   /api/access/validate                                 Validate                   perm=ACCESS.CREATE
    POST   /api/access/manual-override                          ManualOverride             perm=ACCESS.OVERRIDE
    POST   /api/access/revoke/{tokenId:long}                    Revoke                     perm=ACCESS.EDIT
    GET    /api/access/logs                                     GetLogs                    perm=ACCESS.VIEW

### AttendanceController   [base: /api/attendance]
    POST   /api/attendance/check-in                             CheckIn                    
    POST   /api/attendance/check-out                            CheckOut                   
    GET    /api/attendance/my                                   GetMy                      
    GET    /api/attendance/my/open                              GetMyOpen                  
    GET    /api/attendance                                      GetAll                     perm=ATTENDANCE.VIEW
    PUT    /api/attendance/{id:long}                            Correct                    perm=ATTENDANCE.EDIT

### PayrollController   [base: /api/payroll]
    GET    /api/payroll/export                                  Export                     perm=PAYROLL.EXPORT

### AuditController   [base: /api/audit]
    GET    /api/audit                                           GetAll                     perm=AUDIT.VIEW
    GET    /api/audit/modules                                   GetModules                 perm=AUDIT.VIEW
    GET    /api/audit/export                                    Export                     perm=AUDIT.EXPORT

### AuthController   [base: /api/auth]
    POST   /api/auth/login                                      Login                      ANON rate=auth
    POST   /api/auth/register                                   Register                   ANON rate=auth
    POST   /api/auth/verify-otp                                 VerifyOtp                  ANON rate=auth
    POST   /api/auth/resend-otp                                 ResendOtp                  ANON rate=auth
    POST   /api/auth/forgot-password                            ForgotPassword             ANON rate=auth
    POST   /api/auth/reset-password                             ResetPassword              ANON rate=auth
    POST   /api/auth/change-password                            ChangePassword             
    POST   /api/auth/refresh                                    Refresh                    ANON
    POST   /api/auth/logout                                     Logout                     
    GET    /api/auth/me                                         Me                         

### AvailabilityController   [base: /api/availability]
    GET    /api/availability/slots                              GetSlots                   perm=BOOKING.VIEW
    GET    /api/availability/workspaces                         GetWorkspaces              perm=BOOKING.VIEW
    POST   /api/availability/check                              Check                      perm=BOOKING.VIEW

### BannersController   [base: /api/banners]
    GET    /api/banners                                         GetAll                     perm=BANNER.VIEW
    GET    /api/banners/active                                  GetActive                  
    POST   /api/banners                                         Create                     perm=BANNER.CREATE
    PUT    /api/banners/{id:int}                                Update                     perm=BANNER.EDIT
    DELETE /api/banners/{id:int}                                Delete                     perm=BANNER.DELETE

### BookingsController   [base: /api/bookings]
    POST   /api/bookings/{id:long}/cancel                       Cancel                     perm=BOOKING.EDIT
    PUT    /api/bookings/{id:long}                              Reschedule                 perm=BOOKING.EDIT
    POST   /api/bookings/{id:long}/extend                       Extend                     perm=BOOKING.EDIT
    POST   /api/bookings/quote                                  Quote                      perm=BOOKING.VIEW
    POST   /api/bookings                                        CreateHold                 perm=BOOKING.CREATE
    GET    /api/bookings/{id:long}                              GetById                    perm=BOOKING.VIEW
    POST   /api/bookings/{id:long}/confirm                      Confirm                    perm=BOOKING.CREATE
    POST   /api/bookings/admin                                  AdminCreate                perm=BOOKING.CREATE
    GET    /api/bookings/my                                     GetMy                      perm=BOOKING.VIEW
    GET    /api/bookings                                        GetAll                     perm=BOOKING.VIEW

### CalendarController   [base: /api/calendar]
    GET    /api/calendar                                        Get                        perm=CALENDAR.VIEW
    GET    /api/calendar/workspace/{id:int}                     GetWorkspace               perm=CALENDAR.VIEW
    POST   /api/calendar/block                                  Block                      perm=CALENDAR.CREATE
    POST   /api/calendar/unblock/{blockId:long}                 Unblock                    perm=CALENDAR.EDIT

### CustomersController   [base: /api/customers]
    GET    /api/customers                                       GetAll                     perm=CUSTOMER.VIEW
    GET    /api/customers/{id:long}                             GetById                    perm=CUSTOMER.VIEW

### DashboardController   [base: /api/dashboard]
    GET    /api/dashboard/admin                                 GetAdmin                   perm=DASHBOARD.VIEW
    GET    /api/dashboard/customer                              GetCustomer                

### HousekeepingController   [base: /api/housekeeping]
    GET    /api/housekeeping/schedules                          GetSchedules               perm=HOUSEKEEPING.VIEW
    GET    /api/housekeeping/my-schedules                       GetMySchedules             perm=HOUSEKEEPING.VIEW
    POST   /api/housekeeping/schedules                          Create                     perm=HOUSEKEEPING.CREATE
    PUT    /api/housekeeping/schedules/{id:long}                Update                     perm=HOUSEKEEPING.EDIT
    POST   /api/housekeeping/schedules/{id:long}/cancel         Cancel                     perm=HOUSEKEEPING.EDIT
    POST   /api/housekeeping/schedules/{id:long}/check-in       CheckIn                    perm=HOUSEKEEPING.EDIT
    POST   /api/housekeeping/schedules/{id:long}/check-out      CheckOut                   perm=HOUSEKEEPING.EDIT

### InventoryController   [base: /api/inventory]
    GET    /api/inventory/items                                 GetItems                   perm=INVENTORY.VIEW
    GET    /api/inventory/items/{id:int}                        GetItem                    perm=INVENTORY.VIEW
    POST   /api/inventory/items                                 CreateItem                 perm=INVENTORY.CREATE
    PUT    /api/inventory/items/{id:int}                        UpdateItem                 perm=INVENTORY.EDIT
    DELETE /api/inventory/items/{id:int}                        DeleteItem                 perm=INVENTORY.DELETE
    GET    /api/inventory/stock                                 GetStock                   perm=INVENTORY.VIEW
    POST   /api/inventory/transactions                          CreateTransaction          perm=INVENTORY.CREATE
    GET    /api/inventory/transactions                          GetTransactions            perm=INVENTORY.VIEW
    GET    /api/inventory/export                                Export                     perm=INVENTORY.EXPORT

### InvoicesController   [base: /api/invoices]
    GET    /api/invoices                                        GetAll                     perm=INVOICE.VIEW
    GET    /api/invoices/{id:long}                              GetById                    perm=INVOICE.VIEW
    GET    /api/invoices/my                                     GetMy                      perm=INVOICE.VIEW
    GET    /api/invoices/{id:long}/pdf                          GetPdf                     perm=INVOICE.PRINT

### LocationsController   [base: /api/locations]
    GET    /api/locations                                       GetAll                     
    GET    /api/locations/{id:int}                              GetById                    
    POST   /api/locations                                       Create                     perm=LOCATION.CREATE
    PUT    /api/locations/{id:int}                              Update                     perm=LOCATION.EDIT
    DELETE /api/locations/{id:int}                              Delete                     perm=LOCATION.DELETE

### MenusController   [base: /api/menus]
    GET    /api/menus/my                                        My                         
    GET    /api/menus                                           GetAll                     perm=MENU.VIEW
    POST   /api/menus                                           Create                     perm=MENU.CREATE
    PUT    /api/menus/{id:int}                                  Update                     perm=MENU.EDIT
    DELETE /api/menus/{id:int}                                  Delete                     perm=MENU.DELETE

### NotificationsController   [base: /api/notifications]
    GET    /api/notifications/my                                GetMy                      
    GET    /api/notifications/my/unread-count                   GetUnreadCount             
    PATCH  /api/notifications/{id:long}/read                    MarkRead                   
    POST   /api/notifications/read-all                          MarkAllRead                
    DELETE /api/notifications/{id:long}                         Delete                     
    POST   /api/notifications/broadcast                         Broadcast                  perm=NOTIFICATION.CREATE

### PassPlansController   [base: /api/pass-plans]
    GET    /api/pass-plans/active                               GetActive                  
    GET    /api/pass-plans                                      GetAll                     perm=PASS.VIEW
    POST   /api/pass-plans                                      Create                     perm=PASS.CREATE
    PUT    /api/pass-plans/{id:int}                             Update                     perm=PASS.EDIT
    DELETE /api/pass-plans/{id:int}                             Delete                     perm=PASS.DELETE

### PassesController   [base: /api/passes]
    POST   /api/passes/order                                    CreateOrder                perm=PASS.CREATE
    POST   /api/passes/purchase                                 Purchase                   perm=PASS.CREATE
    GET    /api/passes/my                                       GetMy                      perm=PASS.VIEW
    GET    /api/passes                                          GetAll                     perm=PASS.VIEW
    POST   /api/passes/{id:long}/cancel                         Cancel                     perm=PASS.EDIT

### PaymentsController   [base: /api/payments]
    GET    /api/payments                                        GetAll                     perm=PAYMENT.VIEW
    GET    /api/payments/{id:long}                              GetById                    perm=PAYMENT.VIEW
    POST   /api/payments/order                                  CreateOrder                perm=PAYMENT.CREATE
    POST   /api/payments/sandbox-complete                       SandboxComplete            perm=PAYMENT.CREATE
    POST   /api/payments/webhook                                Webhook                    ANON rate=webhook

### ProfileController   [base: /api/profile]
    GET    /api/profile                                         Get                        
    PUT    /api/profile                                         Update                     
    POST   /api/profile/photo                                   SetPhoto                   
    GET    /api/profile/photo                                   GetPhoto                   

### ReportsController   [base: /api/reports]
    GET    /api/reports                                         GetCatalogue               perm=REPORT.VIEW
    GET    /api/reports/{key}                                   Get                        perm=REPORT.VIEW
    GET    /api/reports/{key}/export                            Export                     perm=REPORT.EXPORT

### RolesController   [base: /api/roles]
    GET    /api/roles                                           GetAll                     perm=ROLE.VIEW
    POST   /api/roles                                           Create                     perm=ROLE.CREATE
    PUT    /api/roles/{id:int}                                  Update                     perm=ROLE.EDIT
    DELETE /api/roles/{id:int}                                  Delete                     perm=ROLE.DELETE
    GET    /api/roles/{id:int}/permissions                      GetPermissions             perm=PERMISSION.VIEW
    PUT    /api/roles/{id:int}/permissions                      UpdatePermissions          perm=PERMISSION.EDIT
    GET    /api/roles/{id:int}/menus                            GetMenus                   perm=ROLE.VIEW
    PUT    /api/roles/{id:int}/menus                            UpdateMenus                perm=ROLE.EDIT

### PermissionsController   [base: /api/permissions]
    GET    /api/permissions                                     GetAll                     perm=PERMISSION.VIEW

### ServicesController   [base: /api/services]
    GET    /api/services                                        GetAll                     perm=SERVICE.VIEW
    GET    /api/services/categories                             GetCategories              perm=SERVICE.VIEW
    GET    /api/services/{id:int}                               GetById                    perm=SERVICE.VIEW
    POST   /api/services                                        Create                     perm=SERVICE.EDIT
    PUT    /api/services/{id:int}                               Update                     perm=SERVICE.EDIT
    DELETE /api/services/{id:int}                               Delete                     perm=SERVICE.DELETE

### SettingsController   [base: /api/settings]
    GET    /api/settings                                        GetAll                     perm=SETTING.VIEW
    PUT    /api/settings                                        Update                     perm=SETTING.EDIT
    GET    /api/settings/public                                 GetPublic                  ANON

### TicketCategoriesController   [base: /api/ticket-categories]
    GET    /api/ticket-categories                               GetAll                     perm=TICKET.VIEW

### TicketsController   [base: /api/tickets]
    POST   /api/tickets                                         Create                     perm=TICKET.CREATE
    GET    /api/tickets/my                                      GetMy                      perm=TICKET.VIEW
    GET    /api/tickets                                         GetAll                     perm=TICKET.VIEW
    GET    /api/tickets/{id:long}                               GetById                    perm=TICKET.VIEW
    POST   /api/tickets/{id:long}/messages                      AddMessage                 perm=TICKET.EDIT
    POST   /api/tickets/{id:long}/attachments                   AddAttachment              perm=TICKET.EDIT
    GET    /api/tickets/{id:long}/attachments/{attachmentId:long} DownloadAttachment         perm=TICKET.VIEW
    PATCH  /api/tickets/{id:long}/assign                        Assign                     perm=TICKET.EDIT
    PATCH  /api/tickets/{id:long}/status                        ChangeStatus               perm=TICKET.EDIT
    PATCH  /api/tickets/{id:long}/priority                      ChangePriority             perm=TICKET.EDIT
    POST   /api/tickets/{id:long}/close                         Close                      perm=TICKET.EDIT
    POST   /api/tickets/{id:long}/reopen                        Reopen                     perm=TICKET.EDIT
    GET    /api/tickets/export                                  Export                     perm=TICKET.EXPORT

### UsersController   [base: /api/users]
    GET    /api/users                                           GetAll                     perm=USER.VIEW
    GET    /api/users/{id:long}                                 GetById                    perm=USER.VIEW
    POST   /api/users                                           Create                     perm=USER.CREATE
    PUT    /api/users/{id:long}                                 Update                     perm=USER.EDIT
    PATCH  /api/users/{id:long}/activate                        Activate                   perm=USER.EDIT
    PATCH  /api/users/{id:long}/deactivate                      Deactivate                 perm=USER.EDIT
    PATCH  /api/users/{id:long}/lock                            Lock                       perm=USER.EDIT
    PATCH  /api/users/{id:long}/unlock                          Unlock                     perm=USER.EDIT
    POST   /api/users/{id:long}/reset-password                  ResetPassword              perm=USER.EDIT
    DELETE /api/users/{id:long}                                 Delete                     perm=USER.DELETE

### WalletController   [base: /api/wallet]
    GET    /api/wallet/me                                       GetMyWallet                perm=WALLET.VIEW
    GET    /api/wallet/me/transactions                          GetMyTransactions          perm=WALLET.VIEW
    POST   /api/wallet/recharge                                 Recharge                   perm=WALLET.CREATE
    POST   /api/wallet/recharge/verify                          VerifyRecharge             perm=WALLET.CREATE
    GET    /api/wallet/{customerId:long}                        GetWallet                  perm=WALLET.VIEW
    GET    /api/wallet/{customerId:long}/transactions           GetTransactions            perm=WALLET.VIEW
    POST   /api/wallet/{customerId:long}/adjust                 Adjust                     perm=WALLET.EDIT

### WorkspacesController   [base: /api/workspaces]
    GET    /api/workspaces                                      GetAll                     perm=WORKSPACE.VIEW
    GET    /api/workspaces/{id:int}                             GetById                    perm=WORKSPACE.VIEW
    POST   /api/workspaces                                      Create                     perm=WORKSPACE.CREATE
    PUT    /api/workspaces/{id:int}                             Update                     perm=WORKSPACE.EDIT
    PATCH  /api/workspaces/{id:int}/status                      UpdateStatus               perm=WORKSPACE.EDIT
    DELETE /api/workspaces/{id:int}                             Delete                     perm=WORKSPACE.DELETE

### WorkspaceTypesController   [base: /api/workspace-types]
    GET    /api/workspace-types                                 GetAll                     

### AmenitiesController   [base: /api/amenities]
    GET    /api/amenities                                       GetAll                     
    POST   /api/amenities                                       Create                     perm=WORKSPACE.CREATE
```
