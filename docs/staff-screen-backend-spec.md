# Staff screen & maintenance staff picker — server-side work

**Status:** the SPA side shipped; everything below is outstanding and needs the API source.

The `/admin/staff` screen and the staff dropdown on the maintenance screen were built
against the **deployed** API, because this repo ships `api/` as compiled DLLs with no C#
source and no project files. Everything reachable from the existing endpoints is done.
This document is the remainder — the parts that need `Regalis.Application`,
`Regalis.Infrastructure` and `Regalis.API`.

Each item says what the SPA currently does instead, so you can see what to delete once the
proper version lands.

---

## 1. Location filter + location on the user list

**Why:** the Staff screen is specified to show a *default location* column and filter by
location. `GET /api/users` has no `LocationId` parameter, and `UserListItemDto` carries no
location field — only `UserDetailDto` has `defaultLocationId`.

**Change:**
- `Application/Users/UserDtos.cs` — add `int? LocationId` to the list request record; add
  `int? DefaultLocationId` and `string? DefaultLocationName` to `UserListItemDto`.
- `Infrastructure/Services/UserAdminService.cs` — apply the filter and project the join.
  Keep it a single query; do not N+1 the location per row.

**SPA today:** no location column and no location filter. Both are simply absent. Add them
to `StaffPage` in `UsersPage-BzCggvKW.js` once the fields exist.

---

## 2. Staff lookup endpoint  ← highest value

**Why:** this is the one that actually blocks a role today. The maintenance screen populates
its staff picker from `GET /api/users?userType=3&status=1`, which requires `USER.VIEW`.

Checked against the live UAT grants:

| Role | HOUSEKEEPING.CREATE | USER.VIEW |
|---|---|---|
| ADMIN | yes | yes |
| MANAGER (Operations Manager) | **yes** | **no** |
| SUPPORT | no | no |
| HOUSEKEEPING | no | no |

So an Operations Manager can create a maintenance schedule but cannot list staff. The call
403s, the picker is empty, and the save fails. That is true today and the SPA change does not
fix it — it only reports it clearly instead of silently auto-assigning the first staff user.

**Change:**
- `Application/Housekeeping/HousekeepingDtos.cs` — `public record StaffLookupDto(long UserId, string FullName, string? Email, string? MobileNumber);`
- `Application/Housekeeping/IHousekeepingService.cs` — `Task<IReadOnlyList<StaffLookupDto>> GetStaffLookupAsync(int locationId, CancellationToken ct);`
- `Infrastructure/Services/HousekeepingService.cs` — `UserType.Staff`, `UserStatus.Active`,
  `DefaultLocationId == locationId`, `IsDeleted == false`. Projection only — do not return
  `UserDetailDto`.
- `API/Controllers/HousekeepingController.cs` — `GET /api/housekeeping/staff-lookup`,
  `[HasPermission(PermissionCodes.Housekeeping.View)]`.

**Confirm first:** guarding a user-listing route with `HOUSEKEEPING.VIEW` is a deliberate
widening. It exposes name/email/mobile for active staff at one location to every role holding
`HOUSEKEEPING.VIEW` — which today includes SUPPORT and HOUSEKEEPING. Check
`ROLE_PERMISSION_MATRIX.md` (not in this repo) before committing. If that is too broad, the
alternative is a new `HOUSEKEEPING.ASSIGN` code granted to ADMIN and MANAGER only.

**SPA today:** calls `users.list({userType:3,status:1,pageSize:100})`. Swap the call and drop
the `status`/`userType` params once the lookup exists.

---

## 3. Location scoping on the picker (Invariant 7)

**Why:** the picker is specified to filter to the schedule's `LocationId` and reload when the
form's location changes. Not possible today — see item 1, there is no location on the user
list. Falls out of item 2 for free, since the lookup takes `locationId`.

**SPA today:** loads all active staff regardless of location, once on mount. When the lookup
lands, key the fetch on the form's location so it refetches on change.

---

## 4. Server-side validation of `StaffUserId`

**Why:** schedule create/update currently accepts any `StaffUserId`. There is no check that it
resolves to an active Staff user in the schedule's location.

**Change:** in `HousekeepingService` create/update, before the transaction, resolve the user
and throw `AppValidationException` with a field-level error on `staffUserId` if it is not
`UserType.Staff`, not `UserStatus.Active`, or not in the schedule's location. Let the
middleware map it — do not return a bare 400.

**Do not touch the block path.** A schedule with `BlocksWorkspace` must keep writing its
`TRN_WorkspaceBlock` inside the same transaction (Invariant 12). Reassigning staff must not
open a second calendar path.

---

## 5. Photo upload for staff

**Why:** the Staff screen is specified to take an optional photo. The only photo endpoint is
`POST /api/profile/photo`, which is the *current user's own* photo. Neither
`CreateUserRequest` nor `UpdateUserRequest` has a photo field, so an admin cannot set one for
someone else.

**Change:** `POST /api/users/{id}/photo`, `[HasPermission(PermissionCodes.User.Edit)]`. The
controller wraps `IFormFile` in `AttachmentUpload(fileName, contentType, sizeBytes, stream)` —
the Application layer never sees `IFormFile`. Validate extension, size **and** leading magic
bytes via `FileTypeSniffer`; store outside the web root; serve only through the authorised
download endpoint; persist the path in `MST_User.PhotoPath`.

**SPA today:** no photo control on the form. `StaffView` reads `photoPath` from
`UserDetailDto` but nothing writes it.

---

## 6. `MustChangePassword` on staff creation

**Why:** new staff are specified to be created with `MustChangePassword = true`.
`CreateUserRequest` has no such field, and whether the server sets it for admin-created users
cannot be determined from the compiled assemblies.

**Change:** confirm the current behaviour in `UserAdminService.CreateAsync`. If it is not
already forced, either force it for admin-created accounts or add an explicit flag to the
request.

**SPA today:** the form tells the admin "The staff member must change it at first sign-in",
matching the existing `/admin/users` copy. If the server does not enforce it, that text is a
lie and needs either the server fix or a copy change.

---

## 7. Fields the edit form cannot reach

`UpdateUserRequest` is `fullName, mobileNumber, roleIds, companyName, address,
defaultLocationId`. Consequences:

- **Email is not editable.** The SPA shows it as a disabled field with a hint. If staff email
  should be changeable, add it to the request — and mind the uniqueness rule.
- **Status is not settable on the form.** Status moves only through the
  `activate`/`deactivate`/`lock`/`unlock` PATCH endpoints, so the SPA drives it from the list
  row actions and says so under the form. Fine as-is; noted so it is not mistaken for an
  oversight.

---

## 8. Menu row belongs in `SeedCatalogue`

The sidebar entry shipped as `db/migrations/20260920010000_AddStaffScreenMenu.sql` because the
seeder cannot be edited here, and UAT runs `Database__SeedOnStartup=false` anyway.

Add the same row to `SeedCatalogue` so a fresh database gets it: `MenuCode ADM_ADMIN_STAFF`,
parent `ADM_ADMINISTRATION`, route `/admin/staff`, icon `UsersRound`, `PermissionId` =
`USER.VIEW`, PortalType Admin, DisplayOrder 471, plus the `MAP_RoleMenu` grant to ADMIN.
Seeding is idempotent and matches on `MenuCode`, so it will not duplicate the migration's row.

**Note on `MenuLevel`:** the brief specified `MenuLevel.Screen`. Every rendering sibling under
`ADM_ADMINISTRATION` uses `2`, and the sidebar builds from that, so the migration used `2` to
match `/admin/users`. Move both together if the convention changes.

---

## 9. Validation parity

The SPA validates with zod: name required, Indian mobile `^(?:\+91)?[6-9][0-9]{9}$`, email
format, default location required, at least one role. Note the Indian-format rule is **stricter
than the existing `/admin/users` form**, which uses a generic `^\+?[0-9]{10,15}$`.

Mirror these in FluentValidation (auto-registered by assembly scan) and add what the client
cannot check: email and mobile unique across `MST_User`. Client validation is cosmetic.

Role assignment already flows through `UserAdminService.ValidateRoleAssignmentAsync`; the SPA
mirrors it by filtering the role checkboxes to `portalType != 1`, so a staff account can never
be offered `CUSTOMER`. Server-side enforcement is unchanged and remains the real gate.

---

## Unrelated finding, worth your attention

`MST_Role` RoleId 1 (`CUSTOMER`) holds **all 131 permissions** — the same grant count as
`ADMIN` — including `USER.DELETE`, `ROLE.EDIT` and `SETTING.EDIT`. Three live accounts hold
that role. A proposed, **unapplied** remediation with a dry-run block and a restoring rollback
is at `db/migrations/20260920020000_RevokeCustomerAdminPermissions.sql`; its header has the
evidence and the likely cause.
