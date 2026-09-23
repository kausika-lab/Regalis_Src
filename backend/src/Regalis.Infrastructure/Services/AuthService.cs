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
    IGoogleTokenVerifier googleTokens,
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

        // A Google-only account has no password to check. Saying so beats a generic
        // failure the user can never resolve by retyping: by this point the address is
        // already known to exist, so this leaks nothing a login form would not.
        if (user.PasswordHash is null)
        {
            await RecordLoginAttemptAsync(user.UserId, email, false, "NoPasswordSet", ct);
            throw new UnauthorizedException(
                "This account signs in with Google. Use the Google button, " +
                "or set a password with Forgot Password.");
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

    /// <summary>
    /// Copies whatever structured profile the registration form supplied. Each field is
    /// only written when it is present, so an older client that omits them leaves the
    /// account as it was rather than blanking it — the member then finishes at onboarding.
    /// The legacy single-line Address is recomposed because TRN_Invoice snapshots it.
    /// </summary>
    private static void ApplyProfileFields(User user, RegisterRequest request)
    {
        static string? Clean(string? value) =>
            string.IsNullOrWhiteSpace(value) ? null : value.Trim();

        user.AddressLine1 = Clean(request.AddressLine1) ?? user.AddressLine1;
        user.AddressLine2 = Clean(request.AddressLine2) ?? user.AddressLine2;
        user.City = Clean(request.City) ?? user.City;
        user.State = Clean(request.State) ?? user.State;
        user.Country = Clean(request.Country) ?? user.Country;
        user.PostalCode = Clean(request.PostalCode) ?? user.PostalCode;
        user.DateOfBirth = request.DateOfBirth ?? user.DateOfBirth;
        user.Gender = request.Gender is { } g and >= 1 and <= 4 ? (Gender)g : user.Gender;
        user.Gstin = Clean(request.Gstin)?.ToUpperInvariant() ?? user.Gstin;

        var composed = string.Join(", ", new[]
        {
            user.AddressLine1, user.AddressLine2, user.City, user.State,
            user.PostalCode, user.Country
        }.Where(part => !string.IsNullOrWhiteSpace(part)));
        if (composed.Length > 0)
        {
            user.Address = composed;
        }
    }

    private static bool PortalAccepts(LoginPortal portal, UserType userType) => portal switch
    {
        LoginPortal.Customer => userType == UserType.Customer,
        LoginPortal.Admin => userType is UserType.Admin or UserType.Staff,
        _ => false
    };

    // ── Google sign-in ──────────────────────────────────────────────────────

    /// <summary>
    /// Google Identity Services token flow: the browser gets an ID token from Google and
    /// posts it here. Nothing the SPA says about who the user is is trusted — only what
    /// a Google-signed token says, after it has been verified.
    ///
    /// The result is deliberately the same <see cref="LoginResponse"/> a password login
    /// returns, so nothing downstream has to care how the member signed in.
    /// </summary>
    public async Task<LoginResponse> GoogleSignInAsync(GoogleLoginRequest request,
        CancellationToken ct = default)
    {
        var nowUtc = clock.UtcNow;

        if (!googleTokens.IsConfigured)
        {
            logger.LogWarning("Google sign-in attempted but Authentication:Google:ClientId is empty.");
            throw new BusinessRuleException("Google sign-in is not configured.");
        }

        var identity = await googleTokens.VerifyAsync(request.IdToken, ct);
        if (identity is null)
        {
            await RecordLoginAttemptAsync(null, "(google)", false, "GoogleTokenInvalid", ct);
            throw new UnauthorizedException("Google sign-in could not be verified.");
        }

        // The load-bearing check. Without it the email branch below is an
        // account-takeover path: register a Google account claiming someone else's
        // address, sign in, own their Regalis account.
        if (!identity.EmailVerified)
        {
            await RecordLoginAttemptAsync(null, identity.Email, false, "GoogleEmailUnverified", ct);
            throw new UnauthorizedException("This Google account has no verified email address.");
        }

        // The subject is the identity we store, because a Google account can change its
        // email address but never its subject. The address is only used to find an
        // existing password account the first time, so the two are not separate members.
        var user = await db.Users
            .Include(u => u.UserRoles).ThenInclude(ur => ur.Role)
            .FirstOrDefaultAsync(u => u.GoogleId == identity.Subject, ct);

        if (user is null)
        {
            user = await db.Users
                .Include(u => u.UserRoles).ThenInclude(ur => ur.Role)
                .FirstOrDefaultAsync(u => u.Email == identity.Email, ct);

            if (user is null)
            {
                user = await CreateGoogleCustomerAsync(identity, nowUtc, ct);
            }
            else if (user.GoogleId is not null)
            {
                // The address is already tied to a different Google subject. Re-pointing
                // it would hand the account to whoever holds the newer Google account.
                logger.LogWarning(
                    "Google sign-in for user {UserId} presented subject that does not match the linked one",
                    user.UserId);
                await RecordLoginAttemptAsync(user.UserId, identity.Email, false, "GoogleSubjectMismatch", ct);
                throw new UnauthorizedException("This email is linked to a different Google account.");
            }
            else
            {
                user.GoogleId = identity.Subject;
                user.IsEmailVerified = true;
                // Google has verified the mailbox, which is exactly what the registration
                // OTP proves. Completing a half-finished registration here beats leaving
                // the member locked out of an account they can demonstrably reach.
                if (user.Status == UserStatus.PendingVerification)
                {
                    user.Status = UserStatus.Active;
                }
                AddAuditRow(user.UserId, user.FullName, AuditActions.Update,
                    "Auth", "User", user.UserId.ToString());
            }
        }

        // Google is a member door only. An admin or staff account is not enterable with
        // a Google token however its email reads — staff sign in with a password.
        if (user.UserType != UserType.Customer)
        {
            await RecordLoginAttemptAsync(user.UserId, identity.Email, false, "GoogleWrongPortal", ct);
            throw new ForbiddenException(
                "Google sign-in is for member accounts. Use the staff sign-in page.");
        }

        // A disabled, locked or half-deleted account is not revived by arriving through
        // Google. Soft-deleted rows never reach here — the global query filter hides them.
        if (user.Status == UserStatus.Inactive || !user.IsActive)
        {
            await RecordLoginAttemptAsync(user.UserId, identity.Email, false, "Inactive", ct);
            throw new UnauthorizedException("Your account is inactive. Contact support.",
                ErrorCodes.AccountInactive);
        }

        if (user.Status == UserStatus.Locked ||
            (user.LockoutEndUtc is not null && user.LockoutEndUtc > nowUtc))
        {
            await RecordLoginAttemptAsync(user.UserId, identity.Email, false, "LockedOut", ct);
            throw new UnauthorizedException(
                "Your account is locked. Try again later or contact support.",
                ErrorCodes.AccountLocked);
        }

        if (user.Status != UserStatus.Active)
        {
            await RecordLoginAttemptAsync(user.UserId, identity.Email, false, "NotActive", ct);
            throw new UnauthorizedException("Your account is not active. Contact support.",
                ErrorCodes.AccountInactive);
        }

        user.FailedLoginCount = 0;
        user.LockoutEndUtc = null;
        user.LastLoginUtc = nowUtc;

        var (roles, permissions) = ResolveAccess(user);
        var access = tokenService.CreateAccessToken(user, roles, permissions);
        var refresh = tokenService.CreateRefreshToken();

        // A new family, exactly as a password login starts one. The family plus
        // ConsumedUtc and ReplacedByTokenHash are what implement reuse detection; a
        // token inserted without one silently opts out of it.
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
        await RecordLoginAttemptAsync(user.UserId, identity.Email, true, null, saveNow: false, ct);
        await db.SaveChangesAsync(ct);

        return new LoginResponse(access.Token, access.ExpiresUtc, refresh.RawToken,
            ToUserDto(user), roles, permissions);
    }

    /// <summary>
    /// First Google sign-in. Creates everything OTP registration creates — the user row,
    /// the Customer role and the wallet. An account missing either is broken in ways that
    /// are hard to trace: no role means no perm claims in the JWT, an empty sidebar from
    /// /api/menus/my, and 403 on every guarded endpoint.
    /// </summary>
    private async Task<User> CreateGoogleCustomerAsync(GoogleIdentity identity, DateTime nowUtc,
        CancellationToken ct)
    {
        var fullName = string.IsNullOrWhiteSpace(identity.Name)
            ? identity.Email.Split('@')[0]
            : identity.Name;
        if (fullName.Length > 150)
        {
            fullName = fullName[..150];
        }

        var user = new User
        {
            UserType = UserType.Customer,
            FullName = fullName,
            Email = identity.Email,
            // Google supplies no phone number. Empty rather than invented, so the gap is
            // visible and the member can fill it in on their profile; UX_User_Mobile
            // excludes the empty string so more than one such account can exist.
            MobileNumber = string.Empty,
            // No password: this account cannot be signed into with one until the member
            // sets one through Forgot Password.
            PasswordHash = null,
            GoogleId = identity.Subject,
            // Google verified the address; there is nothing left for an OTP to prove.
            IsEmailVerified = true,
            Status = UserStatus.Active
        };
        db.Users.Add(user);
        await db.SaveChangesAsync(ct);

        // The Role entity itself, not just its id: ResolveAccess reads RoleCode and
        // IsActive off the navigation to build the token's role and perm claims.
        var customerRole = await db.Roles.FirstAsync(r => r.RoleCode == RoleCodes.Customer, ct);
        var membership = new UserRole
        {
            UserId = user.UserId,
            RoleId = customerRole.RoleId,
            Role = customerRole,
            AssignedDateUtc = nowUtc
        };
        db.UserRoles.Add(membership);
        user.UserRoles.Add(membership);

        db.Wallets.Add(new Wallet { CustomerId = user.UserId });

        AddAuditRow(user.UserId, user.FullName, AuditActions.Create, "Auth", "User", user.UserId.ToString());
        logger.LogInformation("Created customer {UserId} from a Google sign-in", user.UserId);
        return user;
    }

    // ── Registration + OTP ──────────────────────────────────────────────────

    public async Task<RegisterResponse> RegisterAsync(RegisterRequest request, CancellationToken ct = default)
    {
        var nowUtc = clock.UtcNow;
        var email = request.Email.Trim();
        var mobile = request.MobileNumber.Trim();

        await EnforcePasswordPolicyAsync(request.Password, ct);

        // Hashed once into a local: User.PasswordHash is nullable now, and the history
        // row below needs the value without the compiler having to prove it is set.
        var passwordHash = passwordHasher.Hash(request.Password);

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
            user.PasswordHash = passwordHash;
            ApplyProfileFields(user, request);
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
                PasswordHash = passwordHash,
                Status = UserStatus.PendingVerification
            };
            ApplyProfileFields(user, request);
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
            PasswordHash = passwordHash,
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

        // A Google-only account has nothing to change. Forgot Password is the route
        // that sets a first password, because it proves control of the mailbox.
        if (user.PasswordHash is null)
        {
            throw new BusinessRuleException(
                "This account has no password yet. Use Forgot Password to set one.");
        }

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
        user.PhotoPath, user.DefaultLocationId, user.IsProfileComplete);
}
