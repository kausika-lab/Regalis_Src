using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Regalis.Application.Abstractions;
using Regalis.Application.Auth;
using Regalis.Application.Common.Exceptions;
using Regalis.Domain.Entities.Identity;
using Regalis.Domain.Enums;
using Regalis.Infrastructure.Persistence;

namespace Regalis.Infrastructure.Services;

/// <summary>Changes the caller's own login email. Customers keep a read-only email
/// (invoices, receipts and Google sign-in hang off it), so this is staff/admin only.
/// The old → new value lands in the audit trail through AuditSaveChangesInterceptor.</summary>
public class ProfileEmailService(RegalisDbContext db, ICurrentUser currentUser, IPasswordHasher passwordHasher, IProfileService profileService, IDateTimeProvider clock) : IProfileEmailService
{
	public async Task<ProfileDto> ChangeEmailAsync(ChangeEmailRequest request, CancellationToken ct = default(CancellationToken))
	{
		User user = await db.Users.FirstAsync((User u) => u.UserId == currentUser.UserId, ct);
		if (user.UserType == UserType.Customer)
		{
			throw new ForbiddenException("Customers cannot change their sign-in email. Contact support.");
		}
		if (string.IsNullOrEmpty(user.PasswordHash) || !passwordHasher.Verify(request.CurrentPassword, user.PasswordHash))
		{
			throw new BusinessRuleException("Your current password is incorrect.");
		}
		string email = request.NewEmail.Trim();
		if (email == user.Email)
		{
			return await profileService.GetAsync(ct);
		}
		if (await db.Users.AnyAsync((User u) => u.Email == email && u.UserId != user.UserId, ct))
		{
			throw new ConflictException("That email is already used by another account.");
		}
		bool sameAddress = string.Equals(email, user.Email, StringComparison.OrdinalIgnoreCase);
		user.Email = email;
		if (!sameAddress)
		{
			// A new address is unproven, and a Google account linked to the old one must
			// not keep signing in to this user.
			user.IsEmailVerified = false;
			user.GoogleId = null;
		}
		await AdminAlerts.AccountAsync(db, user, "Email changed", "changed its sign-in email", clock.UtcNow, ct);
		await db.SaveChangesAsync(ct);
		return await profileService.GetAsync(ct);
	}
}
