# Own sign-in email on the admin portal profile

Admin → Admin Profile → **Edit Profile** now has an editable **Email Address** for staff and
admins. Changing it asks for the **current password**. Customers' email stays read-only
(their invoices, receipts and Google sign-in hang off it).

Decisions (user, 2 Nov 2026): staff/admin only; current password required; local Dev stack
only for now (no git push, no UAT/prod deploy).

## Behaviour

`PUT /api/profile/email` `{ newEmail, currentPassword }` — always the caller's own row.

| Case | Result |
|---|---|
| Customer account | 403 "Customers cannot change their sign-in email. Contact support." |
| Wrong current password | 422 "Your current password is incorrect." (does not count towards lockout) |
| Not an email / empty | 400 validation error |
| Email used by another (non-deleted) account | 409 "That email is already used by another account." |
| OK | 200, returns the profile. `IsEmailVerified = false`, `GoogleId` cleared (a Google account linked to the old address must not keep signing in), admin notification "Email changed", old → new value in the Audit Trail |

The access token in use keeps the old email claim until it is refreshed; refresh re-reads the
user, and the old email no longer signs in.

## Parts

| Part | Where | How it goes in |
|---|---|---|
| New backend files | `backend/src/Regalis.{Application/Auth,Infrastructure/Services,API/Controllers}/` | copy into `Regalis-live\backend\src\` |
| Edit to one existing file | `backend/existing-files.diff` (1 line in `DependencyInjection.cs`) | apply by hand |
| Front end | `frontend/apply.mjs` patches `ProfilePage-DRLk3DkQ.js` in place, bumps its sw.js revision | `node apply.mjs <frontend dir>` |

No database change.

## Tested (local Dev stack, 2 Nov 2026)

Wrong password 422, bad email 400, taken email 409, customer 403, change 200, sign-in with the
new email 200 and with the old one 401, change back 200, audit row written. Patched chunk
passes `node --check`; `apply.mjs` is idempotent.

Local install: `local-dev\api` Regalis.*.dll rebuilt from the decompiled tree with this kit
Compile-linked; backup of the previous DLLs and page in
`D:\Regalis\local-dev-backup-20261102-before-profile-email`.
