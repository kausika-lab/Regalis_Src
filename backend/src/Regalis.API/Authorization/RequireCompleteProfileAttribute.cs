using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;
using Regalis.Application.Abstractions;
using Regalis.Infrastructure.Persistence;
using Regalis.Shared.Api;
using Regalis.Shared.Constants;

namespace Regalis.API.Authorization;

/// <summary>
/// Refuses an action until the caller has finished onboarding.
///
/// The SPA redirects an incomplete member to the onboarding form, but a redirect is not
/// a control — anyone can call the API directly. This is the part that holds, and it goes
/// on the endpoints that cannot do their job without the missing data: anything raising
/// an invoice needs a billing address and a GSTIN.
///
/// An AUTHORIZATION filter, not an action filter, for two reasons. It runs before model
/// binding and validation, so an incomplete member is told to finish their profile rather
/// than being handed a validation error about a payload that was never going to be
/// accepted. And it means a malformed body cannot be used to probe past the gate.
///
/// Read-only endpoints are deliberately left open, so an incomplete member can still see
/// their account instead of meeting a wall everywhere. Staff and admins are never
/// blocked — <c>User.IsProfileComplete</c> treats them as complete by definition.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class RequireCompleteProfileAttribute : Attribute, IAsyncAuthorizationFilter
{
    public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        var services = context.HttpContext.RequestServices;
        var currentUser = services.GetRequiredService<ICurrentUser>();

        // Not signed in: let the authentication middleware say so rather than answering
        // an anonymous caller with a message about their profile.
        if (!currentUser.IsAuthenticated)
        {
            return;
        }

        var db = services.GetRequiredService<RegalisDbContext>();
        var user = await db.Users.AsNoTracking()
            .FirstOrDefaultAsync(u => u.UserId == currentUser.UserId,
                context.HttpContext.RequestAborted);

        if (user is null || user.IsProfileComplete)
        {
            return;
        }

        // 403 with its own code: the SPA keys on PROFILE_INCOMPLETE to route to the
        // onboarding form rather than showing a generic "not allowed".
        context.Result = new ObjectResult(ApiResponse.Fail(
            "Complete your profile before booking or buying — we need a billing address.",
            ErrorCodes.ProfileIncomplete,
            "Onboarding has not been completed for this account."))
        {
            StatusCode = StatusCodes.Status403Forbidden
        };
    }
}
