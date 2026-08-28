using FluentValidation;
using Microsoft.AspNetCore.Mvc.Filters;
using Regalis.Application.Common.Exceptions;
using Regalis.Shared.Api;
using Regalis.Shared.Constants;

namespace Regalis.API.Filters;

/// <summary>
/// Runs the registered FluentValidation validator for every action argument.
/// Failures throw <see cref="AppValidationException"/>, which the exception middleware
/// maps to a 400 envelope with field-level errors — so validation behaves identically
/// on every endpoint.
/// </summary>
public class ValidationActionFilter(IServiceProvider serviceProvider) : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var errors = new List<ApiError>();

        foreach (var argument in context.ActionArguments.Values)
        {
            if (argument is null)
            {
                continue;
            }

            var validatorType = typeof(IValidator<>).MakeGenericType(argument.GetType());
            if (serviceProvider.GetService(validatorType) is not IValidator validator)
            {
                continue;
            }

            var result = await validator.ValidateAsync(
                new ValidationContext<object>(argument), context.HttpContext.RequestAborted);
            if (!result.IsValid)
            {
                errors.AddRange(result.Errors.Select(f =>
                    new ApiError(ErrorCodes.ValidationError, f.ErrorMessage, ToCamelCase(f.PropertyName))));
            }
        }

        if (errors.Count > 0)
        {
            throw new AppValidationException(errors);
        }

        await next();
    }

    private static string ToCamelCase(string propertyName) =>
        string.IsNullOrEmpty(propertyName)
            ? propertyName
            : char.ToLowerInvariant(propertyName[0]) + propertyName[1..];
}
