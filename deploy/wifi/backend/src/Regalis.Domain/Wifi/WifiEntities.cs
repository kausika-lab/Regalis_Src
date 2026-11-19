using System;

namespace Regalis.Domain.Entities.Wifi;

// Wi-Fi Access (Regalis_WorkSpace_WiFi_Feature_Specification v1.0; kit deploy/wifi).
//
// Plain classes, not AuditableEntity: the audit interceptor copies whole rows of auditable
// classes into TRN_AuditLog, and an access row carries the (encrypted) password. The access
// history is TRN_WifiEvent instead (spec 4.4: "every action is written to the access log").
// Access rows are never deleted (spec 6.3); only session rows age out (retention setting).

/// <summary>Why the access exists.</summary>
public enum WifiAccessKind : byte
{
	/// <summary>A paid booking (desk, cabin, meeting room).</summary>
	Booking = 1,
	/// <summary>A paid pass (day pass or membership).</summary>
	Pass = 2,
	/// <summary>A named person on a company booking: their own password (spec 4.4).</summary>
	TeamMember = 3,
	/// <summary>Issued by hand for a visitor, an event or an interview (spec 3.1).</summary>
	Guest = 4
}

/// <summary>Stored status. "In use" and "Expiring" are worked out when read (spec 4.2).</summary>
public enum WifiAccessStatus : byte
{
	/// <summary>Created, not yet delivered (or not yet started).</summary>
	Issued = 1,
	/// <summary>Delivered and inside its dates.</summary>
	Active = 2,
	/// <summary>An invoice of the customer is overdue; the password is kept.</summary>
	Paused = 3,
	Expired = 4,
	Revoked = 5
}

/// <summary>Where the access stands in the Wi-Fi controller.</summary>
public enum WifiControllerState : byte
{
	/// <summary>Not written to the controller yet (starts later, or waiting for a retry).</summary>
	Pending = 0,
	/// <summary>The controller holds the password.</summary>
	Live = 1,
	/// <summary>Three attempts failed; the admin was told "access not created".</summary>
	Failed = 2,
	/// <summary>Taken out of the controller (expired, paused, revoked).</summary>
	Removed = 3
}

public enum WifiSessionResult : byte
{
	Connected = 1,
	RefusedPassword = 2,
	RefusedExpired = 3,
	RefusedDeviceLimit = 4
}

/// <summary>TRN_WifiAccess: one person's network name, username and password (spec 4.1, 7.1).</summary>
public class WifiAccess
{
	public long AccessId { get; set; }

	/// <summary>WIFI-000418, from MST_NumberSequence key WifiAccess.</summary>
	public string AccessNo { get; set; } = string.Empty;

	public WifiAccessKind Kind { get; set; }

	/// <summary>Settings profile the network and device limit came from: member, daypass, short, meeting, guest.</summary>
	public string Profile { get; set; } = string.Empty;

	/// <summary>The customer account (null for a guest issued by hand without one).</summary>
	public long? CustomerId { get; set; }

	/// <summary>The booking that first paid for it.</summary>
	public long? BookingId { get; set; }

	/// <summary>The pass that first paid for it.</summary>
	public long? CustomerPassId { get; set; }

	/// <summary>A team member's access points at the company access it belongs to.</summary>
	public long? ParentAccessId { get; set; }

	/// <summary>Name, email and mobile of the person holding it (copied for customers; typed in for guests and team members).</summary>
	public string HolderName { get; set; } = string.Empty;

	public string? HolderEmail { get; set; }

	public string? HolderMobile { get; set; }

	public string Ssid { get; set; } = string.Empty;

	public string Username { get; set; } = string.Empty;

	/// <summary>AES-GCM, see WifiSecrets. Never returned by an admin endpoint.</summary>
	public string PasswordEnc { get; set; } = string.Empty;

	public DateTime ValidFromUtc { get; set; }

	public DateTime ValidUntilUtc { get; set; }

	public int DeviceLimit { get; set; }

	public WifiAccessStatus Status { get; set; } = WifiAccessStatus.Issued;

	public string? ControllerRef { get; set; }

	public WifiControllerState ControllerState { get; set; } = WifiControllerState.Pending;

	public int ControllerAttempts { get; set; }

	public DateTime? ControllerNextTryUtc { get; set; }

	public string? ControllerError { get; set; }

	/// <summary>Null = the system (on payment); otherwise the admin.</summary>
	public long? IssuedBy { get; set; }

	public DateTime IssuedUtc { get; set; }

	/// <summary>First time the details reached the person (email accepted, or the in-app notice).</summary>
	public DateTime? DeliveredUtc { get; set; }

	/// <summary>sent / failed / none (no address). Null = not tried yet.</summary>
	public string? EmailStatus { get; set; }

	public DateTime? EmailSentUtc { get; set; }

	public string? EmailError { get; set; }

	/// <summary>Last time a WhatsApp click-to-chat message was opened for it.</summary>
	public DateTime? WhatsAppUtc { get; set; }

	public DateTime? SeenInAppUtc { get; set; }

	public DateTime? ReminderSentUtc { get; set; }

	public DateTime? PausedUtc { get; set; }

	public string? PauseReason { get; set; }

	public string? RevokedReason { get; set; }

	public long? RevokedBy { get; set; }

	public DateTime? RevokedUtc { get; set; }

	public DateTime? ExpiredUtc { get; set; }

	public string? Note { get; set; }

	public DateTime CreatedUtc { get; set; }

	public DateTime UpdatedUtc { get; set; }
}

/// <summary>TRN_WifiAccessSource: each booking or pass an access covers. Renewals add rows here instead of a new password.</summary>
public class WifiAccessSource
{
	public long SourceId { get; set; }

	public long AccessId { get; set; }

	/// <summary>1 = booking, 2 = pass.</summary>
	public byte SourceType { get; set; }

	public long RefId { get; set; }

	public string RefNumber { get; set; } = string.Empty;

	public DateTime StartUtc { get; set; }

	public DateTime EndUtc { get; set; }

	public bool IsCancelled { get; set; }

	public DateTime CreatedUtc { get; set; }
}

/// <summary>TRN_WifiDevice: a device that has connected with an access. RemovedUtc frees the slot.</summary>
public class WifiDevice
{
	public long DeviceId { get; set; }

	public long AccessId { get; set; }

	public string MacAddress { get; set; } = string.Empty;

	public string? DeviceName { get; set; }

	public DateTime FirstSeenUtc { get; set; }

	public DateTime LastSeenUtc { get; set; }

	public DateTime? RemovedUtc { get; set; }

	/// <summary>customer / admin.</summary>
	public string? RemovedBy { get; set; }
}

/// <summary>TRN_WifiSession: one device connection, or one refused attempt (spec 4.3).</summary>
public class WifiSession
{
	public long SessionId { get; set; }

	/// <summary>Null = the device matched no access.</summary>
	public long? AccessId { get; set; }

	public string MacAddress { get; set; } = string.Empty;

	public string? DeviceName { get; set; }

	public string? Ssid { get; set; }

	public string? AccessPoint { get; set; }

	public string? IpAddress { get; set; }

	public DateTime StartedUtc { get; set; }

	public DateTime? EndedUtc { get; set; }

	public DateTime LastSeenUtc { get; set; }

	public long BytesUp { get; set; }

	public long BytesDown { get; set; }

	public WifiSessionResult Result { get; set; } = WifiSessionResult.Connected;
}

/// <summary>TRN_WifiEvent: the access log (issued, delivered, resent, connected, reset, extended, paused, revoked, expired, blocked ...).</summary>
public class WifiEvent
{
	public long EventId { get; set; }

	public long? AccessId { get; set; }

	public string Event { get; set; } = string.Empty;

	/// <summary>Null = the system.</summary>
	public long? ActorUserId { get; set; }

	public string Detail { get; set; } = string.Empty;

	public string? MetaJson { get; set; }

	public DateTime CreatedUtc { get; set; }
}

/// <summary>TRN_WifiBlock: devices blocked by an admin.</summary>
public class WifiBlock
{
	public long BlockId { get; set; }

	public string MacAddress { get; set; } = string.Empty;

	public string? Reason { get; set; }

	public long? BlockedBy { get; set; }

	public DateTime BlockedUtc { get; set; }

	public DateTime? UnblockedUtc { get; set; }

	public long? UnblockedBy { get; set; }
}

/// <summary>MST_WifiSetting: key/value settings (spec 5.3, 7.1 WifiSetting).</summary>
public class WifiSetting
{
	public string SettingKey { get; set; } = string.Empty;

	public string? SettingValue { get; set; }

	public long? UpdatedBy { get; set; }

	public DateTime? UpdatedUtc { get; set; }
}

// ---- the simulated controller (no hardware yet; open item 1) ------------------------------------
// These three tables stand in for the Wi-Fi equipment. The app never reads them except through
// SimulatedWifiController, exactly as it will read a real controller through its adapter.

/// <summary>TRN_WifiSimKey: a password the simulated controller accepts.</summary>
public class WifiSimKey
{
	public string ControllerRef { get; set; } = string.Empty;

	public string Ssid { get; set; } = string.Empty;

	public string Username { get; set; } = string.Empty;

	public string PasswordEnc { get; set; } = string.Empty;

	public DateTime CreatedUtc { get; set; }
}

/// <summary>TRN_WifiSimClient: a device the simulated controller knows (online when ConnectedUtc is set).</summary>
public class WifiSimClient
{
	public string MacAddress { get; set; } = string.Empty;

	public string? ControllerRef { get; set; }

	public string? DeviceName { get; set; }

	public string? Ssid { get; set; }

	public string? AccessPoint { get; set; }

	public string? IpAddress { get; set; }

	public DateTime? ConnectedUtc { get; set; }

	public long BytesUp { get; set; }

	public long BytesDown { get; set; }

	public bool IsBlocked { get; set; }
}

/// <summary>TRN_WifiSimEvent: a refused attempt seen by the simulated controller.</summary>
public class WifiSimEvent
{
	public long SimEventId { get; set; }

	public string MacAddress { get; set; } = string.Empty;

	public string? DeviceName { get; set; }

	public string? Ssid { get; set; }

	public string? AccessPoint { get; set; }

	public string? ControllerRef { get; set; }

	public byte Result { get; set; }

	public DateTime AtUtc { get; set; }
}
