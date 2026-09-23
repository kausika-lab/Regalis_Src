using Regalis.Domain.Enums;

namespace Regalis.Application.Auth;

/// <summary>Which portal the client is signing in to. The portal constrains the
/// user type: customer → Customer, admin → Admin/Staff.</summary>
public enum LoginPortal
{
    Customer = 1,
    Admin = 2
}

public record LoginRequest(string Email, string Password, string Portal);

public record AuthUserDto(
    long UserId,
    string FullName,
    string Email,
    byte UserType,
    string? PhotoPath,
    int? DefaultLocationId,
    // False when the member still owes us onboarding details. The SPA routes on this;
    // the API independently blocks booking and purchase, so a client that ignores it
    // gets no further.
    bool IsProfileComplete = true);

public record LoginResponse(
    string AccessToken,
    DateTime AccessTokenExpiresUtc,
    string RefreshToken,
    AuthUserDto User,
    IReadOnlyList<string> Roles,
    IReadOnlyList<string> Permissions);

/// <summary>The ID token the browser received from Google Identity Services. The SPA
/// posts it unchanged; the API verifies it against Google rather than trusting anything
/// the SPA claims about who the user is.</summary>
public record GoogleLoginRequest(string IdToken);

/// <summary>
/// Self-registration. The profile fields are optional on the wire so an older client
/// keeps working, but the SPA sends them all and whatever arrives is stored — until now
/// they were accepted and silently dropped, which left every account incomplete.
/// Anyone who registers without them is sent to onboarding at first sign-in.
/// </summary>
public record RegisterRequest(
    string FullName,
    string Email,
    string MobileNumber,
    string Password,
    string ConfirmPassword,
    string? CompanyName,
    string? AddressLine1 = null,
    string? AddressLine2 = null,
    string? City = null,
    string? State = null,
    string? Country = null,
    string? PostalCode = null,
    DateOnly? DateOfBirth = null,
    byte? Gender = null,
    string? Gstin = null);

public record RegisterResponse(string Destination, int OtpTtlMinutes);

public record VerifyOtpRequest(string Destination, string Purpose, string Code);

/// <summary>ResetToken is set only for the ForgotPassword purpose.</summary>
public record VerifyOtpResponse(bool Verified, string? ResetToken);

public record ResendOtpRequest(string Destination, string Purpose);

public record ForgotPasswordRequest(string Email);

public record ResetPasswordRequest(string ResetToken, string NewPassword, string ConfirmPassword);

public record ChangePasswordRequest(string CurrentPassword, string NewPassword, string ConfirmPassword);

public record RefreshRequest(string RefreshToken);

public record RefreshResponse(string AccessToken, DateTime AccessTokenExpiresUtc, string RefreshToken);

public record LogoutRequest(string RefreshToken);

public record MeResponse(AuthUserDto User, IReadOnlyList<string> Roles, IReadOnlyList<string> Permissions);

/// <summary>One node of the caller's dynamic sidebar (GET /menus/my).</summary>
public record MenuNodeDto(
    int MenuId,
    int? ParentMenuId,
    string MenuCode,
    string DisplayName,
    string? Route,
    string? Icon,
    int DisplayOrder,
    byte MenuLevel,
    List<MenuNodeDto> Children);

public static class OtpPurposes
{
    public const string Registration = "registration";
    public const string ForgotPassword = "forgot-password";

    public static OtpPurpose? Parse(string value) => value.ToLowerInvariant() switch
    {
        Registration => OtpPurpose.Registration,
        ForgotPassword => OtpPurpose.ForgotPassword,
        _ => null
    };
}
