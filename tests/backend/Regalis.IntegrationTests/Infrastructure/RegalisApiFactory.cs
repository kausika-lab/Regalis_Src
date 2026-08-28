using System.Collections.Concurrent;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Regalis.Application.Abstractions;
using Regalis.Domain.Enums;

namespace Regalis.IntegrationTests.Infrastructure;

/// <summary>
/// Boots the real API (environment "Test") against RegalisWorkSpace_Test on the
/// local SQL Server: migrations + seed run on startup, so the tests exercise the
/// same stack QA tests by hand. The database is dropped once per test run.
/// </summary>
public class RegalisApiFactory : WebApplicationFactory<Program>
{
    public const string AdminEmail = "enquiry@amstag.in";
    public const string AdminPassword = "Integration!Test1";

    private const string TestConnectionString =
        "Server=localhost;Database=RegalisWorkSpace_Test;Trusted_Connection=True;" +
        "TrustServerCertificate=True;MultipleActiveResultSets=True";

    public CapturingOtpSender OtpSender { get; } = new();

    static RegalisApiFactory()
    {
        // Fresh schema per run — before any host starts migrating.
        using var connection = new SqlConnection(
            "Server=localhost;Database=master;Trusted_Connection=True;TrustServerCertificate=True");
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = """
            IF DB_ID('RegalisWorkSpace_Test') IS NOT NULL
            BEGIN
                ALTER DATABASE [RegalisWorkSpace_Test] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
                DROP DATABASE [RegalisWorkSpace_Test];
            END
            """;
        command.ExecuteNonQuery();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Test");
        // UseSetting flows into the minimal-hosting builder before Program's own
        // top-level configuration reads — ConfigureAppConfiguration would be too late.
        builder.UseSetting("ConnectionStrings:Default", TestConnectionString);
        builder.UseSetting("Jwt:SigningKey",
            "integration-test-signing-key-0123456789abcdef0123456789abcdef");
        builder.UseSetting("Database:MigrateOnStartup", "true");
        builder.UseSetting("Database:SeedOnStartup", "true");
        builder.UseSetting("Seed:AdminPassword", AdminPassword);
        // Rate limits are not under test here; raise them far above use.
        builder.UseSetting("RateLimiting:AuthPerMinute", "10000");
        builder.UseSetting("RateLimiting:WritePerMinute", "10000");
        builder.UseSetting("RateLimiting:ReadPerMinute", "10000");
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IOtpSender>();
            services.AddSingleton<IOtpSender>(OtpSender);
        });
    }
}

/// <summary>Captures OTP codes per destination so tests can complete the flows.</summary>
public class CapturingOtpSender : IOtpSender
{
    private readonly ConcurrentDictionary<(string Destination, OtpPurpose Purpose), string> _codes = new();

    public Task SendAsync(string destination, OtpChannel channel, OtpPurpose purpose, string code,
        CancellationToken ct = default)
    {
        _codes[(destination.ToLowerInvariant(), purpose)] = code;
        return Task.CompletedTask;
    }

    public string GetCode(string destination, OtpPurpose purpose) =>
        _codes[(destination.ToLowerInvariant(), purpose)];
}
