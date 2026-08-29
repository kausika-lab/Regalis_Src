using FluentValidation;
using Regalis.Application.Operations;

namespace Regalis.Application.Auth;

public record ProfileDto(
    long UserId,
    string FullName,
    string Email,
    string MobileNumber,
    string? CompanyName,
    string? Address,
    bool HasPhoto,
    byte UserType,
    DateTime CreatedDate);

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
    /// <summary>.png .jpg .jpeg .webp, ≤2 MB, content-sniffed like ticket attachments.</summary>
    Task<ProfileDto> SetPhotoAsync(AttachmentUpload upload, CancellationToken ct = default);
    Task<ProfilePhotoContent> GetPhotoAsync(CancellationToken ct = default);
}
