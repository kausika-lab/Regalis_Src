using System.Security.Cryptography;
using System.Text;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Regalis.API.Authorization;
using Regalis.API.Filters;
using Regalis.API.Middleware;
using Regalis.API.Services;
using Regalis.Application;
using Regalis.Application.Abstractions;
using Regalis.Infrastructure;
using Regalis.Infrastructure.Persistence;
using Regalis.Infrastructure.Security;
using Regalis.Shared.Api;
using Regalis.Shared.Constants;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// ── Logging ─────────────────────────────────────────────────────────────────
builder.Host.UseSerilog((context, services, loggerConfiguration) => loggerConfiguration
    .ReadFrom.Configuration(context.Configuration)
    .ReadFrom.Services(services)
    .Enrich.FromLogContext());

// ── JWT signing key: environment / user-secrets. Development gets an ephemeral
//    key so a fresh clone runs, with a warning that tokens die on restart. ─────
var signingKey = builder.Configuration["Jwt:SigningKey"];
if (string.IsNullOrWhiteSpace(signingKey))
{
    if (!builder.Environment.IsDevelopment())
    {
        throw new InvalidOperationException(
            "Jwt:SigningKey is not configured. Set it via environment variables or user-secrets.");
    }
    signingKey = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
    builder.Configuration["Jwt:SigningKey"] = signingKey;
}

// ── MVC + validation ────────────────────────────────────────────────────────
builder.Services.AddScoped<ValidationActionFilter>();
builder.Services.AddControllers(options => options.Filters.AddService<ValidationActionFilter>());

// Model-binding failures produce the same envelope as FluentValidation failures.
builder.Services.Configure<ApiBehaviorOptions>(options =>
{
    options.InvalidModelStateResponseFactory = context =>
    {
        var errors = context.ModelState
            .Where(kvp => kvp.Value?.Errors.Count > 0)
            .SelectMany(kvp => kvp.Value!.Errors.Select(e =>
                new ApiError(ErrorCodes.ValidationError,
                    string.IsNullOrEmpty(e.ErrorMessage) ? "Invalid value." : e.ErrorMessage,
                    kvp.Key)))
            .ToList();
        return new BadRequestObjectResult(
            ApiResponse.Fail("One or more validation errors occurred.", errors));
    };
});

// ── Swagger ─────────────────────────────────────────────────────────────────
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Regalis WorkSpace API",
        Version = "v1",
        Description = "Coworking-space management platform — one backend serving the customer and admin portals."
    });
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "JWT access token. Obtain via POST /api/auth/login."
    });
    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
            },
            Array.Empty<string>()
        }
    });
    foreach (var xmlFile in Directory.GetFiles(AppContext.BaseDirectory, "Regalis.*.xml"))
    {
        options.IncludeXmlComments(xmlFile);
    }
});

// ── Authentication (JWT bearer, raw claims) ─────────────────────────────────
builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.MapInboundClaims = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidateAudience = true,
            ValidAudience = builder.Configuration["Jwt:Audience"],
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(30),
            NameClaimType = "name",
            RoleClaimType = "role"
        };
        options.Events = new JwtBearerEvents
        {
            // Keep the response contract: 401/403 also arrive as ApiResponse envelopes.
            OnChallenge = async context =>
            {
                context.HandleResponse();
                if (context.Response.HasStarted)
                {
                    return;
                }
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                var code = context.AuthenticateFailure is SecurityTokenExpiredException
                    ? ErrorCodes.TokenExpired
                    : ErrorCodes.AuthenticationFailed;
                await context.Response.WriteAsJsonAsync(ApiResponse.Fail(
                    "Authentication is required to access this resource.",
                    [new ApiError(code, "Missing, invalid or expired access token.")]));
            },
            OnForbidden = async context =>
            {
                if (context.Response.HasStarted)
                {
                    return;
                }
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                await context.Response.WriteAsJsonAsync(ApiResponse.Fail(
                    "You do not have permission to perform this action.",
                    [new ApiError(ErrorCodes.AuthorizationFailed, "Required permission is missing.")]));
            }
        };
    });

// ── Dynamic permission authorization (ARCHITECTURE §5) ──────────────────────
builder.Services.AddAuthorization();
builder.Services.AddSingleton<IAuthorizationPolicyProvider, PermissionPolicyProvider>();
builder.Services.AddScoped<IAuthorizationHandler, PermissionAuthorizationHandler>();

// ── Application + Infrastructure ────────────────────────────────────────────
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUser, CurrentUser>();
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

// ── CORS: origins from configuration only ───────────────────────────────────
const string CorsPolicy = "Frontend";
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
builder.Services.AddCors(options => options.AddPolicy(CorsPolicy, policy => policy
    .WithOrigins(allowedOrigins)
    .AllowAnyHeader()
    .AllowAnyMethod()
    .AllowCredentials()
    .WithExposedHeaders(CorrelationIdMiddleware.HeaderName)));

// ── Rate limiting (API_PLAN §15) ────────────────────────────────────────────
var rateLimits = builder.Configuration.GetSection("RateLimiting");
int Limit(string key, int fallback) => rateLimits.GetValue(key, fallback);

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.OnRejected = async (context, cancellationToken) =>
    {
        if (!context.HttpContext.Response.HasStarted)
        {
            await context.HttpContext.Response.WriteAsJsonAsync(ApiResponse.Fail(
                "Too many requests. Please try again shortly.",
                [new ApiError(ErrorCodes.RateLimitExceeded, "Request rate limit exceeded.")]),
                cancellationToken);
        }
    };

    // Strict policy for /auth/* endpoints — 5/minute/IP.
    options.AddPolicy("auth", context => RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = Limit("AuthPerMinute", 5),
            Window = TimeSpan.FromMinutes(1)
        }));

    // Payment gateway webhook — 120/minute/IP.
    options.AddPolicy("webhook", context => RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = Limit("WebhookPerMinute", 120),
            Window = TimeSpan.FromMinutes(1)
        }));

    // Global: writes 60/minute, reads 300/minute, partitioned per user (or IP).
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
    {
        var caller = context.User.Identity?.IsAuthenticated == true
            ? context.User.FindFirst("sub")?.Value ?? "anonymous"
            : context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        var isWrite = HttpMethods.IsPost(context.Request.Method) ||
                      HttpMethods.IsPut(context.Request.Method) ||
                      HttpMethods.IsPatch(context.Request.Method) ||
                      HttpMethods.IsDelete(context.Request.Method);
        return RateLimitPartition.GetFixedWindowLimiter(
            $"{caller}:{(isWrite ? "write" : "read")}",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = isWrite ? Limit("WritePerMinute", 60) : Limit("ReadPerMinute", 300),
                Window = TimeSpan.FromMinutes(1)
            });
    });
});

// ── Health checks ───────────────────────────────────────────────────────────
builder.Services.AddHealthChecks()
    .AddDbContextCheck<RegalisDbContext>("database", tags: ["ready"]);

var app = builder.Build();

// ── Pipeline (ARCHITECTURE §3) ──────────────────────────────────────────────
app.UseMiddleware<CorrelationIdMiddleware>();
app.UseMiddleware<ExceptionHandlingMiddleware>();
app.UseMiddleware<RequestLoggingMiddleware>();
app.UseMiddleware<SecurityHeadersMiddleware>();

if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
    app.UseHttpsRedirection();
}

var swaggerEnabled = !app.Environment.IsProduction() ||
                     app.Configuration.GetValue("Swagger:EnableInProduction", false);
if (swaggerEnabled)
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "Regalis WorkSpace API v1");
        options.DocumentTitle = "Regalis WorkSpace API";
    });
}

app.UseCors(CorsPolicy);
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHealthChecks("/health", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    Predicate = _ => false // liveness: process is up
});
app.MapHealthChecks("/health/ready", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready")
});

app.Run();

/// <summary>Exposed for WebApplicationFactory in integration tests.</summary>
public partial class Program;
