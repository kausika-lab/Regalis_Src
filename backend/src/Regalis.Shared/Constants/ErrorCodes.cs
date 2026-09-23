namespace Regalis.Shared.Constants;

/// <summary>Machine-readable error codes carried in <c>ApiError.Code</c> (API_PLAN §1).</summary>
public static class ErrorCodes
{
    public const string ValidationError = "VALIDATION_ERROR";
    public const string AuthenticationFailed = "AUTHENTICATION_FAILED";
    public const string TokenExpired = "TOKEN_EXPIRED";
    public const string TokenRevoked = "TOKEN_REVOKED";
    public const string AccountLocked = "ACCOUNT_LOCKED";
    public const string AccountInactive = "ACCOUNT_INACTIVE";
    public const string AuthorizationFailed = "AUTHORIZATION_FAILED";
    /// <summary>Authenticated, but onboarding is unfinished. Distinct from a plain 403
    /// so the SPA can route to the onboarding form rather than say "not allowed".</summary>
    public const string ProfileIncomplete = "PROFILE_INCOMPLETE";
    public const string NotFound = "NOT_FOUND";
    public const string SlotUnavailable = "SLOT_UNAVAILABLE";
    public const string InsufficientWalletBalance = "INSUFFICIENT_WALLET_BALANCE";
    public const string PaymentVerificationFailed = "PAYMENT_VERIFICATION_FAILED";
    public const string BusinessRuleViolation = "BUSINESS_RULE_VIOLATION";
    public const string DuplicateRecord = "DUPLICATE_RECORD";
    public const string OtpInvalid = "OTP_INVALID";
    public const string OtpExpired = "OTP_EXPIRED";
    public const string OtpMaxAttempts = "OTP_MAX_ATTEMPTS";
    public const string RateLimitExceeded = "RATE_LIMIT_EXCEEDED";
    public const string FileTypeNotAllowed = "FILE_TYPE_NOT_ALLOWED";
    public const string FileTooLarge = "FILE_TOO_LARGE";
    public const string InternalError = "INTERNAL_ERROR";
}
