using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Regalis.Application.Abstractions;
using Regalis.Application.Administration;
using Regalis.Application.Auth;
using Regalis.Infrastructure.Adapters;
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

        return services;
    }
}
