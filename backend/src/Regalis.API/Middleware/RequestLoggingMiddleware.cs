using System.Diagnostics;
using System.Security.Claims;

namespace Regalis.API.Middleware;

/// <summary>Structured request log: method, path, status, elapsed ms, user id.</summary>
public class RequestLoggingMiddleware(RequestDelegate next, ILogger<RequestLoggingMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            await next(context);
        }
        finally
        {
            stopwatch.Stop();
            var userId = context.User.FindFirstValue(ClaimTypes.NameIdentifier)
                         ?? context.User.FindFirstValue("sub");
            logger.LogInformation(
                "HTTP {Method} {Path} responded {StatusCode} in {ElapsedMs} ms (user: {UserId})",
                context.Request.Method,
                context.Request.Path.Value,
                context.Response.StatusCode,
                stopwatch.ElapsedMilliseconds,
                userId ?? "anonymous");
        }
    }
}
