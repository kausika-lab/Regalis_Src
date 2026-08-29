using FluentValidation;
using Regalis.Shared.Api;

namespace Regalis.Application.Access;

/// <summary>The documented deny reasons (DATABASE_DESIGN §7).</summary>
public static class DenyReasons
{
    public const string TokenNotFound = "TokenNotFound";
    public const string TokenRevoked = "TokenRevoked";
    public const string OutsideValidWindow = "OutsideValidWindow";
    public const string BookingCancelled = "BookingCancelled";
    public const string WrongWorkspace = "WrongWorkspace";
    public const string AlreadyCheckedOut = "AlreadyCheckedOut";
    public const string DeviceOffline = "DeviceOffline";
    public const string UsageExceeded = "UsageExceeded";
}

public record ValidateAccessRequest(
    // Raw token value or the full signed QR payload (RGL1.{token}.{sig}).
    string Payload,
    int? WorkspaceId,
    // 1 In, 2 Out.
    byte Direction,
    string? DeviceId,
    // 1 Qr, 2 Card, 3 Manual, 4 Nfc, 5 App.
    byte AccessMethod);

public record AccessDecisionDto(
    bool IsGranted,
    // 1 Granted, 2 Denied, 3 DeviceError.
    byte Result,
    string? DenyReason,
    long AccessLogId,
    string? PersonName,
    // Booking or pass number the token belongs to.
    string? Reference,
    DateTime? ValidToUtc);

public record ManualOverrideRequest(
    long UserId,
    int LocationId,
    int? WorkspaceId,
    byte Direction,
    long? BookingId,
    string Reason);

public record MyAccessTokenDto(
    long AccessTokenId,
    string QrPayload,
    byte TokenPurpose,
    string Reference,
    string? WorkspaceName,
    DateTime ValidFromUtc,
    DateTime ValidToUtc,
    byte Status);

public record AccessLogDto(
    long AccessLogId,
    long? UserId,
    string? PersonName,
    int? WorkspaceId,
    int LocationId,
    byte Direction,
    byte AccessMethod,
    byte Result,
    string? DenyReason,
    string? DeviceId,
    bool IsManualOverride,
    string? OverrideReason,
    DateTime LoggedUtc);

public class AccessLogListRequest : PagedRequest
{
    public byte? Result { get; set; }
    public long? UserId { get; set; }
    public DateOnly? From { get; set; }
    public DateOnly? To { get; set; }
}

public class ValidateAccessRequestValidator : AbstractValidator<ValidateAccessRequest>
{
    public ValidateAccessRequestValidator()
    {
        RuleFor(x => x.Payload).NotEmpty().MaximumLength(500);
        RuleFor(x => x.Direction).Must(d => d is 1 or 2).WithMessage("Direction must be 1 (in) or 2 (out).");
        RuleFor(x => x.AccessMethod).InclusiveBetween((byte)1, (byte)5);
        RuleFor(x => x.DeviceId).MaximumLength(80);
    }
}

public class ManualOverrideRequestValidator : AbstractValidator<ManualOverrideRequest>
{
    public ManualOverrideRequestValidator()
    {
        RuleFor(x => x.UserId).GreaterThan(0);
        RuleFor(x => x.LocationId).GreaterThan(0);
        RuleFor(x => x.Direction).Must(d => d is 1 or 2).WithMessage("Direction must be 1 (in) or 2 (out).");
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(500)
            .WithMessage("A reason is mandatory — overrides are exception-logged.");
    }
}

public interface IAccessService
{
    /// <summary>The device/reader endpoint: evaluates OUR rules, commands the door
    /// through the adapter, and ALWAYS writes a TRN_AccessLog row. A device failure
    /// is DeviceError, never a grant (Invariant 9).</summary>
    Task<AccessDecisionDto> ValidateAsync(ValidateAccessRequest request, CancellationToken ct = default);

    /// <summary>The device-down fallback (ACCESS.OVERRIDE): grants by explicit human
    /// decision, writes an exception log row + ACCESS_OVERRIDE audit entry.</summary>
    Task<AccessDecisionDto> ManualOverrideAsync(ManualOverrideRequest request, CancellationToken ct = default);

    Task<List<MyAccessTokenDto>> GetMyTokensAsync(CancellationToken ct = default);

    Task<PagedResult<AccessLogDto>> GetLogsAsync(AccessLogListRequest request, CancellationToken ct = default);

    Task RevokeTokenAsync(long accessTokenId, CancellationToken ct = default);
}
