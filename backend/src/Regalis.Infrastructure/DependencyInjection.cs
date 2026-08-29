using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Regalis.Application.Abstractions;
using Regalis.Application.Administration;
using Regalis.Application.Auth;
using Regalis.Application.Bookings;
using Regalis.Application.Finance;
using Regalis.Application.Masters;
using Regalis.Infrastructure.Adapters;
using Regalis.Infrastructure.BackgroundJobs;
using Regalis.Infrastructure.Persistence;
using Regalis.Infrastructure.Persistence.Interceptors;
using Regalis.Infrastructure.Persistence.Seed;
using Regalis.Infrastructure.Security;
using Regalis.Infrastructure.Services;

namespace Regalis.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<AuditSaveChangesInterceptor>();

        services.AddDbContext<RegalisDbContext>((provider, dbOptions) =>
        {
            dbOptions.UseSqlServer(
                configuration.GetConnectionString("Default"),
                sql => sql.EnableRetryOnFailure(maxRetryCount: 3));
            dbOptions.AddInterceptors(provider.GetRequiredService<AuditSaveChangesInterceptor>());
        });

        services.Configure<JwtOptions>(configuration.GetSection(JwtOptions.SectionName));

        services.AddSingleton<IDateTimeProvider, SystemDateTimeProvider>();
        services.AddSingleton<IPasswordHasher, PasswordHasher>();
        services.AddSingleton<ITokenService, JwtTokenService>();

        services.AddMemoryCache();
        services.AddScoped<ISettingService, SettingService>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IMenuQueryService, MenuQueryService>();
        services.AddScoped<IUserAdminService, UserAdminService>();
        services.AddScoped<IRoleAdminService, RoleAdminService>();
        services.AddScoped<IMenuAdminService, MenuAdminService>();
        services.AddScoped<DatabaseSeeder>();

        // Adapter selection by configuration (Invariant 5): only the console
        // channel exists until SMTP credentials arrive (execution-plan risk #3).
        services.AddScoped<IOtpSender, ConsoleOtpSender>();

        // Masters
        services.AddScoped<ILocationService, LocationService>();
        services.AddScoped<IWorkspaceService, WorkspaceService>();

        // Booking engine
        services.AddScoped<INumberSequenceService, NumberSequenceService>();
        services.AddScoped<IAvailabilityService, AvailabilityService>();
        services.AddScoped<IBookingService, BookingService>();
        services.AddHostedService<BookingHoldSweeper>();

        services.AddScoped<ICalendarService, CalendarService>();

        // Finance
        services.AddScoped<IWalletService, WalletService>();
        services.AddScoped<IInvoiceService, InvoiceService>();
        services.AddScoped<IPaymentService, PaymentService>();

        // Payment gateway — chosen by Payments:Provider (Invariant 5). Sandbox is
        // registered alongside so the demo checkout can sign in dev/test.
        services.AddScoped<SandboxPaymentGateway>();
        services.AddHttpClient<RazorpayPaymentGateway>();
        services.AddScoped<IPaymentGateway>(provider =>
            string.Equals(configuration["Payments:Provider"], RazorpayPaymentGateway.Name,
                StringComparison.OrdinalIgnoreCase)
                ? provider.GetRequiredService<RazorpayPaymentGateway>()
                : provider.GetRequiredService<SandboxPaymentGateway>());

        return services;
    }
}
