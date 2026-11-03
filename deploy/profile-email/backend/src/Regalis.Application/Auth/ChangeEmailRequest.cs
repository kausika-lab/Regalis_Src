using System.Threading;
using System.Threading.Tasks;
using FluentValidation;

namespace Regalis.Application.Auth;

/// <summary>Own-email change from the admin portal profile. Staff and admins only;
/// the current password proves it is the account holder at the keyboard.</summary>
public record ChangeEmailRequest(string NewEmail, string CurrentPassword);

public class ChangeEmailRequestValidator : AbstractValidator<ChangeEmailRequest>
{
	public ChangeEmailRequestValidator()
	{
		RuleFor((ChangeEmailRequest x) => x.NewEmail).NotEmpty().MaximumLength(256).EmailAddress();
		RuleFor((ChangeEmailRequest x) => x.CurrentPassword).NotEmpty();
	}
}

/// <summary>PUT /api/profile/email.</summary>
public interface IProfileEmailService
{
	Task<ProfileDto> ChangeEmailAsync(ChangeEmailRequest request, CancellationToken ct = default(CancellationToken));
}
