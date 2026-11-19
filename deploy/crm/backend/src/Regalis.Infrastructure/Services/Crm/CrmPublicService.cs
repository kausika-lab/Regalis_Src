using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Regalis.Application.Common.Exceptions;
using Regalis.Application.Crm;
using Regalis.Domain.Entities.Crm;
using Regalis.Infrastructure.Persistence;

namespace Regalis.Infrastructure.Services.Crm;

/// <summary>
/// The only CRM endpoints reachable without a login (document sections 6 and 14.2). Hostile input:
/// every field is length-capped, query strings are never stored (they can carry reset tokens), a
/// honeypot and a timing check catch bots, a per-address limit is applied silently, and nothing
/// ever says whether a mobile number already exists.
/// </summary>
public class CrmPublicService(RegalisDbContext db, CrmEngine engine, ILogger<CrmPublicService> logger) : ICrmPublicService
{
	private static readonly ConcurrentDictionary<string, Queue<DateTime>> Hits = new ConcurrentDictionary<string, Queue<DateTime>>();

	private static DateTime _lastSweep = DateTime.MinValue;

	/// <summary>Sliding-window limit kept in memory. True = allowed.</summary>
	internal static bool Allow(string key, int max, TimeSpan window, DateTime now)
	{
		if ((now - _lastSweep).TotalMinutes > 10)
		{
			_lastSweep = now;
			foreach (var kv in Hits)
			{
				lock (kv.Value)
				{
					if (kv.Value.Count == 0 || now - kv.Value.Peek() > TimeSpan.FromHours(2))
					{
						Hits.TryRemove(kv.Key, out _);
					}
				}
			}
		}
		Queue<DateTime> q = Hits.GetOrAdd(key, _ => new Queue<DateTime>());
		lock (q)
		{
			while (q.Count > 0 && now - q.Peek() > window)
			{
				q.Dequeue();
			}
			if (q.Count >= max)
			{
				return false;
			}
			q.Enqueue(now);
			return true;
		}
	}

	public async Task<CrmPublicConfigDto> GetConfigAsync(CancellationToken ct = default)
	{
		CrmSettingsSnapshot s = await engine.SettingsAsync(ct);
		List<CrmIdName> looking = await db.Set<CrmLookingFor>().AsNoTracking().Where(l => l.IsActive).OrderBy(l => l.SortOrder).Select(l => new CrmIdName(l.LookingForId, l.Name)).ToListAsync(ct);
		return new CrmPublicConfigDto(s.ConsentText, s.MarketingText, s.WhatsAppNumber, s.PublicPhone, s.PrivacyUrl, looking);
	}

	// ---- visits -------------------------------------------------------------------------------

	internal static string? CleanPath(string? path)
	{
		if (string.IsNullOrWhiteSpace(path))
		{
			return null;
		}
		string p = path.Trim();
		int cut = p.IndexOfAny(new[] { '?', '#' });
		if (cut >= 0)
		{
			p = p.Substring(0, cut);
		}
		if (!p.StartsWith("/", StringComparison.Ordinal))
		{
			return null;
		}
		return p.Length > 300 ? p.Substring(0, 300) : p;
	}

	internal static string? CleanReferrer(string? referrer)
	{
		// Keep only scheme + host + path of the referring page; never its query string.
		if (string.IsNullOrWhiteSpace(referrer) || !Uri.TryCreate(referrer.Trim(), UriKind.Absolute, out Uri? u) || (u.Scheme != "http" && u.Scheme != "https"))
		{
			return null;
		}
		string clean = u.Scheme + "://" + u.Host + u.AbsolutePath;
		return clean.Length > 300 ? clean.Substring(0, 300) : clean;
	}

	private static bool IsBot(string? userAgent)
	{
		if (string.IsNullOrWhiteSpace(userAgent))
		{
			return true;
		}
		string ua = userAgent.ToLowerInvariant();
		return ua.Contains("bot") || ua.Contains("crawler") || ua.Contains("spider") || ua.Contains("headless") || ua.Contains("lighthouse") || ua.Contains("preview");
	}

	internal static (string Device, string Browser) ParseAgent(string? userAgent)
	{
		string ua = (userAgent ?? "").ToLowerInvariant();
		string device = ua.Contains("ipad") || ua.Contains("tablet") ? "Tablet"
			: ua.Contains("android") ? "Android"
			: ua.Contains("iphone") ? "iPhone"
			: ua.Contains("windows") ? "Windows"
			: ua.Contains("mac os") ? "Mac"
			: ua.Contains("linux") ? "Linux"
			: "Other";
		string browser = ua.Contains("edg/") ? "Edge"
			: ua.Contains("opr/") || ua.Contains("opera") ? "Opera"
			: ua.Contains("samsungbrowser") ? "Samsung Internet"
			: ua.Contains("chrome/") || ua.Contains("crios") ? "Chrome"
			: ua.Contains("firefox") || ua.Contains("fxios") ? "Firefox"
			: ua.Contains("safari") ? "Safari"
			: "Other";
		return (device, browser);
	}

	/// <summary>Tag first, then the referring site, then Direct — honest, and not guessed at.</summary>
	private async Task<int?> ResolveSourceAsync(string? utmSource, string? utmMedium, string? referrer, CancellationToken ct)
	{
		List<CrmSource> sources = await db.Set<CrmSource>().AsNoTracking().Where(s => s.IsWebsite && s.IsActive).OrderBy(s => s.SortOrder).ToListAsync(ct);
		string utm = (utmSource ?? "").Trim().ToLowerInvariant();
		if (utm != "")
		{
			// Rules that also name a medium (google + cpc = Google Ads) are tried before plain ones.
			foreach (CrmSource s in sources.OrderBy(x => string.IsNullOrWhiteSpace(x.UtmMedium)).ThenBy(x => x.SortOrder))
			{
				string[] keys = (s.UtmSource ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
				bool mediumOk = string.IsNullOrWhiteSpace(s.UtmMedium) || string.Equals(s.UtmMedium, utmMedium?.Trim(), StringComparison.OrdinalIgnoreCase);
				if (keys.Contains(utm, StringComparer.OrdinalIgnoreCase) && mediumOk)
				{
					return s.SourceId;
				}
			}
			foreach (CrmSource s in sources)
			{
				if ((s.UtmSource ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Contains(utm, StringComparer.OrdinalIgnoreCase))
				{
					return s.SourceId;
				}
			}
			int other = sources.FirstOrDefault(s => s.Code == "OTHER_WEB")?.SourceId ?? 0;
			return other == 0 ? null : other;
		}
		string? host = CrmService.HostOf(referrer)?.ToLowerInvariant();
		if (host != null)
		{
			if (host.EndsWith("myregalis.com", StringComparison.Ordinal) || host.Contains("ts.net") || host == "localhost" || host.StartsWith("127.", StringComparison.Ordinal))
			{
				return null;
			}
			foreach (CrmSource s in sources)
			{
				if ((s.ReferrerMatch ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Any(m => host.Contains(m, StringComparison.OrdinalIgnoreCase)))
				{
					return s.SourceId;
				}
			}
			int other = sources.FirstOrDefault(s => s.Code == "OTHER_WEB")?.SourceId ?? 0;
			return other == 0 ? null : other;
		}
		return null;
	}

	private static string NewKey()
	{
		Span<byte> b = stackalloc byte[18];
		RandomNumberGenerator.Fill(b);
		return Convert.ToBase64String(b).Replace('+', '-').Replace('/', '_');
	}

	private async Task<CrmVisitor?> FindVisitorAsync(string? key, CancellationToken ct)
	{
		if (string.IsNullOrWhiteSpace(key) || key.Length > 64)
		{
			return null;
		}
		string k = key.Trim();
		return await db.Set<CrmVisitor>().FirstOrDefaultAsync(v => v.VisitorKey == k, ct);
	}

	public async Task<CrmPublicVisitResultDto> RecordVisitAsync(CrmPublicVisitRequest r, string? ip, string? userAgent, string? cityHint, string? countryHint, CancellationToken ct = default)
	{
		DateTime now = engine.Now;
		string key = string.IsNullOrWhiteSpace(r.VisitorKey) || r.VisitorKey.Length > 64 ? NewKey() : r.VisitorKey.Trim();
		if (IsBot(userAgent) || !Allow("visit:" + (ip ?? "?"), 240, TimeSpan.FromMinutes(1), now))
		{
			return new CrmPublicVisitResultDto(key, null);
		}
		CrmVisitor? visitor = await FindVisitorAsync(r.VisitorKey, ct);
		if (r.PageViewId.HasValue)
		{
			// Time spent on an earlier page, sent when the visitor leaves it.
			if (visitor != null && r.Seconds is > 0)
			{
				CrmPageView? view = await db.Set<CrmPageView>().FirstOrDefaultAsync(p => p.PageViewId == r.PageViewId && p.VisitorId == visitor.VisitorId, ct);
				if (view != null)
				{
					view.SecondsOnPage = Math.Min(r.Seconds.Value, 4 * 3600);
					await db.SaveChangesAsync(ct);
				}
			}
			return new CrmPublicVisitResultDto(visitor?.VisitorKey ?? key, r.PageViewId);
		}
		string? path = CleanPath(r.Path);
		if (path == null)
		{
			return new CrmPublicVisitResultDto(visitor?.VisitorKey ?? key, null);
		}
		CrmSettingsSnapshot s = await engine.SettingsAsync(ct);
		string? referrer = CleanReferrer(r.Referrer);
		string? utmSource = CrmText.Clip(r.UtmSource, 100)?.ToLowerInvariant();
		string? utmMedium = CrmText.Clip(r.UtmMedium, 100)?.ToLowerInvariant();
		string? utmCampaign = CrmText.Clip(r.UtmCampaign, 100);
		if (visitor == null)
		{
			(string device, string browser) = ParseAgent(userAgent);
			visitor = new CrmVisitor
			{
				VisitorKey = key,
				FirstSeenUtc = now,
				LastSeenUtc = now,
				SourceId = await ResolveSourceAsync(utmSource, utmMedium, referrer, ct),
				UtmSource = utmSource,
				UtmMedium = utmMedium,
				UtmCampaign = utmCampaign,
				CampaignSeenUtc = utmSource != null || utmCampaign != null ? now : null,
				Referrer = referrer,
				LandingPage = path,
				City = CrmText.Clip(cityHint, 100),
				Country = CrmText.Clip(countryHint, 100),
				DeviceType = device,
				Browser = browser,
				VisitCount = 1,
				DeleteAfterUtc = now.AddDays(s.VisitRetentionDays)
			};
			db.Set<CrmVisitor>().Add(visitor);
			await db.SaveChangesAsync(ct);
		}
		else
		{
			if (r.NewVisit)
			{
				visitor.VisitCount++;
			}
			// The original source is kept; a campaign tag is refreshed and kept for 90 days.
			if (utmSource != null || utmCampaign != null)
			{
				visitor.UtmCampaign = utmCampaign ?? visitor.UtmCampaign;
				visitor.CampaignSeenUtc = now;
				if (visitor.SourceId == null)
				{
					visitor.SourceId = await ResolveSourceAsync(utmSource, utmMedium, referrer, ct);
					visitor.UtmSource = utmSource;
					visitor.UtmMedium = utmMedium;
				}
			}
			else if (visitor.CampaignSeenUtc.HasValue && (now - visitor.CampaignSeenUtc.Value).TotalDays > 90)
			{
				visitor.UtmCampaign = null;
				visitor.CampaignSeenUtc = null;
			}
			visitor.LastSeenUtc = now;
			if (visitor.LeadId == null)
			{
				visitor.DeleteAfterUtc = now.AddDays(s.VisitRetentionDays);
			}
		}
		visitor.PageViewCount++;
		CrmPageView pv = new CrmPageView { VisitorId = visitor.VisitorId, Path = path, ViewedUtc = now, VisitNumber = Math.Max(1, visitor.VisitCount) };
		db.Set<CrmPageView>().Add(pv);
		await db.SaveChangesAsync(ct);
		return new CrmPublicVisitResultDto(visitor.VisitorKey, pv.PageViewId);
	}

	// ---- enquiries ------------------------------------------------------------------------------

	public async Task SubmitEnquiryAsync(CrmPublicEnquiryRequest r, string? ip, string? userAgent, string? cityHint, string? countryHint, CancellationToken ct = default)
	{
		DateTime now = engine.Now;
		string kind = (r.Kind ?? "form").Trim().ToLowerInvariant();
		if (kind is not ("form" or "callback" or "quote"))
		{
			kind = "form";
		}
		// Bots and floods get the same success message as everyone else (section 16.4).
		if (!string.IsNullOrEmpty(r.Website) || (r.ElapsedMs.HasValue && r.ElapsedMs < 2500) || IsBot(userAgent))
		{
			logger.LogInformation("CRM public enquiry dropped as a bot ({Reason})", !string.IsNullOrEmpty(r.Website) ? "honeypot" : "timing/agent");
			return;
		}
		string? name = CrmText.Clip(r.Name, 150);
		if (kind != "callback" && name == null)
		{
			throw new BusinessRuleException("Please tell us your name.");
		}
		if (!CrmText.IsValidMobile(r.Mobile))
		{
			throw new BusinessRuleException("Please give a mobile number so we can call you back.");
		}
		if (!r.ContactConsent)
		{
			throw new BusinessRuleException("Please tick the box so we know we may contact you.");
		}
		string? email = CrmText.Clip(r.Email, 256);
		if (email != null && !CrmText.IsValidEmail(email))
		{
			email = null;
		}
		string mobileNorm = CrmText.NormaliseMobile(r.Mobile);
		if (!Allow("enq:" + (ip ?? "?"), 5, TimeSpan.FromMinutes(10), now) || !Allow("enqm:" + mobileNorm, 3, TimeSpan.FromHours(1), now))
		{
			logger.LogInformation("CRM public enquiry dropped by the rate limit");
			return;
		}
		CrmSettingsSnapshot s = await engine.SettingsAsync(ct);
		CrmVisitor? visitor = await FindVisitorAsync(r.VisitorKey, ct);
		string? pagePath = CleanPath(r.PagePath);
		int? lookingFor = r.LookingForId.HasValue && await db.Set<CrmLookingFor>().AnyAsync(l => l.LookingForId == r.LookingForId && l.IsActive, ct) ? r.LookingForId : null;
		CrmChannel channel = kind switch { "callback" => CrmChannel.Callback, "quote" => CrmChannel.QuoteRequest, _ => CrmChannel.WebForm };
		string howObtained = kind switch
		{
			"callback" => "Website callback bar (“Busy? We will call you.”)",
			"quote" => "Website “Send me a quote” form",
			_ => "Website “Book a tour” enquiry form"
		} + (pagePath == null ? "" : " on " + pagePath);
		string? message = CrmText.Clip(r.Message, 2000);
		CrmLeadConsent consent = new CrmLeadConsent
		{
			HowObtained = CrmText.Clip(howObtained, 200)!,
			ConsentTextShown = CrmText.Clip(s.ConsentText + (r.MarketingConsent ? " | " + s.MarketingText : ""), 1000),
			ContactConsent = true,
			MarketingConsent = r.MarketingConsent,
			ConsentedUtc = now,
			SourceIpHash = CrmText.HashIp(ip)
		};
		await db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
		{
			await using var tx = await db.Database.BeginTransactionAsync(ct);
			// Same number, lead still open: add to that lead instead of creating a second one.
			CrmLead? existing = await db.Set<CrmLead>().Where(l => l.MobileNormalised == mobileNorm && l.ClosedUtc == null).OrderByDescending(l => l.CreatedDate).FirstOrDefaultAsync(ct);
			if (existing != null)
			{
				engine.Log(existing.LeadId, CrmActivityKind.Enquiry, $"Enquired again: {howObtained}." + (message == null ? "" : " They wrote: “" + message + "”"), null, true);
				consent.LeadId = existing.LeadId;
				db.Set<CrmLeadConsent>().Add(consent);
				if (existing.VisitorId == null && visitor != null && visitor.LeadId == null)
				{
					existing.VisitorId = visitor.VisitorId;
					visitor.LeadId = existing.LeadId;
					visitor.DeleteAfterUtc = null;
				}
				if (existing.Email == null && email != null)
				{
					existing.Email = email;
					existing.EmailNormalised = CrmText.NormaliseEmail(email);
				}
				if (existing.OwnerUserId.HasValue)
				{
					engine.InApp(existing.LeadId, "lead.again", existing.OwnerUserId.Value, $"{existing.LeadNumber} enquired again", $"{existing.Name} used the website again ({kind}).");
				}
				await db.SaveChangesAsync(ct);
				await tx.CommitAsync(ct);
				return;
			}
			int? sourceId = visitor?.SourceId;
			if (visitor == null)
			{
				sourceId = await ResolveSourceAsync(CrmText.Clip(r.UtmSource, 100), CrmText.Clip(r.UtmMedium, 100), CleanReferrer(r.Referrer), ct);
			}
			int direct = await engine.SourceIdByCodeAsync("DIRECT", ct);
			CrmSource source = await db.Set<CrmSource>().AsNoTracking().FirstAsync(x => x.SourceId == (sourceId ?? direct), ct);
			CrmStatus status = await engine.StatusByCodeAsync("NEW", ct);
			string? detail = visitor?.UtmCampaign != null ? "Campaign: " + visitor.UtmCampaign : (visitor?.Referrer != null ? "Referring site: " + CrmService.HostOf(visitor.Referrer) : null);
			CrmLead lead = new CrmLead
			{
				Name = name ?? "Callback request",
				Mobile = r.Mobile!.Trim(),
				MobileNormalised = mobileNorm,
				Email = email,
				EmailNormalised = CrmText.NormaliseEmail(email),
				LookingForId = lookingFor,
				PeopleCount = r.People is > 0 and < 10000 ? r.People : null,
				NeededFrom = r.FromWhen,
				StatusId = status.StatusId,
				SourceId = source.SourceId,
				SourceDetail = CrmText.Clip(detail, 300),
				Channel = channel,
				FirstMessage = message,
				VisitorId = visitor?.VisitorId,
				LeadNumber = await engine.NextLeadNumberAsync(ct)
			};
			db.Set<CrmLead>().Add(lead);
			await db.SaveChangesAsync(ct);
			engine.Log(lead.LeadId, CrmActivityKind.Enquiry, $"{howObtained}." + (message == null ? "" : " They wrote: “" + message + "”"), null, true);
			consent.LeadId = lead.LeadId;
			db.Set<CrmLeadConsent>().Add(consent);
			if (visitor != null)
			{
				visitor.LeadId = lead.LeadId;
				visitor.DeleteAfterUtc = null;
			}
			await engine.AfterCreateAsync(lead, source.Code, null, true, null, ct);
			await db.SaveChangesAsync(ct);
			await engine.AnnounceNewLeadAsync(lead, source.Name, ct);
			if (email != null && s.EmailEnabled && s.AckEmailEnabled)
			{
				await engine.EmailAsync(lead.LeadId, "ack", null, email, "Thanks — we have your enquiry",
					$"<p>Hello {CrmText.Html(lead.Name == "Callback request" ? "there" : lead.Name)},</p><p>Thanks — we have your enquiry and we will call you back today.</p>" +
					(s.PublicPhone == null ? "" : $"<p>If it is urgent, call us on {CrmText.Html(s.PublicPhone)}.</p>"), ct, isAcknowledgement: true);
				engine.Log(lead.LeadId, CrmActivityKind.AutoAck, "Acknowledgement email sent: “thanks, we will call you”.", null, true);
			}
			await db.SaveChangesAsync(ct);
			await tx.CommitAsync(ct);
			logger.LogInformation("CRM web lead {Lead} created ({Kind}, source {Source})", lead.LeadNumber, kind, source.Code);
		});
	}

	// ---- WhatsApp reference -------------------------------------------------------------------

	public async Task<CrmPublicWhatsAppDto> IssueWhatsAppRefAsync(CrmPublicWhatsAppRequest r, string? ip, string? userAgent, CancellationToken ct = default)
	{
		DateTime now = engine.Now;
		CrmSettingsSnapshot s = await engine.SettingsAsync(ct);
		string? path = CleanPath(r.Path) ?? "/";
		string page = PageName(path);
		CrmVisitor? visitor = await FindVisitorAsync(r.VisitorKey, ct);
		string code = "";
		if (visitor != null && !IsBot(userAgent) && Allow("wa:" + (ip ?? "?"), 20, TimeSpan.FromMinutes(10), now))
		{
			CrmWhatsAppRef? recent = await db.Set<CrmWhatsAppRef>().Where(x => x.VisitorId == visitor.VisitorId && x.LeadId == null).OrderByDescending(x => x.CreatedUtc).FirstOrDefaultAsync(ct);
			if (recent != null && (now - recent.CreatedUtc).TotalHours < 24)
			{
				code = recent.Code;
			}
			else
			{
				for (int i = 0; i < 6 && code == ""; i++)
				{
					string candidate = "W-" + RandomNumberGenerator.GetInt32(1000, 100000);
					if (!await db.Set<CrmWhatsAppRef>().AnyAsync(x => x.Code == candidate, ct))
					{
						code = candidate;
					}
				}
				if (code != "")
				{
					db.Set<CrmWhatsAppRef>().Add(new CrmWhatsAppRef { Code = code, VisitorId = visitor.VisitorId, PagePath = path, CreatedUtc = now });
					await db.SaveChangesAsync(ct);
				}
			}
		}
		string message = code == "" ? $"Hi, I saw the {page} page on myregalis.com." : $"Hi, I saw the {page} page (ref {code})";
		string? digits = s.WhatsAppNumber == null ? null : new string(s.WhatsAppNumber.Where(char.IsAsciiDigit).ToArray());
		if (digits != null && digits.Length == 10)
		{
			digits = "91" + digits;
		}
		string? url = string.IsNullOrEmpty(digits) ? null : $"https://wa.me/{digits}?text={Uri.EscapeDataString(message)}";
		return new CrmPublicWhatsAppDto(code, message, url);
	}

	private static string PageName(string path)
	{
		string first = path.Trim('/').Split('/').FirstOrDefault() ?? "";
		return first switch
		{
			"" => "home",
			"enquire" => "Book a tour",
			"login" => "sign-in",
			"register" => "sign-up",
			_ => char.ToUpperInvariant(first[0]) + first.Substring(1).Replace('-', ' ')
		};
	}
}
