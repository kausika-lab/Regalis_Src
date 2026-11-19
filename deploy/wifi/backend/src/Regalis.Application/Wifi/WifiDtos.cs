using System;
using System.Collections.Generic;
using Regalis.Shared.Api;

namespace Regalis.Application.Wifi;

// Wi-Fi Access DTOs (spec section 7.3). Passwords appear ONLY in WifiPortalAccessDto, which only
// the customer who owns the access receives (spec 4.4, 8).

// ---- controller adapter (spec 7.2) --------------------------------------------------------------

/// <summary>What the app writes into the controller for one access.</summary>
public record WifiKeySpec(string Ssid, string Username, string Password, int DeviceLimit, int SpeedMbps, DateTime ValidUntilUtc);

/// <summary>One device the controller sees online. ControllerRef names the password/login it used.</summary>
public record WifiClientInfo(string Mac, string? DeviceName, string? Ssid, string? AccessPoint, string? Ip, string? ControllerRef, DateTime ConnectedSinceUtc, long BytesUp, long BytesDown);

/// <summary>A refused attempt reported by the controller. Result: 2 wrong password, 3 expired, 4 device limit.</summary>
public record WifiAuthEvent(string Mac, string? DeviceName, string? Ssid, string? AccessPoint, string? ControllerRef, byte Result, DateTime AtUtc);

public record WifiControllerStatus(bool Connected, string Message, int AccessPoints, int Clients);

// ---- connected now (spec 5.1) ---------------------------------------------------------------------

public class WifiConnectedRequest
{
	public string? Search { get; set; }

	/// <summary>members / daypass / guests / staff (staff = devices that matched no customer access).</summary>
	public string? Who { get; set; }

	public string? Ssid { get; set; }

	public string? Area { get; set; }

	public int Page { get; set; } = 1;

	public int PageSize { get; set; } = 25;
}

public record WifiConnectedRowDto(long SessionId, long? AccessId, string? AccessNo, long? CustomerId, string? HolderName, string? Company, string? Mobile, string? Email,
	string Who, bool OtherDevice, string? DeviceName, string Mac, string? Ssid, string? AccessPoint, string? Ip, DateTime StartedUtc, long Bytes,
	string? BookingNumber, string? BookingLabel, DateTime? ValidUntilUtc, bool Blocked);

public record WifiConnectedCardsDto(int OnlinePeople, int OnlineDevices, int CustomersToday, int CustomersWithAccess, int GuestsOnline,
	long BytesToday, int PeakDevices, DateTime? PeakAtUtc, int FailedToday, int FailedExpired, int FailedPassword, int FailedLimit);

public record WifiConnectedDto(WifiConnectedCardsDto Cards, PagedResult<WifiConnectedRowDto> Page, List<string> Networks, List<string> Areas,
	DateTime? LastPollUtc, bool LastPollOk, string? LastPollError, string ControllerType, string Mode);

// ---- access issued (spec 5.2) ------------------------------------------------------------------

public class WifiAccessListRequest
{
	/// <summary>all / active / expiring / expired / revoked / undelivered / paused.</summary>
	public string? Chip { get; set; }

	public string? Search { get; set; }

	/// <summary>Profile key: member, daypass, short, meeting, guest; or "team".</summary>
	public string? Kind { get; set; }

	/// <summary>month (default) / 7 / 30 / 90 / all: validity overlapping the period.</summary>
	public string? Period { get; set; }

	public int Page { get; set; } = 1;

	public int PageSize { get; set; } = 25;
}

public record WifiAccessRowDto(long AccessId, string AccessNo, string Kind, string Profile, long? CustomerId, string HolderName, string? Email, string? Mobile, string? Company,
	string? BookingNumber, string? BookingLabel, string Username, string Ssid, DateTime ValidFromUtc, DateTime ValidUntilUtc, int DevicesUsed, int DeviceLimit,
	string Status, string StatusLabel, string? EmailStatus, string? EmailError, DateTime? DeliveredUtc, DateTime? SeenInAppUtc, DateTime? WhatsAppUtc,
	string ControllerState, string? ControllerError, int MemberCount, string? MembersPreview, int MemberDevicesUsed, int MemberDeviceLimit, string? RevokedReason, string? PauseReason, string IssuedBy, bool Renews);

public record WifiAccessCountsDto(int All, int Active, int ExpiringToday, int Expired, int Revoked, int NotDelivered, int Paused);

public record WifiAccessListDto(WifiAccessCountsDto Counts, PagedResult<WifiAccessRowDto> Page);

public record WifiDeviceDto(long DeviceId, string Mac, string? DeviceName, DateTime FirstSeenUtc, DateTime LastSeenUtc, bool Online, DateTime? OnlineSinceUtc, DateTime? RemovedUtc, string? RemovedBy);

public record WifiEventDto(long EventId, string Event, string Detail, string Actor, DateTime CreatedUtc);

public record WifiSourceDto(string Type, string Number, string Label, DateTime StartUtc, DateTime EndUtc, bool Cancelled);

public record WifiAccessDetailDto(WifiAccessRowDto Row, List<WifiDeviceDto> Devices, List<WifiEventDto> Events, List<WifiAccessRowDto> Members, List<WifiSourceDto> Sources);

public record WifiGuestRequest(string Name, string? Mobile, string? Email, string? Profile, DateTime ValidUntilUtc, DateTime? ValidFromUtc, int? DeviceLimit, string? Note, long? CustomerId);

public record WifiMemberRequest(string Name, string? Email, string? Mobile);

/// <summary>channel: email / whatsapp.</summary>
public record WifiResendRequest(string Channel);

/// <summary>For WhatsApp the caller opens Url (click-to-chat; open item 7).</summary>
public record WifiResendResultDto(string Channel, bool Ok, string Message, string? Url);

public record WifiUpdateAccessRequest(DateTime? ValidUntilUtc, int? DeviceLimit, string? Reason);

public record WifiRevokeRequest(string Reason);

public record WifiDisconnectRequest(bool Block, string? Reason);

// ---- usage log -------------------------------------------------------------------------------

public class WifiSessionRequest
{
	public DateTime? FromUtc { get; set; }

	public DateTime? ToUtc { get; set; }

	public string? Search { get; set; }

	/// <summary>connected / refused / unknown.</summary>
	public string? Result { get; set; }

	public long? AccessId { get; set; }

	public int Page { get; set; } = 1;

	public int PageSize { get; set; } = 50;
}

public record WifiSessionDto(long SessionId, long? AccessId, string? AccessNo, string? HolderName, string? DeviceName, string Mac, string? Ssid, string? AccessPoint,
	DateTime StartedUtc, DateTime? EndedUtc, int Minutes, long Bytes, string Result);

// ---- settings (spec 5.3) ---------------------------------------------------------------------

public record WifiNetworkDto(string Ssid, int SpeedMbps, string Purpose, bool Internal);

/// <summary>Key: member, daypass, short, meeting, virtual, guest. Ssid "" = this kind of booking gets no Wi-Fi.</summary>
public record WifiProfileDto(string Key, string Label, string Ssid, int DeviceLimit);

public record WifiSettingsDto(
	string ControllerType, string? ControllerUrl, string? ControllerUser, string? ControllerKey, bool HasControllerKey, string? ControllerSite,
	string Mode, string? SharedPassword, bool HasSharedPassword,
	List<WifiNetworkDto> Networks, List<WifiProfileDto> Profiles,
	bool AutoIssue, DateTime? IssueSinceUtc, bool PayLater,
	bool DeliverEmail, bool DeliverWhatsApp, bool DeliverQr, bool Reminder,
	string DayPassUntil, int IdleMinutes, bool RuleCancel, int OverdueDays, bool RuleUnknownDevice, int RetentionMonths,
	bool? Connected, string? ControllerMessage, DateTime? LastPollUtc, int? AccessPoints, int? Clients);

// ---- simulated controller lab ------------------------------------------------------------------

/// <summary>How: right (the access's own password), wrong (a typo), shared (a password that belongs to nobody, e.g. the old shared one).</summary>
public record WifiSimJoinRequest(long? AccessId, string How, string? DeviceName, string? Mac, string? AccessPoint, string? Ssid);

public record WifiSimLeaveRequest(string Mac);

public record WifiSimResultDto(string Mac, string Outcome);

// ---- customer portal (spec 5.4, 7.3) -----------------------------------------------------------

public record WifiPortalDeviceDto(long DeviceId, string? DeviceName, string Mac, DateTime FirstSeenUtc, bool Online, DateTime? OnlineSinceUtc, DateTime LastSeenUtc);

public record WifiPortalAccessDto(long AccessId, string AccessNo, string Title, string Status, string StatusLabel, string Ssid, string Username, string? Password,
	string? QrText, DateTime ValidFromUtc, DateTime ValidUntilUtc, bool Renews, int DeviceLimit, List<WifiPortalDeviceDto> Devices, bool Ready, string? Notice, string Mode);

public record WifiPortalHistoryDto(long AccessId, string Title, string Ssid, DateTime ValidFromUtc, DateTime ValidUntilUtc, string StatusLabel);

public record WifiPortalDto(List<WifiPortalAccessDto> Current, List<WifiPortalHistoryDto> Earlier, bool WhatsAppEnabled, int MemberDevices, int PassDevices, string RecordedNotice);

/// <summary>"Having trouble connecting?": raises a support ticket.</summary>
public record WifiHelpRequest(string? Message, bool PasswordShared);
