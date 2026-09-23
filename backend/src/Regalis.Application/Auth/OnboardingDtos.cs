using FluentValidation;

namespace Regalis.Application.Auth;

/// <summary>
/// The details a member supplies to finish onboarding.
///
/// Google gives us a name and a verified email and nothing else, so an account created
/// that way starts with no phone number and no address — and an address is what an
/// invoice needs. The same form also repairs accounts that predate the structured
/// columns, which is every account created before this shipped.
///
/// Email is absent on purpose: it is the login, it arrived verified from Google or from
/// an OTP, and changing it is an admin action with re-verification.
/// </summary>
public record CompleteOnboardingRequest(
    string FullName,
    string MobileNumber,
    string CompanyName,
    string AddressLine1,
    string AddressLine2,
    string City,
    string State,
    string Country,
    string PostalCode,
    DateOnly DateOfBirth,
    byte Gender,
    string Gstin);

/// <summary>What the SPA needs to decide whether to send a member to onboarding, and to
/// pre-fill the form when it does.</summary>
public record OnboardingStatusDto(
    bool IsComplete,
    IReadOnlyList<string> MissingFields,
    ProfileDto Profile);

public class CompleteOnboardingRequestValidator : AbstractValidator<CompleteOnboardingRequest>
{
    public CompleteOnboardingRequestValidator()
    {
        RuleFor(x => x.FullName).NotEmpty().MaximumLength(150);

        RuleFor(x => x.MobileNumber)
            .NotEmpty()
            .Matches(@"^\+?[0-9]{10,15}$")
            .WithMessage("Enter a valid mobile number (10–15 digits).");

        RuleFor(x => x.CompanyName).NotEmpty().MaximumLength(200);

        RuleFor(x => x.AddressLine1).NotEmpty().MaximumLength(200);
        RuleFor(x => x.AddressLine2).NotEmpty().MaximumLength(200);
        RuleFor(x => x.City).NotEmpty().MaximumLength(100);
        RuleFor(x => x.State).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Country).NotEmpty().MaximumLength(100);

        RuleFor(x => x.PostalCode)
            .NotEmpty()
            .Matches(@"^[1-9][0-9]{5}$")
            .WithMessage("Enter a valid 6-digit PIN code.");

        RuleFor(x => x.DateOfBirth)
            .Must(d => d != default).WithMessage("Date of birth is required.")
            .Must(d => d < DateOnly.FromDateTime(DateTime.UtcNow))
            .WithMessage("Date of birth must be in the past.")
            // A floor rather than an age policy: an age limit belongs in MST_AppSetting,
            // not in a validator. This only rejects obvious data-entry nonsense.
            .Must(d => d > new DateOnly(1900, 1, 1))
            .WithMessage("Enter a valid date of birth.");

        RuleFor(x => x.Gender)
            .Must(g => g is >= 1 and <= 4)
            .WithMessage("Select a gender from the list.");

        // Case-insensitive here and upper-cased on save, so someone typing their GSTIN in
        // lower case is not told it is invalid when it plainly is not.
        RuleFor(x => x.Gstin)
            .NotEmpty()
            .Matches(@"^[0-9]{2}[A-Z]{5}[0-9]{4}[A-Z]{1}[A-Z0-9]{1}Z[A-Z0-9]{1}$",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase)
            .WithMessage("Enter a valid 15-character GSTIN.");
    }
}
