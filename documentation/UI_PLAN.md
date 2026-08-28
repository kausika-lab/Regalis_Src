# UI_PLAN.md
**Regalis WorkSpace — front-end design and screen plan**

---

## 1. Brand and design tokens

Black, Gold and White, per the requirement that the interface read as premium, elegant
and modern.

```css
--regalis-black:        #0B0B0D;   /* primary surface for chrome            */
--regalis-black-soft:   #16161A;   /* raised surface on dark                */
--regalis-black-line:   #26262C;   /* borders on dark                       */
--regalis-gold:         #C9A227;   /* primary action, active state, accent  */
--regalis-gold-light:   #E3C767;   /* hover                                 */
--regalis-gold-dark:    #9C7C15;   /* pressed                               */
--regalis-gold-tint:    #FBF6E6;   /* selected row, subtle fill on light    */
--regalis-white:        #FFFFFF;
--surface:              #F7F7F9;   /* app background on light               */
--text-primary:         #16161A;
--text-secondary:       #5C5C66;
--text-muted:           #8A8A96;
--border:               #E4E4EA;
--success: #1E8E5A;  --warning: #C77A0A;  --danger: #C0392B;  --info: #2E6BB8;
--radius-sm: 6px; --radius-md: 10px; --radius-lg: 16px;
--shadow-sm: 0 1px 2px rgba(11,11,13,.06);
--shadow-md: 0 4px 16px rgba(11,11,13,.10);
```

Type scale: 12 / 13 / 14 / 16 / 20 / 24 / 32 px. Body 14 px, 1.55 line-height.
Font stack: `"Inter", "Segoe UI", system-ui, -apple-system, sans-serif`.
Spacing scale: 4 / 8 / 12 / 16 / 24 / 32 / 48 px.

Status colours are used consistently everywhere a status appears — the same green means
the same thing on a booking card, a table row and a report.

| Status | Colour |
|--------|--------|
| Confirmed / Paid / Granted / Available / Active | success |
| Pending / Waiting | warning |
| Cancelled / Failed / Denied / Expired | danger |
| Completed / Closed | neutral |
| CheckedIn / InProgress | info |
| Blocked / Maintenance | gold-dark |

---

## 2. Responsive breakpoints

Verified at every width the master prompt §37 lists.

| Width | Target | Layout |
|-------|--------|--------|
| 1920 / 1600 | Desktop | Sidebar 260 px expanded, content max 1440 px centred |
| 1366 / 1280 | Laptop | Sidebar 240 px, denser table padding |
| 1024 | Small laptop / landscape tablet | Sidebar collapses to 64 px icon rail |
| 768 | Tablet | Sidebar becomes an overlay drawer; cards go 2-up |
| 390 | Mobile | Drawer + bottom navigation; tables become stacked cards |

**No horizontal page scroll at any width.** Wide tables scroll inside their own
`overflow-x:auto` container with the first column sticky, or switch to the card
renderer below 768 px.

---

## 3. Layouts

### AuthLayout
Split screen: left half a full-bleed workspace photograph with the Regalis wordmark on a
black scrim; right half the form on white, max 420 px.

### CustomerLayout
```
┌──────────────────────────────────────────────────────────┐
│ ☰   REGALIS WORKSPACE          [Location ▾]   🔔   ◯ user │  fixed header, black
├──────────────────────────────────────────────────────────┤
│                                                          │
│                     page content                         │
│                                                          │
├──────────────────────────────────────────────────────────┤
│   Home    Booking    Services    Day Pass   Regalis+      │  bottom nav ≤ 768 px
└──────────────────────────────────────────────────────────┘
        ⌗ floating QR-scan button, bottom-right above the nav
```
`☰` opens the left drawer: avatar, name, mobile, email (tapping opens Profile), then the
menu items returned by `GET /menus/my`.

### AdminLayout
```
┌────────────┬─────────────────────────────────────────────┐
│  REGALIS   │  Breadcrumb / Page title      🔔   Admin ▾   │
│  ────────  ├─────────────────────────────────────────────┤
│ Dashboard  │                                             │
│ Customers  │  PageHeader + actions                       │
│ Workspaces │  Filters                                    │
│ Calendar   │  DataTable / form / calendar                │
│ Bookings   │                                             │
│ …dynamic…  │                                             │
└────────────┴─────────────────────────────────────────────┘
```
Sidebar is dark, gold active indicator, groups expand/collapse, state persisted.
Entirely generated from the menu API — no route list is hard-coded in the component.

---

## 4. Reusable component library (`src/components/`)

Master prompt §9 in full. Each is built once, typed, and used everywhere. No module
re-implements any of these.

| Component | Notes |
|-----------|-------|
| `DataTable` | Generic over `T`. Server-side pagination/sort/filter, column defs, row actions, sticky header, sticky first column, loading skeleton, empty state, error state, mobile card renderer, row selection, CSV export hook |
| `SearchBox` | Debounced 350 ms, clearable |
| `Pagination` | Page size selector, jump-to-page, "showing x–y of z" |
| `Modal` | Focus trap, `Escape`, backdrop click, sizes sm/md/lg/xl, scroll lock |
| `ConfirmDialog` | Danger variant, typed confirmation for destructive actions |
| `Dropdown` / `MultiSelect` | Searchable, async option loading, keyboard navigation |
| `DatePicker` / `DateRangePicker` | Min/max, disabled dates (past dates are unselectable on booking) |
| `TimeSlotPicker` | The booking-specific slot grid; disabled slots are visibly unavailable, not merely unclickable |
| `TextInput` `NumberInput` `TextArea` `PasswordInput` | Label, hint, error, required marker; `PasswordInput` has show/hide and a strength meter |
| `FileUpload` | Drag/drop, type + size validation client-side (server re-validates), preview, progress |
| `LoadingSpinner` / `Skeleton` | |
| `Toast` | Success/error/warning/info, stacked, auto-dismiss, screen-reader announced |
| `Alert` | Inline banner |
| `EmptyState` | Illustration, message, optional primary action |
| `ErrorState` | Message, correlation id, retry |
| `Breadcrumb` | Derived from the menu tree |
| `PageHeader` | Title, subtitle, breadcrumb, action slot |
| `ExportButton` / `ImportButton` | Format menu, permission-aware, progress |
| `StatusBadge` | Single source of truth for status → colour + label |
| `Card` `StatCard` `Tabs` `Badge` `Avatar` `Tooltip` `Switch` `Checkbox` `Radio` `Stepper` | |
| `QrDisplay` / `QrScanner` | Render and camera-scan |
| `CalendarGrid` | Day/week/month, shared by both portals |
| `PermissionGate` | `<PermissionGate permission="BOOKING.CREATE">` wraps an action |

---

## 5. Key screen specifications

### Login (`/login`)
One page, two clearly separated panels: **Customer Login** (email, password, Forgot
Password?, Register) and **Admin Login** (admin email, password). Below 768 px the
panels become tabs. Show/hide password, inline validation, disabled submit while
pending, a single error region, and no hint about whether the email exists.

### Register (`/register`)
Full Name*, Mobile Number*, Address*, Company Name (optional — assumption A11), Email
ID*, Photo (optional). Async uniqueness check on email and mobile. Submit → OTP screen →
account activated → login → dashboard.

### Customer Home (`/`)
Header (hamburger, wordmark, location selector, bell) · auto-sliding promotional banner
carousel from `MST_Banner` · horizontally scrolling Featured Services strip with Quick
Add · **Today's Booking** panel showing type, number, date, time, status, QR and a
Check-in button — or `No Booking Found` with a Book Now action · floating QR button ·
bottom navigation.

### Calendar / Book Workspace (`/book`) — the primary flow
```
Step 1  Type      [ All ] [ Desk ] [ Cabin ] [ Meeting Room ]
Step 2  Location  [ Regalis WorkSpace ▾ ]
Step 3  Date      ‹  August 2026  ›     past dates disabled
Step 4  Time      slot grid, unavailable slots visibly disabled
Step 5  Duration  ( ) 30 min  ( ) 1 hour  ( ) 2 hours
Step 6  Result    available workspaces — name, capacity, price/hr, [Select]
        (meeting rooms also offer the room × time grid view)
Step 7  Summary   location, workspace, date, time, duration, price, tax, total
Step 8  Payment   wallet / gateway
Step 9  Confirmed  booking id, [View Booking] [View QR] [View Invoice]
```
Availability is re-checked on the server at Step 7 **and** again inside the confirm
transaction. A `409 SLOT_UNAVAILABLE` returns the user to Step 4 with a clear message
and refreshed availability, never a generic failure.

### Admin Calendar (`/admin/calendar`)
Filters: Location · Workspace Type (All/Desk/Cabin/Meeting Room) · Workspace · Search ·
Quick filters (Today / Tomorrow / This Week / This Month) · Status (All / Available /
Booked / Blocked / Cancelled) · view toggle Day / Week / Month · `+ New Booking`.

Day view = time rows × workspace columns. Week view = workspace rows × day columns.
Month view = date cells with density dots; clicking a date opens Day view.

Clicking a **booked** cell opens a popup with booking id, customer, workspace, date,
time, duration, amount, payment, status and the actions View Customer / View Booking /
View Invoice / View QR / Cancel Booking. Clicking an **available** cell offers Create
Booking or Block Slot. **There is no permanent-delete action anywhere on this screen.**

### Wallet (`/wallet`)
Tabs Personal Wallet / Online Wallet. Balance card in black with a gold figure, Add
Funds primary action, month filter, category chips (F&B, Bookings, Wallet Recharge,
Refunds, Parking, Printing, Events), transaction list with id, date/time, type, service,
amount, status, method, branch, reference. Empty state: `No Transactions Found` +
Explore Services.

### Invoices (`/invoices`)
Tabs Pending / Paid, date-range filter, search by invoice number / booking id / service.
Pending rows offer View, Download PDF, Pay Now; paid rows offer View, Download PDF,
Download Receipt. Detail view renders the GST invoice with company and customer
snapshots, line items, CGST/SGST break-up and payment reference.

### Support (`/support`)
Raise Ticket is a guided three-step flow: Category → Issue Type → Details (subject,
description, attachments, priority). All Tickets lists ticket id, subject, category,
issue type, created, assignee, priority and status with filters and search. Detail view
shows the full conversation thread and lets the customer reply, attach and close.

### Reports (`/admin/reports`)
A landing grid of the 16 reports, then a shared report shell: filter bar → results table
→ summary strip → Export Excel / Export PDF. One `ReportShell` component drives all 16;
each report supplies its column definitions and filter set.

---

## 6. State, errors and accessibility

- **Loading** — skeletons for tables and cards, never a bare spinner over content the
  user was already reading.
- **Empty** — every list has a designed empty state with the next best action.
- **Error** — inline for field errors, toast for action failures, a full `ErrorState`
  with the correlation id for page-level failures.
- **Forbidden** — a dedicated 403 screen naming the permission required, not a blank page.
- **Session expiry** — the axios interceptor refreshes once silently; on failure it shows
  a "session expired" modal and routes to login preserving the intended destination.
- **Accessibility** — semantic landmarks, labels tied to inputs, visible focus rings,
  `aria-live` for toasts, full keyboard operation of modals/dropdowns/date pickers,
  ≥ 4.5:1 contrast (gold on black and gold on white are both checked; gold text on white
  is only used at ≥ 20 px or with the darker gold).

## 7. PWA

`manifest.webmanifest` — name "Regalis WorkSpace", short name "Regalis", black theme,
gold accent, 192/512 icons, maskable icon, `standalone`, portrait-primary.
Workbox: precache the shell, `NetworkFirst` for `/api`, `CacheFirst` with expiry for
images/fonts, offline fallback page, update prompt on new service worker.
