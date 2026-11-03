using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Regalis.Application.Auth;
using Regalis.Shared.Api;

namespace Regalis.API.Controllers;

/// <summary>Own sign-in email (admin portal profile). Always the caller's row.</summary>
[Route("api/profile/email")]
[Authorize]
public class ProfileEmailController(IProfileEmailService profileEmailService) : ApiControllerBase
{
	[HttpPut]
	[ProducesResponseType(typeof(ApiResponse<ProfileDto>), 200)]
	[ProducesResponseType(typeof(ApiResponse), 400)]
	[ProducesResponseType(typeof(ApiResponse), 409)]
	public async Task<IActionResult> Change(ChangeEmailRequest request, CancellationToken ct)
	{
		return OkEnvelope(await profileEmailService.ChangeEmailAsync(request, ct), "Email updated. Use the new address the next time you sign in.");
	}
}
