# Wi-Fi Access (19 Nov 2026)

Built from `Regalis_WorkSpace_WiFi_Feature_Specification.docx` (v1.0, October 2026). Before this kit the app had no Wi-Fi feature at all.

Installed on the **local Dev stack only**. Not on UAT or production.

Product-owner decisions taken before building:

| Question | Decision |
|---|---|
| Wi-Fi hardware (open item 1) | **Not known yet.** Everything is built behind a controller adapter (`IWifiController`), with a **simulated controller** in place of the equipment. A real adapter (UniFi, Omada, RADIUS, Mikrotik) is a new class later; the screens and rules do not change. |
| WhatsApp (open item 7) | **Click-to-chat links** (`wa.me`), as in quotations and CRM. Email and the in-app notice are the automatic channels. |
| Open items 2-6, 8 | The spec's assumptions: 3 devices for members, 2 for a day pass; day pass ends at closing time; pause after 7 days overdue; one access per named company member; 12-month retention; speed set per network. Every one is a setting. |
| Install | Local Dev + commit on the new branch `DHK/WiFi`. No push, no UAT or production deploy. |

Existing behaviour is unchanged. No existing table, service or screen was edited; the only edits to existing files are listed under **Files**.

## Coverage, section by section

| § | What the spec asks | Built |
|---|---|---|
| 1.1 | Three ways of creating access, the per-customer one recommended | Settings → *How access is created*: one password per customer (default), login page (portal), or one shared password. The consequence of each option is written on it. In shared mode the Connected list says devices cannot be named. |
| 1.2 | Admin: Device / Access → Wi-Fi with four tabs under `/admin/wifi`; customer: My Wi-Fi at `/portal/wifi` | System → **Device / Access Control → Wi-Fi** (`/admin/wifi`, `/admin/wifi/access`, `/admin/wifi/usage`, `/admin/wifi/settings`). Customer menu **My Wi-Fi** at `/wifi`, because customer routes have no `/portal` prefix; `/portal/wifi` redirects there. |
| 2 | Within seconds of payment: email (network, password, dates, devices, QR), WhatsApp, the app | The job checks every 15 s. The email has the network and password, dates, device limit, a QR image (`wifi-qr.png`, attached) and a button into My Wi-Fi. The in-app notice opens `/wifi`. WhatsApp is a button (click-to-chat). |
| 2 | Renewed: same password, new end date; 1-day reminder; ends: stops working, shows Expired | Done. A renewal adds the booking to the same access (even after it has expired, as in the lifecycle chart). The reminder is a setting (off by default, as on the design). |
| 2.1 | Device limit; full = remove one first; ask again; "Having trouble connecting?" raises a ticket | Done. A device over the limit is refused, and the customer is told to remove one. Remove frees the slot. Asking again works by email or WhatsApp. The ticket goes through the existing Support Tickets module ("Product / Technical Support"), with a "my password may be shared" tick. |
| 3.1 | Disconnect / block, resend, reset one password, extend / revoke with reason, guest access, Excel export | Done, all written to the access log with the admin's name. |
| 4.1, 4.2 | Fields; statuses Issued, Active, In use, Expiring, Expired, Paused, Revoked | Done. "In use" and "Expiring" are worked out when read. The access number is `WIFI-000001`. |
| 4.3 | Session per device connection with result | `TRN_WifiSession`: connected, refused (wrong password / expired / device limit). |
| 4.4 | Business rules | Done: only after a successful payment; renew = same password; one access per named team member; automatic expiry; cancel revokes at once; 7 days overdue pauses and paying restores; unknown device alerts the admin; everything logged; passwords encrypted and never shown to the admin. |
| 5.1 | Connected now: 5 cards, filters, columns, second device, amber unknown + red Block, 30 s refresh, phone cards | Done. The peak is shown as the most devices online today (data rates are not reported by the simulator). |
| 5.2 | Access issued: chips incl. red Not delivered, columns, countdown, failed email → Send by WhatsApp, company → Members, ⋯ menu | Done. The ⋯ menu also has *Add a team member*, *Retry the Wi-Fi controller* and *Re-issue* (for expired access). |
| 5.3 | Settings: controller + test, networks, creation mode, delivery, rules | Done, plus *Simulate a device* while the simulated controller is selected. Controller types without an adapter are listed but cannot be saved. |
| 5.4, 5.5 | My Wi-Fi; app screen; email | Done. On a phone the password block is gold on black, as on the app design. The page includes "How to connect" steps, Copy / Hide / QR, devices with Remove, Earlier access and House rules. |
| 6 | Issue → controller → deliver; connecting; lifecycle; never deleted | Done. Expired and revoked rows stay, with their history. |
| 7.1 | Data model | See **Database**. Two tables were added: `TRN_WifiAccessSource` (which bookings an access covers) and `TRN_WifiBlock`. |
| 7.2 | Adapter with 4 methods; poll every 30 s and cache; match by password; 3 retries then "access not created"; never say ready when it is not; credentials encrypted | Done. `IWifiController` also has `ListAuthEvents`, `Unblock` and `Test`. The admin screen reads the cached copy and never waits on the controller. |
| 7.3 | API | All 13 endpoints, plus `members`, `retry`, `devices/{id}` (admin), `unblock`, `settings/test`, `simulator/*` and `portal/wifi/help`. |
| 7.4 | Jobs: 30 s poll, 15 min expiry, hourly overdue, 08:00 reminders, monthly retention | One worker: every 15 s (issue, write to the controller, expire), poll every 30 s, overdue hourly, reminders at 08:00 IST, retention once a day. |
| 8 | Privacy | The text is on Settings and My Wi-Fi, and in the email. Retention is a setting (12 months). Permissions are `WIFI.VIEW` / `WIFI.MANAGE`. |

## Where it differs from the spec, and why

- **The simulated controller.** No Wi-Fi equipment is connected, so devices "join" only through Settings → *Simulate a device*, or through `POST /api/admin/wifi/simulator/join`. The rest is real: issuing, the controller calls (against the simulator's own tables), delivery, expiry, pause, refusals and the admin alerts. Every Wi-Fi screen says plainly that the controller is simulated.
- **"Idle disconnect"** is stored as a setting for the real controller to apply. The simulator does not disconnect idle devices.
- **"Data today … peak Mbps"** is shown as the peak number of devices online today. Throughput is not available from the simulator.
- **Bounced email.** The mail server tells the app "accepted" or "refused", not "bounced later". A refused email (or no address) shows red *Email failed* / *No email address*, with *Send by WhatsApp* in its place.
- **WhatsApp "automatically if the email bounces"** is not possible with click-to-chat. The button turns primary instead. Click-to-chat types the message, password included, into the sender's own WhatsApp, so an admin who presses it can see it. A WhatsApp sending service would remove this.
- **Company bookings.** The app has no list of a company's people, so an admin adds each named member (name + email or mobile) in the access's *Members*. Each member gets their own username and password, and their own email. Members follow the company access: extend, expire, pause and revoke.
- **Pay-later bookings** get Wi-Fi only once paid (the spec: "only after a successful payment"). A setting can allow it before payment.
- **When access goes live.** Access for a booking that starts later is created at payment but written to the controller (and delivered) **one hour before it starts**. My Wi-Fi meanwhile says when the details will be ready. This keeps the rule "never tell the customer their Wi-Fi is ready when it is not".
- **Start date.** Only bookings and passes paid *after the migration ran* get access automatically, so existing customers are not all emailed on day one. Settings has a button that also covers bookings already paid and still running.
- **Ticket category.** "Having trouble connecting?" files under the first active category whose name contains "Technical" (on Dev: *Product / Technical Support*, issue type *General*).

## Database

`db/migrations/20261119180000_Wifi.sql` (rollback: `..._Rollback.sql`, which **drops the Wi-Fi tables**). Idempotent, one transaction.

- **New tables:**
  - `TRN_WifiAccess`, `TRN_WifiAccessSource`, `TRN_WifiDevice`, `TRN_WifiSession`, `TRN_WifiEvent` (the access log)
  - `TRN_WifiBlock`, `MST_WifiSetting`
  - `TRN_WifiSimKey`, `TRN_WifiSimClient`, `TRN_WifiSimEvent` (the simulated controller only)
- **Seeded:**
  - The settings defaults.
  - `issue.sinceUtc` = when the script ran.
  - Number sequence `WifiAccess` (`WIFI-`).
- **Permissions:**
  - `WIFI.VIEW` → Administrator, Operations Manager, Support Executive (Support answers the connection tickets).
  - `WIFI.MANAGE` → Administrator, Operations Manager.
- **Menus:**
  - `ADM_ACCESS_WIFI` (Wi-Fi, under `ADM_ACCESS` "Device / Access Control", route `/admin/wifi`, icon `Wifi`), for roles with `WIFI.VIEW`. This also gives Support the Device / Access Control parent; the rollback takes it back.
  - `CUST_WIFI` (My Wi-Fi, `/wifi`, after Payment History) for the Customer role. It has no permission row, because the API serves only the signed-in customer's own access.
- **Not audited row by row.** Access rows hold an encrypted password, so they are plain classes, not `AuditableEntity`. `TRN_WifiEvent` is their history.

## Passwords and keys

- Passwords (and the controller API key, and the shared password) are encrypted with AES-256-GCM.
- The key is taken from configuration **`Wifi:EncryptionKey`** (base64, 32 bytes). Without it, a random key is created once in `App_Data/keys/wifi-secrets.key` next to the API, and a warning is logged.
- **Set `Wifi:EncryptionKey` on UAT and production (or back up that file).** If the key is lost, stored passwords cannot be read. *Reset password* then issues new ones.
- The key is never stored in the database.

## API

`api/admin/wifi/*` needs `WIFI.VIEW` (read) or `WIFI.MANAGE` (act). No admin endpoint returns a password. All routes are in `backend/src/Regalis.API/Controllers/WifiController.cs`.

**Admin:**

- `connected` (+ `export`)
- `access` (GET list, POST guest), `access/{id}` (GET, PATCH)
- `access/{id}/resend|reset|revoke|retry|members`, DELETE `access/{id}/devices/{deviceId}`
- `clients/{mac}/disconnect|unblock`
- `sessions` (+ `export`)
- `settings` (GET/PUT, `test`)
- `simulator/join|leave`

**Portal** (signed-in customers only):

- `GET api/portal/wifi`
- `POST resend`
- `DELETE devices/{id}`
- `POST help`

## Files

- **Backend, new** (`backend/src`):
  - Domain: `Wifi/WifiEntities.cs`
  - Application: `Wifi/WifiDtos.cs`, `Wifi/IWifiServices.cs`
  - Infrastructure: `Persistence/Configurations/WifiConfigurations.cs`, `Services/Wifi/{WifiEngine, WifiService, WifiPortalService, WifiControllers, WifiSecrets, WifiQr}.cs`, `BackgroundJobs/WifiWorker.cs`
  - API: `Controllers/WifiController.cs`
- **Backend, existing:** `backend/existing-files.diff` holds the only change, 8 registration lines in `DependencyInjection.cs` (made next to the crm kit's lines). Apply it by hand.
- **Frontend:** `frontend/apply.mjs` (idempotent; it expects the crm kit's patches, which are in the repo):
  - Copies `assets/WifiPages-WF1.js`.
  - Patches `index-DI5X4c9f.js`: the lazy chunk, the admin routes (`WIFI.VIEW`), `/wifi` and `/portal/wifi`, and a `Wifi` sidebar icon.
  - Patches `sw.js` (revision `wifi-1`).
  - The QR is drawn with the existing `AssetQr-AS1.js`.
  - It has already been run on the repo `frontend/`.
- **Tests:** `tests/e2e_wifi.py`, `tests/ui_wifi.py`, `tests/qr_check/`.

## Deploy order (when asked; not done)

1. Backend source (Regalis-live): copy `backend/src/**`, apply `existing-files.diff`, and build.
2. Set `Wifi:EncryptionKey` in the API's configuration (32 random bytes, base64).
3. Run `20261119180000_Wifi.sql` together with the new API.
4. Front end: promote `assets/WifiPages-WF1.js`, `assets/index-DI5X4c9f.js` and `sw.js`.
5. Go-live checks:
   - **Hardware (open item 1):** until an adapter for the real controller is written, production would run the *simulated* controller. Customers would be emailed passwords that no real network accepts. **Do not deploy to production before that adapter exists**, or switch automatic issuing off in Settings.
   - **Email:** the Wi-Fi email goes out through the same mailer as reports (Microsoft 365 / SMTP). Send one to yourself first.
   - **Networks:** in Settings, type the real network names and speeds. Choose which kind of booking gets which network.
   - **Existing customers:** use "Include bookings already paid" only when everyone should be emailed their details.

## Tested (local Dev stack, 19 Nov 2026)

How it was installed:

- `local-dev\api` Regalis.*.dll were rebuilt from the decompiled tree with this kit linked.
- The migration was run on Dev twice (it is idempotent).
- `apply.mjs` was run on the repo `frontend/` (and run again: everything "already done").
- Backups: `D:\Regalis\local-dev-backup-20261119-before-wifi` (the API DLLs) and `RegalisWorkSpace_Dev_20261119_before_wifi.bak` (in the instance Backup folder).

For the tests, the API ran with Microsoft 365 mail switched off and SMTP pointed at a local sink (`127.0.0.1:2525`), so no email left the machine. **Local build note:** the decompiled `TicketService.cs` had an artefact (a `Func` cast inside an EF query) that made `GET /api/tickets` fail with 500 in local builds since the CRM install. It was corrected in the local compile tree only. The real server source never had it, and this kit does not touch tickets.

Results:

- `tests/e2e_wifi.py` (API + SQL): **135/135**. It covers:
  - Menus and permissions per role (Support can look but not act; customers and admins kept apart).
  - A monthly pass bought through the sandbox gateway → access within seconds, encrypted, in the controller, emailed (subject, password, QR PNG) and in the app.
  - The password never appears in admin responses.
  - Devices: named on Connected now, second-device marker, device limit refused + customer told, wrong password refused, unknown device alerted / blocked / unblocked; disconnect; customer removes a device.
  - A booking covered by a membership (no new password); reset password.
  - A booking for tomorrow → issued ahead, live at its start, follows a moved booking; expiry; refused-expired; renewal with the same password; cancel → revoked with the booking named.
  - An unpaid pay-later booking gets nothing.
  - Overdue invoice → paused → paid → back with the same password.
  - Guest access (validation, WhatsApp link, resend, extend with reason, revoke); a team member with their own access.
  - Controller down → 3 tries → "access not created", guest not told → retry delivers; expiry job.
  - Help ticket; customer resend; usage log + both Excel exports; settings rules; existing endpoints still answer.
- `tests/ui_wifi.py` (headless Chrome, 1366 px and 390 px): **47/47**. It covers:
  - All four admin tabs.
  - Block from the list; detail dialog; add a team member; issue and revoke guest access; simulate a device; save and test settings.
  - My Wi-Fi: password, QR, devices, Hide, email again, help ticket, remove device; `/portal/wifi` redirect.
  - No sideways scroll on a phone; no JS errors.
  - Existing admin and customer pages unchanged.
- `tests/qr_check`: the C# QR encoder matches the bundled qrcode library module for module, **63/63**.
- Frontend unit tests: **24/24**.

All test scripts delete the Wi-Fi rows they create and soft-delete their users. The test customers' passes, invoices and cancelled bookings stay on Dev; they belong to soft-deleted users.
