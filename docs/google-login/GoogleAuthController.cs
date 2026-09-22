// =============================================================================
//  Regalis WorkSpace — Google sign-in (Google Identity Services token flow)
//
//  DROP THIS INTO THE Regalis.API SOURCE REPOSITORY. It cannot be compiled from
//  the deployment snapshot, which ships DLLs only.
//
//  Written against the contracts visible in the shipped Regalis.Application.xml
//  doc comments and the live database schema — NOT against source that was read.
//  Expect to fix names where they differ (IAuthService members, DbContext set
//  names, the exact DI registration). The shape and the security decisions are
//  the part worth keeping.
//
//  Placement, per the project layout:
//      Regalis.API/Controllers/GoogleAuthController.cs      <- this file, part 1
//      Regalis.Application/Auth/IGoogleAuthService.cs       <- part 2
//      Regalis.Application/Auth/AuthDtos.cs                 <- add GoogleLoginRequest
//      Regalis.Infrastructure/Services/GoogleAuthService.cs <- part 3
// =============================================================================

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Regalis.Application.Auth;

namespace Regalis.API.Controllers;

/// <summary>
/// Google sign-in. The SPA obtains a Google ID token in the browser through
/// Google Identity Services and posts it here; this endpoint verifies it and
/// returns the same payload as <c>POST /api/auth/login</c>, so every caller
/// downstream is identical whether the user signed in with a password, an OTP
/// or Google.
/// </summary>
[ApiController]
[Route("api/auth")]
public sealed class GoogleAuthController : ApiControllerBase
{
    private readonly IGoogleAuthService _google;

    public GoogleAuthController(IGoogleAuthService google) => _google = google;

    /// <summary>Exchange a Google ID token for a Regalis session.</summary>
    /// <remarks>
    /// Anonymous and rate limited on the same policy as the other auth
    /// endpoints — this is an unauthenticated surface that creates accounts.
    /// </remarks>
    [HttpPost("google")]
    [AllowAnonymous]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> Google(
        [FromBody] GoogleLoginRequest request,
        CancellationToken ct)
    {
        // Returns the same AuthResponse as Login: access token, refresh token,
        // user summary. Failures surface as the typed exceptions the middleware
        // already maps — do not hand-roll status codes here.
        var result = await _google.SignInAsync(request, HttpContext.Connection.RemoteIpAddress?.ToString(), ct);
        return Ok(result);
    }
}

// =============================================================================
//  PART 2 — Regalis.Application/Auth/
// =============================================================================

namespace Regalis.Application.Auth
{
    /// <summary>The ID token the browser received from Google Identity Services.</summary>
    public sealed record GoogleLoginRequest(string IdToken);

    /// <summary>
    /// Verifies a Google ID token and resolves it to a Regalis session,
    /// creating the customer account on first sign-in.
    /// </summary>
    public interface IGoogleAuthService
    {
        /// <summary>
        /// Verifies the token against Google's published keys, links or creates
        /// the user, and issues the same token pair as a password login.
        /// </summary>
        Task<AuthResponse> SignInAsync(GoogleLoginRequest request, string? ip, CancellationToken ct);
    }
}

// =============================================================================
//  PART 3 — Regalis.Infrastructure/Services/GoogleAuthService.cs
//
//  NuGet:  dotnet add package Google.Apis.Auth
//
//  Google.Apis.Auth.GoogleJsonWebSignature.ValidateAsync does the signature
//  check, issuer check, expiry check and audience check against Google's JWKS,
//  with key caching. Do not hand-roll this, and do not "decode" the token
//  without verifying it — an unverified ID token is attacker-controlled JSON.
// =============================================================================
/*

using Google.Apis.Auth;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Regalis.Application.Abstractions;
using Regalis.Application.Auth;
using Regalis.Domain.Entities.Identity;
using Regalis.Shared.Constants;

namespace Regalis.Infrastructure.Services;

public sealed class GoogleAuthService : IGoogleAuthService
{
    private readonly RegalisDbContext _db;
    private readonly ITokenService _tokens;
    private readonly IDateTimeProvider _clock;
    private readonly IConfiguration _config;

    public GoogleAuthService(
        RegalisDbContext db,
        ITokenService tokens,
        IDateTimeProvider clock,
        IConfiguration config)
    {
        _db = db; _tokens = tokens; _clock = clock; _config = config;
    }

    public async Task<AuthResponse> SignInAsync(
        GoogleLoginRequest request, string? ip, CancellationToken ct)
    {
        // ---------------------------------------------------------------
        // 1. Verify the token. Audience is pinned to our own client id so a
        //    token minted for some other Google app cannot be replayed here.
        // ---------------------------------------------------------------
        var clientId = _config["Authentication:Google:ClientId"];
        if (string.IsNullOrWhiteSpace(clientId))
            throw new BusinessRuleException("Google sign-in is not configured.");

        GoogleJsonWebSignature.Payload payload;
        try
        {
            payload = await GoogleJsonWebSignature.ValidateAsync(
                request.IdToken,
                new GoogleJsonWebSignature.ValidationSettings
                {
                    Audience = new[] { clientId }
                });
        }
        catch (InvalidJwtException)
        {
            throw new UnauthorizedException("Google sign-in could not be verified.");
        }

        // ---------------------------------------------------------------
        // 2. THE load-bearing check. Without it, anyone who can create a
        //    Google account claiming an address takes over the matching
        //    Regalis account on the email-match branch below.
        // ---------------------------------------------------------------
        if (!payload.EmailVerified || string.IsNullOrWhiteSpace(payload.Email))
            throw new UnauthorizedException("This Google account has no verified email address.");

        var googleId = payload.Subject;                 // stable, never reused
        var email    = payload.Email.Trim().ToLowerInvariant();
        var now      = _clock.UtcNow;

        // ---------------------------------------------------------------
        // 3. Resolve the user: GoogleId first, then verified email, then create.
        // ---------------------------------------------------------------
        var user = await _db.Users
            .FirstOrDefaultAsync(u => u.GoogleId == googleId && !u.IsDeleted, ct);

        if (user is null)
        {
            user = await _db.Users
                .FirstOrDefaultAsync(u => u.Email == email && !u.IsDeleted, ct);

            if (user is not null)
            {
                // Link an existing password account to this Google identity.
                user.GoogleId        = googleId;
                user.IsEmailVerified = true;
                user.ModifiedDate    = now;
            }
            else
            {
                // First sign-in. This MUST go through the same path as OTP
                // registration, which also writes MAP_UserRole (Customer) and
                // MST_Wallet. A bare MST_User row logs in and can do nothing:
                // no role means no perm claims, an empty sidebar from
                // /api/menus/my, and 403 on every guarded endpoint.
                //
                // Prefer: _auth.CreateCustomerAsync(...) — reuse, do not fork.
                user = await CreateCustomerFromGoogleAsync(payload, googleId, email, now, ct);
            }
        }

        // ---------------------------------------------------------------
        // 4. A disabled or deleted account is not revived by signing in
        //    with Google.
        // ---------------------------------------------------------------
        if (user.IsDeleted || !user.IsActive || user.Status != UserStatus.Active)
            throw new ForbiddenException("This account is not active.");

        user.LastLoginUtc     = now;
        user.FailedLoginCount = 0;

        // ---------------------------------------------------------------
        // 5. Issue the app's own tokens — same shape as password login.
        //    CreateAccessToken needs roles AND permissions; a token without
        //    perm claims authenticates but authorises nothing.
        // ---------------------------------------------------------------
        var roles = await _db.UserRoles.Where(r => r.UserId == user.UserId)
                                       .Select(r => r.Role.RoleName)
                                       .ToListAsync(ct);

        var perms = await _db.RolePermissions
            .Where(rp => _db.UserRoles.Any(ur => ur.UserId == user.UserId && ur.RoleId == rp.RoleId))
            .Select(rp => rp.Permission.PermissionCode)
            .Distinct()
            .ToListAsync(ct);

        var access  = _tokens.CreateAccessToken(user, roles, perms);
        var refresh = _tokens.CreateRefreshToken();

        // Refresh tokens rotate in families with reuse detection
        // (TRN_RefreshToken.FamilyId / ConsumedUtc / ReplacedByTokenHash).
        // A Google sign-in starts a NEW family, exactly as a password login does.
        _db.RefreshTokens.Add(new RefreshToken
        {
            UserId      = user.UserId,
            TokenHash   = _tokens.HashRefreshToken(refresh),
            FamilyId    = Guid.NewGuid(),
            ExpiresUtc  = now.AddDays(_config.GetValue<int>("Jwt:RefreshTokenDays")),
            CreatedUtc  = now,
            CreatedByIp = ip
        });

        await _db.SaveChangesAsync(ct);

        return new AuthResponse(access, refresh, /* user summary */ user);
    }
}

*/
