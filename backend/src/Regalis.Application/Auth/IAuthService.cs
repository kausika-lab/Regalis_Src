namespace Regalis.Application.Auth;

public interface IAuthService
{
    Task<LoginResponse> LoginAsync(LoginRequest request, CancellationToken ct = default);
    /// <summary>Exchanges a Google ID token for the same session a password login
    /// returns, creating the customer account on first sign-in. Customer portal only:
    /// an admin or staff account cannot be entered this way.</summary>
    Task<LoginResponse> GoogleSignInAsync(GoogleLoginRequest request, CancellationToken ct = default);
    Task<RegisterResponse> RegisterAsync(RegisterRequest request, CancellationToken ct = default);
    Task<VerifyOtpResponse> VerifyOtpAsync(VerifyOtpRequest request, CancellationToken ct = default);
    Task ResendOtpAsync(ResendOtpRequest request, CancellationToken ct = default);
    /// <summary>Always succeeds from the caller's perspective — no account enumeration.</summary>
    Task ForgotPasswordAsync(ForgotPasswordRequest request, CancellationToken ct = default);
    Task ResetPasswordAsync(ResetPasswordRequest request, CancellationToken ct = default);
    Task ChangePasswordAsync(ChangePasswordRequest request, CancellationToken ct = default);
    Task<RefreshResponse> RefreshAsync(RefreshRequest request, string? ipAddress, CancellationToken ct = default);
    Task LogoutAsync(LogoutRequest request, CancellationToken ct = default);
    Task<MeResponse> GetMeAsync(CancellationToken ct = default);
}
