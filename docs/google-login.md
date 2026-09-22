# Google social login for Regalis WorkSpace

**Status: not implemented. This document is the implementation spec.**

It cannot be implemented from this repository — see *Why* below. Everything here is
written so that whoever has the API source can apply it without re-deriving the
Regalis-specific parts, and so the keys can be provisioned in parallel.

The code in this file is written against contracts visible in the shipped
`Regalis.Application.xml` doc comments, **not** against source that was read.
Compile-check it against the real repository; signatures may differ.

---

## 1. Why it cannot be done here

| Check | Result |
|---|---|
| `.csproj` / `.sln` in the repo | none |
| `.csproj` / `.sln` anywhere on this machine | none |
| `api/` contents | compiled DLLs, PDBs and XML docs only |
| `Microsoft.AspNetCore.Authentication.Google.dll` in `api/` | absent |
| `"Google"` / `"OAuth"` / `"signin-google"` in `Regalis.API.dll` | absent |
| External-login endpoint in `AuthController` | none (10 endpoints, all local) |
| .NET SDK installed | yes, 10.0.400 — but nothing to build |

`api/web.config` runs the published DLL through `AspNetCoreModuleV2`. Adding a NuGet
package, editing `Program.cs` or adding a controller all require the source tree and a
`dotnet publish`. `.vs/Regalis_Src.slnx` is an empty stub — Visual Studio opened this
folder, not a solution.

**What is needed to proceed:** the `Regalis.API` source repository.

---

## 2. Keys to provision

Create the credential in Google Cloud Console → APIs & Services → Credentials →
Create Credentials → **OAuth client ID** → Application type **Web application**.

You will be issued two values:

| Value | Shape |
|---|---|
| Client ID | `123456789-xxxxxxxx.apps.googleusercontent.com` |
| Client secret | `GOCSPX-xxxxxxxxxxxxxxxx` |

And you must register these in the same screen:

| Field | Value |
|---|---|
| Authorized redirect URI | `https://<api-host>/signin-google` |
| Authorized JavaScript origin | `https://<frontend-host>` — only if the SPA uses Google Identity Services directly |

Also configure the OAuth consent screen (app name, support email, scopes
`openid`, `email`, `profile`). Unverified apps are limited to test users until Google
verifies them — add the QA accounts as test users.

### Redirect URI for this deployment, specifically

Not `https://localhost:7000/signin-google` as in the generic guide. The actual IIS
bindings on this box are:

```
SITE "regalis-api"  id:2  bindings: http/*:81:      state:Started
SITE "regalis-ui"   id:3  bindings: http/*:8080:    state:Started
```

The redirect URI must point at **regalis-api**, not the SPA on 8080.

**Both sites are HTTP-only — there is no HTTPS binding at all.** Google rejects
non-HTTPS redirect URIs for anything except `localhost`, so the feature cannot work
over `http://<host>:81` from another machine. This is a hard prerequisite and it has
lead time (certificate issuance), so start it first:

1. Install a TLS certificate for the API host.
2. Add an `https` binding to `regalis-api` (IIS Manager → Bindings → Add → https).
3. Register `https://<api-host>/signin-google` as the Authorized redirect URI.

It also explains the startup warning *"Failed to determine the https port for
redirect"* — `UseHttpsRedirection()` is active with no HTTPS port to redirect to.

For purely local testing, Google does allow `http://localhost:<port>/signin-google`.

---

## 3. Where the keys go in this app

Not `appsettings.json` (committed) and not `dotnet user-secrets` (development only, and
the app runs under IIS as a Windows service account).

This deployment already passes configuration as **environment variables** in
`api/web.config`, using ASP.NET Core's double-underscore convention:

```xml
<environmentVariables>
  <environmentVariable name="ASPNETCORE_ENVIRONMENT" value="UAT" />
  <environmentVariable name="ConnectionStrings__Default" value="..." />
  <environmentVariable name="Jwt__SigningKey" value="..." />
</environmentVariables>
```

So the two new keys are:

```
Authentication__Google__ClientId       = <client id>
Authentication__Google__ClientSecret   = <client secret>
```

read in code as `Configuration["Authentication:Google:ClientId"]`.

> **`api/web.config` is tracked in git.** `Jwt__SigningKey` is already committed there,
> which is its own problem — anyone with repo access can mint a valid token for any
> user, so rotate it separately. Do not add the Google secret to it.

Set them outside the repository instead, on `regalis-api` (site id 2). From an elevated
prompt, substituting the real values:

```
%windir%\system32\inetsrv\appcmd.exe set config "regalis-api" -section:system.webServer/aspNetCore ^
  /+"environmentVariables.[name='Authentication__Google__ClientId',value='<client-id>']" ^
  /commit:apphost

%windir%\system32\inetsrv\appcmd.exe set config "regalis-api" -section:system.webServer/aspNetCore ^
  /+"environmentVariables.[name='Authentication__Google__ClientSecret',value='<client-secret>']" ^
  /commit:apphost
```

`/commit:apphost` writes to `applicationHost.config`, not to `api/web.config`, so the
secret never enters git. The app pool recycles on commit. The same thing can be done in
IIS Manager → regalis-api → Configuration Editor → `system.webServer/aspNetCore` →
`environmentVariables`.

**Never paste the client secret into a chat, ticket or commit message.** If it has been,
reset it in Google Cloud Console (Credentials → the OAuth client → Reset secret) before
putting it into the deployment.

---

## 4. Corrections to the generic flow, for this codebase

The generic guide is sound OAuth, but six points would break Regalis if applied as-is.

### 4.1 Do not make cookies the default scheme

The guide sets `DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme`.
Regalis authenticates **every** API call with JWT bearer, and
`PermissionAuthorizationHandler` reads `perm` claims from that JWT. Changing the
default scheme breaks every existing endpoint.

Add Google as an *additional* scheme and give it its own short-lived external cookie,
used only to carry the OAuth correlation between challenge and callback:

```csharp
builder.Services.AddAuthentication()          // leave the existing JWT default alone
    .AddCookie("External", o =>
    {
        o.Cookie.Name        = "regalis.external";
        o.Cookie.SameSite    = SameSiteMode.Lax;   // Lax is required for the OAuth redirect
        o.Cookie.SecurePolicy = CookieSecurePolicy.Always;
        o.ExpireTimeSpan     = TimeSpan.FromMinutes(5);
    })
    .AddGoogle("Google", o =>
    {
        o.ClientId     = builder.Configuration["Authentication:Google:ClientId"]!;
        o.ClientSecret = builder.Configuration["Authentication:Google:ClientSecret"]!;
        o.CallbackPath = "/signin-google";
        o.SignInScheme = "External";               // NOT the app's scheme
        o.Scope.Add("email");
        o.Scope.Add("profile");
        o.SaveTokens   = false;
    });
```

### 4.2 Return the `ApiResponse` envelope

`return Ok(new { success = true, token })` bypasses `ApiResponse<T>`. The SPA's
response helper unwraps `.data`, so a raw object breaks the client. Inherit
`ApiControllerBase` and return through the same envelope as every other endpoint.

### 4.3 `MST_User.PasswordHash` is `NOT NULL`

The generic guide assumes `PasswordHash = NULL` for a Google account. This schema does
not allow it. Either make the column nullable (entity + configuration + EF migration),
or store a random, unusable hash. **Nullable is preferable** — a random hash is
indistinguishable from a real one and hides "this account has no password" from every
later code path.

### 4.4 There is no `GoogleId` column

`MST_User` has 26 columns and none of them is an external identifier. Add
`GoogleId nvarchar(64) NULL` plus a **filtered unique index**
(`WHERE GoogleId IS NOT NULL`) so two accounts cannot claim one Google subject. Per
project convention this is an EF migration in `Regalis.Infrastructure`, added to the
existing `IdentityConfigurations.cs`, not a hand-written `.sql`.

### 4.5 Creating the user row is not enough

A `MST_User` row alone produces an account that can log in and do nothing. A customer
also needs:

- a **`MAP_UserRole`** row for the Customer role — without it the JWT carries no
  `role`/`perm` claims, `GET /api/menus/my` returns an empty sidebar, and every guarded
  endpoint returns 403;
- a **wallet** row — the customer portal calls `GET /api/wallet/me` on load;
- `IsEmailVerified = true`, since Google has already verified it.

Do **not** hand-roll these inserts in the controller. Reuse the existing
registration path in `IAuthService` so Google signup and OTP signup converge on one
code path — otherwise the two drift and only one of them stays correct.

### 4.6 Issue the app's own token, with claims

`ITokenService` is already the right seam:

```csharp
// 15-minute JWT with sub, email, user_type, name, role (multi), perm (multi), loc, jti
string CreateAccessToken(User user, IReadOnlyCollection<string> roles,
                         IReadOnlyCollection<string> perms);
string CreateRefreshToken();      // 7-day, 256-bit; raw returned once, hash stored
string HashRefreshToken(string raw);
```

Load the user's roles and permissions and pass them in. A bare JWT without `perm`
claims authenticates but authorises nothing — exactly the 403 loop that
`PASS.EDIT` produced this week.

Return **both** tokens, matching `POST /api/auth/login`, or the SPA's refresh
interceptor will sign the user out after 15 minutes.

---

## 5. Account-linking rule (decide before coding)

When a Google email matches an existing password account:

1. **Reject Google identities whose `email_verified` is false.** Without this check,
   anyone who can create a Google account claiming an address can take over the
   matching Regalis account. This is the single most important line in the feature.
2. Match on `GoogleId` first. Only fall back to email when `email_verified` is true.
3. On an email match, link by writing `GoogleId` onto the existing row — do not create
   a second account.
4. Never let a Google signup choose its own role or portal. Force the Customer role;
   `UserAdminService` already enforces that a role matches the user's portal type.
5. Honour `Status` and `IsDeleted` — a disabled or deleted account must not be revived
   by signing in with Google.

---

## 6. Endpoint shape

Following the existing `AuthController` conventions (`ANON`, `rate=auth`):

```
GET  /api/auth/google              ANON  rate=auth   -> Challenge("Google")
GET  /api/auth/google/callback     ANON  rate=auth   -> exchange, link/create, issue JWT
     /signin-google                                   handled by the Google middleware
```

The callback cannot return JSON to a browser that arrived by redirect — it must
**redirect back into the SPA** with the tokens. Prefer setting the refresh token as an
`HttpOnly` cookie and passing a short-lived one-time code in the query string that the
SPA exchanges, rather than putting a JWT in a URL where it lands in browser history,
`Referer` headers and IIS logs.

---

## 7. Frontend

Deliberately not built yet. A "Continue with Google" button that 404s is worse than no
button. Once `GET /api/auth/google` exists, the SPA side is small:

- a button on the login page (`frontend/assets/` — the login view lives in the index
  chunk) that navigates to `/api/auth/google`;
- a callback route that reads the one-time code, calls the exchange endpoint, stores the
  tokens through the existing session store, and redirects to `/`;
- a precache revision bump in `frontend/sw.js` for whichever chunk changes.

---

## 8. Order of work

1. Provision the Google credential and the API HTTPS binding (can start now).
2. Set the two environment variables at the IIS application level.
3. In the API source: package, schema (`GoogleId`, nullable `PasswordHash`), EF
   migration, auth wiring, service method, controller.
4. Publish the API to `api/`.
5. Frontend button and callback route.
6. Test: new Google account; existing-email link; unverified-email rejection; disabled
   account; token refresh after 15 minutes.
