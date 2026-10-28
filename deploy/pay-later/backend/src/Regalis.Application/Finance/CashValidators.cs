using FluentValidation;

namespace Regalis.Application.Finance;

public class RecordCollectionRequestValidator : AbstractValidator<RecordCollectionRequest>
{
    public RecordCollectionRequestValidator()
    {
        RuleFor(x => x.BookingId).GreaterThan(0L);
        RuleFor(x => x.Amount).GreaterThan(0m).LessThanOrEqualTo(10000000m)
            .WithMessage("Enter an amount greater than zero.");
        RuleFor(x => x.Amount).PrecisionScale(12, 2, ignoreTrailingZeros: true).WithMessage("At most 2 decimal places.");
        RuleFor(x => x.PaymentMethod).Must(m => m is 2 or 3 or 4 or 6)
            .WithMessage("Choose cash, UPI or card.");
        When(x => x.PaymentMethod == 6 && x.AmountTendered.HasValue, () =>
            RuleFor(x => x.AmountTendered!.Value).GreaterThanOrEqualTo(x => x.Amount)
                .WithMessage("The cash handed over is less than the amount."));
        When(x => x.PaymentMethod != 6, () =>
            RuleFor(x => x.ReferenceNumber).NotEmpty()
                .WithMessage("Enter the UPI transaction id or the card slip number."));
        RuleFor(x => x.ReferenceNumber).MaximumLength(50);
        RuleFor(x => x.Notes).MaximumLength(500);
    }
}

public class VoidCollectionRequestValidator : AbstractValidator<VoidCollectionRequest>
{
    public VoidCollectionRequestValidator()
    {
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(500).WithMessage("Say why the receipt is voided.");
    }
}

public class OpenRegisterRequestValidator : AbstractValidator<OpenRegisterRequest>
{
    public OpenRegisterRequestValidator()
    {
        RuleFor(x => x.LocationId).GreaterThan(0);
        RuleFor(x => x.OpeningFloat).GreaterThanOrEqualTo(0m).LessThanOrEqualTo(1000000m);
    }
}

public class CloseRegisterRequestValidator : AbstractValidator<CloseRegisterRequest>
{
    public CloseRegisterRequestValidator()
    {
        RuleFor(x => x.CountedCash).GreaterThanOrEqualTo(0m).LessThanOrEqualTo(10000000m);
        RuleFor(x => x.VarianceReason).MaximumLength(500);
    }
}
