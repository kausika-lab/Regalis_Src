using FluentValidation;
using Regalis.Application.Operations;

namespace Regalis.Application.Auth;

/// <summary>
/// The caller's own profile. <c>Address</c> is the legacy single line, composed from the
/// structured parts on save and kept because TRN_Invoice snapshots it — read the parts.
/// The new members are optional so existing callers compile unchanged.
/// </summary>
public record ProfileDto(
    long UserId,
    string FullName,
    string Email,
    string MobileNumber,
    string? CompanyName,
    string? Address,
    bool HasPhoto,
    byte UserType,
    DateTime CreatedDate,
    string? AddressLine1 = null,
    string? AddressLine2 = null,
    string? City = null,
    string? State = null,
    string? Country = null,
    string? PostalCode = null,
    DateOnly? DateOfBirth = null,
    byte? Gender = null,
    string? Gstin = null,
    bool IsProfileComplete = true,
    // False on a Google-only account, so the SPA can offer "set a password" instead of
    // a change-password form that cannot work.
    bool HasPassword = true,
    bool IsGoogleLinked = false);

/// <summary>Email is the login and is never edited here; a change of email is an
/// admin action with re-verification (out of Phase 1 scope).</summary>
public record UpdateProfileRequest(
    string FullName,
    string MobileNumber,
    string? CompanyName,
    string? Address);

public record ProfilePhotoContent(byte[] Content, string ContentType);

public class UpdateProfileRequestValidator : AbstractValidator<UpdateProfileRequest>
{
    public UpdateProfileRequestValidator()
    {
        RuleFor(x => x.FullName).NotEmpty().MaximumLength(150);
        RuleFor(x => x.MobileNumber).NotEmpty().Matches(@"^\+?[0-9]{10,15}$");
        RuleFor(x => x.CompanyName).MaximumLength(200);
        RuleFor(x => x.Address).MaximumLength(500);
    }
}

/// <summary>Own-record profile (API_PLAN §4): GET/PUT /profile + photo.</summary>
public interface IProfileService
{
    Task<ProfileDto> GetAsync(CancellationToken ct = default);
    Task<ProfileDto> UpdateAsync(UpdateProfileRequest request, CancellationToken ct = default);
    /// <summary>What is still missing, for the SPA's onboarding gate.</summary>
    Task<OnboardingStatusDto> GetOnboardingStatusAsync(CancellationToken ct = default);
    /// <summary>Finishes onboarding. Customers only — staff are created by an admin and
    /// are never asked for a GSTIN or a date of birth.</summary>
    Task<ProfileDto> CompleteOnboardingAsync(CompleteOnboardingRequest request,
        CancellationToken ct = default);
    /// <summary>.png .jpg .jpeg .webp, ≤2 MB, content-sniffed like ticket attachments.</summary>
    Task<ProfileDto> SetPhotoAsync(AttachmentUpload upload, CancellationToken ct = default);
    Task<ProfilePhotoContent> GetPhotoAsync(CancellationToken ct = default);
}
