namespace Regalis.Shared.Constants;

/// <summary>Values written to <c>TRN_AuditLog.Action</c>.</summary>
public static class AuditActions
{
    public const string Login = "LOGIN";
    public const string LoginFailed = "LOGIN_FAILED";
    public const string Logout = "LOGOUT";
    public const string Create = "CREATE";
    public const string Update = "UPDATE";
    public const string Delete = "DELETE";
    public const string Approve = "APPROVE";
    public const string Reject = "REJECT";
    public const string Export = "EXPORT";
    public const string Import = "IMPORT";
    public const string PasswordChange = "PASSWORD_CHANGE";
    public const string AccessOverride = "ACCESS_OVERRIDE";
    public const string AuthorizationFailed = "AUTHORIZATION_FAILED";
}

/// <summary>Values written to <c>TRN_AuditLog.Result</c>.</summary>
public static class AuditResults
{
    /// <summary>The action completed and was saved.</summary>
    public const string Success = "Success";
    /// <summary>Attempted, but an error occurred: validation, exception, wrong password.</summary>
    public const string Failed = "Failed";
    /// <summary>Blocked by a permission or a business rule.</summary>
    public const string Denied = "Denied";

    public static readonly string[] All = [Success, Failed, Denied];
}
