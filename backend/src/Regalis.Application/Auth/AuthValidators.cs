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
