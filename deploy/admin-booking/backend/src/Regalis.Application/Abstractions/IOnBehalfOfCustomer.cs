namespace Regalis.Application.Abstractions;

/// <summary>
/// admin-booking kit (27 Nov 2026): a caller identity that is a staff user acting for one
/// customer in the admin "New booking" screen. <see cref="ICurrentUser.UserId"/> and
/// <see cref="ICurrentUser.UserType"/> are the customer's, so the customer booking engine
/// (holds, monthly terms, offers, coupons, Virtual Office) runs exactly as it does for the
/// customer. <see cref="StaffUserId"/> is the staff user who is really signed in.
///
/// Only <c>OnBehalfBookingService</c> creates one, after checking the staff caller and the
/// customer. The DI-registered <see cref="ICurrentUser"/> is never replaced, so the audit
/// interceptors still record the staff user.
/// </summary>
public interface IOnBehalfOfCustomer : ICurrentUser
{
	/// <summary>The staff user who is signed in and acting for the customer.</summary>
	long StaffUserId { get; }
}
