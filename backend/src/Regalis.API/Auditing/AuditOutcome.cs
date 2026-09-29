using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Mvc.Controllers;
using Regalis.Application.Abstractions;
using Regalis.Domain.Entities.Identity;
using Regalis.Infrastructure.Persistence;
using Regalis.Shared.Constants;

namespace Regalis.API.Auditing;

/// <summary>
/// Builds the audit row for a request that did NOT go through: Failed (validation error,
/// exception) or Denied (missing permission, business rule) — Audit Trail Enhancements
/// §5.3. Successful changes are audited by the save-changes interceptor; this covers the
/// attempts that never reach a save, so blocked and failed operations are visible too.
///
/// What was attempted is inferred from the endpoint: HTTP verb and action name give the
/// action, the controller gives the entity, the route's id gives the entity ID. The JSON
/// body the caller sent (secrets stripped) is kept as the "After" the user tried to save.
/// </summary>
public static class AuditOutcome
{
    private const int MaxBodyBytes = 64 * 1024;
    public const string BodyItemKey = "Audit.RequestBody";

    private static readonly Lazy<Dictionary<string, string>> DomainModules = new(() =>
        typeof(AuditLog).Assembly.GetTypes()
            .Where(t => t is { IsClass: true, IsAbstract: false } && t.Namespace?.StartsWith("Regalis.Domain.Entities") == true)
            .GroupBy(t => t.Name)
            .ToDictionary(g => g.Key, g => g.First().Namespace!.Split('.').Last(), StringComparer.OrdinalIgnoreCase));

    private static readonly string[] SecretWords = ["password", "token", "otp", "secret", "hash", "pin"];

    /// <summary>Call before the endpoint runs: keeps a copy of a small JSON body so a
    /// failed attempt can show what was sent.</summary>
    public static async Task CaptureBodyAsync(HttpContext http)
    {
        var request = http.Request;
        if (!IsMutation(request.Method) || request.ContentType?.Contains("json", StringComparison.OrdinalIgnoreCase) != true
            || request.ContentLength is null or > MaxBodyBytes || request.Path.StartsWithSegments("/api/auth"))
        {
            return;
        }
        request.EnableBuffering();
        using var reader = new StreamReader(request.Body, Encoding.UTF8, leaveOpen: true);
        var body = await reader.ReadToEndAsync();
        request.Body.Position = 0;
        http.Items[BodyItemKey] = body;
    }

    public static bool IsMutation(string method) =>
        HttpMethods.IsPost(method) || HttpMethods.IsPut(method) || HttpMethods.IsPatch(method) || HttpMethods.IsDelete(method);

    public static AuditLog Build(HttpContext http, ICurrentUser user, DateTime nowUtc, string result, string reason,
        string? fallbackAction = null)
    {
        var descriptor = http.GetEndpoint()?.Metadata.GetMetadata<ControllerActionDescriptor>();
        var controller = descriptor?.ControllerName ?? "Unknown";
        var actionName = descriptor?.ActionName ?? "";
        var entity = EntityFor(controller);
        var entityId = http.Request.RouteValues.FirstOrDefault(kv =>
            kv.Key.Equals("id", StringComparison.OrdinalIgnoreCase)
            || (kv.Key.EndsWith("Id", StringComparison.OrdinalIgnoreCase) && kv.Key is not "controller" and not "action")).Value?.ToString();

        return new AuditLog
        {
            UserId = user.IsAuthenticated ? user.UserId : null,
            UserName = user.FullName,
            Action = ActionFor(http.Request.Method, actionName, entityId, fallbackAction),
            ModuleName = DomainModules.Value.TryGetValue(entity, out var module) ? module : controller,
            EntityName = entity,
            EntityId = entityId,
            NewValue = SanitisedBody(http),
            IpAddress = user.IpAddress,
            UserAgent = user.UserAgent is { Length: > 300 } ua ? ua[..300] : user.UserAgent,
            CorrelationId = user.CorrelationId,
            TimestampUtc = nowUtc,
            Result = result,
            FailureReason = reason.Length <= 500 ? reason : reason[..500]
        };
    }

    /// <summary>Writes the row through a fresh scope: the request's own context may hold
    /// the half-made changes that just failed.</summary>
    public static async Task RecordAsync(HttpContext http, string result, string reason, string? fallbackAction = null)
    {
        using var scope = http.RequestServices.GetRequiredService<IServiceScopeFactory>().CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RegalisDbContext>();
        var user = http.RequestServices.GetRequiredService<ICurrentUser>();
        var clock = http.RequestServices.GetRequiredService<IDateTimeProvider>();
        db.AuditLogs.Add(Build(http, user, clock.UtcNow, result, reason, fallbackAction));
        await db.SaveChangesAsync();
    }

    private static string ActionFor(string method, string actionName, string? entityId, string? fallback)
    {
        if (actionName.Contains("Approve", StringComparison.OrdinalIgnoreCase)) return AuditActions.Approve;
        if (actionName.Contains("Reject", StringComparison.OrdinalIgnoreCase)) return AuditActions.Reject;
        if (actionName.Contains("Export", StringComparison.OrdinalIgnoreCase)) return AuditActions.Export;
        if (HttpMethods.IsDelete(method)) return AuditActions.Delete;
        if (HttpMethods.IsPut(method) || HttpMethods.IsPatch(method)) return AuditActions.Update;
        if (HttpMethods.IsPost(method)) return entityId is null ? AuditActions.Create : AuditActions.Update;
        return fallback ?? AuditActions.AuthorizationFailed;
    }

    private static string EntityFor(string controller)
    {
        var name = controller.StartsWith("Admin", StringComparison.Ordinal) && controller.Length > 5 ? controller[5..] : controller;
        if (name.EndsWith("ies", StringComparison.Ordinal)) return name[..^3] + "y";
        if (name.EndsWith("sses", StringComparison.Ordinal)) return name[..^2];
        if (name.EndsWith('s') && !name.EndsWith("ss", StringComparison.Ordinal)) return name[..^1];
        return name;
    }

    private static string? SanitisedBody(HttpContext http)
    {
        if (http.Items[BodyItemKey] is not string body || string.IsNullOrWhiteSpace(body)) return null;
        try
        {
            var node = JsonNode.Parse(body);
            Strip(node);
            var text = node?.ToJsonString(new JsonSerializerOptions { WriteIndented = false });
            return text is { Length: > 8000 } ? text[..8000] : text;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static void Strip(JsonNode? node)
    {
        switch (node)
        {
            case JsonObject obj:
                foreach (var key in obj.Select(kv => kv.Key).ToList())
                {
                    if (SecretWords.Any(w => key.Contains(w, StringComparison.OrdinalIgnoreCase)))
                        obj[key] = "***REDACTED***";
                    else
                        Strip(obj[key]);
                }
                break;
            case JsonArray arr:
                foreach (var item in arr) Strip(item);
                break;
        }
    }
}
