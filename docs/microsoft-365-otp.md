# Microsoft 365 OTP delivery (Microsoft Graph)

One-time codes can be delivered through a Microsoft 365 mailbox instead of SMTP or the
console. This is a **delivery channel only** — everything else about OTP was already
built and is unchanged.

## What already existed, and was reused

Nothing about the OTP lifecycle is new. `TRN_Otp` already stored codes hashed
(`SHA256(code|destination)`, never plaintext), with `ExpiresUtc`, `AttemptCount`,
`MaxAttempts` and `ConsumedUtc`. `AuthService` already generated codes with
`RandomNumberGenerator.GetInt32(0, 1_000_000)`, compared them in constant time,
invalidated outstanding codes on resend, enforced a 60-second resend cooldown, and
activated the account on success. `POST /api/auth/verify-otp` and
`POST /api/auth/resend-otp` already existed.

So no new OTP table, entity, migration, endpoint or verification logic was added, and
none of the above changed. Adding a parallel `send-email-otp` / `verify-email-otp` set
would have been a second copy of authentication logic.

## What was added

| | |
|---|---|
| `GraphOtpSender` | `Regalis.Infrastructure/Adapters/GraphOtpSender.cs` — a third `IOtpSender`, beside `ConsoleOtpSender` and `SmtpOtpSender` |
| `GraphMailOptions` | same file; bound from the `MicrosoftGraph` section |
| Channel selection | `Otp:Channel` now accepts `Graph` as well as `Smtp` and `Console` |
| Boot check | `Program.cs` fails fast if the channel is `Graph` and any key is missing |
| `EMAIL_NOT_VERIFIED` | a distinct error code on login, so the SPA can route an unverified member to the OTP screen without also routing a deactivated one |

Packages: `Microsoft.Graph` 6.7.0 and `Azure.Identity` 1.13.2, both on
`Regalis.Infrastructure`. `System.IdentityModel.Tokens.Jwt` moved 8.3.0 → 8.18.0 —
forced, not chosen: Graph pulls `Microsoft.IdentityModel.Protocols.OpenIdConnect`
8.18.0, which requires it. Azure.Identity is pinned to 1.13.2 because 1.21.0 drags in
`Microsoft.Extensions.Hosting.Abstractions` 10.x, which does not belong in a net8.0 app.

## How the authentication works

App-only, no signed-in user:

```
ClientSecretCredential(tenantId, clientId, clientSecret)
        -> Microsoft Entra ID
        -> access token for https://graph.microsoft.com/.default
        -> POST /users/{SenderEmail}/sendMail
```

`.default` requests exactly the application permissions the tenant has already
consented to — app-only flows cannot ask for scopes at runtime. The credential is built
once and reused: it caches the token and refreshes it on expiry, so a per-email
credential would mean a token request per email.

`SaveToSentItems` is `false`; OTP mail does not belong in a shared mailbox's Sent
folder.

## Configuration

Set these as environment variables, never in a committed file:

```
Otp__Channel=Graph
MicrosoftGraph__TenantId=<directory (tenant) id>
MicrosoftGraph__ClientId=<application (client) id>
MicrosoftGraph__ClientSecret=<client secret VALUE, not the secret id>
MicrosoftGraph__SenderEmail=noreply@yourdomain.com
```

On this UAT box they live in `api/web.config` under `<environmentVariables>`, which is
where the JWT signing key already lives and which is excluded from every commit.
`appsettings.json` carries the four keys as empty placeholders so the shape is
discoverable.

`Otp:Channel` stays `Console` until `SenderEmail` is filled in. The boot check refuses
to start with `Graph` and an incomplete section, so flipping the channel early takes the
site down rather than failing quietly at the first registration.

## What must be true in Microsoft 365

1. The app registration has **Microsoft Graph → Mail.Send → Application** (not
   Delegated), with **admin consent granted** — the "Granted for …" tick in Entra.
2. `SenderEmail` is a **real, licensed mailbox** in the same tenant. App-only
   `sendMail` posts as a specific user; it cannot send as the application, and a
   distribution list or unlicensed alias will not work.
3. Optionally, scope the app with an **application access policy** so it can only send
   as that one mailbox. Without one, `Mail.Send` application permission lets the app
   send as *any* mailbox in the tenant, which is a large blast radius for an OTP sender:

   ```powershell
   New-ApplicationAccessPolicy -AppId <client id> `
     -PolicyScopeGroupId noreply@yourdomain.com `
     -AccessRight RestrictAccess `
     -Description "Regalis OTP sender"
   ```

## Failure handling

Graph reports a missing mailbox, withheld consent and a bad secret all as `ODataError`.
The sender logs the full error and rethrows, so the existing exception middleware maps
it to the standard generic envelope — the caller never sees Graph's text, a token, or
the secret.

Logs record the purpose and a masked address (`k******a@regalis.in`). The **code is
never logged**, on any path, including failures: an OTP in a log file is an OTP that
anyone with log access can use. The client secret never appears in a log, an exception
message or a response.

## Testing

`GraphOtpSenderTests` covers the configuration contract, the fail-fast message naming
the missing keys, that neither the secret nor the code leaks into that message, and the
SMS fallback. Nothing in the suite reaches the network or sends mail — every test either
stops before a Graph client is built or takes a path that never builds one.

Sending for real needs a mailbox and a consented tenant, so it is a manual check:
register a customer, then read the inbox.
