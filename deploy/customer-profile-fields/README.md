# Customer profile: every detail the member filled in, on the admin Customer screen

Admin → Customers → a customer → **Profile Details** shows "Not captured" for the
GSTIN, date of birth, gender and the structured address, even when the member
filled them in on the customer portal. The values are saved in `MST_User`; the API
just never sends them to the admin portal. `GET /api/customers/{id}` returns
`UserDetailDto`, which carries only the legacy single-line `Address` and none of
`Gstin`, `DateOfBirth`, `Gender`, `AddressLine1/2`, `City`, `State`, `PostalCode`,
`Country`. (Only `GET /api/profile` returns them, and that is the member's own.)

The SPA side is already done: `AdminCustomersPages-IA5T7i_C.js` renders all of these
from `customer.*` and falls back to the single address line for accounts that have
no structured address. Only the API needs the change below.

## Interim: patching the compiled API (applied to a local dev copy only)

The API source is not in this repo. On 27 Oct 2026 a local dev copy of
`api\Regalis.Application.dll` and `api\Regalis.Infrastructure.dll` was patched in place
with `DtoPatcher\`; **the patched DLLs are not committed**, so the DLLs in this repo are
still the originals (SHA-256 below) and the fields stay "Not captured" until the source
change below is deployed or the patch is applied. The patch (Mono.Cecil) adds
ten settable properties on `UserDetailDto` (`Gstin`, `DateOfBirth`,
`Gender`, `AddressLine1`, `AddressLine2`, `City`, `State`, `PostalCode`, `Country`,
`IsProfileComplete`) and `ToDetail` setting them from the user right after it builds
the record. Nothing else in either assembly changed (same type/method counts apart
from the 10 properties and their 20 accessors).

| File | Original SHA-256 | Patched SHA-256 |
|---|---|---|
| Regalis.Application.dll | `cec8f40c…547290` | `2947626c…e0abf` |
| Regalis.Infrastructure.dll | `6051203a…e23abf` | `bb1fadf8…e008` |

To undo: `git checkout -- api/Regalis.Application.dll api/Regalis.Infrastructure.dll`
and restart the API.

To reapply (e.g. to a fresh build of the same version):
`dotnet run --project DtoPatcher -- <dir with the two original dlls> <out dir> "C:\Program Files\dotnet\shared\Microsoft.NETCore.App\<version>" <api dir>`.
It refuses to run on an assembly that is already patched or whose `ToDetail` no longer
ends in `return new UserDetailDto(...)`.

**The next publish from source overwrites these two DLLs** and the fields disappear
again, so make the source change below as well before the next API deploy.

## The change (in the API source, `backend\src`)

Two edits. The new members are optional, so nothing else that builds the record has
to change, and both `/api/users/{id}` and `/api/customers/{id}` pick them up (both go
through `UserAdminService.GetUserAsync` → `ToDetail`, the only place the record is
constructed).

**1. `Regalis.Application/Administration/UserAdminDtos.cs`** — trailing members on `UserDetailDto`:

```csharp
public record UserDetailDto(long UserId, string FullName, string Email, string MobileNumber,
    byte UserType, byte Status, string? CompanyName, string? Address, string? PhotoPath,
    int? DefaultLocationId, bool IsEmailVerified, bool IsMobileVerified, bool MustChangePassword,
    DateTime? LastLoginUtc, DateTime? LockoutEndUtc, IReadOnlyList<int> RoleIds,
    IReadOnlyList<string> Roles, DateTime CreatedDate, bool IsActive,
    string? Gstin = null, DateOnly? DateOfBirth = null, byte? Gender = null,
    string? AddressLine1 = null, string? AddressLine2 = null, string? City = null,
    string? State = null, string? PostalCode = null, string? Country = null,
    bool IsProfileComplete = true);
```

**2. `Regalis.Infrastructure/Services/UserAdminService.cs`** — `ToDetail`, pass them through:

```csharp
private static UserDetailDto ToDetail(User user) => new(
    user.UserId, user.FullName, user.Email, user.MobileNumber, (byte)user.UserType,
    (byte)user.Status, user.CompanyName, user.Address, user.PhotoPath, user.DefaultLocationId,
    user.IsEmailVerified, user.IsMobileVerified, user.MustChangePassword, user.LastLoginUtc,
    user.LockoutEndUtc, user.UserRoles.Select(ur => ur.RoleId).ToList(),
    user.UserRoles.Select(ur => ur.Role.RoleName).ToList(), user.CreatedDate, user.IsActive,
    user.Gstin, user.DateOfBirth, (byte?)user.Gender,
    user.AddressLine1, user.AddressLine2, user.City, user.State, user.PostalCode, user.Country,
    user.IsProfileComplete);
```

No migration: every column already exists. No permission change: the endpoints keep
`CUSTOMER.VIEW` / `USER.VIEW`.

## Not covered: the photo

Only the member can download their photo (`GET /api/profile/photo`). Showing it to an
admin needs a new endpoint, e.g. `GET /api/customers/{id}/photo` under
`CUSTOMER.VIEW` doing what `ProfileService.GetPhotoAsync` does for a given user id,
plus the page fetching it as a blob. Until then the page says whether one was uploaded.

## Deploy

Build and publish the API the usual way (as in `deploy-reports-analysis.cmd`), copy
the binaries except `web.config`, restart the `regalis-api` pool. Then copy the two
front-end files if they are not live yet:

- `frontend/assets/AdminCustomersPages-IA5T7i_C.js`
- `frontend/sw.js` (revision `cust-profile-fields-2`)

## Check

`GET /api/customers/10032` (signed in as admin) now includes `gstin`, `dateOfBirth`
(`"YYYY-MM-DD"`), `gender`, `addressLine1` … `country`. On the Customer Profile page
every field shows the member's value; one they have not entered reads "Not captured".
