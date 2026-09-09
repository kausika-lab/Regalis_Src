# MST_User — structured address, DOB, gender, GSTIN

Migration `20260908000000_AddUserProfileFields` adds nine nullable columns to
`MST_User`:

| Column | Type | Convention followed |
|---|---|---|
| `AddressLine1` | `nvarchar(200)` | `MST_Location.AddressLine1` |
| `AddressLine2` | `nvarchar(200)` | `MST_Location.AddressLine2` |
| `City` | `nvarchar(100)` | `MST_Location.City` |
| `State` | `nvarchar(100)` | `MST_Location.State` |
| `Country` | `nvarchar(100)` | no precedent — follows City/State |
| `PostalCode` | `nvarchar(20)` | `MST_Location.PostalCode` |
| `DateOfBirth` | `date` | `TRN_CustomerPass.ValidFrom` |
| `Gender` | `tinyint` | `MST_User.UserType`, `Status` (byte enums) |
| `Gstin` | `nvarchar(20)` | `MST_Supplier.Gstin`, `TRN_Invoice.CustomerGstin` |

Names reuse the schema's existing vocabulary rather than the `address1` /
`postal_code` / `gst_number` spelling from the request, so `MST_User` reads the
same way as `MST_Location` and `MST_Supplier`.

The pre-existing `Address nvarchar(500)` column is **kept and untouched** —
`ProfileDto`, `UpdateProfileRequest`, `UpdateUserRequest` and `UserDetailDto` all
still expose it, and `TRN_Invoice.BillingAddress` snapshots it. Removing it would
be a breaking API change. Treat the structured columns as the new source of
truth and keep `Address` populated (composed from the parts) for backward
compatibility.

## Files

- `20260908000000_AddUserProfileFields.cs` — EF Core migration, namespace
  `Regalis.Infrastructure.Persistence.Migrations`, context `RegalisDbContext`.
- `20260908000000_AddUserProfileFields.sql` — the same DDL as idempotent T-SQL,
  with a verification query.
- `20260908000000_AddUserProfileFields_Rollback.sql` — drops only those nine
  columns.

## Applying it — pick ONE path

This database is managed by EF Core 8.0.19 migrations (`__EFMigrationsHistory`,
last applied `20260903191813_RemoveDeadNumberPrefixSettings`). The two paths must
not be mixed.

### Path A — EF-native (recommended)

1. Add the nine properties to `Regalis.Domain.Entities.Identity.User`.
2. Add the `HasMaxLength` / `HasColumnType` lines to the `User` EF configuration.
3. `dotnet ef migrations add AddUserProfileFields -p Regalis.Infrastructure -s Regalis.API`
4. Compare the generated file against the `.cs` here — it should match column for
   column — then `dotnet ef database update`.

EF writes both the migration and `RegalisDbContextModelSnapshot.cs`, so the model
stays consistent. Delete the hand-written `.cs` here once EF has generated its
own; keep it only as the column spec.

### Path B — SQL-first (only if deployment is script-driven)

1. Run `20260908000000_AddUserProfileFields.sql` against the target database.
2. Still do steps 1–3 of Path A in source, then **do not** run
   `database update` — the columns already exist and EF's DDL is unguarded, so it
   would fail. Instead insert the real generated id into the history table:

   ```sql
   INSERT INTO __EFMigrationsHistory (MigrationId, ProductVersion)
   VALUES ('<the id dotnet ef actually generated>', '8.0.19');
   ```

The script deliberately does not write a history row itself: the id EF generates
carries its own timestamp and will not match the placeholder used here.

## The migration alone does nothing

Columns are inert until the API is taught about them. Still outstanding, all of
it compiled C# that needs the source tree:

- `User` entity + EF configuration — the nine properties.
- `Gender` — a new `byte` enum (nothing equivalent exists anywhere in the
  schema or the assemblies).
- `RegisterRequest` + its FluentValidation validator — the nine fields, all
  optional, so existing registrations keep working unchanged.
- `AuthService.RegisterAsync` — map request → entity, and compose `Address` from
  the parts so the legacy column stays populated.
- `ProfileDto` (and `UserDetailDto` where useful) — return the new fields
  alongside the existing `address`, never instead of it.
- `UpdateProfileRequest` — **remove `MobileNumber`** so email and mobile cannot
  be changed through `PUT /api/profile`. Today that DTO still accepts
  `mobileNumber`, so the restriction is UI-only and bypassable by a crafted
  request. Structural removal beats validation here. Add a permission-gated
  admin path if staff must change a number.
- `IPostalLookupService` + HTTP adapter behind `AddHttpClient`, exposed as e.g.
  `GET /api/postal-codes/{code}`, with the provider URL and timeout in
  `MST_AppSetting` read through `ISettingService` (Invariant 6), following the
  existing `IPaymentGateway` / `IAccessControlAdapter` seam (Invariant 5). The
  SPA currently calls the provider directly — see the note in the summary.
- `MST_User.Gstin` should feed `TRN_Invoice.CustomerGstin` /
  `TRN_Quotation.CustomerGstin` at document generation, which today have no
  customer-level source.

## Validation rules to implement server-side

Frontend validation is not a control. Mirror these in the validator:

All nine fields are **required on registration** (`RegisterRequest` validator
only) — the SPA enforces the same rules client-side. `CompanyName` became
required at the same time, so registration is now business-only.

| Field | Rule in `RegisterRequestValidator` |
|---|---|
| `AddressLine1`, `AddressLine2` | `NotEmpty`, `MaximumLength(200)` |
| `City`, `State`, `Country` | `NotEmpty`, `MaximumLength(100)` |
| `PostalCode` | `NotEmpty`, `Matches(@"^[1-9][0-9]{5}$")` — Indian PIN, never starts with 0 |
| `DateOfBirth` | `NotEmpty`, must be in the past; add a minimum age only if that is policy, and put the age in `MST_AppSetting`, not in code |
| `Gender` | `NotEmpty`, must be a defined enum value |
| `Gstin` | `NotEmpty`, `Matches(@"^[0-9]{2}[A-Z]{5}[0-9]{4}[A-Z]{1}[A-Z0-9]{1}Z[A-Z0-9]{1}$")`, upper-cased on save — the 15-char format already stored in `MST_Supplier.Gstin` |
| `CompanyName` | `NotEmpty`, `MaximumLength(400)` |

**The columns must stay NULLable regardless.** Required applies to the
*registration request*, not to the table: the four existing users have no values
for any of these, and `MST_User.CompanyName` is already nullable. Making any
column `NOT NULL` would fail on the existing rows and break every code path that
writes a user without them (admin user creation, seeding, imports).

Required-on-registration is also a **breaking change to the registration API
contract** for any other client. Keep `UpdateProfileRequest` and
`UpdateUserRequest` lenient so existing users can still save a profile without
back-filling all nine values.

---

# Removing mobile verification

Migration `20260909000000_RemoveMobileVerification` drops
`MST_User.IsMobileVerified` (`bit NOT NULL`).

The feature was never wired up, which is why removal is safe:

- no endpoint sets it — all 5 users are `IsMobileVerified = 0`, so no data is lost
- every `TRN_Otp` row is `Channel = 1` / `Purpose = 1` (email / registration);
  the SMS channel has never issued a code
- no index, default constraint, foreign key or computed column referenced it
- its only consumer was a badge on the admin customer detail screen, which could
  therefore only ever render "Unverified" — already removed from the SPA

## ⚠ Deployment order is not optional

EF Core names every column explicitly in its `SELECT` statements. While
`User.IsMobileVerified` is still in the model, dropping the column makes **every
query against `MST_User` fail** with `Invalid column name 'IsMobileVerified'` —
that is login, the profile endpoint, and every admin user screen.

1. Remove the property, its EF configuration and the DTO field (below).
2. Deploy the API.
3. Then run the migration.

Doing it the other way round is an outage, not a warning.

## Backend removal list

| Where | Change |
|---|---|
| `Regalis.Domain.Entities.Identity.User` | delete the `IsMobileVerified` property |
| `User` EF configuration | delete its mapping line, if explicitly configured |
| `UserDetailDto` | delete `IsMobileVerified` and its projection/mapping — a **breaking response change**, but the admin SPA no longer reads it |
| registration / seeding / admin user creation | delete any `IsMobileVerified = false` assignment |

## Deliberately left alone

- **`IsEmailVerified`** — email OTP verification is live and in use.
- **`TRN_Otp`** — its `Channel` column carries the email channel too; the table
  needs no change.
- **The SMS OTP channel** (`OtpChannel.Sms` and the "No SMS sender configured;
  DEV OTP …" fallback in `Regalis.Infrastructure`) — dead in practice but it is
  OTP *delivery*, not mobile *verification*. Remove it only if you also want SMS
  OTP gone, and check first that notifications do not reuse the same SMS seam.
