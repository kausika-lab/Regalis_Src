# Admin + customer changes (13–14 Nov 2026)

The brief listed 19 numbered changes, but only 13 were included: 2, 3, 4, 5, 7, 8, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19. Items 1, 6 and 9 were missing. Before building, every unclear point was put to the product owner. Their answers are recorded in the "Decision" column below.

Installed on the **local Dev stack only**. Not on UAT or production.

## What changed

| # | Requirement | Decision | What was done | Where |
|---|---|---|---|---|
| 2 | Email + mobile validation, country code | Keep one `MobileNumber` column and store `+<code><digits>`. Old 10-digit rows are read as +91. | A shared `PhoneField` with a country-code dropdown (default **IN +91**, 23 countries) beside a digits-only number box. Letters and symbols are dropped as you type. Each country has its own allowed length (India: 10 digits starting 6–9). A pasted `+91…` or leading `0` is stripped. One email rule is used everywhere. Numbers are shown as `+91 9876543210`. **API:** duplicate checks treat `9876543210` and `+919876543210` as the same number. No rows are rewritten. | `PhoneField-PH1.js` (new); `RegisterPage`, `OnboardingPage`, `ProfilePage` (customer + admin profile), `UsersPage` (new/edit user, new/edit staff), `AdminCustomersPages` (display), `AdminWaitlistPage` (display); `MobileNumbers.cs` (new); `AuthService`, `ProfileService`, `UserAdminService` |
| 3 | Admin cancels a booking | Same refund policy as a customer cancellation. | Booking detail → **Cancel booking** appears for a Pending or Confirmed booking that has not started, if the user holds `BOOKING.EDIT`. A dialog shows the booking and the policy, and a reason is required. It uses the existing `POST /bookings/{id}/cancel`, which already allowed staff. The status, history, refund to wallet, cancellation of an unpaid pay-later invoice and the customer notification all come from that endpoint. | `AdminBookingsPage-BK2.js` |
| 4 | Admin location dropdown | A header switcher, like the customer's. | The top-bar location picker now shows on admin too. Admin **Bookings** (list + monthly chart), **Calendar** (both ways) and **Dashboard** follow it. | `index-DI5X4c9f.js`, `AdminBookingsPage-BK2.js`, `BookingsMonthlyChart-BK2.js`, `AdminCalendarPage-Cw-YZzwr.js`, `AdminDashboardPage-DgTMoJ1J.js`, `reports-CNtuzowc.js`; API `GET /dashboard/admin?locationId=` |
| 5 | Location on admin bookings | — | The New booking form already had Location. Added a **Location** column to the list and a Location row to the booking detail. | `AdminBookingsPage-BK2.js` |
| 7 | Membership payment page font/alignment | Two columns. | The payment step has the order summary on the left and a **Pay with** card on the right: total payable, then Wallet / Pay online / Pay later stacked full width, then the notes. Below 900 px it becomes one column. Numbers use tabular figures. The "You save" amount takes the note colour. The step title and "← Change …" sit on one line. | `MembershipPage-Bk9mQr2v.js`, `index-BgQ7mK4p.css` |
| 8 | Hide My QR Access | Hide only. | `MST_Menu` CUST_QR `IsVisible = 0`. The `/qr` page, its API and the tokens are untouched. | migration |
| 10 | Virtual Office pay later | Already built on **Membership**. Added to **Book Workspace** on 14 Nov, at the user's request. | Membership already offered pay later (`PayLater.AllowedPassKinds` default `DayPass,Membership`). Book Workspace → Virtual Office → Review & pay offered only Wallet / Pay online. It now also offers **Pay later at the front desk** (outlined, like bookings) when `GET /passes/pay-later` allows "Membership", with a note under the buttons. It buys the VO plan with `paymentMethod: "paylater"`. The confirmation reads "Virtual Office reserved … including GST, to pay at the front desk". No API change. | `BookWorkspacePage-MB2.js` (sw `admin-customer-2`) |
| 11 | Seat waiting list removal | A new feature. Join only when a **day-pass date is full**. Notify everyone; the first to buy gets the desk. Customer removes their own entry; admin screen with remove + reason. | **Customer:** on Passes, when the first visit date has no free desk, a **Join the waiting list** button appears. A **My waiting list** card shows each date and its position, with Remove. **Worker (every minute):** past dates → Expired. A customer who buys a pass covering the date → Booked. A desk freeing up → an in-app notice "A desk is free on …" linking to `/passes?date=…`, which is also emailed. Nothing is held, and it re-arms if the date fills again. **Admin:** Operations → **Waiting List** (filters: search, status, from/to, location from the header). Remove needs a reason and the customer is notified. | `PassesPage-CxAkHUg-.js`, `AdminWaitlistPage-WL1.js` (new), `index-DI5X4c9f.js` (route); backend: `SeatWaitlistEntry`, configuration, `WaitlistService`, `SeatWaitlistJobs/Worker`, `WaitlistController` (all new) |
| 12 | Dashboard occupancy | Booked hours ÷ open hours, fixed. | **Bug found:** the card added the full `DurationMinutes` of every booking touching today. One confirmed monthly booking alone gave ~400%. It also divided all locations' workspaces by the default location's hours. **Now:** per active location, each booking is clipped to today's opening hours. Only active, bookable workspaces count. The hint shows "X of Y h booked · N bookable workspaces". | `DashboardService`, `IDashboardService`, `DashboardController` |
| 13 | Crown logo → dashboard | Both portals. | The brand is a link to `/` (customer) or `/admin`, and it closes the phone sidebar. | `index-DI5X4c9f.js` |
| 14 | Remove new-pass validation | The "one pass at a time" rule. | Already shipped by the customer-ux kit (`Passes.OnePassAtATime = false`, 127735c). Verified: every day-pass plan can be bought while one is running. **That kit must be deployed with this one.** | — |
| 15 | Profile picture edit | Move the photo to the left. | The photo is on the left, with name, email and "Click the picture to change it." beside it. Click to change still works (hover is no longer required). | `ProfilePage-DRLk3DkQ.js` |
| 16 | Remove "Customer" label | Customers only. | The badge is gone for customers. Staff and admin still see "Staff". | `ProfilePage-DRLk3DkQ.js` |
| 17 | Customer emails | Test with a local mail catcher. | The existing worker (customer-portal kit) emails each customer notification. Tested by pointing the API at `../customer-portal/tests/smtp_sink.py`: booking cancelled, desk free and waiting-list removal each reached the customer's registered address. | — |
| 18 | Remove membership percentage | Only the "Discount (10%)" label. | It now reads "Discount". The amounts and the term-card badges are unchanged. | `MembershipPage-Bk9mQr2v.js` |
| 19 | What's included + Amenities | Split the existing list. | Two cards side by side, stacking when narrow. **What's included** = free meeting-room hours plus what the term or cabin adds. **Amenities** = Wi-Fi, power backup, tea/coffee, lounge, printing, GST invoicing ("Available to every member, on every plan"). The texts are unchanged. | `MembershipPage-Bk9mQr2v.js` |

The `frontend/sw.js` revision for every changed or new chunk is `admin-customer-1`. Three chunks that had never been precached (`AdminBookingsPage-BK2`, `BookingsMonthlyChart-BK2`, `OnboardingPage`) were added to the list.

## Database

`db/migrations/20261113120000_AdminCustomerChanges.sql` (rollback `..._Rollback.sql`). It is idempotent; it was run twice on Dev.

- `MST_Menu` CUST_QR `IsVisible = 0`.
- New table `TRN_SeatWaitlist`. FKs to `MST_User`, `MST_Location` and `MST_WorkspaceType`. Status is tinyint 1–5 with a check constraint. A filtered unique index allows only one open entry per customer + location + type + date.
- New menu `ADM_WAITLIST` → `/admin/waitlist` under Operations (order 65, icon ListChecks). It uses `PASS.VIEW`, and its role grants copy those of ADM_PASS_DAY.
- The rollback shows My QR again and soft-deletes the menu row. It keeps the table; dropping it is a commented step.

There is no EF migration for `TRN_SeatWaitlist`. When the entity goes into the server source, either add an EF migration whose `Up` checks `IF OBJECT_ID(...) IS NULL`, or mark it as applied. Otherwise the next `dotnet ef migrations add` will try to create the table again.

## API

| Method | Route | Permission | Notes |
|---|---|---|---|
| GET | `/api/dashboard/admin?locationId=` | DASHBOARD.VIEW | New optional `locationId`. Occupancy fixed (item 12). |
| POST | `/api/waitlist` `{waitDate, locationId?, passPlanId?}` | PASS.CREATE, customers only | Refused when the date is past, more than 90 days ahead, closed, or still has a free desk (`SEAT_AVAILABLE`). Joining twice returns the same entry. |
| GET | `/api/waitlist/my` | PASS.CREATE, customers only | Open entries, with position in line. |
| POST | `/api/waitlist/my/{id}/remove` | PASS.CREATE, own entries only | |
| GET | `/api/waitlist?page&pageSize&search&status&locationId&from&to` | PASS.VIEW, staff only | Status defaults to 1 (Waiting); 0 = all. |
| POST | `/api/waitlist/{id}/remove` `{reason}` | PASS.EDIT, staff only | The reason is required (max 300) and is sent to the customer. |

No new permission codes. Existing endpoints used unchanged: `POST /bookings/{id}/cancel`, `GET /bookings?locationId`, `GET /bookings/monthly?locationId`, `GET /calendar?locationId`.

## Deploy order (when asked; not done)

1. Backend (Regalis-live source):
   - Copy `backend/src/**` (new files).
   - Apply `backend/existing-files.diff` by hand (7 files). It was made against the decompiled DLLs, so match by the lines shown.
   - Deploy the **customer-ux** backend too (item 14).
2. Run the SQL migration.
3. Front end: promote the changed `frontend/assets` files and `sw.js`.

The new front end needs the new API for the dashboard location (the old API ignores the parameter) and for `/waitlist` (404 on the old API: the join button errors and the admin page is empty).

## Tested (local Dev stack, 14 Nov 2026)

Install:
- `local-dev\api` Regalis.*.dll rebuilt from the decompiled tree with this kit linked.
- `local-dev\frontend`: the changed chunks were copied. The edits to `index`, `ProfilePage` and `AdminCustomersPages` were replayed onto the local copies, because those copies also carry the billing and profile-email patches.
- Backup: `D:\Regalis\local-dev-backup-20261114-before-admin-customer` plus the DB backup `RegalisWorkSpace_Dev_20261114_before_admin_customer.bak` (instance Backup folder).
- Each test run started from that backup plus the migration. Dev was restored again afterwards.

Results:
- `tests/e2e_admin_customer.py` (API + SQL): **47/47**. Covers:
  - menu without /qr
  - occupancy with a 30-day booking: adds exactly one workspace's open hours, % = booked/open
  - dashboard by location
  - admin create → list by location → cancel: 100% refund, DB status 5 with reason, customer sees it and is notified, no double cancel
  - `+91` save, duplicate across old and new forms, letters refused
  - several day passes
  - VO pay later: active, unpaid, invoice
  - waiting list: refusals, join, idempotent, my list, admin list, worker notice within a minute, no repeat, customer remove, rejoin, admin remove needs a reason
  - emails captured by the sink, to the registered address
- `tests/ui_admin_customer.py` (headless Chrome, 1366 px and 390 px): **80/80**. Covers:
  - register phone row: default +91, digits only, India and UAE lengths, email message, every field inside the panel
  - sidebar without QR, crown → dashboard
  - profile photo left, no Customer badge, `+91 …` display, phone in Edit Profile with validation
  - membership cards, two-column payment (one column on the phone), "Discount" label, no sideways scroll
  - passes: full date → join → My waiting list → remove
  - admin: picker, crown → /admin, occupancy card, Location column and detail, cancel from the screen (DB status 5), Waiting List menu and page, remove with reason, calendar follows the header, new-user phone field
  - no JS errors on any page
- `frontend/tests/*.test.mjs`: 24/24.
- `tests/ui_vo_paylater.py` (14 Nov, after the Book Workspace VO change): **8/8**. Covers: VO card → Review & pay shows Wallet, Pay online and Pay later plus the note; Pay later → "Virtual Office reserved"; DB has one new MEM-VIRTUAL pass, active, IsPayLater=1, nothing paid; no JS errors. The test pass (MEM-000008) was then cancelled as admin, and its unpaid invoice was cancelled with it.

## Open points

- Items **1, 6, 9** were not in the brief.
- **Admin location picker** always holds one location, like the customer's. With several locations there is no "All locations" view on Bookings, Calendar or Dashboard.
- **Staff mobile** used to be India-only (`^(?:\+91)?[6-9]\d{9}$`). It now accepts any listed country. **Housekeepers** (Maintenance) and **suppliers** keep their own rules: the API still requires a 10-digit Indian number for housekeepers, so they were not switched to the country-code field.
- **Profile mobile** was not editable before (it was sent back unchanged). It is now an editable field with the country code, as item 2 asked.
- Seen, not changed: in the admin sidebar, "Dashboard" stays highlighted on every admin page (`/admin` matches as a prefix).

## Follow-up, 14 Nov 2026: Profile layout, Role, photo pencil

The user's decisions: centre the photo with the name below; Role is a free-text job title; it applies to customers and staff.

| Change | Detail | Where |
|---|---|---|
| **Centred header** (replaces item 15's "photo on the left") | The photo is centred in the profile card. Name (plus the "Staff" badge for staff), role, email and a short hint are centred below it. The rest of the form is unchanged. Desktop and 390 px. | `ProfilePage-DRLk3DkQ.js`, `index-BgQ7mK4p.css` |
| **Pencil on the photo** | A small gold round button with a pencil, at the photo's bottom-right ("Change profile picture"). It opens the file picker; clicking the photo still works too. A spinner shows while uploading. Files over 3 MB are refused before upload. The new picture shows at once (profile and top-bar avatar) and stays after a reload: it is saved by the existing `POST /profile/photo`. | same |
| **Role field** | Optional free-text "Role" (job title, e.g. "Marketing Manager", max 100). Shown as a row on the profile and under the name. Editable on Edit Profile for customers and staff. Also shown to admins on Admin → Customers → Profile Details. It is **not** an access role. | `ProfilePage`, `AdminCustomersPages`; API below |

API / DB:
- New column `MST_User.JobTitle nvarchar(100) NULL`. Migration `db/migrations/20261114120000_ProfileJobTitle.sql` (rollback drops the column). **Run it before the new API starts**: EF reads the column on every user query.
- `User.JobTitle`, mapped with max length 100.
- `UpdateProfileRequest` gains a trailing optional `JobTitle` (validator: max 100). `ProfileService.UpdateAsync` saves it trimmed, with blank stored as NULL.
- `ProfileDto` and `UserDetailDto` gain a trailing optional `JobTitle`.
- These edits are in `backend/existing-files.diff`, now 13 files.

Tested: `tests/ui_profile_role_photo.py` **23/23** on the local stack. Covers:
- centred photo and text, pencil at bottom-right, no sideways scroll, at 1366 and 390 px
- upload via the pencil: "Photo updated", picture and top-bar avatar change, `PhotoPath` saved, still there after a reload
- Role: input present, saved to `JobTitle`, shown after a reload, 100-character limit
- admin profile header
- Admin → Customers shows the role

The test puts the customer's photo and role back afterwards. `tests/ui_admin_customer.py` was updated to the centred layout but not re-run, because that needs a Dev restore.
