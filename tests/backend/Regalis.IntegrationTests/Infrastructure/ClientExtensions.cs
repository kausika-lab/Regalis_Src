using System.Net.Http.Headers;
using System.Net.Http.Json;
using Regalis.Application.Auth;
using Regalis.Shared.Api;

namespace Regalis.IntegrationTests.Infrastructure;

public static class ClientExtensions
{
    public static async Task<LoginResponse> LoginAsync(this HttpClient client, string email,
        string password, string portal)
    {
        var response = await client.PostAsJsonAsync("/api/auth/login",
            new LoginRequest(email, password, portal));
        response.EnsureSuccessStatusCode();
        var envelope = await response.Content.ReadFromJsonAsync<ApiResponse<LoginResponse>>();
        return envelope!.Data!;
    }

    public static async Task<LoginResponse> LoginAsAdminAsync(this HttpClient client) =>
        await client.LoginAsync(RegalisApiFactory.AdminEmail, RegalisApiFactory.AdminPassword, "admin");

    /// <summary>Registers + verifies a fresh customer and returns their session.</summary>
    public static async Task<(string Email, string Password, LoginResponse Session)> RegisterCustomerAsync(
        this HttpClient client, RegalisApiFactory factory)
    {
        var email = $"cust-{Guid.NewGuid():N}@test.regalis.in";
        const string password = "Cust0mer!Pass";
        var mobile = $"9{Random.Shared.NextInt64(100_000_000, 999_999_999)}";

        var register = await client.PostAsJsonAsync("/api/auth/register",
            new RegisterRequest("Test Customer", email, mobile, password, password, null));
        register.EnsureSuccessStatusCode();

        var code = factory.OtpSender.GetCode(email, Domain.Enums.OtpPurpose.Registration);
        var verify = await client.PostAsJsonAsync("/api/auth/verify-otp",
            new VerifyOtpRequest(email, "registration", code));
        verify.EnsureSuccessStatusCode();

        return (email, password, await client.LoginAsync(email, password, "customer"));
    }

    public static void UseToken(this HttpClient client, string accessToken) =>
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
}
