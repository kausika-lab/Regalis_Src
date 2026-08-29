using Microsoft.AspNetCore.Mvc;
using Regalis.API.Authorization;
using Regalis.Application.Administration;
using Regalis.Shared.Api;
using Regalis.Shared.Constants;

namespace Regalis.API.Controllers;

/// <summary>Customer reads for operational roles (API_PLAN §4): managers and support
/// hold CUSTOMER.VIEW without USER.VIEW — this is their window onto members.</summary>
[Route("api/customers")]
public class CustomersController(IUserAdminService userAdminService) : ApiControllerBase
{
    [HttpGet]
    [HasPermission(PermissionCodes.Customer.View)]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<UserListItemDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll([FromQuery] UserListRequest request, CancellationToken ct)
    {
        request.UserType = 1; // customers only, whatever the caller asks for
        return OkEnvelope(await userAdminService.GetUsersAsync(request, ct));
    }

    [HttpGet("{id:long}")]
    [HasPermission(PermissionCodes.Customer.View)]
    [ProducesResponseType(typeof(ApiResponse<UserDetailDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetById(long id, CancellationToken ct)
    {
        var user = await userAdminService.GetUserAsync(id, ct);
        if (user.UserType != 1)
        {
            return NotFound(ApiResponse.Fail("Customer not found.",
                [new ApiError(ErrorCodes.NotFound, $"Customer {id} was not found.")]));
        }
        return OkEnvelope(user);
    }
}
