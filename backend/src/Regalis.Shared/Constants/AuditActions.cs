namespace Regalis.Shared.Constants;

/// <summary>Values written to <c>TRN_AuditLog.Action</c>.</summary>
public static class AuditActions
{
    public const string Login = "LOGIN";
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
