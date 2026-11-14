using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Regalis.Application.Abstractions;
using Regalis.Application.Account;
using Regalis.Application.Common.Exceptions;
using Regalis.Domain.Entities.Finance;
using Regalis.Domain.Enums;
using Regalis.Infrastructure.Persistence;
using Regalis.Shared;
using Regalis.Shared.Api;

namespace Regalis.Infrastructure.Services;

/// <summary>
/// The signed-in customer's own payments, newest first, straight from TRN_Payment
/// (customer-10 kit, item 10). Every query is filtered on CustomerId = the caller, so a
/// customer can never see another customer's rows (a foreign id is a 404).
/// </summary>
public class MyPaymentsService(RegalisDbContext db, ICurrentUser currentUser, IDateTimeProvider clock) : IMyPaymentsService
{
	/// <summary>A Pending online checkout older than this is shown as "Not completed".</summary>
	private static readonly TimeSpan AbandonedAfter = TimeSpan.FromMinutes(30);

	private sealed record Row(Payment P, string? BookingNumber, string? WorkspaceName, DateTime? StartUtc, string? PassNumber, string? PlanName, byte? PassType, string? InvoiceNumber);

	public async Task<PagedResult<MyPaymentDto>> ListAsync(MyPaymentsQuery query, CancellationToken ct = default)
	{
		long customerId = RequireCustomer();
		int page = Math.Max(1, query.Page);
		int size = Math.Clamp(query.PageSize, 1, 100);
		IQueryable<Payment> q = db.Payments.AsNoTracking().Where(p => p.CustomerId == customerId);
		DateTime abandonCut = clock.UtcNow - AbandonedAfter;
		switch ((query.Status ?? "").Trim().ToLowerInvariant())
		{
			case "success": q = q.Where(p => p.Status == PaymentStatus.Success); break;
			case "pending": q = q.Where(p => p.Status == PaymentStatus.Pending && p.CreatedDate >= abandonCut); break;
			case "failed": q = q.Where(p => p.Status == PaymentStatus.Failed || p.Status == PaymentStatus.Cancelled || (p.Status == PaymentStatus.Pending && p.CreatedDate < abandonCut)); break;
			case "refunded": q = q.Where(p => p.Status == PaymentStatus.Refunded || p.RefundedAmount > 0m); break;
		}
		int total = await q.CountAsync(ct);
		List<Row> rows = await Project(q.OrderByDescending(p => p.CreatedDate).ThenByDescending(p => p.PaymentId).Skip((page - 1) * size).Take(size)).ToListAsync(ct);
		return PagedResult<MyPaymentDto>.Create(rows.Select(ToDto).ToList(), page, size, total);
	}

	public async Task<MyPaymentDto> GetAsync(long paymentId, CancellationToken ct = default)
	{
		long customerId = RequireCustomer();
		Row row = await Project(db.Payments.AsNoTracking().Where(p => p.PaymentId == paymentId && p.CustomerId == customerId)).FirstOrDefaultAsync(ct)
			?? throw new NotFoundException("Payment", paymentId);
		return ToDto(row);
	}

	private IQueryable<Row> Project(IQueryable<Payment> q)
	{
		return from p in q
			from b in db.Bookings.Where(b => (long?)b.BookingId == p.BookingId).DefaultIfEmpty()
			from cp in db.CustomerPasses.Where(x => (long?)x.CustomerPassId == p.CustomerPassId).DefaultIfEmpty()
			from i in db.Invoices.Where(x => (long?)x.InvoiceId == p.InvoiceId).DefaultIfEmpty()
			select new Row(p,
				b != null ? b.BookingNumber : null,
				b != null ? b.Workspace.WorkspaceName : null,
				b != null ? (DateTime?)b.StartUtc : null,
				cp != null ? cp.PassNumber : null,
				cp != null ? cp.PassPlan.PlanName : null,
				cp != null ? (byte?)cp.PassType : null,
				i != null ? i.InvoiceNumber : null);
	}

	private MyPaymentDto ToDto(Row r)
	{
		Payment p = r.P;
		string type;
		string? reference;
		string description;
		if (p.BookingId != null)
		{
			type = "Booking";
			reference = r.BookingNumber;
			description = r.WorkspaceName != null && r.StartUtc != null
				? $"{r.WorkspaceName}, {IstClock.ToIst(r.StartUtc.Value):dd MMM yyyy, h:mm tt}"
				: "Workspace booking";
		}
		else if (p.CustomerPassId != null)
		{
			type = r.PassType == (byte)Regalis.Domain.Enums.PassType.DayPass ? "Day pass" : "Membership";
			reference = r.PassNumber;
			description = r.PlanName ?? type;
		}
		else if (p.InvoiceId != null)
		{
			type = "Invoice";
			reference = r.InvoiceNumber;
			description = "Invoice payment";
		}
		else
		{
			type = "Wallet top-up";
			reference = p.PaymentNumber;
			description = "Money added to your Regalis wallet";
		}
		bool abandoned = p.Status == PaymentStatus.Pending && p.CreatedDate < clock.UtcNow - AbandonedAfter;
		string display = p.Status switch
		{
			PaymentStatus.Pending => abandoned ? "Not completed" : "Pending",
			PaymentStatus.Success => (p.RefundedAmount ?? 0m) > 0m ? "Partly refunded" : "Successful",
			PaymentStatus.Failed => "Failed",
			PaymentStatus.Refunded => "Refunded",
			PaymentStatus.Cancelled => "Cancelled",
			_ => p.Status.ToString()
		};
		string method = p.PaymentMethod switch
		{
			PaymentMethod.Wallet => "Wallet",
			PaymentMethod.Upi => p.Gateway != null ? "Online" : "UPI",
			PaymentMethod.CreditCard => "Credit card",
			PaymentMethod.DebitCard => "Debit card",
			PaymentMethod.NetBanking => "Net banking",
			PaymentMethod.Cash => "Cash",
			_ => "Other"
		};
		return new MyPaymentDto(p.PaymentId, p.PaymentNumber, p.GatewayPaymentId ?? p.PaymentNumber, p.GatewayOrderId, p.GatewayPaymentId,
			type, reference, p.BookingId, p.CustomerPassId, p.InvoiceId, r.InvoiceNumber ?? (p.InvoiceId != null ? null : null), description,
			p.Amount, (byte)p.PaymentMethod, method, p.Gateway, (byte)p.Status, display, p.CreatedDate, p.PaidUtc,
			p.FailureReason, p.RefundedAmount, p.RefundReference, p.Status == PaymentStatus.Success);
	}

	private long RequireCustomer()
	{
		if (currentUser.UserType != UserType.Customer)
		{
			throw new ForbiddenException("Payment history is for customers. Staff use Finance > Payments.");
		}
		return currentUser.UserId;
	}
}
