using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Regalis.Application.Auth;
using Regalis.Application.Operations;
using Regalis.Shared.Api;

namespace Regalis.API.Controllers;

/// <summary>Own-record profile (API_PLAN §4). Always the caller's row — there is no
/// id parameter to tamper with.</summary>
[Route("api/profile")]
[Authorize]
public class ProfileController(IProfileService profileService) : ApiControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<ProfileDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Get(CancellationToken ct) =>
        OkEnvelope(await profileService.GetAsync(ct));

    [HttpPut]
    [ProducesResponseType(typeof(ApiResponse<ProfileDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Update(UpdateProfileRequest request, CancellationToken ct) =>
        OkEnvelope(await profileService.UpdateAsync(request, ct),
            "Profile updated. Issued invoices keep their original details.");

    /// <summary>What onboarding still needs, plus the profile to pre-fill the form.</summary>
    /// <remarks>Deliberately reachable while the profile is incomplete — it is how the
    /// SPA finds out that it is.</remarks>
    [HttpGet("onboarding")]
    [ProducesResponseType(typeof(ApiResponse<OnboardingStatusDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> OnboardingStatus(CancellationToken ct) =>
        OkEnvelope(await profileService.GetOnboardingStatusAsync(ct));

    /// <summary>Finishes onboarding: phone, company, address, date of birth, gender and
    /// GSTIN in one submission. Member accounts only.</summary>
    [HttpPost("onboarding")]
    [ProducesResponseType(typeof(ApiResponse<ProfileDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CompleteOnboarding(CompleteOnboardingRequest request,
        CancellationToken ct) =>
        OkEnvelope(await profileService.CompleteOnboardingAsync(request, ct),
            "Thanks — your profile is complete.");

    [HttpPost("photo")]
    [RequestSizeLimit(3 * 1024 * 1024)]
    [ProducesResponseType(typeof(ApiResponse<ProfileDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> SetPhoto(IFormFile file, CancellationToken ct)
    {
        await using var stream = file.OpenReadStream();
        var profile = await profileService.SetPhotoAsync(
            new AttachmentUpload(file.FileName, file.ContentType, file.Length, stream), ct);
        return OkEnvelope(profile, "Photo updated.");
    }

    [HttpGet("photo")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPhoto(CancellationToken ct)
    {
        var photo = await profileService.GetPhotoAsync(ct);
        return File(photo.Content, photo.ContentType);
    }
}
