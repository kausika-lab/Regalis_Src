# SECURITY.md
**Regalis WorkSpace (`D:\Projects\Regalis`, the release line) — OWASP Top 10 (2021) posture**
Version 1.0 · 30 August 2026 · covers tag `release-3` + the `release-3.1` defect fixes

> **Scope.** A defensive document about code we own. No exploit code was written and no live
> system was probed. Unlike a source-only review, every claim here is backed by a **running
> system**: 101 automated tests (87 integration against a real API + SQL Server, 14 unit)
> execute on every merge, including a live execution of the full role/permission matrix.
>
> **Lineage note.** A different `SECURITY.md` circulating in `c:\Users\nandh\source\repos\
> Regalis_Phase1_Secured` documents a **separate, parallel implementation** — its findings
> (file-serving routes, `TRN_UploadedFile`, cookie names) do not describe this codebase.
> Its transferable findings were cross-checked against this repo; the results are F-01…F-03
> below.

---

## 1. Status at a glance

| | |
|---|---|
| Findings across all reviews (Day 10, Day 11, this pass) | **6** |
| Fixed with regression tests | **5** (F-01, F-03, and the three Day 10/11 findings) |
| Accepted with mitigations and a scheduled revisit | **1** (F-02) |
| Automated evidence | `HardeningTests` (authorization coverage by reflection, matrix-in-full, headers), `FeatureCompleteTests` (leak regressions), `SupportAndReportsTests` (upload sniffing), `AuthFlowTests` (lockout, rotation, enumeration) |

---

## 2. Findings and dispositions

### F-01 · Registration disclosed whether an account existed (A07) — **fixed in release-3.1**
`POST /auth/register` answered `"An account with this email or mobile number already
exists."` while login, forgot-password and resend-otp were already deliberately generic —
registration was the one remaining enumeration oracle over the member base.
**Fix:** the response is now byte-for-byte the shape of a fresh registration; nothing is
created or overwritten, no OTP is issued (so verification cannot proceed), and the attempt
is logged server-side. Regression test proves the impostor's password does not work and the
original still does. *When SMTP arrives (risk #3), also notify the real owner out-of-band.*

### F-02 · Refresh token persisted in `localStorage` (A02/A07) — **accepted for RC, revisit at go-live**
The SPA persists the 7-day refresh token in `localStorage`, readable by script on our origin.
**Why accepted now:** (1) no XSS sink exists in the frontend — no `dangerouslySetInnerHTML`,
`innerHTML` or `eval`; user content renders through React text nodes; (2) refresh tokens are
**single-use and rotate**, stored server-side only as hashes, and **reuse detection revokes
the whole token family** (`AuthService` — a stolen token dies the moment the victim's client
refreshes); (3) logout and password change revoke outstanding tokens. **Scheduled fix**
(go-live window): move the refresh token to an `HttpOnly; Secure; SameSite=Strict` cookie
scoped to `/api/auth` — this requires the same-site SPA/API deployment shape already
documented in `deploy/DEPLOYMENT.md`, which is why it is sequenced with the deployment.

### F-03 · Forwarded headers were not honoured (A05) — **fixed in release-3.1**
Behind the runbook's reverse proxy, `RemoteIpAddress` was the proxy and the scheme was the
backend hop's — so HTTPS redirection, HSTS and the per-IP rate-limit partitions all keyed on
the wrong values. **Fix:** `UseForwardedHeaders` (X-Forwarded-For/Proto) ahead of the
pipeline. *Precondition:* the default trusts loopback proxies only, matching the runbook's
same-host nginx; a remote proxy must be added to `KnownProxies`.

### Previously found and fixed (regression-tested)
- **Day 11:** `/dashboard/admin` was reachable with the customer-held `DASHBOARD.VIEW` —
  operational aggregates (revenue, occupancy) are now admin-portal only.
- **Day 10:** `includeUnavailable=true` on `/services` leaked hidden catalogue items to
  customers holding `SERVICE.VIEW` — now honoured only for `SERVICE.EDIT` holders.
- **Day 10:** the full `/banners` grid leaked future/admin banners to customers holding
  `BANNER.VIEW` — customers now receive only live customer-portal rows.

---

## 3. OWASP Top 10 (2021) coverage

| Category | Verdict | Evidence |
|---|---|---|
| **A01** Broken Access Control | 3 findings, all **fixed** | Fallback authorization policy: authentication is the default; a reflection test fails the build if any action lacks explicit `[Authorize]`/`[HasPermission]`/`[AllowAnonymous]`. The seeded role/permission matrix is executed **live** across Manager/Finance/Support/Housekeeping + a customer negative sweep (`HardeningTests`). Row scoping returns **404, never 403**, for another customer's booking/ticket (tested). |
| **A02** Cryptographic Failures | 1 accepted (F-02) | PBKDF2 password hashing with per-user salt and password history; refresh tokens and OTPs stored hashed, single-use, family-revoked on reuse; QR payloads HMAC-SHA256-signed and verified in constant time. |
| **A03** Injection | **clean** | EF Core parameterisation throughout; the one raw-SQL site (`NumberSequenceService`) is a fixed statement with parameters; no `innerHTML`/`eval` in the SPA. |
| **A04** Insecure Design | **clean** | Booking confirm and inventory issue run serializable with re-checks inside the transaction (19-of-20 conflict test); gap-free numbers by atomic UPDATE; append-only ledgers (wallet, inventory, access log, audit) with compensating-entry corrections. |
| **A05** Security Misconfiguration | 1 finding (F-03) **fixed** | Empty secrets in committed config; startup refuses a missing signing key outside Development; Swagger off in Production; CORS origins from config only; security headers asserted by test on live responses; HSTS + HTTPS redirect outside Development. |
| **A06** Vulnerable Components | **clean** | .NET 8 LTS + EF Core 8, ClosedXML 0.105, QuestPDF 2024.12; `npm audit` clean at Release 3. Re-run both scans at each release (checklist §5). |
| **A07** Identification & Auth | 1 finding (F-01) **fixed** | Lockout after N failures (setting-driven), generic login/forgot/resend/register responses, OTP hashed + attempt-capped + single-use, refresh rotation with family revocation, portal separation enforced at login. |
| **A08** Software & Data Integrity | **clean** | Gateway payments verified by recomputing the HMAC signature server-side; webhooks idempotent on the gateway payment id; a client-reported success is never trusted (tested). |
| **A09** Logging & Monitoring | **clean** | Serilog with correlation ids on every response; append-only `TRN_AuditLog` (read-only screen + export; no update/delete path); audit interceptor redacts hashes/signatures; overrides and denials audited. |
| **A10** SSRF | **clean** | No user-supplied URL reaches an HTTP client; the only outbound call is the configured payment gateway. |

**File uploads** (tickets, profile photos): extension allow-list **and** size ceilings
**and** leading-magic-byte sniffing (a renamed executable is refused — tested), GUID storage
names outside the web root, streaming only through ownership-checked endpoints.

---

## 4. Deployment preconditions

1. **Trusted proxy list** — F-03's forwarded headers trust loopback by default; set
   `KnownProxies` if the proxy is not on the same host.
2. **HTTPS end-to-end** including UAT (HSTS is active outside Development).
3. **`Storage:UploadRoot` outside any web root** (default `App_Data/uploads`; re-check any
   override).
4. **`Jwt__SigningKey` ≥ 32 random bytes** from the environment/secret store — the app
   refuses to start without it outside Development.
5. **F-02 revisit at go-live**: same-site SPA/API shape → HttpOnly cookie refresh.

## 5. Re-verification checklist (each release)

- [x] Full suite green: 87 integration + 14 unit (includes matrix-in-full, header, sniffing
      and enumeration regressions) — release-3.1
- [x] `dotnet publish -c Release` smoke: health 200 / unauth 401 / admin login / export 200 —
      release-3
- [x] `npm audit --omit=dev`: 0 vulnerabilities; `dotnet list package --vulnerable
      --include-transitive`: none — re-run 30 Aug 2026 (release-3.1)
- [ ] Secret scan over the repo (no credentials outside the test project)
- [ ] On the deployed host: HSTS header present; `/health/ready` 200; rate limiter returns
      429 under the configured burst; forwarded client IP visible in the request log
