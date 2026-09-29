using Regalis.API.Auditing;
using Regalis.Application.Abstractions;
using Regalis.Application.Common.Exceptions;
using Regalis.Shared.Api;
using Regalis.Shared.Constants;

namespace Regalis.API.Middleware;

/// <summary>Maps exceptions to the ApiResponse envelope (ARCHITECTURE §9).
/// Stack traces reach the log, never the client.</summary>
public class ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await AuditOutcome.CaptureBodyAsync(context);
            await next(context);
        }
        catch (Exception exception)
        {
            var correlationId = context.Items[CorrelationIdMiddleware.ItemKey]?.ToString();
            await RecordAuditAsync(context, exception);

            var (statusCode, response) = exception switch
            {
                AppValidationException ex => (StatusCodes.Status400BadRequest,
                    ApiResponse.Fail("One or more validation errors occurred.", ex.ToErrors(), correlationId)),
                UnauthorizedException ex => (StatusCodes.Status401Unauthorized,
                    ApiResponse.Fail(ex.Message, ex.ToErrors(), correlationId)),
                ForbiddenException ex => (StatusCodes.Status403Forbidden,
                    ApiResponse.Fail(ex.Message, ex.ToErrors(), correlationId)),
                NotFoundException ex => (StatusCodes.Status404NotFound,
                    ApiResponse.Fail(ex.Message, ex.ToErrors(), correlationId)),
                ConflictException ex => (StatusCodes.Status409Conflict,
                    ApiResponse.Fail(ex.Message, ex.ToErrors(), correlationId)),
                BusinessRuleException ex => (StatusCodes.Status422UnprocessableEntity,
                    ApiResponse.Fail(ex.Message, ex.ToErrors(), correlationId)),
                _ => (StatusCodes.Status500InternalServerError,
                    ApiResponse.Fail("An unexpected error occurred. Please try again later.",
                        [new ApiError(ErrorCodes.InternalError, "An unexpected error occurred.")],
                        correlationId))
            };

            if (statusCode >= StatusCodes.Status500InternalServerError)
            {
                logger.LogError(exception, "Unhandled exception for {Method} {Path}",
                    context.Request.Method, context.Request.Path.Value);
            }
            else
            {
                logger.LogWarning("Handled {ExceptionType} for {Method} {Path}: {Message}",
                    exception.GetType().Name, context.Request.Method,
                    context.Request.Path.Value, exception.Message);
            }

            if (context.Response.HasStarted)
            {
                throw;
            }

            context.Response.Clear();
            context.Response.Headers[CorrelationIdMiddleware.HeaderName] = correlationId;
            context.Response.StatusCode = statusCode;
            await context.Response.WriteAsJsonAsync(response);
        }
    }

    /// <summary>Audit Trail §5.3: a change that was refused is Denied (permission or
    /// business rule), one that broke is Failed (validation or an unexpected error).
    /// Only signed-in changes are recorded — sign-in failures have their own
    /// LOGIN_FAILED rows, reads are not audited, and a 404 is not an attempted change.</summary>
    private async Task RecordAuditAsync(HttpContext context, Exception exception)
    {
        if (!AuditOutcome.IsMutation(context.Request.Method) || context.Request.Path.StartsWithSegments("/api/auth"))
        {
            return;
        }
        var (result, reason) = exception switch
        {
            ForbiddenException ex => (AuditResults.Denied, ex.Message),
            BusinessRuleException ex => (AuditResults.Denied, ex.Message),
            ConflictException ex => (AuditResults.Denied, ex.Message),
            AppValidationException ex => (AuditResults.Failed,
                "Validation error: " + string.Join("; ", ex.ToErrors().Select(e => e.Message).Distinct().Take(3))),
            UnauthorizedException or NotFoundException => (null, null),
            _ => (AuditResults.Failed, "Unexpected error — see the API log for request " +
                                       (context.Items[CorrelationIdMiddleware.ItemKey]?.ToString() ?? "?"))
        };
        if (result is null || context.RequestServices.GetRequiredService<ICurrentUser>() is not { IsAuthenticated: true })
        {
            return;
        }
        try
        {
            await AuditOutcome.RecordAsync(context, result, reason!);
        }
        catch (Exception auditError)
        {
            // Never let the audit write replace the original error response.
            logger.LogError(auditError, "Could not write the {Result} audit row", result);
        }
    }
}
