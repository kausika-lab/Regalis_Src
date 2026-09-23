using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Regalis.Application.Auth;
using Regalis.Shared.Api;

namespace Regalis.API.Controllers;

/// <summary>Authentication — API_PLAN §2. The sensitive endpoints carry the strict
/// "auth" rate-limit policy (5/minute/IP).</summary>
[Route("api/auth")]
public class AuthController(IAuthService authService) : ApiControllerBase
{
    /// <summary>Customer or admin login. Returns access + refresh token, user, roles, permissions.</summary>
    [HttpPost("login")]
    [AllowAnonymous]
    [EnableRateLimiting("auth")]
    [ProducesResponseType(typeof(ApiResponse<LoginResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Login(LoginRequest request, CancellationToken ct) =>
        OkEnvelope(await authService.LoginAsync(request, ct), "Signed in successfully.");

    /// <summary>Google sign-in. Exchanges a Google ID token for the same session
    /// <c>login</c> returns, creating the member account on first use.</summary>
    /// <remarks>Anonymous and rate limited like the rest — an unauthenticated surface
    /// that can create accounts.</remarks>
    [HttpPost("google")]
    [AllowAnonymous]
    [EnableRateLimiting("auth")]
    [ProducesResponseType(typeof(ApiResponse<LoginResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Google(GoogleLoginRequest request, CancellationToken ct) =>
        OkEnvelope(await authService.GoogleSignInAsync(request, ct), "Signed in successfully.");

    /// <summary>Customer self-registration. Creates a PendingVerification account and sends an OTP.</summary>
    [HttpPost("register")]
    [AllowAnonymous]
    [EnableRateLimiting("auth")]
    [ProducesResponseType(typeof(ApiResponse<RegisterResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Register(RegisterRequest request, CancellationToken ct) =>
        OkEnvelope(await authService.RegisterAsync(request, ct),
            "Registration received. Enter the verification code we sent you.");

    /// <summary>Verifies an OTP. Activates the account (registration) or returns a reset token (forgot-password).</summary>
    [HttpPost("verify-otp")]
    [AllowAnonymous]
    [EnableRateLimiting("auth")]
    [ProducesResponseType(typeof(ApiResponse<VerifyOtpResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> VerifyOtp(VerifyOtpRequest request, CancellationToken ct) =>
        OkEnvelope(await authService.VerifyOtpAsync(request, ct), "Code verified.");

    /// <summary>Resends an OTP. 60-second cooldown.</summary>
    [HttpPost("resend-otp")]
    [AllowAnonymous]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> ResendOtp(ResendOtpRequest request, CancellationToken ct)
    {
        await authService.ResendOtpAsync(request, ct);
        return OkEnvelope("If the address is registered, a new code has been sent.");
    }

    /// <summary>Sends a reset OTP. Always returns 200 — no account enumeration.</summary>
    [HttpPost("forgot-password")]
    [AllowAnonymous]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> ForgotPassword(ForgotPasswordRequest request, CancellationToken ct)
    {
        await authService.ForgotPasswordAsync(request, ct);
        return OkEnvelope("If the address is registered, a reset code has been sent.");
    }

    /// <summary>Sets a new password using the reset token from verify-otp.</summary>
    [HttpPost("reset-password")]
    [AllowAnonymous]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> ResetPassword(ResetPasswordRequest request, CancellationToken ct)
    {
        await authService.ResetPasswordAsync(request, ct);
        return OkEnvelope("Password reset. Sign in with your new password.");
    }

    /// <summary>Changes the caller's password and revokes every refresh token.</summary>
    [HttpPost("change-password")]
    [Authorize]
    public async Task<IActionResult> ChangePassword(ChangePasswordRequest request, CancellationToken ct)
    {
        await authService.ChangePasswordAsync(request, ct);
        return OkEnvelope("Password changed. Sign in again on your other devices.");
    }

    /// <summary>Rotates the refresh token. Reuse of a consumed token revokes the whole family.</summary>
    [HttpPost("refresh")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ApiResponse<RefreshResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Refresh(RefreshRequest request, CancellationToken ct) =>
        OkEnvelope(await authService.RefreshAsync(
            request, HttpContext.Connection.RemoteIpAddress?.ToString(), ct));

    /// <summary>Revokes the presented refresh token server-side.</summary>
    [HttpPost("logout")]
    [Authorize]
    public async Task<IActionResult> Logout(LogoutRequest request, CancellationToken ct)
    {
        await authService.LogoutAsync(request, ct);
        return OkEnvelope("Signed out.");
    }

    /// <summary>The current user with fresh roles and permissions.</summary>
    [HttpGet("me")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<MeResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Me(CancellationToken ct) =>
        OkEnvelope(await authService.GetMeAsync(ct));
}
