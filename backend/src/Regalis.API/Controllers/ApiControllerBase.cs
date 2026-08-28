using Microsoft.AspNetCore.Mvc;
using Regalis.Shared.Api;

namespace Regalis.API.Controllers;

/// <summary>Base for every controller: produces the ApiResponse envelope so it is
/// implemented once, never hand-rolled (ARCHITECTURE §3).</summary>
[ApiController]
[Produces("application/json")]
public abstract class ApiControllerBase : ControllerBase
{
    protected IActionResult OkEnvelope(string message = "Operation completed successfully.") =>
        Ok(ApiResponse.Ok(message));

    protected IActionResult OkEnvelope<T>(T data, string message = "Operation completed successfully.") =>
        Ok(ApiResponse<T>.Ok(data, message));

    protected IActionResult CreatedEnvelope<T>(string location, T data,
        string message = "Resource created successfully.") =>
        Created(location, ApiResponse<T>.Ok(data, message));
}
