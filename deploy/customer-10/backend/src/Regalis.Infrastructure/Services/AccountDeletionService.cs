using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Regalis.Application.Abstractions;
using Regalis.Application.Account;
using Regalis.Application.Common.Exceptions;
using Regalis.Domain.Entities.Access;
using Regalis.Domain.Entities.Booking;
using Regalis.Domain.Entities.Finance;
using Regalis.Domain.Entities.Identity;
using Regalis.Domain.Entities.Waitlist;
using Regalis.Domain.Enums;
using Regalis.Infrastructure.Persistence;
using Regalis.Shared;

namespace Regalis.Infrastructure.Services;

/// <summary>
/// Customer deletes their own account (customer-10 kit, item 9). Product decisions:
/// identity by password (email code for Google-only accounts); refused while anything is
/// open or owed; the account is closed and its personal details anonymised. Bookings,
/// passes, invoices (which keep their own billing snapshot) and payments stay for the
/// records. The email and mobile are freed, so the person can register again.
/// The user row is NOT soft-deleted: staff screens join bookings and payments to it.
/// </summary>
public class AccountDeletionService(RegalisDbContext db, IPasswordHasher passwordHasher, IOtpSender otpSender, ISettingService settings, ICurrentUser currentUser, IDateTimeProvider clock, ILogger<AccountDeletionService> logger) : IAccountDeletionService
{
	public async Task<AccountDeletionCheckDto> CheckAsync(CancellationToken ct = default)
	{
		User user = await RequireCustomerAsync(ct);
		List<DeletionBlocker> blockers = await BlockersAsync(user.UserId, ct);
		return new AccountDeletionCheckDto(blockers.Count == 0, user.PasswordHash != null ? "password" : "otp", Mask(user.Email), blockers);
	}

	public async Task SendCodeAsync(CancellationToken ct = default)
	{
		User user = await RequireCustomerAsync(ct);
		DateTime nowUtc = clock.UtcNow;
		DateTime? last = await db.Otps.Where(o => o.Destination == user.Email && o.Purpose == OtpPurpose.AccountDeletion)
			.OrderByDescending(o => o.CreatedUtc).Select(o => (DateTime?)o.CreatedUtc).FirstOrDefaultAsync(ct);
		if (last.HasValue && (nowUtc - last.Value).TotalSeconds < 60.0)
		{
			throw new BusinessRuleException("Please wait a minute before requesting another code.", "RATE_LIMIT_EXCEEDED");
		}
		foreach (Otp old in await db.Otps.Where(o => o.Destination == user.Email && o.Purpose == OtpPurpose.AccountDeletion && o.ConsumedUtc == null).ToListAsync(ct))
		{
			old.ConsumedUtc = nowUtc;
		}
		int ttl = await settings.GetIntAsync("Security.OtpTtlMinutes", 10, ct);
		int maxAttempts = await settings.GetIntAsync("Security.OtpMaxAttempts", 5, ct);
		string code = RandomNumberGenerator.GetInt32(0, 1000000).ToString("D6");
		db.Otps.Add(new Otp
		{
			UserId = user.UserId,
			Destination = user.Email,
			Channel = OtpChannel.Email,
			Purpose = OtpPurpose.AccountDeletion,
			CodeHash = HashOtp(code, user.Email),
			ExpiresUtc = nowUtc.AddMinutes(ttl),
			MaxAttempts = maxAttempts,
			CreatedUtc = nowUtc
		});
		await otpSender.SendAsync(user.Email, OtpChannel.Email, OtpPurpose.AccountDeletion, code, ct);
		await db.SaveChangesAsync(ct);
	}

	public async Task DeleteAsync(DeleteAccountRequest request, CancellationToken ct = default)
	{
		User user = await RequireCustomerAsync(ct);
		DateTime nowUtc = clock.UtcNow;
		if (user.PasswordHash != null)
		{
			if (string.IsNullOrEmpty(request.Password) || !passwordHasher.Verify(request.Password, user.PasswordHash))
			{
				throw new BusinessRuleException("The password is not correct.", "INVALID_PASSWORD");
			}
		}
		else
		{
			string code = (request.Code ?? "").Trim();
			Otp otp = await db.Otps.Where(o => o.Destination == user.Email && o.Purpose == OtpPurpose.AccountDeletion && o.ConsumedUtc == null)
				.OrderByDescending(o => o.CreatedUtc).FirstOrDefaultAsync(ct);
			if (otp == null || otp.ExpiresUtc <= nowUtc || otp.AttemptCount >= otp.MaxAttempts)
			{
				throw new BusinessRuleException("The code has expired. Send a new one.", "OTP_EXPIRED");
			}
			if (!CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(HashOtp(code, user.Email)), Encoding.UTF8.GetBytes(otp.CodeHash)))
			{
				otp.AttemptCount++;
				await db.SaveChangesAsync(ct);
				throw new BusinessRuleException("The code is not correct.", "OTP_INVALID");
			}
			otp.ConsumedUtc = nowUtc;
		}

		List<DeletionBlocker> blockers = await BlockersAsync(user.UserId, ct);
		if (blockers.Count > 0)
		{
			throw new BusinessRuleException("Your account cannot be deleted yet: " + string.Join(" ", blockers.Select(b => b.Message)), "ACCOUNT_HAS_OPEN_ITEMS");
		}

		long userId = user.UserId;
		string? photoPath = user.PhotoPath;
		user.FullName = "Deleted customer";
		user.Email = $"deleted-{userId}@deleted.invalid";
		user.MobileNumber = "";
		user.PasswordHash = null;
		user.GoogleId = null;
		user.CompanyName = null;
		user.Address = null;
		user.AddressLine1 = null;
		user.AddressLine2 = null;
		user.City = null;
		user.State = null;
		user.Country = null;
		user.PostalCode = null;
		user.DateOfBirth = null;
		user.Gender = null;
		user.Gstin = null;
		user.JobTitle = null;
		user.PhotoPath = null;
		user.IsEmailVerified = false;
		user.IsMobileVerified = false;
		user.PayLaterAllowed = false;
		user.Status = UserStatus.Inactive;

		foreach (RefreshToken t in await db.RefreshTokens.Where(t => t.UserId == userId && t.RevokedUtc == null).ToListAsync(ct))
		{
			t.RevokedUtc = nowUtc;
			t.RevokedReason = "AccountDeleted";
		}
		foreach (AccessToken t in await db.AccessTokens.Where(t => t.UserId == userId && t.Status == AccessTokenStatus.Active).ToListAsync(ct))
		{
			t.Status = AccessTokenStatus.Revoked;
			t.RevokedReason = "AccountDeleted";
		}
		foreach (SeatWaitlistEntry w in await db.Set<SeatWaitlistEntry>().Where(w => w.CustomerId == userId && w.Status == SeatWaitlistStatus.Waiting).ToListAsync(ct))
		{
			w.Status = SeatWaitlistStatus.RemovedByCustomer;
			w.ClosedUtc = nowUtc;
			w.ClosedByUserId = userId;
		}
		db.AuditLogs.Add(new AuditLog
		{
			UserId = userId,
			UserName = "Deleted customer",
			Action = "ACCOUNT_DELETED",
			ModuleName = "Profile",
			EntityName = "User",
			EntityId = userId.ToString(),
			IpAddress = currentUser.IpAddress,
			UserAgent = currentUser.UserAgent,
			CorrelationId = currentUser.CorrelationId,
			TimestampUtc = nowUtc
		});
		await db.SaveChangesAsync(ct);

		if (!string.IsNullOrEmpty(photoPath))
		{
			try
			{
				if (File.Exists(photoPath))
				{
					File.Delete(photoPath);
				}
			}
			catch (Exception ex)
			{
				logger.LogWarning(ex, "Account {UserId} deleted; its photo file could not be removed", userId);
			}
		}
		logger.LogInformation("Customer {UserId} deleted their account (anonymised; records kept)", userId);
	}

	private async Task<User> RequireCustomerAsync(CancellationToken ct)
	{
		if (currentUser.UserType != UserType.Customer)
		{
			throw new ForbiddenException("Only customers can delete their account here.");
		}
		long id = currentUser.UserId;
		return await db.Users.FirstOrDefaultAsync(u => u.UserId == id && u.Status == UserStatus.Active, ct)
			?? throw new NotFoundException("User", id);
	}

	private async Task<List<DeletionBlocker>> BlockersAsync(long userId, CancellationToken ct)
	{
		DateTime nowUtc = clock.UtcNow;
		DateOnly todayIst = IstClock.IstDateOf(nowUtc);
		int holdMinutes = await settings.GetIntAsync("Booking.PendingHoldMinutes", 10, ct);
		DateTime holdCutoff = nowUtc.AddMinutes(-holdMinutes);
		var list = new List<DeletionBlocker>();

		decimal owedInvoices = await db.Invoices.AsNoTracking()
			.Where(i => i.CustomerId == userId && (i.Status == InvoiceStatus.Pending || i.Status == InvoiceStatus.PartiallyPaid || i.Status == InvoiceStatus.Sent) && i.TotalAmount > i.PaidAmount)
			.SumAsync(i => (decimal?)(i.TotalAmount - i.PaidAmount), ct) ?? 0m;
		decimal owedBookings = await db.Bookings.AsNoTracking()
			.Where(b => b.CustomerId == userId && b.IsPayLater && b.AmountPaid < b.TotalAmount && b.Status != BookingStatus.Cancelled && b.Status != BookingStatus.Expired)
			.SumAsync(b => (decimal?)(b.TotalAmount - b.AmountPaid), ct) ?? 0m;
		decimal owed = Math.Max(owedInvoices, owedBookings);
		if (owed > 0m)
		{
			list.Add(new DeletionBlocker("DUES", $"₹{owed:N2} is still to be paid. Please clear your dues first.", "Invoices", "/invoices"));
		}
		int bookings = await db.Bookings.AsNoTracking().CountAsync(b => b.CustomerId == userId && b.EndUtc > nowUtc
			&& (b.Status == BookingStatus.Confirmed || b.Status == BookingStatus.CheckedIn || (b.Status == BookingStatus.Pending && b.CreatedDate >= holdCutoff)), ct);
		if (bookings > 0)
		{
			list.Add(new DeletionBlocker("BOOKINGS", $"You have {bookings} upcoming or running booking{(bookings == 1 ? "" : "s")}. Cancel {(bookings == 1 ? "it" : "them")} or wait until {(bookings == 1 ? "it ends" : "they end")}.", "My Bookings", "/bookings"));
		}
		int passes = await db.CustomerPasses.AsNoTracking().CountAsync(p => p.CustomerId == userId && p.ValidTo >= todayIst
			&& (p.Status == CustomerPassStatus.Active || p.Status == CustomerPassStatus.AwaitingPayment), ct);
		if (passes > 0)
		{
			list.Add(new DeletionBlocker("PASSES", $"You have {passes} active pass{(passes == 1 ? "" : "es")} or membership{(passes == 1 ? "" : "s")}. Cancel or use {(passes == 1 ? "it" : "them")} first.", "Passes", "/passes"));
		}
		decimal balance = await db.Wallets.AsNoTracking().Where(w => w.CustomerId == userId).Select(w => (decimal?)w.Balance).FirstOrDefaultAsync(ct) ?? 0m;
		if (balance > 0m)
		{
			list.Add(new DeletionBlocker("WALLET", $"Your wallet still holds ₹{balance:N2}. Use it, or contact the front desk about it, first.", "Wallet", "/wallet"));
		}
		return list;
	}

	private static string Mask(string email)
	{
		int at = email.IndexOf('@');
		if (at <= 1)
		{
			return email;
		}
		return email[0] + new string('•', Math.Min(6, at - 1)) + email.Substring(at);
	}

	private static string HashOtp(string code, string destination)
	{
		return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(code + "|" + destination.ToLowerInvariant())));
	}
}
