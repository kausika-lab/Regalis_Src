using System.Collections.Generic;
using Regalis.Application.Abstractions;
using Regalis.Domain.Enums;
using Regalis.Shared.Constants;

namespace Regalis.Infrastructure.Services.OnBehalf;

/// <summary>
/// admin-booking kit (27 Nov 2026): the identity the customer booking engine sees when a staff
/// user books for a customer. UserId, UserType, email and name are the customer's, so every
/// "the caller's own rows" rule (holds, offers, coupons, passes) works on that customer. Roles
/// are the customer role. Permissions, location, IP, user agent and correlation id stay the
/// staff user's. Created only by <see cref="OnBehalfBookingService"/>; never registered in DI.
/// </summary>
public sealed class OnBehalfCurrentUser(ICurrentUser staff, long customerId, string? customerEmail, string? customerName) : IOnBehalfOfCustomer
{
	private static readonly IReadOnlySet<string> CustomerRole = new HashSet<string> { RoleCodes.Customer };

	public long StaffUserId => staff.UserId;

	public bool IsAuthenticated => true;

	public long UserId => customerId;

	public string? Email => customerEmail;

	public string? FullName => customerName;

	public UserType? UserType => Regalis.Domain.Enums.UserType.Customer;

	public int? LocationId => staff.LocationId;

	public IReadOnlySet<string> Roles => CustomerRole;

	public IReadOnlySet<string> Permissions => staff.Permissions;

	public string? IpAddress => staff.IpAddress;

	public string? UserAgent => staff.UserAgent;

	public string? CorrelationId => staff.CorrelationId;

	public bool HasPermission(string permissionCode) => staff.HasPermission(permissionCode);
}
