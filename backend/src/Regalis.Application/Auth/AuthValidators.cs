using FluentValidation;

namespace Regalis.Application.Auth;

/// <summary>Shared password-policy rule (ARCHITECTURE §4): minimum length is enforced
/// dynamically against Security.PasswordMinLength in the service; the composition
/// rule (upper + lower + digit + symbol) is validated here.</summary>
public static class PasswordRules
{
    public const string CompositionMessage =
        "Password must contain an uppercase letter, a lowercase letter, a digit and a symbol.";

    public static IRuleBuilderOptions<T, string> MustBeWellComposed<T>(this IRuleBuilder<T, string> rule) =>
        rule
            .Must(p => !string.IsNullOrEmpty(p) &&
                       p.Any(char.IsUpper) &&
                       p.Any(char.IsLower) &&
                       p.Any(char.IsDigit) &&
                       p.Any(c => !char.IsLetterOrDigit(c)))
            .WithMessage(CompositionMessage);
}

public class LoginRequestValidator : AbstractValidator<LoginRequest>
{
    public LoginRequestValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(256);
        RuleFor(x => x.Password).NotEmpty();
        RuleFor(x => x.Portal)
            .Must(p => p is "customer" or "admin")
            .WithMessage("Portal must be 'customer' or 'admin'.");
    }
}

public class GoogleLoginRequestValidator : AbstractValidator<GoogleLoginRequest>
{
    public GoogleLoginRequestValidator()
    {
        // A sanity bound only. Everything that matters about this token — signature,
        // issuer, audience, expiry — is settled by Google's verifier, not here.
        RuleFor(x => x.IdToken).NotEmpty().MaximumLength(4096);
    }
}

public class RegisterRequestValidator : AbstractValidator<RegisterRequest>
{
    public RegisterRequestValidator()
    {
        RuleFor(x => x.FullName).NotEmpty().MaximumLength(150);
        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(256);
        RuleFor(x => x.MobileNumber)
            .NotEmpty()
            .Matches(@"^\+?[0-9]{10,15}$")
            .WithMessage("Enter a valid mobile number (10–15 digits).");
        RuleFor(x => x.Password).NotEmpty().MinimumLength(8).MustBeWellComposed();
        RuleFor(x => x.ConfirmPassword)
            .Equal(x => x.Password)
            .WithMessage("Passwords do not match.");
        RuleFor(x => x.CompanyName).MaximumLength(200);

        // The profile fields stay OPTIONAL on this endpoint even though the SPA marks
        // them required. Making them mandatory here would break any other client and,
        // more importantly, there is already a place that insists on them: onboarding,
        // which a member cannot get past without filling them in. Validate shape, not
        // presence.
        RuleFor(x => x.AddressLine1).MaximumLength(200);
        RuleFor(x => x.AddressLine2).MaximumLength(200);
        RuleFor(x => x.City).MaximumLength(100);
        RuleFor(x => x.State).MaximumLength(100);
        RuleFor(x => x.Country).MaximumLength(100);
        RuleFor(x => x.PostalCode)
            .Matches(@"^[1-9][0-9]{5}$").When(x => !string.IsNullOrWhiteSpace(x.PostalCode))
            .WithMessage("Enter a valid 6-digit PIN code.");
        RuleFor(x => x.DateOfBirth)
            .Must(d => d < DateOnly.FromDateTime(DateTime.UtcNow)).When(x => x.DateOfBirth is not null)
            .WithMessage("Date of birth must be in the past.");
        RuleFor(x => x.Gender)
            .Must(g => g is >= 1 and <= 4).When(x => x.Gender is not null)
            .WithMessage("Select a gender from the list.");
        RuleFor(x => x.Gstin)
            .Matches(@"^[0-9]{2}[A-Z]{5}[0-9]{4}[A-Z]{1}[A-Z0-9]{1}Z[A-Z0-9]{1}$",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase)
            .When(x => !string.IsNullOrWhiteSpace(x.Gstin))
            .WithMessage("Enter a valid 15-character GSTIN.");
    }
}

public class VerifyOtpRequestValidator : AbstractValidator<VerifyOtpRequest>
{
    public VerifyOtpRequestValidator()
    {
        RuleFor(x => x.Destination).NotEmpty().MaximumLength(256);
        RuleFor(x => x.Purpose)
            .Must(p => OtpPurposes.Parse(p) is not null)
            .WithMessage("Purpose must be 'registration' or 'forgot-password'.");
        RuleFor(x => x.Code).NotEmpty().Matches("^[0-9]{6}$").WithMessage("Code must be 6 digits.");
    }
}

public class ResendOtpRequestValidator : AbstractValidator<ResendOtpRequest>
{
    public ResendOtpRequestValidator()
    {
        RuleFor(x => x.Destination).NotEmpty().MaximumLength(256);
        RuleFor(x => x.Purpose)
            .Must(p => OtpPurposes.Parse(p) is not null)
            .WithMessage("Purpose must be 'registration' or 'forgot-password'.");
    }
}

public class ForgotPasswordRequestValidator : AbstractValidator<ForgotPasswordRequest>
{
    public ForgotPasswordRequestValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(256);
    }
}

public class ResetPasswordRequestValidator : AbstractValidator<ResetPasswordRequest>
{
    public ResetPasswordRequestValidator()
    {
        RuleFor(x => x.ResetToken).NotEmpty();
        RuleFor(x => x.NewPassword).NotEmpty().MinimumLength(8).MustBeWellComposed();
        RuleFor(x => x.ConfirmPassword)
            .Equal(x => x.NewPassword)
            .WithMessage("Passwords do not match.");
    }
}

public class ChangePasswordRequestValidator : AbstractValidator<ChangePasswordRequest>
{
    public ChangePasswordRequestValidator()
    {
        RuleFor(x => x.CurrentPassword).NotEmpty();
        RuleFor(x => x.NewPassword).NotEmpty().MinimumLength(8).MustBeWellComposed();
        RuleFor(x => x.ConfirmPassword)
            .Equal(x => x.NewPassword)
            .WithMessage("Passwords do not match.");
    }
}

public class RefreshRequestValidator : AbstractValidator<RefreshRequest>
{
    public RefreshRequestValidator()
    {
        RuleFor(x => x.RefreshToken).NotEmpty();
    }
}

public class LogoutRequestValidator : AbstractValidator<LogoutRequest>
{
    public LogoutRequestValidator()
    {
        RuleFor(x => x.RefreshToken).NotEmpty();
    }
}
