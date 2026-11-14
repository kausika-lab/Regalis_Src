using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Regalis.Application.Abstractions;
using Regalis.Domain.Entities.Finance;
using Regalis.Domain.Entities.Operations;
using Regalis.Domain.Enums;

namespace Regalis.Infrastructure.Persistence.Interceptors;

/// <summary>
/// A pay-later pass (day pass or membership, Virtual Office included) is bought in
/// <see cref="CustomerPassStatus.AwaitingPayment"/> and cannot be used until it is paid.
/// Every path that records money against it (desk receipt, desk void, online invoice
/// payment, finance receipt or reversal) moves <c>AmountPaid</c>. This interceptor watches
/// that one column, so the status follows the money whichever path saved it:
/// paid in full: AwaitingPayment to Active, and the customer is told;
/// a receipt voided so it is no longer paid in full: Active to AwaitingPayment.
/// Used, expired and cancelled passes are never touched.
/// </summary>
public sealed class PassActivationInterceptor(IDateTimeProvider clock) : SaveChangesInterceptor
{
	public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
	{
		Apply(eventData.Context);
		return base.SavingChanges(eventData, result);
	}

	public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
	{
		Apply(eventData.Context);
		return base.SavingChangesAsync(eventData, result, cancellationToken);
	}

	private void Apply(DbContext? context)
	{
		if (context == null)
		{
			return;
		}
		context.ChangeTracker.DetectChanges();
		var changed = context.ChangeTracker.Entries<CustomerPass>()
			.Where(e => e.State == EntityState.Modified && e.Entity.IsPayLater && e.Property(p => p.AmountPaid).IsModified)
			.ToList();
		foreach (var entry in changed)
		{
			CustomerPass pass = entry.Entity;
			if (pass.Status == CustomerPassStatus.AwaitingPayment && pass.AmountPaid >= pass.TotalAmount)
			{
				pass.Status = CustomerPassStatus.Active;
				context.Add(new Notification
				{
					UserId = pass.CustomerId,
					Audience = NotificationAudience.SingleUser,
					Title = "Pass activated",
					Message = $"Payment received. {pass.PassNumber} is now active, valid {pass.ValidFrom:dd MMM} to {pass.ValidTo:dd MMM yyyy}.",
					Category = NotificationCategory.Pass,
					Priority = NotificationPriority.Normal,
					ReferenceType = "CustomerPass",
					ReferenceId = pass.CustomerPassId,
					ActionLabel = "View passes",
					ActionRoute = "/passes/my",
					SentUtc = clock.UtcNow
				});
			}
			else if (pass.Status == CustomerPassStatus.Active && pass.AmountPaid < pass.TotalAmount)
			{
				pass.Status = CustomerPassStatus.AwaitingPayment;
			}
		}
	}
}
