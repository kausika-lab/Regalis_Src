# ROLE_PERMISSION_MATRIX.md
**Regalis WorkSpace — dynamic role-based access control**

Nothing in this document is hard-coded in either tier. Roles, permissions, menus and
their mappings are rows in `MST_Role`, `MST_Permission`, `MST_Menu`, `MAP_RolePermission`
and `MAP_RoleMenu`. This file records the **seeded** state shipped by
`Database_Seed.sql`.

---

## 1. Permission code format

```
MODULE.ACTION
```

Actions (master prompt §6): `VIEW` `CREATE` `EDIT` `DELETE` `APPROVE` `EXPORT` `IMPORT` `PRINT`

## 2. Permission catalogue (118 permissions across 26 modules)

| Module | Permissions |
|--------|-------------|
| `DASHBOARD` | VIEW |
| `USER` | VIEW CREATE EDIT DELETE EXPORT |
| `ROLE` | VIEW CREATE EDIT DELETE |
| `PERMISSION` | VIEW EDIT |
| `MENU` | VIEW CREATE EDIT DELETE |
| `CUSTOMER` | VIEW CREATE EDIT DELETE EXPORT |
| `LOCATION` | VIEW CREATE EDIT DELETE |
| `WORKSPACE` | VIEW CREATE EDIT DELETE |
| `CALENDAR` | VIEW CREATE EDIT |
| `BOOKING` | VIEW CREATE EDIT DELETE APPROVE EXPORT PRINT |
| `PAYMENT` | VIEW CREATE EXPORT |
| `WALLET` | VIEW CREATE EDIT EXPORT |
| `INVOICE` | VIEW CREATE EDIT EXPORT PRINT |
| `PASS` | VIEW CREATE EDIT DELETE EXPORT |
| `ACCESS` | VIEW CREATE EDIT OVERRIDE EXPORT |
| `ATTENDANCE` | VIEW CREATE EDIT APPROVE EXPORT |
| `PAYROLL` | VIEW EXPORT |
| `INVENTORY` | VIEW CREATE EDIT DELETE EXPORT |
| `HOUSEKEEPING` | VIEW CREATE EDIT DELETE APPROVE |
| `SERVICE` | VIEW CREATE EDIT DELETE |
| `BANNER` | VIEW CREATE EDIT DELETE |
| `NOTIFICATION` | VIEW CREATE EDIT DELETE |
| `TICKET` | VIEW CREATE EDIT DELETE APPROVE EXPORT |
| `REPORT` | VIEW EXPORT PRINT |
| `SETTING` | VIEW EDIT |
| `AUDIT` | VIEW EXPORT |

`ACCESS.OVERRIDE` is a deliberate ninth action. It exists only so that the manual
fallback when the access-control device is down (Design Invariant 9) is a distinct,
separately grantable, separately audited privilege.

---

## 3. Seeded roles

| Code | Name | Portal | Intent |
|------|------|--------|--------|
| `CUSTOMER` | Customer | Customer | A registered member. Sees only their own data. |
| `ADMIN` | Administrator | Admin | Full operational control. Seeded to `enquiry@amstag.in`. |
| `MANAGER` | Operations Manager | Admin | Day-to-day operations, no user/role/menu/settings administration. |
| `FINANCE` | Finance | Admin | Money and reporting, read-only on operations. |
| `SUPPORT` | Support Executive | Admin | Tickets, customers, bookings — read-mostly. |
| `HOUSEKEEPING` | Housekeeping Staff | Admin | Owns a login and access tokens; sees only their own schedule. No portal screens in Phase 1. |

The document baseline requires only **Customer** and **Admin** to be live. The other four
are seeded inactive-but-present so the engine is proven and Phase 2 needs no schema
change. Set `IsActive = 0` on them to hide them entirely.

---

## 4. Role → permission matrix

`F` = full (all actions for that module) · `R` = VIEW only · `–` = none ·
explicit action list where partial.

| Module | CUSTOMER | ADMIN | MANAGER | FINANCE | SUPPORT | HOUSEKEEPING |
|--------|----------|-------|---------|---------|---------|--------------|
| DASHBOARD | VIEW | F | VIEW | VIEW | VIEW | – |
| USER | – | F | – | – | – | – |
| ROLE | – | F | – | – | – | – |
| PERMISSION | – | F | – | – | – | – |
| MENU | – | F | – | – | – | – |
| CUSTOMER | – | F | VIEW EDIT EXPORT | VIEW EXPORT | VIEW EDIT | – |
| LOCATION | VIEW | F | VIEW EDIT | R | R | – |
| WORKSPACE | VIEW | F | F | R | R | R |
| CALENDAR | VIEW | F | F | R | R | R |
| BOOKING | VIEW CREATE EDIT | F | F | VIEW EXPORT | VIEW EDIT | – |
| PAYMENT | VIEW CREATE | F | VIEW | F | R | – |
| WALLET | VIEW CREATE | F | VIEW | F | R | – |
| INVOICE | VIEW PRINT | F | VIEW PRINT | F | R | – |
| PASS | VIEW CREATE | F | F | VIEW EXPORT | R | – |
| ACCESS | VIEW | F | VIEW CREATE EDIT OVERRIDE | R | R | VIEW |
| ATTENDANCE | VIEW *(own)* | F | F | VIEW EXPORT | – | VIEW *(own)* |
| PAYROLL | – | F | – | F | – | – |
| INVENTORY | – | F | F | VIEW EXPORT | – | R |
| HOUSEKEEPING | – | F | F | – | R | VIEW *(own)* |
| SERVICE | VIEW CREATE | F | F | R | R | – |
| BANNER | VIEW | F | F | – | – | – |
| NOTIFICATION | VIEW EDIT *(own)* | F | VIEW CREATE | – | VIEW CREATE | VIEW |
| TICKET | VIEW CREATE EDIT *(own)* | F | F | – | F | – |
| REPORT | – | F | VIEW EXPORT PRINT | F | VIEW EXPORT | – |
| SETTING | – | F | – | – | – | – |
| AUDIT | – | F | – | VIEW EXPORT | – | – |

*(own)* means the permission is granted but the **service layer additionally scopes rows
to the calling user** via `ICurrentUser`. A customer with `BOOKING.VIEW` can never read
another customer's booking, whatever id they pass.

---

## 5. Menu → role mapping (seeded)

### Customer portal
```
Dashboard                    CUSTOMER
Calendar / Book Workspace    CUSTOMER
  ├ All / Desk / Cabin / Meeting Room   (filters, one screen)
My Bookings                  CUSTOMER
Day Pass                     CUSTOMER
Monthly Pass                 CUSTOMER
Wallet                       CUSTOMER
Invoices                     CUSTOMER
My QR / Access               CUSTOMER
My Work Status               CUSTOMER
Services                     CUSTOMER
Notifications                CUSTOMER
Support / Ticket             CUSTOMER
  ├ Raise Ticket
  └ All Tickets
Contact Us                   CUSTOMER
Profile                      CUSTOMER
```

### Admin portal
```
Dashboard                    ADMIN MANAGER FINANCE SUPPORT
Customers                    ADMIN MANAGER FINANCE SUPPORT
Workspaces                   ADMIN MANAGER
  ├ Desks / Cabins / Meeting Rooms
Calendar                     ADMIN MANAGER
Bookings                     ADMIN MANAGER SUPPORT
Access Control               ADMIN MANAGER
  ├ Scan / Validate
  └ Access Logs
Attendance                   ADMIN MANAGER FINANCE
  └ Payroll Export           ADMIN FINANCE
Billing & Finance            ADMIN FINANCE
  ├ Payments
  ├ Invoices
  └ Wallet
Day Pass                     ADMIN MANAGER
Monthly Pass                 ADMIN MANAGER
Inventory                    ADMIN MANAGER
  ├ Items
  └ Transactions
Maintenance / Housekeeping   ADMIN MANAGER
Location                     ADMIN
Services / Coffee            ADMIN MANAGER
Banners                      ADMIN MANAGER
Notifications                ADMIN MANAGER SUPPORT
Support / Tickets            ADMIN MANAGER SUPPORT
Reports & Analysis           ADMIN MANAGER FINANCE
  └ 16 reports
Administration               ADMIN
  ├ Users
  ├ Roles & Permissions
  ├ Menu Management
  └ Audit Trail
Settings                     ADMIN
Admin Profile                ADMIN MANAGER FINANCE SUPPORT
```

---

## 6. Enforcement — the part that actually matters

Frontend menu filtering and button hiding are **cosmetic**. Three independent gates run
on every request:

1. **Authentication** — a valid, unexpired, unrevoked JWT.
2. **Permission** — `[HasPermission("BOOKING.CREATE")]` on the controller action.
   Resolved by `PermissionPolicyProvider` against the `perm` claims stamped at login.
   Failure → `403` + a `AUTHORIZATION_FAILED` audit row naming the user, endpoint and
   missing permission.
3. **Row scope** — the service layer intersects the query with `ICurrentUser`. This is
   what stops `GET /api/bookings/9931` from leaking someone else's booking.

### QA role-test checklist (master prompt §26)

For each of `ADMIN`, `MANAGER`, `FINANCE`, `SUPPORT`, `CUSTOMER`, and an unauthenticated
caller:

- [ ] Sidebar shows only the mapped menus
- [ ] Direct navigation to an unmapped route renders the Forbidden screen
- [ ] Direct `GET/POST/PUT/DELETE` to a restricted endpoint with a **valid token** returns `403`
- [ ] Action buttons absent where the permission is absent
- [ ] Customer A cannot read Customer B's booking, invoice, wallet, ticket or QR by id
- [ ] Expired token → `401`; revoked refresh token → `401` and no rotation
- [ ] Permission revoked mid-session takes effect within one access-token lifetime (15 min) or immediately on refresh
- [ ] `ACCESS.OVERRIDE` absent ⇒ manual override endpoint returns `403` and writes an audit row
