using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Regalis.API.Authorization;
using Regalis.Application.Account;
using Regalis.Shared.Api;

namespace Regalis.API.Controllers;

/// <summary>
/// customer-10 kit. Payment History (PAYMENT.VIEW, which customers already hold; the
/// service scopes every row to the caller) and Delete Account (signed-in customers only;
/// the service refuses staff). The delete and code endpoints use the "auth" rate limit,
/// like sign-in, so a password cannot be guessed through them.
/// </summary>
[Route("api/account")]
public class CustomerAccountController(IAccountDeletionService deletion, IMyPaymentsService payments) : ApiControllerBase
{
	[HttpGet("payments")]
	[HasPermission("PAYMENT.VIEW")]
	[ProducesResponseType(typeof(ApiResponse<PagedResult<MyPaymentDto>>), 200)]
	public async Task<IActionResult> Payments([FromQuery] MyPaymentsQuery query, CancellationToken ct)
	{
		return OkEnvelope(await payments.ListAsync(query, ct));
	}

	[HttpGet("payments/{id:long}")]
	[HasPermission("PAYMENT.VIEW")]
	[ProducesResponseType(typeof(ApiResponse<MyPaymentDto>), 200)]
	public async Task<IActionResult> Payment(long id, CancellationToken ct)
	{
		return OkEnvelope(await payments.GetAsync(id, ct));
	}

	[HttpGet("deletion")]
	[ProducesResponseType(typeof(ApiResponse<AccountDeletionCheckDto>), 200)]
	public async Task<IActionResult> DeletionCheck(CancellationToken ct)
	{
		return OkEnvelope(await deletion.CheckAsync(ct));
	}

	[HttpPost("deletion/code")]
	[EnableRateLimiting("auth")]
	public async Task<IActionResult> SendCode(CancellationToken ct)
	{
		await deletion.SendCodeAsync(ct);
		return OkEnvelope("We have emailed you a code.");
	}

	[HttpPost("deletion")]
	[EnableRateLimiting("auth")]
	public async Task<IActionResult> Delete(DeleteAccountRequest request, CancellationToken ct)
	{
		await deletion.DeleteAsync(request, ct);
		return OkEnvelope("Your account has been deleted.");
	}
}
