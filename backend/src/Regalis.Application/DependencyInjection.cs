using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

namespace Regalis.Application;

public static class DependencyInjection
{
    /// <summary>Registers application services and every FluentValidation validator
    /// in this assembly. Module services register here as they are built.</summary>
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddValidatorsFromAssembly(typeof(DependencyInjection).Assembly, includeInternalTypes: true);
        return services;
    }
}
