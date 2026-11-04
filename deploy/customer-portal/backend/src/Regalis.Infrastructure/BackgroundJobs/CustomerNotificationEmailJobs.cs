using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Regalis.Application.Abstractions;
using Regalis.Domain.Entities.Identity;
using Regalis.Domain.Enums;
using Regalis.Infrastructure.Persistence;
using Regalis.Infrastructure.Reporting.Analytics;

namespace Regalis.Infrastructure.BackgroundJobs;

/// <summary>
/// Emails customers a copy of their own notifications: booking confirmed, extended,
/// cancelled or released, payment received, invoice ready, pass activated, ticket replies,
/// and so on. The TRN_Notification row stays the notification; this only adds the email,
/// sent with the same mailer as scheduled reports (Microsoft Graph when configured, SMTP
/// otherwise).
///
/// Progress is a high-water mark, MST_AppSetting <see cref="MarkKey" />, not a column:
/// rows are taken in NotificationId order and the mark is moved past each row with a
/// compare-and-swap BEFORE its email goes out. A row is therefore emailed at most once,
/// even when two API processes overlap during an app-pool recycle; a send that fails is
/// logged and not retried (the in-app row is still there).
///
/// Rows younger than <see cref="SettleDelay" /> wait for the next pass. An id is taken
/// when a row is inserted but the row is only visible once its transaction commits, so a
/// fresh row may still have an uncommitted neighbour with a lower id that the mark would
/// otherwise step over.
///
/// Not emailed: broadcasts (anything but a single-user row), staff and admin accounts,
/// rows older than a day when picked up, and events that already send their own email:
/// announcements (their own Email option), money added at the desk (the receipt PDF is
/// emailed) and a new quotation (staff send it by email or WhatsApp).
/// </summary>
public class CustomerNotificationEmailJobs(RegalisDbContext db, ISettingService settings, IReportMailer mailer, IDateTimeProvider clock, ILogger<CustomerNotificationEmailJobs> logger)
{
	public const string EnabledKey = "Notifications.CustomerEmailEnabled";

	public const string MarkKey = "Notifications.CustomerEmailLastId";

	public const string PortalUrlKey = "Notifications.CustomerEmailPortalUrl";

	internal static readonly TimeSpan SettleDelay = TimeSpan.FromMinutes(2.0);

	private static readonly TimeSpan MaxAge = TimeSpan.FromDays(1.0);

	private const int BatchSize = 50;

	/// <summary>One pass. Returns how many emails were sent.</summary>
	public async Task<int> DeliverPendingAsync(CancellationToken ct)
	{
		if (!(await settings.GetBoolAsync(EnabledKey, fallback: false, ct)))
		{
			return 0;
		}
		string markText = await (from s in db.AppSettings.AsNoTracking()
			where s.SettingKey == MarkKey
			select s.SettingValue).FirstOrDefaultAsync(ct);
		if (!long.TryParse(markText, NumberStyles.None, CultureInfo.InvariantCulture, out var mark))
		{
			logger.LogWarning("Customer notification email is on but {Key} is missing or not a number; nothing sent. Run db/migrations/20261103120000_CustomerPortalChanges.sql", MarkKey);
			return 0;
		}
		DateTime nowUtc = clock.UtcNow;
		var rows = await (from n in db.Notifications.IgnoreQueryFilters().AsNoTracking()
			where n.NotificationId > mark
			orderby n.NotificationId
			select new { n.NotificationId, n.UserId, n.Audience, n.Category, n.Title, n.Message, n.ReferenceType, n.ActionLabel, n.ActionRoute, n.CreatedDate, n.IsDeleted }).Take(BatchSize).ToListAsync(ct);
		if (rows.Count == 0)
		{
			return 0;
		}
		List<long> userIds = (from r in rows
			where r.UserId.HasValue
			select r.UserId.Value).Distinct().ToList();
		var customers = (await (from u in db.Users.AsNoTracking()
			where userIds.Contains(u.UserId) && u.UserType == UserType.Customer && u.Status == UserStatus.Active && u.IsActive
			select new { u.UserId, u.Email, u.FullName }).ToListAsync(ct)).ToDictionary(u => u.UserId);
		string portal = (await settings.GetStringAsync(PortalUrlKey, "", ct)).Trim().TrimEnd('/');
		int sent = 0;
		foreach (var row in rows)
		{
			if (row.CreatedDate > nowUtc - SettleDelay)
			{
				break;
			}
			if (!(await ClaimAsync(mark, row.NotificationId, ct)))
			{
				logger.LogInformation("Customer notification email: another process moved {Key} past {Mark}; stopping this pass", MarkKey, mark);
				break;
			}
			mark = row.NotificationId;
			if (row.IsDeleted || row.Audience != NotificationAudience.SingleUser || !row.UserId.HasValue || row.CreatedDate < nowUtc - MaxAge || HasOwnEmail(row.Category, row.ReferenceType, row.Title) || !customers.TryGetValue(row.UserId.Value, out var customer) || string.IsNullOrWhiteSpace(customer.Email))
			{
				continue;
			}
			string failure = await mailer.SendAsync(new string[1] { customer.Email }, "Regalis WorkSpace — " + row.Title, EmailHtml(customer.FullName, row.Title, row.Message, row.ActionLabel, Link(portal, row.ActionRoute)), Array.Empty<(string, string, byte[])>(), ct);
			if (failure == null)
			{
				sent++;
				logger.LogInformation("Notification {NotificationId} ({Title}) emailed to customer {UserId}", row.NotificationId, row.Title, row.UserId);
			}
			else
			{
				logger.LogWarning("Notification {NotificationId} ({Title}) not emailed to customer {UserId}: {Reason}", row.NotificationId, row.Title, row.UserId, failure);
			}
		}
		return sent;
	}

	/// <summary>Moves the mark from <paramref name="from" /> to <paramref name="to" />;
	/// false when someone else moved it first.</summary>
	private async Task<bool> ClaimAsync(long from, long to, CancellationToken ct)
	{
		string fromText = from.ToString(CultureInfo.InvariantCulture);
		string toText = to.ToString(CultureInfo.InvariantCulture);
		return await db.AppSettings.Where((Regalis.Domain.Entities.Masters.AppSetting s) => s.SettingKey == MarkKey && s.SettingValue == fromText).ExecuteUpdateAsync(u => u.SetProperty((Regalis.Domain.Entities.Masters.AppSetting s) => s.SettingValue, toText), ct) == 1;
	}

	/// <summary>Events whose own code already emails the customer.</summary>
	internal static bool HasOwnEmail(NotificationCategory category, string? referenceType, string title)
	{
		if (category == NotificationCategory.Announcement)
		{
			return true;
		}
		if (referenceType == "Payment" && title == "Money added to your wallet")
		{
			return true;
		}
		return referenceType == "Quotation" && title == "A new quotation is ready";
	}

	/// <summary>The page to open, as a link, when a portal address is set. Only the first
	/// path segment is kept: rows carry routes such as /bookings/123 that the customer
	/// portal serves as /bookings.</summary>
	internal static string? Link(string portal, string? route)
	{
		if (portal.Length == 0 || string.IsNullOrWhiteSpace(route) || !route.StartsWith('/'))
		{
			return null;
		}
		string first = route.Split('/', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? "";
		return portal + "/" + first;
	}

	internal static string EmailHtml(string name, string title, string message, string? actionLabel, string? link)
	{
		string button = (link == null) ? "" : $"\r\n  <p style=\"margin:20px 0 0;\"><a href=\"{WebUtility.HtmlEncode(link)}\" style=\"display:inline-block;background:#C9A227;color:#0B0B0D;text-decoration:none;font-weight:bold;padding:10px 18px;border-radius:8px;\">{WebUtility.HtmlEncode(string.IsNullOrWhiteSpace(actionLabel) ? "Open Regalis WorkSpace" : actionLabel)}</a></p>";
		return $"<div style=\"font-family:Arial,sans-serif;max-width:560px;margin:0 auto;padding:20px;color:#241F19;\">\r\n  <p style=\"margin:0 0 12px;\">Dear {WebUtility.HtmlEncode(name)},</p>\r\n  <p style=\"font-weight:bold;font-size:16px;margin:0 0 8px;\">{WebUtility.HtmlEncode(title)}</p>\r\n  <p style=\"line-height:1.5;margin:0;\">{WebUtility.HtmlEncode(message)}</p>{button}\r\n  <p style=\"color:#8C8272;font-size:12px;margin-top:24px;\">Sent to the email address on your Regalis WorkSpace account.\r\n    The same message is under the bell icon when you sign in.</p>\r\n</div>";
	}
}
