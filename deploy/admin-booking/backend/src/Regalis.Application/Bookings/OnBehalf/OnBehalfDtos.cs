using System;
using System.Collections.Generic;
using FluentValidation;

namespace Regalis.Application.Bookings.OnBehalf;

/// <summary>admin-booking kit: a Virtual Office bought by staff for a customer. There is no
/// payment method: it is always settled the pay-later way (nothing paid, status AwaitingPayment
/// until the money is recorded on the Payments screen).</summary>
public record OnBehalfPassRequest(int PassPlanId, DateOnly ValidFrom, IReadOnlyList<int>? OfferIds = null, string? CouponCode = null);

public class OnBehalfPassRequestValidator : AbstractValidator<OnBehalfPassRequest>
{
	public OnBehalfPassRequestValidator()
	{
		RuleFor(x => x.PassPlanId).GreaterThan(0);
		RuleFor(x => x.CouponCode).MaximumLength(30);
	}
}
