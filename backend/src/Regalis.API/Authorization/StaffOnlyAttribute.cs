using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Regalis.API.Auditing;
using Regalis.Application.Abstractions;
using Regalis.Domain.Enums;
using Regalis.Shared.Api;
using Regalis.Shared.Constants;

namespace Regalis.API.Authorization;

/// <summary>
/// Refuses customer logins on an admin-portal endpoint, whatever permissions their token
/// carries.
///
/// Permissions alone cannot separate the portals: a customer legitimately holds
/// WALLET.VIEW and QUOTATION.VIEW for their OWN wallet and quotations, and those same
/// codes guard the admin screens that read EVERY customer's. Without this filter, one
/// customer could read another's wallet ledger (/api/wallet/{id}, /api/admin/wallets/{id})
/// or quotations (GET /api/quotations) — found 2026-09-28. Put it on any route whose data
/// spans customers; the customer portal keeps its own "my" routes.
///
/// An authorization filter, so it runs before model binding, like RequireCompleteProfile.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class StaffOnlyAttribute : Attribute, IAsyncAuthorizationFilter
{
    public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        var currentUser = context.HttpContext.RequestServices.GetRequiredService<ICurrentUser>();
        // Anonymous callers are left to the authentication middleware (401).
        if (!currentUser.IsAuthenticated || currentUser.UserType != UserType.Customer)
        {
            return;
        }
        context.Result = new ObjectResult(ApiResponse.Fail(
            "This is an admin-portal endpoint.", ErrorCodes.AuthorizationFailed,
            "Customer accounts cannot use admin endpoints."))
        {
            StatusCode = StatusCodes.Status403Forbidden
        };
        // Audit Trail §4.4: a customer probing an admin route is a Denied row.
        await AuditOutcome.RecordAsync(context.HttpContext, AuditResults.Denied,
            "Customer accounts cannot use admin endpoints");
    }
}
