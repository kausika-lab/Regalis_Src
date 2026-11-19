using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Regalis.Application.Abstractions;
using Regalis.Application.Common.Exceptions;
using Regalis.Application.Operations;
using Regalis.Application.Wifi;
using Regalis.Domain.Entities.Operations;
using Regalis.Domain.Entities.Wifi;
using Regalis.Domain.Enums;
using Regalis.Infrastructure.Persistence;

namespace Regalis.Infrastructure.Services.Wifi;

/// <summary>My Wi-Fi (spec 5.4): the signed-in customer's own access only. The only place a password is returned.</summary>
public class WifiPortalService(RegalisDbContext db, WifiEngine engine, ICurrentUser currentUser, ITicketService tickets) : IWifiPortalService
{
	private DateTime Now => engine.Now;

	private long Me
	{
		get
		{
			if (currentUser.UserType != UserType.Customer)
			{
				throw new ForbiddenException("My Wi-Fi is for customers.");
			}
			return currentUser.UserId;
		}
	}

	private IQueryable<WifiAccess> Mine(long me) => db.Set<WifiAccess>().Where(a => a.CustomerId == me && (a.Kind == WifiAccessKind.Booking || a.Kind == WifiAccessKind.Pass));

	public async Task<WifiPortalDto> GetAsync(CancellationToken ct)
	{
		long me = Me;
		WifiConfig cfg = await engine.ConfigAsync(ct);
		DateTime now = Now;
		List<WifiAccess> all = await Mine(me).OrderByDescending(a => a.ValidUntilUtc).ToListAsync(ct);
		List<WifiAccess> current = all.Where(a => a.Status is WifiAccessStatus.Issued or WifiAccessStatus.Active or WifiAccessStatus.Paused)
			.OrderBy(a => a.Status == WifiAccessStatus.Active ? 0 : 1).ThenBy(a => a.ValidFromUtc).ToList();
		List<long> ids = current.Select(a => a.AccessId).ToList();
		List<WifiDevice> devices = await db.Set<WifiDevice>().AsNoTracking().Where(d => ids.Contains(d.AccessId) && d.RemovedUtc == null).OrderBy(d => d.FirstSeenUtc).ToListAsync(ct);
		List<WifiSession> open = await db.Set<WifiSession>().AsNoTracking().Where(s => s.AccessId != null && ids.Contains(s.AccessId.Value) && s.EndedUtc == null && s.Result == WifiSessionResult.Connected).ToListAsync(ct);
		List<WifiPortalAccessDto> cards = new List<WifiPortalAccessDto>();
		bool changed = false;
		foreach (WifiAccess a in current)
		{
			bool ready = a.ControllerState == WifiControllerState.Live && a.Status != WifiAccessStatus.Paused;
			string? notice = a.Status == WifiAccessStatus.Paused
				? (a.PauseReason ?? "Paused.") + " Pay it and your Wi-Fi comes back with the same password."
				: a.ControllerState == WifiControllerState.Failed ? "Your Wi-Fi is being set up. We will tell you as soon as it is ready."
				: !ready && a.ValidFromUtc > now ? $"Your Wi-Fi details will be ready on {WifiEngine.IstDateTime(a.ValidFromUtc.AddHours(-1))}, an hour before your booking starts."
				: !ready ? "Your Wi-Fi is being set up. This page will show the password as soon as it is ready." : null;
			string? pw = ready ? engine.Password(a, cfg) : null;
			(string key, string label) = WifiEngine.DisplayStatus(a, open.Any(s => s.AccessId == a.AccessId), now);
			List<WifiPortalDeviceDto> devs = devices.Where(d => d.AccessId == a.AccessId).Select(d =>
			{
				WifiSession? on = open.FirstOrDefault(s => s.AccessId == a.AccessId && s.MacAddress == d.MacAddress);
				return new WifiPortalDeviceDto(d.DeviceId, d.DeviceName, d.MacAddress, d.FirstSeenUtc, on != null, on?.StartedUtc, d.LastSeenUtc);
			}).ToList();
			bool renews = await db.Set<WifiAccessSource>().AnyAsync(s => s.AccessId == a.AccessId && s.SourceType == 1, ct) || a.Profile == "member";
			cards.Add(new WifiPortalAccessDto(a.AccessId, a.AccessNo, await engine.TitleAsync(a, ct), key, label, a.Ssid, a.Username, pw,
				pw == null ? null : Regalis.Infrastructure.Services.Wifi.WifiSecrets.QrText(a.Ssid, pw, cfg.Mode == "portal"),
				a.ValidFromUtc, a.ValidUntilUtc, renews, a.DeviceLimit, devs, ready, notice, cfg.Mode));
			if (ready)
			{
				if (a.SeenInAppUtc == null)
				{
					a.SeenInAppUtc = now;
					engine.Log(a.AccessId, "seen", "Opened by the customer in My Wi-Fi.", me);
					changed = true;
				}
				if (a.Status == WifiAccessStatus.Issued && a.ValidFromUtc <= now.AddHours(1))
				{
					a.Status = WifiAccessStatus.Active;
					a.DeliveredUtc ??= now;
					changed = true;
				}
			}
		}
		if (changed)
		{
			await db.SaveChangesAsync(ct);
		}
		List<WifiPortalHistoryDto> earlier = new List<WifiPortalHistoryDto>();
		foreach (WifiAccess a in all.Where(a => a.Status is WifiAccessStatus.Expired or WifiAccessStatus.Revoked).Take(20))
		{
			earlier.Add(new WifiPortalHistoryDto(a.AccessId, await engine.TitleAsync(a, ct), a.Ssid, a.ValidFromUtc, a.ValidUntilUtc, a.Status == WifiAccessStatus.Revoked ? "Ended" : "Expired"));
		}
		return new WifiPortalDto(cards, earlier, cfg.DeliverWhatsApp, cfg.Profile("member").DeviceLimit, cfg.Profile("daypass").DeviceLimit,
			"Your connections (which device, when, for how long and how much data) are recorded against your account. Regalis never records which websites you visit.");
	}

	private async Task<WifiAccess> CurrentAsync(long me, CancellationToken ct) =>
		await Mine(me).Where(a => a.Status == WifiAccessStatus.Active || a.Status == WifiAccessStatus.Issued).OrderBy(a => a.Status == WifiAccessStatus.Active ? 0 : 1).ThenBy(a => a.ValidFromUtc).FirstOrDefaultAsync(ct)
			?? throw new BusinessRuleException("You have no Wi-Fi access right now. It is created when a booking or pass is paid.");

	public async Task<WifiResendResultDto> ResendAsync(WifiResendRequest r, CancellationToken ct)
	{
		long me = Me;
		WifiAccess a = await CurrentAsync(me, ct);
		if (a.ControllerState != WifiControllerState.Live)
		{
			throw new BusinessRuleException("Your Wi-Fi is still being set up; the details are sent as soon as it is ready.");
		}
		DateTime hour = Now.AddHours(-1);
		int recent = await db.Set<WifiEvent>().CountAsync(e => e.AccessId == a.AccessId && e.Event == "resent" && e.ActorUserId == me && e.CreatedUtc > hour, ct);
		if (recent >= 5)
		{
			throw new BusinessRuleException("You have asked for the details several times in the last hour. They are always on this page.");
		}
		if (r.Channel == "whatsapp")
		{
			string url = await engine.WhatsAppUrlAsync(a, ct) ?? throw new BusinessRuleException("Add your mobile number to your profile first.");
			engine.Log(a.AccessId, "resent", "Customer opened Send on WhatsApp.", me);
			a.WhatsAppUtc = Now;
			await db.SaveChangesAsync(ct);
			return new WifiResendResultDto("whatsapp", true, "WhatsApp opened with your details.", url);
		}
		string? error = await engine.SendEmailAsync(a, "resend", ct);
		engine.Log(a.AccessId, "resent", error == null ? $"Customer asked for the details again: emailed to {a.HolderEmail}." : $"Customer asked for the details again; the email failed: {error}", me);
		await db.SaveChangesAsync(ct);
		return new WifiResendResultDto("email", error == null, error == null ? $"Sent to {a.HolderEmail}." : "We could not send the email just now. Please try again later, or use WhatsApp.", null);
	}

	public async Task RemoveDeviceAsync(long deviceId, CancellationToken ct)
	{
		long me = Me;
		WifiDevice d = await (from dev in db.Set<WifiDevice>()
			join a in Mine(me) on dev.AccessId equals a.AccessId
			where dev.DeviceId == deviceId && dev.RemovedUtc == null
			select dev).FirstOrDefaultAsync(ct) ?? throw new NotFoundException("Device", deviceId);
		await WifiService.RemoveDeviceCoreAsync(engine, db, d, "customer", me, ct);
		await db.SaveChangesAsync(ct);
	}

	public async Task<string> HelpAsync(WifiHelpRequest r, CancellationToken ct)
	{
		long me = Me;
		WifiAccess? a = await Mine(me).OrderByDescending(x => x.Status == WifiAccessStatus.Active).ThenByDescending(x => x.ValidUntilUtc).FirstOrDefaultAsync(ct);
		var cat = await (from c in db.TicketCategories.AsNoTracking()
			join t in db.TicketIssueTypes.AsNoTracking() on c.TicketCategoryId equals t.TicketCategoryId
			where c.IsActive && c.CategoryName.Contains("Technical")
			orderby c.TicketCategoryId descending
			select new { c.TicketCategoryId, t.TicketIssueTypeId }).FirstOrDefaultAsync(ct)
			?? await (from c in db.TicketCategories.AsNoTracking()
				join t in db.TicketIssueTypes.AsNoTracking() on c.TicketCategoryId equals t.TicketCategoryId
				where c.IsActive
				orderby c.TicketCategoryId
				select new { c.TicketCategoryId, t.TicketIssueTypeId }).FirstOrDefaultAsync(ct)
			?? throw new BusinessRuleException("Support tickets are not set up. Please ask at the front desk.");
		string what = a == null ? "no Wi-Fi access on record" : $"{a.AccessNo}, network {a.Ssid}, username {a.Username}, status {a.Status}, valid until {WifiEngine.IstDateTime(a.ValidUntilUtc)}";
		string description = (r.PasswordShared ? "I think someone else has my Wi-Fi password. Please issue a new one.\n\n" : "") +
			(string.IsNullOrWhiteSpace(r.Message) ? "I am having trouble connecting to the Wi-Fi." : r.Message.Trim()) +
			$"\n\n[Wi-Fi: {what}]";
		Regalis.Application.Operations.TicketDetailDto ticket = await tickets.CreateAsync(new CreateTicketRequest(cat.TicketCategoryId, cat.TicketIssueTypeId,
			r.PasswordShared ? "Wi-Fi: my password may have been shared" : "Wi-Fi: trouble connecting", description.Length > 3900 ? description.Substring(0, 3900) : description,
			(byte)(r.PasswordShared ? TicketPriority.High : TicketPriority.Medium)), ct);
		if (a != null)
		{
			engine.Log(a.AccessId, "help", $"Customer raised support ticket {ticket.TicketNumber}" + (r.PasswordShared ? " (password may be shared)." : "."), me);
			await db.SaveChangesAsync(ct);
		}
		return ticket.TicketNumber;
	}
}
