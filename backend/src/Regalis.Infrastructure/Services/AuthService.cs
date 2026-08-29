using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Regalis.Application.Abstractions;
using Regalis.Application.Auth;
using Regalis.Application.Common.Exceptions;
using Regalis.Domain.Entities.Finance;
using Regalis.Domain.Entities.Identity;
using Regalis.Domain.Enums;
using Regalis.Infrastructure.Persistence;
using Regalis.Infrastructure.Security;
using Regalis.Shared.Constants;

namespace Regalis.Infrastructure.Services;

/// <summary>
/// Authentication per ARCHITECTURE §4: PBKDF2 passwords, 15-minute JWTs, rotating
/// single-use refresh tokens with family revocation on reuse, lockout, hashed OTPs,
/// password history, and audit/login-history rows for every attempt.
/// </summary>
public class AuthService(
    RegalisDbContext db,
    IPasswordHasher passwordHasher,
    ITokenService tokenService,
    ISettingService settings,
    IOtpSender otpSender,
    ICurrentUser currentUser,
    IDateTimeProvider clock,
    IOptions<JwtOptions> jwtOptions,
    ILogger<AuthService> logger) : IAuthService
{
    private const string ResetTokenPurposeClaim = "pwreset";
    private const int PasswordHistoryDepth = 3;

    // ── Login ───────────────────────────────────────────────────────────────

    public async Task<LoginResponse> LoginAsync(LoginRequest request, CancellationToken ct = default)
    {
        var nowUtc = clock.UtcNow;
        var email = request.Email.Trim();
        var portal = request.Portal.Equals("admin", StringComparison.OrdinalIgnoreCase)
            ? LoginPortal.Admin
            : LoginPortal.Customer;

        var user = await db.Users
            .Include(u => u.UserRoles).ThenInclude(ur => ur.Role)
            .FirstOrDefaultAsync(u => u.Email == email, ct);

        // One generic failure message for wrong email, wrong password and wrong
        // portal — no account enumeration.
        if (user is null || !PortalAccepts(portal, user.UserType))
        {
            await RecordLoginAttemptAsync(null, email, false, "UserNotFoundOrWrongPortal", ct);
            throw new UnauthorizedException("Invalid email or password.");
        }

        if (user.Status == UserStatus.PendingVerification)
        {
            await RecordLoginAttemptAsync(user.UserId, email, false, "PendingVerification", ct);
            throw new UnauthorizedException(
                "Your account is not verified yet. Complete OTP verification first.",
                ErrorCodes.AccountInactive);
        }

        if (user.Status == UserStatus.Inactive || !user.IsActive)
        {
            await RecordLoginAttemptAsync(user.UserId, email, false, "Inactive", ct);
            throw new UnauthorizedException("Your account is inactive. Contact support.",
                ErrorCodes.AccountInactive);
        }

        if (user.Status == UserStatus.Locked ||
            (user.LockoutEndUtc is not null && user.LockoutEndUtc > nowUtc))
        {
            await RecordLoginAttemptAsync(user.UserId, email, false, "LockedOut", ct);
            throw new UnauthorizedException(
                "Your account is locked. Try again later or contact support.",
                ErrorCodes.AccountLocked);
        }

        if (!passwordHasher.Verify(request.Password, user.PasswordHash))
        {
            user.FailedLoginCount++;
            var maxFailed = await settings.GetIntAsync(AppSettingKeys.Security.MaxFailedLogins, 5, ct);
            if (user.FailedLoginCount >= maxFailed)
            {
                var lockoutMinutes = await settings.GetIntAsync(AppSettingKeys.Security.LockoutMinutes, 15, ct);
                user.LockoutEndUtc = nowUtc.AddMinutes(lockoutMinutes);
                user.FailedLoginCount = 0;
                logger.LogWarning("User {UserId} locked out until {LockoutEnd}", user.UserId, user.LockoutEndUtc);
            }
            await RecordLoginAttemptAsync(user.UserId, email, false, "WrongPassword", ct);
            throw new UnauthorizedException("Invalid email or password.");
        }

        user.FailedLoginCount = 0;
        user.LockoutEndUtc = null;
        user.LastLoginUtc = nowUtc;

        var (roles, permissions) = ResolveAccess(user);
        var access = tokenService.CreateAccessToken(user, roles, permissions);
        var refresh = tokenService.CreateRefreshToken();

        db.RefreshTokens.Add(new RefreshToken
        {
            UserId = user.UserId,
            TokenHash = refresh.TokenHash,
            FamilyId = Guid.NewGuid(),
            ExpiresUtc = refresh.ExpiresUtc,
            CreatedUtc = nowUtc,
            CreatedByIp = currentUser.IpAddress
        });

        AddAuditRow(user.UserId, user.FullName, AuditActions.Login, "Auth", "User", user.UserId.ToString());
        await RecordLoginAttemptAsync(user.UserId, email, true, null, saveNow: false, ct);
        await db.SaveChangesAsync(ct);

        return new LoginResponse(access.Token, access.ExpiresUtc, refresh.RawToken,
            ToUserDto(user), roles, permissions);
    }

    private static bool PortalAccepts(LoginPortal portal, UserType userType) => portal switch
    {
        LoginPortal.Customer => userType == UserType.Customer,
        LoginPortal.Admin => userType is UserType.Admin or UserType.Staff,
        _ => false
    };

    // ── Registration + OTP ──────────────────────────────────────────────────

    public async Task<RegisterResponse> RegisterAsync(RegisterRequest request, CancellationToken ct = default)
    {
        var nowUtc = clock.UtcNow;
        var email = request.Email.Trim();
        var mobile = request.MobileNumber.Trim();

        await EnforcePasswordPolicyAsync(request.Password, ct);

        var existing = await db.Users.FirstOrDefaultAsync(
            u => u.Email == email || u.MobileNumber == mobile, ct);

        User user;
        if (existing is not null)
        {
            // A half-finished registration may retry with corrected details.
            // A verified account may not be re-registered — but the response must be
            // indistinguishable from a fresh registration, or this endpoint becomes an
            // account-enumeration oracle over the member base (SECURITY.md F-01).
            // Nothing is created and no OTP exists, so verification cannot proceed;
            // the real owner signs in or uses Forgot Password. When SMTP lands, also
            // notify them out-of-band here.
            if (existing.Status != UserStatus.PendingVerification)
            {
                logger.LogWarning(
                    "Registration attempted against an existing account (user {UserId}); " +
                    "generic response returned", existing.UserId);
                var ttl = await settings.GetIntAsync(AppSettingKeys.Security.OtpTtlMinutes, 10, ct);
                return new RegisterResponse(email, ttl);
            }
            user = existing;
            user.FullName = request.FullName.Trim();
            user.Email = email;
            user.MobileNumber = mobile;
            user.CompanyName = request.CompanyName?.Trim();
            user.PasswordHash = passwordHasher.Hash(request.Password);
        }
        else
        {
            user = new User
            {
                UserType = UserType.Customer,
                FullName = request.FullName.Trim(),
                Email = email,
                MobileNumber = mobile,
                CompanyName = request.CompanyName?.Trim(),
                PasswordHash = passwordHasher.Hash(request.Password),
                Status = UserStatus.PendingVerification
            };
            db.Users.Add(user);
            await db.SaveChangesAsync(ct);

            var customerRoleId = await db.Roles
                .Where(r => r.RoleCode == RoleCodes.Customer)
                .Select(r => r.RoleId)
                .FirstAsync(ct);
            db.UserRoles.Add(new UserRole
            {
                UserId = user.UserId,
                RoleId = customerRoleId,
                AssignedDateUtc = nowUtc
            });
            db.Wallets.Add(new Wallet { CustomerId = user.UserId });
        }

        db.PasswordHistories.Add(new PasswordHistory
        {
            UserId = user.UserId,
            PasswordHash = user.PasswordHash,
            ChangedUtc = nowUtc
        });

        var ttlMinutes = await CreateAndSendOtpAsync(user, email, OtpPurpose.Registration, ct);
        await db.SaveChangesAsync(ct);

        return new RegisterResponse(email, ttlMinutes);
    }

    public async Task<VerifyOtpResponse> VerifyOtpAsync(VerifyOtpRequest request, CancellationToken ct = default)
    {
        var nowUtc = clock.UtcNow;
        var purpose = OtpPurposes.Parse(request.Purpose)
                      ?? throw new BusinessRuleException("Unknown OTP purpose.");
        var destination = request.Destination.Trim();

        var otp = await db.Otps
            .Where(o => o.Destination == destination && o.Purpose == purpose && o.ConsumedUtc == null)
            .OrderByDescending(o => o.CreatedUtc)
            .FirstOrDefaultAsync(ct);

        if (otp is null)
        {
            throw new BusinessRuleException("The code is invalid or has already been used.",
                ErrorCodes.OtpInvalid);
        }
        if (otp.ExpiresUtc <= nowUtc)
        {
            throw new BusinessRuleException("The code has expired. Request a new one.",
                ErrorCodes.OtpExpired);
        }
        if (otp.AttemptCount >= otp.MaxAttempts)
        {
            throw new BusinessRuleException(
                "Too many incorrect attempts. Request a new code.",
                ErrorCodes.OtpMaxAttempts);
        }

        if (!ConstantTimeEquals(HashOtp(request.Code, destination), otp.CodeHash))
        {
            otp.AttemptCount++;
            await db.SaveChangesAsync(ct);
            throw new BusinessRuleException("The code is incorrect.", ErrorCodes.OtpInvalid);
        }

        otp.ConsumedUtc = nowUtc;

        var user = await db.Users.FirstOrDefaultAsync(u => u.Email == destination, ct)
                   ?? throw new BusinessRuleException("The code is invalid.", ErrorCodes.OtpInvalid);

        string? resetToken = null;
        if (purpose == OtpPurpose.Registration)
        {
            user.Status = UserStatus.Active;
            user.IsEmailVerified = true;
        }
        else
        {
            resetToken = CreateResetToken(user.UserId, nowUtc);
        }

        await db.SaveChangesAsync(ct);
        return new VerifyOtpResponse(true, resetToken);
    }

    public async Task ResendOtpAsync(ResendOtpRequest request, CancellationToken ct = default)
    {
        var nowUtc = clock.UtcNow;
        var purpose = OtpPurposes.Parse(request.Purpose)
                      ?? throw new BusinessRuleException("Unknown OTP purpose.");
        var destination = request.Destination.Trim();

        var lastCreatedUtc = await db.Otps
            .Where(o => o.Destination == destination && o.Purpose == purpose)
            .OrderByDescending(o => o.CreatedUtc)
            .Select(o => (DateTime?)o.CreatedUtc)
            .FirstOrDefaultAsync(ct);

        if (lastCreatedUtc is not null && (nowUtc - lastCreatedUtc.Value).TotalSeconds < 60)
        {
            throw new BusinessRuleException(
                "Please wait a minute before requesting another code.",
                ErrorCodes.RateLimitExceeded);
        }

        var user = await db.Users.FirstOrDefaultAsync(u => u.Email == destination, ct);
        var eligible = purpose == OtpPurpose.Registration
            ? user is { Status: UserStatus.PendingVerification }
            : user is not null;

        // Silently succeed for ineligible destinations — no account enumeration.
        if (eligible)
        {
            await CreateAndSendOtpAsync(user!, destination, purpose, ct);
            await db.SaveChangesAsync(ct);
        }
    }

    public async Task ForgotPasswordAsync(ForgotPasswordRequest request, CancellationToken ct = default)
    {
        var email = request.Email.Trim();
        var user = await db.Users.FirstOrDefaultAsync(
            u => u.Email == email && u.Status != UserStatus.PendingVerification, ct);

        // Always succeeds from the caller's perspective — no account enumeration.
        if (user is not null)
        {
            await CreateAndSendOtpAsync(user, email, OtpPurpose.ForgotPassword, ct);
            await db.SaveChangesAsync(ct);
        }
    }

    // ── Password reset / change ─────────────────────────────────────────────

    public async Task ResetPasswordAsync(ResetPasswordRequest request, CancellationToken ct = default)
    {
        var nowUtc = clock.UtcNow;
        var (userId, issuedUtc) = ValidateResetToken(request.ResetToken);

        var user = await db.Users.FirstOrDefaultAsync(u => u.UserId == userId, ct)
                   ?? throw new UnauthorizedException("Invalid or expired reset token.",
                       ErrorCodes.TokenExpired);

        // A reset token issued before the last password change is spent.
        if (user.LastPasswordChangeUtc is not null && user.LastPasswordChangeUtc >= issuedUtc)
        {
            throw new UnauthorizedException("This reset link has already been used.",
                ErrorCodes.TokenRevoked);
        }

        await SetPasswordAsync(user, request.NewPassword, nowUtc, ct);
        await db.SaveChangesAsync(ct);
    }

    public async Task ChangePasswordAsync(ChangePasswordRequest request, CancellationToken ct = default)
    {
        var nowUtc = clock.UtcNow;
        var user = await db.Users.FirstOrDefaultAsync(u => u.UserId == currentUser.UserId, ct)
                   ?? throw new UnauthorizedException();

        if (!passwordHasher.Verify(request.CurrentPassword, user.PasswordHash))
        {
            throw new BusinessRuleException("The current password is incorrect.");
        }

        await SetPasswordAsync(user, request.NewPassword, nowUtc, ct);
        await db.SaveChangesAsync(ct);
    }

    /// <summary>Policy + history check, hash, history row, revoke every refresh
    /// token, audit PASSWORD_CHANGE. Caller saves.</summary>
    private async Task SetPasswordAsync(User user, string newPassword, DateTime nowUtc, CancellationToken ct)
    {
        await EnforcePasswordPolicyAsync(newPassword, ct);

        var recentHashes = await db.PasswordHistories
            .Where(h => h.UserId == user.UserId)
            .OrderByDescending(h => h.ChangedUtc)
            .Take(PasswordHistoryDepth)
            .Select(h => h.PasswordHash)
            .ToListAsync(ct);
        if (recentHashes.Any(hash => passwordHasher.Verify(newPassword, hash)))
        {
            throw new BusinessRuleException(
                $"The new password must differ from your last {PasswordHistoryDepth} passwords.");
        }

        user.PasswordHash = passwordHasher.Hash(newPassword);
        user.LastPasswordChangeUtc = nowUtc;
        user.MustChangePassword = false;
        db.PasswordHistories.Add(new PasswordHistory
        {
            UserId = user.UserId,
            PasswordHash = user.PasswordHash,
            ChangedUtc = nowUtc
        });

        var activeTokens = await db.RefreshTokens
            .Where(t => t.UserId == user.UserId && t.RevokedUtc == null && t.ConsumedUtc == null)
            .ToListAsync(ct);
        foreach (var token in activeTokens)
        {
            token.RevokedUtc = nowUtc;
            token.RevokedReason = "PasswordChanged";
        }

        AddAuditRow(user.UserId, user.FullName, AuditActions.PasswordChange, "Auth",
            "User", user.UserId.ToString());
    }

    private async Task EnforcePasswordPolicyAsync(string password, CancellationToken ct)
    {
        var minLength = await settings.GetIntAsync(AppSettingKeys.Security.PasswordMinLength, 8, ct);
        if (password.Length < minLength)
        {
            throw new BusinessRuleException($"Password must be at least {minLength} characters.");
        }
    }

    // ── Refresh rotation ────────────────────────────────────────────────────

    public async Task<RefreshResponse> RefreshAsync(RefreshRequest request, string? ipAddress,
        CancellationToken ct = default)
    {
        var nowUtc = clock.UtcNow;
        var presentedHash = tokenService.HashRefreshToken(request.RefreshToken);

        var token = await db.RefreshTokens
            .Include(t => t.User).ThenInclude(u => u.UserRoles).ThenInclude(ur => ur.Role)
            .FirstOrDefaultAsync(t => t.TokenHash == presentedHash, ct);

        if (token is null)
        {
            throw new UnauthorizedException("Invalid refresh token.");
        }

        // Reuse of a consumed/revoked token = theft signal: kill the whole family.
        if (token.ConsumedUtc is not null || token.RevokedUtc is not null)
        {
            var family = await db.RefreshTokens
                .Where(t => t.FamilyId == token.FamilyId && t.RevokedUtc == null)
                .ToListAsync(ct);
            foreach (var member in family)
            {
                member.RevokedUtc = nowUtc;
                member.RevokedReason = "ReuseDetected";
            }
            await db.SaveChangesAsync(ct);
            logger.LogWarning("Refresh token reuse detected for user {UserId}; family {FamilyId} revoked",
                token.UserId, token.FamilyId);
            throw new UnauthorizedException("The session is no longer valid. Sign in again.",
                ErrorCodes.TokenRevoked);
        }

        if (token.IsExpired(nowUtc))
        {
            throw new UnauthorizedException("The session has expired. Sign in again.",
                ErrorCodes.TokenExpired);
        }

        var user = token.User;
        if (user.Status != UserStatus.Active || !user.IsActive)
        {
            throw new UnauthorizedException("Your account is inactive.", ErrorCodes.AccountInactive);
        }

        var (roles, permissions) = ResolveAccess(user);
        var access = tokenService.CreateAccessToken(user, roles, permissions);
        var replacement = tokenService.CreateRefreshToken();

        token.ConsumedUtc = nowUtc;
        token.ReplacedByTokenHash = replacement.TokenHash;
        db.RefreshTokens.Add(new RefreshToken
        {
            UserId = user.UserId,
            TokenHash = replacement.TokenHash,
            FamilyId = token.FamilyId,
            ExpiresUtc = replacement.ExpiresUtc,
            CreatedUtc = nowUtc,
            CreatedByIp = ipAddress
        });
        await db.SaveChangesAsync(ct);

        return new RefreshResponse(access.Token, access.ExpiresUtc, replacement.RawToken);
    }

    public async Task LogoutAsync(LogoutRequest request, CancellationToken ct = default)
    {
        var nowUtc = clock.UtcNow;
        var presentedHash = tokenService.HashRefreshToken(request.RefreshToken);
        var token = await db.RefreshTokens.FirstOrDefaultAsync(t => t.TokenHash == presentedHash, ct);

        if (token is not null && token.RevokedUtc is null)
        {
            token.RevokedUtc = nowUtc;
            token.RevokedReason = "Logout";
            AddAuditRow(token.UserId, currentUser.FullName, AuditActions.Logout, "Auth",
                "User", token.UserId.ToString());
            await db.SaveChangesAsync(ct);
        }
    }

    public async Task<MeResponse> GetMeAsync(CancellationToken ct = default)
    {
        var user = await db.Users
            .Include(u => u.UserRoles).ThenInclude(ur => ur.Role)
            .FirstOrDefaultAsync(u => u.UserId == currentUser.UserId, ct)
            ?? throw new UnauthorizedException();

        var (roles, permissions) = ResolveAccess(user);
        return new MeResponse(ToUserDto(user), roles, permissions);
    }

    // ── Helpers ─────────────────────────────────────────────────────────────

    /// <summary>Effective roles + permission codes, resolved from the database
    /// (never hard-coded) and stamped into the JWT.</summary>
    private (List<string> Roles, List<string> Permissions) ResolveAccess(User user)
    {
        var roleIds = user.UserRoles
            .Where(ur => ur.Role.IsActive && !ur.Role.IsDeleted)
            .Select(ur => ur.RoleId)
            .ToList();
        var roles = user.UserRoles
            .Where(ur => roleIds.Contains(ur.RoleId))
            .Select(ur => ur.Role.RoleCode)
            .Distinct()
            .ToList();

        var permissions = db.RolePermissions
            .Where(rp => roleIds.Contains(rp.RoleId) && rp.Permission.IsActive)
            .Select(rp => rp.Permission.PermissionCode)
            .Distinct()
            .ToList();

        return (roles, permissions);
    }

    private async Task<int> CreateAndSendOtpAsync(User user, string destination, OtpPurpose purpose,
        CancellationToken ct)
    {
        var nowUtc = clock.UtcNow;
        var ttlMinutes = await settings.GetIntAsync(AppSettingKeys.Security.OtpTtlMinutes, 10, ct);
        var maxAttempts = await settings.GetIntAsync(AppSettingKeys.Security.OtpMaxAttempts, 5, ct);

        // Invalidate previous outstanding codes for this destination + purpose.
        var outstanding = await db.Otps
            .Where(o => o.Destination == destination && o.Purpose == purpose && o.ConsumedUtc == null)
            .ToListAsync(ct);
        foreach (var stale in outstanding)
        {
            stale.ConsumedUtc = nowUtc;
        }

        var code = RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6");
        db.Otps.Add(new Otp
        {
            UserId = user.UserId,
            Destination = destination,
            Channel = OtpChannel.Email,
            Purpose = purpose,
            CodeHash = HashOtp(code, destination),
            ExpiresUtc = nowUtc.AddMinutes(ttlMinutes),
            MaxAttempts = maxAttempts,
            CreatedUtc = nowUtc
        });

        await otpSender.SendAsync(destination, OtpChannel.Email, purpose, code, ct);
        return ttlMinutes;
    }

    private static string HashOtp(string code, string destination) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes($"{code}|{destination.ToLowerInvariant()}")));

    private static bool ConstantTimeEquals(string a, string b) =>
        a.Length == b.Length &&
        CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(a), Encoding.UTF8.GetBytes(b));

    /// <summary>Short-lived, stateless reset token. Replay is blocked by comparing
    /// the issue time against LastPasswordChangeUtc.</summary>
    private string CreateResetToken(long userId, DateTime nowUtc)
    {
        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.Value.SigningKey)),
            SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(
            issuer: jwtOptions.Value.Issuer,
            audience: jwtOptions.Value.Audience,
            claims:
            [
                new Claim(JwtRegisteredClaimNames.Sub, userId.ToString()),
                new Claim("purpose", ResetTokenPurposeClaim),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N"))
            ],
            notBefore: nowUtc,
            expires: nowUtc.AddMinutes(10),
            signingCredentials: credentials);
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private (long UserId, DateTime IssuedUtc) ValidateResetToken(string resetToken)
    {
        var handler = new JwtSecurityTokenHandler { MapInboundClaims = false };
        try
        {
            var principal = handler.ValidateToken(resetToken, new TokenValidationParameters
            {
                ValidIssuer = jwtOptions.Value.Issuer,
                ValidAudience = jwtOptions.Value.Audience,
                IssuerSigningKey = new SymmetricSecurityKey(
                    Encoding.UTF8.GetBytes(jwtOptions.Value.SigningKey)),
                ValidateLifetime = true,
                ClockSkew = TimeSpan.FromSeconds(30)
            }, out var validated);

            if (principal.FindFirst("purpose")?.Value != ResetTokenPurposeClaim)
            {
                throw new UnauthorizedException("Invalid or expired reset token.", ErrorCodes.TokenExpired);
            }
            var userId = long.Parse(principal.FindFirst(JwtRegisteredClaimNames.Sub)!.Value);
            return (userId, ((JwtSecurityToken)validated).ValidFrom);
        }
        catch (Exception ex) when (ex is not UnauthorizedException)
        {
            throw new UnauthorizedException("Invalid or expired reset token.", ErrorCodes.TokenExpired);
        }
    }

    private async Task RecordLoginAttemptAsync(long? userId, string email, bool success,
        string? failureReason, CancellationToken ct) =>
        await RecordLoginAttemptAsync(userId, email, success, failureReason, saveNow: true, ct);

    private async Task RecordLoginAttemptAsync(long? userId, string email, bool success,
        string? failureReason, bool saveNow, CancellationToken ct)
    {
        db.LoginHistories.Add(new LoginHistory
        {
            UserId = userId,
            AttemptedEmail = email,
            IsSuccess = success,
            FailureReason = failureReason,
            IpAddress = currentUser.IpAddress,
            UserAgent = currentUser.UserAgent,
            AttemptedUtc = clock.UtcNow
        });
        if (saveNow)
        {
            await db.SaveChangesAsync(ct);
        }
    }

    private void AddAuditRow(long userId, string? userName, string action, string module,
        string entityName, string entityId) =>
        db.AuditLogs.Add(new AuditLog
        {
            UserId = userId,
            UserName = userName,
            Action = action,
            ModuleName = module,
            EntityName = entityName,
            EntityId = entityId,
            IpAddress = currentUser.IpAddress,
            UserAgent = currentUser.UserAgent,
            CorrelationId = currentUser.CorrelationId,
            TimestampUtc = clock.UtcNow
        });

    private static AuthUserDto ToUserDto(User user) => new(
        user.UserId, user.FullName, user.Email, (byte)user.UserType,
        user.PhotoPath, user.DefaultLocationId);
}
