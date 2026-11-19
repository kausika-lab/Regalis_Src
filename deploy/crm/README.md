# CRM — Leads and Web Enquiries (19 Nov 2026)

Built from `Regalis_WorkSpace_CRM_Build_Document.docx` (v1.0, Nov 2026). Before this kit there was no CRM at all: migration `20260916020000` had left the CRM menu row out because there was "no controller, no route, no page".

Installed on the **local Dev stack only**. Not on UAT or production.

Product-owner decisions taken before building:

| Question | Decision |
|---|---|
| Scope | Everything in the document: all admin screens, plus the public capture endpoints, tracking script and enquiry form. |
| "Sales / front desk" role | A **new role `SALES`** ("Sales / Front desk"). Owner = `ADMIN`, Manager = `MANAGER`. |
| Website capture | myregalis.com is the Regalis app itself: it has no Home / Cabins / Pricing marketing pages. So there is a public **`/enquire`** page in the app, linked from the sign-in page, plus `crm-track.js`, which a future marketing site can load. |
| Install | Local Dev + commit on `DHK/CRM`. No push, no UAT or production deploy. |

Existing behaviour is unchanged. All existing code is untouched except for the additive edits listed under **Files**.

## Coverage, section by section

| § | What the document asks | Built |
|---|---|---|
| 1 | Two layers: anonymous visits, then a lead when details are handed over | `TRN_CrmVisitor` + `TRN_CrmPageView` record visits. A form, callback, quote or WhatsApp ref creates the lead and attaches the session. The limitation sentence is printed on the lead screen. The Visitors screen has a "What a visit can and cannot tell you" panel. |
| 2 | SALES band between OPERATIONS and FINANCE. CRM parent with Leads / Follow-ups / Website Visitors / CRM Reports. CRM Settings under Administration | `ADM_SEC_SALES` (order 250). `ADM_CRM` expands like Assets. `ADM_CRM_SETTINGS` sits under Administration. Nothing existing is renamed or moved. |
| 3 | Leads list: 5 figures, filters, columns, cream row / red edge, sort, 50 a page, Excel export, + Add lead | Done. Figures count the whole filter, and the page says so. Search matches the last 10 mobile digits. |
| 4 | One lead: header, 4 buttons, 5 tabs, About, journey panel, activity log, rail (follow-up, owner, linked records, consent) | Done. Hand-entered leads show "Entered by X … No website history". The activity log is append-only: the API refuses edits with the document's message. The red "Waiting for first contact" row is shown. |
| 5 | Add a lead by hand: chips (Walk-in and Referral reveal fields), 2 required fields, live duplicate check (leads **and** customers, last 10 digits, email without case), part two, assignment, follow-up, green panel, Save and add another, also on the Dashboard | Done. Phone-call and walk-in leads start as **Contacted**, because the conversation happened. Nothing automatic is sent to a hand-entered lead. |
| 6 | Enquiry form (2 required fields + consent), WhatsApp button with a reference code, callback bar, "Send me a quote", hidden journey fields, anonymous session rules, campaign kept 90 days, Direct when nothing is known | `/enquire` (+ `?kind=quote&looking=Cabin`) and `crm-track.js`. The original source is kept on return visits. Query strings are never stored, only the utm_* values. |
| 7 | Website Visitors: 5 figures, sources with visits vs enquiries, session list, cream "not yet — Nth visit", 4 rules | Done. Clicking a session shows its pages. Sessions older than the retention period are deleted by the worker. |
| 8 | Follow-ups: 4 figures, 3 columns, Done / Take it, outcome + next follow-up required, response targets in working hours, assignment rules, notification matrix | Done. The worker nudges the owner when the target passes and tells the manager at the escalation time, counting working hours only. "In-app" means the **CRM alerts** list on the Follow-ups page. |
| 9 | Pipeline by drag, Lost needs a reason, stages are rows; conversion through Customers, existing customer checked first, never deleted, cannot be undone | Done. Dropping on Won opens the convert dialog; dropping on Visit booked asks for the tour time. |
| 10 | 4 reports, date range (default 90 days), Excel | Done, with one workbook of 4 sheets. Revenue won = what the converted customer has been invoiced since conversion (cancelled invoices excluded), else the deal value. |
| 11 | Settings: sources (switch off, never delete), statuses with rules, lost reasons, looking-for, targets + hours, draggable rules (safety net red), consent + retention, roles | Done. Rules and lists are reordered with ↑/↓. The safety net cannot be switched off. There is also a "Who handles leads" table (on duty, manager, location). |
| 12 | Connections; CRM never creates a customer, quotation or booking itself | The customer comes from `IUserAdminService.CreateUserAsync` (the Customers module). Quotations are made in Billing & Finance. Bookings are only opened, never written. |
| 13 | Data model | See **Database**. |
| 14 | API | See **API**. |
| 15 | Permissions | 10 `CRM.*` codes; see **Database**. |
| 16 | Validation messages | Taken word for word from the document (`tests/e2e_crm.py` checks them). |
| 17 | DPDP: consent copied, separate marketing tick, delete on request, retention job | Done. Lead, visitor and consent tables are **not** audited row by row (see below). Delete on request removes everything in one transaction. |
| 18 | Website work list | Items 1, 2, 3, 4, 5 and 8 are built into the app. Item 6 (privacy notice) is a setting that links to a page someone has to write. Item 7 (UTM tags on published links) is a job for whoever manages the listings. |
| 19 | Not built on purpose | Respected: no visitor identification, campaigns, drip mail, scoring, dialler, social inbox or lead portal. |

## Where it differs from the document, and why

- **No new columns on existing tables.** §13.2 suggests `Customer.source_lead_id`, `Quotation.lead_id`, `Booking.lead_id` and `User.is_on_duty`. Instead:
  - The lead holds `CustomerId` and `QuotationId`.
  - A quotation made with **Send quotation** carries the lead number (L-…) as its enquiry reference. The worker links it within a minute and moves the lead to Quotation sent once the quotation is sent.
  - "On duty" is a tick in CRM Settings, or being checked in through Attendance today.
  - Reason: existing tables and services stay untouched.
- **The tour is not a Bookings row.** A Bookings row needs a customer, and a lead is not one yet. The tour date sits on the lead, moves it to Visit booked, and creates a follow-up at that time. It is not on the Calendar page.
- **Lead numbers look like `L-000001`.** That is the existing number-sequence format (prefix + 6 digits). The document shows L-00241.
- **Converting needs an email.** The Customers module needs one to create a sign-in. If there is none, the dialog asks for it and the lead is left unchanged.
- **City** is filled only when the hosting edge sends a city header (Cloudflare-style). Otherwise it shows "not known", so no city is guessed. This setup has no such header.
- **WhatsApp** uses a click-to-chat link (open question 5). When the message arrives, staff choose the WhatsApp chip in Add lead and type the `W-…` code; this attaches the visit history and the website source.
- **The callback bar** also has the consent tick, because §17.1 says every public form has one. A callback lead is called "Callback request" until staff edit it.
- **In-app notifications** are CRM's own list (Follow-ups page → "Your CRM alerts"). They do not go into the shared admin bell, so the existing Notifications page is untouched.
- **Email is OFF.** A test email must be sent from CRM Settings before it can be switched on (§8.5). The acknowledgement email goes only to website leads who ticked consent.
- **Rate limit on public forms:** at most 5 enquiries per address per 10 minutes, and 3 per mobile per hour. This is applied silently in the service, so bots get the normal success message. The app's own 429 limiter would tell them they were caught.
- **Not done:** listing the 4 CRM reports in the Reports & Analysis library (§10.5). That needs a change to that module's catalogue. The reports have their own page and Excel export.
- **Not done:** a "came from lead L-…" line on the Customers page (the customer-360 view from Phase 1). The lead itself keeps the link to the customer.

## Database

`db/migrations/20261119120000_Crm.sql` (rollback: `..._Rollback.sql`, which **drops the CRM tables**). Idempotent, one transaction.

- **New tables:**
  - `TRN_CrmLead`, `TRN_CrmLeadActivity`, `TRN_CrmFollowUp`, `TRN_CrmLeadConsent`
  - `TRN_CrmVisitor`, `TRN_CrmPageView`, `TRN_CrmWhatsAppRef`, `TRN_CrmNotification`
  - `MST_CrmSource`, `MST_CrmStatus`, `MST_CrmLostReason`, `MST_CrmLookingFor`
  - `MST_CrmAssignmentRule`, `MST_CrmTeamMember`, `MST_CrmSetting`
- **Seeded:**
  - 15 sources and the 6 stages.
  - The 6 lost reasons and 8 "looking for" rows, mapped to the workspace types.
  - The 5 assignment rules and the settings defaults.
  - Number sequence `CrmLead` (`L-`).
- **Permissions:** `CRM.VIEW`, `CRM.LEAD.CREATE / EDIT / VIEWALL / ASSIGN / CONVERT / DELETE`, `CRM.VISITORS.VIEW`, `CRM.REPORTS.VIEW`, `CRM.SETTINGS`.
  - ADMIN gets all of them.
  - MANAGER gets all except `CRM.SETTINGS`.
  - SALES gets `CRM.VIEW`, `CRM.LEAD.CREATE`, `CRM.LEAD.EDIT` and `DASHBOARD.VIEW`.
  - FINANCE, ACCOUNTANT, AUDITOR, SUPPORT and HOUSEKEEPING get nothing.
- **Sidebar:** `ADM_SEC_SALES`, `ADM_CRM` (+4 children) and `ADM_CRM_SETTINGS`. They go to each role that holds the permission. SALES also gets Dashboard and Admin Profile.
- **Audit trail:** the tables that hold a person's details do not derive from `AuditableEntity`, so the audit interceptor never copies their rows into `TRN_AuditLog`. Delete on request therefore leaves no copy (the e2e test checks this). The settings lists are audited as usual.

## API

`api/crm/*` needs a login; `api/public/*` does not. All routes are in `backend/src/Regalis.API/Controllers/CrmController.cs`:

- `options`
- `leads` (list, `export`, create → **409** with the matches, `check-duplicate`, `{id}` get/put, `activities`, `status`, `assign`, `take`, `tour`, `quotation`, `follow-ups`, `convert` GET preview / POST, `lose`, `reopen`, DELETE)
- `customers?search=`
- `follow-ups` (+ `{id}/done`)
- `notifications` (+ `read`)
- `visitors` (+ `{id}`)
- `reports/sources|funnel|response-times|owners|export`
- `settings` (+ `test-email`)
- `assignment-rules`, `team/{userId}`
- `{sources|statuses|lost-reasons|looking-for}` (POST/PUT) and `{list}/order`

**Public:**

- `GET api/public/crm-config`
- `POST api/public/visits`
- `POST api/public/enquiries`
- `POST api/public/whatsapp-ref`

A marketing site on another domain must be added to `Cors:AllowedOrigins`.

## Files

- **Backend, new** (`backend/src`):
  - Domain: `Crm/CrmEntities.cs`
  - Application: `Crm/CrmDtos.cs`, `Crm/ICrmServices.cs`
  - Infrastructure: `Persistence/Configurations/CrmConfigurations.cs`, `Services/Crm/{CrmEngine, CrmService.Leads, CrmService.Work, CrmService.Insights, CrmService.Settings, CrmPublicService}.cs`, `BackgroundJobs/CrmWorker.cs`
  - API: `Controllers/CrmController.cs`
- **Backend, existing:** `backend/existing-files.diff` holds the only change, 6 registration lines in `DependencyInjection.cs`. It was made against the decompiled DLLs, so apply it by hand.
- **Frontend:** `frontend/apply.mjs` (idempotent; it matches CRLF chunks too) does the following:
  - Copies `assets/CrmPages-CRM1.js`, `assets/CrmEnquirePage-CRM1.js` and `crm-track.js`.
  - Patches `index-DI5X4c9f.js`: routes, `/enquire`, 3 sidebar icons, CRM as an expandable group, and "Book a tour" on sign-in.
  - Patches `AdminQuotationPages-*.js`: a `?lead=` prefill. Without `?lead=` the form opens as before.
  - Patches `AdminDashboardPage-*.js`: "+ Add lead" for `CRM.LEAD.CREATE`.
  - Patches `index.html` (loads the tracker) and `sw.js` (revision `crm-1`).
  - It has already been run on the repo `frontend/`.

## Deploy order (when asked; not done)

1. Backend source (Regalis-live): copy `backend/src/**`, apply `existing-files.diff`, and build.
2. Run `20261119120000_Crm.sql` together with the new API.
3. Front end: promote the changed `frontend/` files (`assets/*`, `crm-track.js`, `index.html`, `sw.js`).
4. Go-live checks:
   - **Who handles leads:** in CRM Settings, tick who is on the team, on duty and a manager. Until then every web lead is Unassigned and the managers are told.
   - **Email:** send a test email to an outside address and look at it. Only then switch email on.
   - **WhatsApp button:** fill in the WhatsApp number; without it the button stays hidden.
   - **Privacy notice:** fill in the privacy notice link once that page exists.
   - **Sales accounts:** give front-desk users the Sales / Front desk role.
   - **Tagged links:** tag every published link (§6.5), e.g. `https://myregalis.com/enquire?utm_source=instagram&utm_medium=bio`.

## Tested (local Dev stack, 19 Nov 2026)

How it was installed:

- `local-dev\api` Regalis.*.dll were rebuilt from the decompiled tree with this kit linked.
- The migration was run on Dev twice (it is idempotent).
- `apply.mjs` was run on the repo `frontend/`.
- Backups: `D:\Regalis\local-dev-backup-20261119-before-crm\api` and `RegalisWorkSpace_Dev_20261119_before_crm.bak` (in the instance Backup folder).

Note: `local-dev\start-local.cmd` in the repo starts the repo's old `api\` build. For this test the API was started from `D:\Regalis\local-dev\api` with the same settings.

Results:

- `tests/e2e_crm.py` (API + SQL): **101/101**. It covers:
  - Permissions per role, and the sidebar.
  - Every §16 message.
  - Normalising and the duplicate check, including against customers.
  - Own + unassigned visibility.
  - Status rules; follow-up done needs a next one; "not on duty — assign anyway".
  - Lost / reopen; conversion (new customer with wallet, existing customer reused, cannot undo).
  - Public visit (query string not stored, Google source from the referrer, original source kept, bots ignored).
  - Enquiry (consent, honeypot and timing silent, same number = no second lead), WhatsApp ref.
  - Visitors, reports and both Excel exports; settings rules (email locked until a test).
  - Delete on request, with no audit copy; existing endpoints still answer.
- `tests/ui_crm.py` (headless Chrome, 1366 px and 390 px): **45/45**. It covers:
  - Sign-in → Book a tour → enquiry → web lead with an Instagram journey.
  - Add lead with the duplicate panel; Book a tour; Send quotation (prefill); the convert dialog.
  - Pipeline, Follow-ups, Visitors, Reports, Settings; no sideways scroll on a phone; no JS errors.
  - Dashboard, Customers, Quotations and Waiting List unchanged; a new quotation without `?lead=` is still empty.
- Frontend unit tests: 24/24.

Both test scripts delete what they create.
