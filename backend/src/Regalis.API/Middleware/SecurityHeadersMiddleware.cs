namespace Regalis.API.Middleware;

/// <summary>Security response headers (ARCHITECTURE §3). HSTS is added separately
/// by UseHsts outside Development.</summary>
public class SecurityHeadersMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context)
    {
        var headers = context.Response.Headers;
        headers["X-Content-Type-Options"] = "nosniff";
        headers["X-Frame-Options"] = "DENY";
        headers["Referrer-Policy"] = "no-referrer";
        headers["X-XSS-Protection"] = "0";
        // The API serves JSON (plus Swagger UI in non-production); a restrictive CSP is safe.
        headers["Content-Security-Policy"] =
            "default-src 'self'; img-src 'self' data:; style-src 'self' 'unsafe-inline'; script-src 'self'";
        headers["Permissions-Policy"] = "camera=(), microphone=(), geolocation=()";

        await next(context);
    }
}
