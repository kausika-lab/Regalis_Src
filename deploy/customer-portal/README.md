# Customer portal changes (3 Nov 2026)

Eight changes to the customer portal: Google sign-in, simpler passwords, dashboard, price
style, profile fields, Book Workspace steps, notifications by email, sidebar.

## What changed

| # | Change | Where |
|---|---|---|
| 1 | Google sign-in: **no code change needed.** The button renders on desktop and phone (checked at 1366 px and 390 px). Production hides it on purpose: `env.js` there sets `__REGALIS_GOOGLE_CLIENT_ID__ = ""` because Google refuses the public address (`deploy/production/sync-frontend-config.ps1`, commit 305165e). `env.js` is not precached, so this hides it on every device. A phone that shows the button is reaching UAT, not production. | see "Google sign-in" below |
| 2 | Password: at least 8 characters, nothing else. No upper/lower/digit/symbol rule on register, reset or change. Kept: the `Security.PasswordMinLength` check and "not one of your last 3 passwords". Admin-created staff accounts keep the composition rule. | `RegisterPage`, `ResetPasswordPage`, `ProfilePage`; `Register/ResetPassword/ChangePasswordRequestValidator` |
| 3 | Dashboard: "At a glance" removed. The customisation banner moved under Wallet balance so the two columns stay even. | `CustomerHomePage` |
| 4 | Prices: amounts appear in `span.rg-price` (bold, `#0B0B0D`, white in dark mode). Inside dark banners and buttons the amount keeps the surrounding colour. Not changed: amounts inside sentences (toasts, "Pay ₹X at the desk"), and the wallet ledger's +/− column (its green/red colour means credit/debit). | `index-BgQ7mK4p.css`, `BookWorkspace`, `MyBookings`, `Wallet`, `Passes`, `Membership`, `Invoices`, `Services`, `Quotations`, `CustomerHome` |
| 5 | Date of Birth is optional on Register, Onboarding and Profile. A profile no longer needs it to count as complete. The gender list drops "Other" (3). Existing 3 values stay stored and still show as "Other". The API refuses 3 on register and onboarding. Profile update is unchanged. | `RegisterPage`, `OnboardingPage`, `ProfilePage`; `User.IsProfileComplete`, `ProfileService.MissingFields`, `CompleteOnboardingRequest(+Validator)`, `RegisterRequestValidator` |
| 6 | Book Workspace: the step chips are gone. The steps, Back/Continue and payment are unchanged. | `BookWorkspacePage-MB2.js` |
| 7 | Notifications by email: a new worker emails each customer notification to the address on the account (see below). | `backend/src/.../CustomerNotificationEmail{Jobs,Worker}.cs` |
| 8 | Sidebar: the Notifications entry is hidden (`MST_Menu.IsVisible = 0`). The `/notifications` page and the bell in the top bar still work. | `db/migrations/20261103120000_CustomerPortalChanges.sql` |

`frontend/sw.js` revisions for the changed chunks are `customer-portal-1`.

## Notification email (7)

- Every minute, `CustomerNotificationEmailWorker` reads new `TRN_Notification` rows in id order. It emails single-user rows that belong to an active customer. The mailer is `IReportMailer`: Microsoft Graph when configured, SMTP otherwise.
- Progress is stored in `MST_AppSetting Notifications.CustomerEmailLastId`, so there is no schema change. The mark moves past a row with a compare-and-swap before that row's email is sent. That makes each notification **at most once**, even when two API processes overlap during an app-pool recycle. A failed send is logged and not retried; the in-app copy remains.
- Rows wait 2 minutes, so a row whose transaction commits late is not skipped.
- Not emailed:
  - announcements (they have their own Email option)
  - "Money added to your wallet" (the desk receipt PDF is already emailed)
  - "A new quotation is ready" (staff send the quotation by email or WhatsApp)
  - staff and admin rows
  - broadcasts
  - rows older than a day
- Settings:
  - `Notifications.CustomerEmailEnabled`: `true` from the script.
  - `Notifications.CustomerEmailPortalUrl`: empty means the email has no button. Set it to the public portal address to get a "View …" button.
- The script seeds the mark with the newest NotificationId, so switching this on never emails old notifications.

## Deploy order

1. Backend (Regalis-live source):
   - Copy `backend/src/Regalis.Infrastructure/BackgroundJobs/*.cs`.
   - Apply `backend/existing-files.diff` by hand. It was made against the decompiled DLLs; the server source may differ slightly in those 8 files, so match by the lines shown.
2. Run `db/migrations/20261103120000_CustomerPortalChanges.sql`. Rollback: `..._Rollback.sql`.
3. Front end: the changed files are in `frontend/` (repo). Promote them as usual.

**The front end needs the new API.** Onboarding now sends `dateOfBirth: null`, which the old API (non-nullable `DateOnly`) answers with 400.

## Google sign-in on production (1) — needs someone with Google Cloud access

1. In Google Cloud Console, open OAuth client `429301379606-liseccp78bpo396jdrv7u1dc7unbprmg`. Under **Authorised JavaScript origins**, add `https://amstagserver.discus-ordinal.ts.net` and any other public address in use.
2. In `deploy/production/sync-frontend-config.ps1`, set `$googleOriginApproved = $true` and run the script elevated on the server.

Until both are done, the button stays hidden on production on purpose. Showing it earlier would only produce Google's "origin not allowed" error.

## Tested (local Dev stack, 3 Nov 2026)

Install: `local-dev\api` Regalis.*.dll rebuilt from the decompiled tree with this kit linked; `local-dev\frontend` got the changed chunks, with the Profile edits reapplied on top of the profile-email patch. The SQL was run on RegalisWorkSpace_Dev twice and is idempotent. Backup: `D:\Regalis\local-dev-backup-20261103-before-customer-portal`.

- `tests/ui_customer_portal.py` (headless Chrome, 1366 px and 390 px): **43/43**.
  - The Google button renders at both widths.
  - The register form has no "Other" and marks DOB optional.
  - The dashboard has no "At a glance".
  - The sidebar has no Notifications entry and the bell is still there.
  - Book Workspace has no step chips and its first step renders.
  - Prices show in `rgb(11,11,13)`.
  - No JS errors on dashboard, book, wallet, membership, day-pass, bookings, invoices, services, quotations or profile.
- `tests/e2e_customer_portal.py` (API): **15/15** on the first run, after a fix to the test's own `/notifications/my` URL.
  - A simple 14-character password is accepted and works for sign-in; `short` is refused.
  - Onboarding with no DOB is accepted and the profile counts as complete.
  - Gender 3 is refused.
  - The script restores the password hash and DOB at the end.
  - On a second run, reusing the password is refused (password history works). Logins can return 429 from the rate limiter.
- Email, with the API pointed at `tests/smtp_sink.py` on 127.0.0.1:2525 and Graph blanked:
  - 5 test rows: booking confirmed, payment received, announcement, desk wallet receipt, staff ticket.
  - Exactly 2 emails, booking and payment, both to the customer, with the "View booking" button when a portal URL is set.
  - Still 2 after several more passes (no duplicates).
  - The test rows were deleted afterwards.

Not deployed to UAT or production.
