# Google sign-in — what to add to the `Regalis.API` source

One new endpoint, `POST /api/auth/google`. Everything else is existing machinery.

Code: [`GoogleAuthController.cs`](GoogleAuthController.cs) — controller, interface,
DTO and the service implementation (the last one commented out at the bottom of the
file so the snippet stays a single unit).

Design rationale and the deployment prerequisites: [`../google-login.md`](../google-login.md).

---

## Why the token flow, not the redirect flow

The generic ASP.NET guide uses the authorization-code redirect flow
(`/signin-google`, cookie middleware, `Challenge()`). That works, but on this codebase
it costs more and risks more:

| | Redirect flow | Token flow (chosen) |
|---|---|---|
| New endpoints | 2 + middleware callback | **1** |
| Cookie auth scheme | required — and setting it as the default breaks every JWT-guarded endpoint | **none** |
| Google redirect URI registration | required | not required |
| Response | browser redirect, token handed off via URL or cookie | plain JSON, same as `/api/auth/login` |
| SPA change | new callback route + token parsing | button only |

The token flow also keeps the browser out of the credential path: the SPA never sees a
client secret, and the API never trusts anything the SPA says about who the user is —
only a Google-signed ID token it verifies itself.

---

## Steps

**1. Package**

```
dotnet add Regalis.Infrastructure package Google.Apis.Auth
```

`GoogleJsonWebSignature.ValidateAsync` verifies signature, issuer, audience and expiry
against Google's JWKS with key caching. Do not decode the token manually — an
unverified ID token is attacker-controlled JSON.

**2. Schema** — `MST_User` needs one column and one constraint relaxed.

```csharp
// Regalis.Domain/Entities/Identity/User.cs
public string? GoogleId { get; set; }
public string? PasswordHash { get; set; }   // was non-nullable
```

```csharp
// Regalis.Infrastructure/Persistence/Configurations/IdentityConfigurations.cs
builder.Property(u => u.GoogleId).HasMaxLength(64);
builder.HasIndex(u => u.GoogleId)
       .IsUnique()
       .HasFilter("[GoogleId] IS NOT NULL");   // two accounts cannot claim one subject
builder.Property(u => u.PasswordHash).IsRequired(false);
```

```
dotnet ef migrations add AddGoogleIdToUser --project Regalis.Infrastructure
```

`PasswordHash` is currently `NOT NULL`. A Google-only account has no password; storing a
random unusable hash instead would be indistinguishable from a real one and would hide
"this account cannot use password login" from every later code path.

**3. Service + DI**

```csharp
// Regalis.Infrastructure/DependencyInjection.cs
services.AddScoped<IGoogleAuthService, GoogleAuthService>();
```

No `AddAuthentication` / `AddGoogle` / `AddCookie` changes. The existing JWT bearer
default stays exactly as it is — that is the point of this flow.

**4. Configuration**

`api/appsettings.json` gets the shape with empty values; real values come from
environment variables at the IIS application level. Names and the `appcmd` commands are
in [`../google-login.md`](../google-login.md) §3. The **client ID is public** — it ships
in the SPA. Only the secret is sensitive, and this flow does not need it server-side
unless you later add the redirect flow too.

**5. Publish** to `api/`, then the SPA button.

---

## Review checklist

Security-relevant, in rough order of how badly it goes wrong:

- [ ] **`payload.EmailVerified` is checked.** Without it the email-match branch is an
      account-takeover path: create a Google account claiming `admin@…`, sign in, own
      the account.
- [ ] **Audience pinned** to our own client ID in `ValidationSettings`, so an ID token
      minted for a different Google app cannot be replayed.
- [ ] **New accounts get `MAP_UserRole` and `MST_Wallet`**, ideally by reusing the OTP
      registration path rather than a second `INSERT`. A bare `MST_User` row produces an
      account with no `perm` claims — empty sidebar, 403 everywhere. This exact failure
      cost a day of debugging on `PASS.EDIT`.
- [ ] **Role is forced to Customer.** A self-service signup must never be able to pick a
      role or a portal type.
- [ ] **`IsDeleted` / `IsActive` / `Status` are honoured**, so a disabled account is not
      revived by signing in with Google.
- [ ] **Refresh token starts a new `FamilyId`**, matching password login. The family +
      `ConsumedUtc` + `ReplacedByTokenHash` columns implement reuse detection; a token
      inserted without a family silently opts out of it.
- [ ] Endpoint is `[AllowAnonymous]` **and** rate-limited on the `auth` policy — it is
      an unauthenticated surface that creates accounts.
- [ ] Response goes through `ApiResponse<T>` (inherit `ApiControllerBase`). The SPA
      unwraps `.data`; a raw object breaks the client.
- [ ] Failures throw the typed exceptions (`UnauthorizedException`,
      `ForbiddenException`, `BusinessRuleException`) and let the middleware map them.

---

## Tests worth having

1. New Google account → user created, Customer role attached, wallet exists, JWT carries
   `perm` claims.
2. Google email matches an existing password account → linked, **one** user row, no
   duplicate.
3. `email_verified = false` → rejected.
4. ID token with a different `aud` → rejected.
5. Expired / tampered token → rejected.
6. Deleted or suspended account → rejected, not revived.
7. Refresh works 15 minutes later and rotates within one family.
