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
    int? DefaultLocationId);

public record LoginResponse(
    string AccessToken,
    DateTime AccessTokenExpiresUtc,
    string RefreshToken,
    AuthUserDto User,
    IReadOnlyList<string> Roles,
    IReadOnlyList<string> Permissions);

public record RegisterRequest(
    string FullName,
    string Email,
    string MobileNumber,
    string Password,
    string ConfirmPassword,
    string? CompanyName);

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
