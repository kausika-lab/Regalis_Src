namespace Regalis.Domain.Enums;

public enum AccessTokenPurpose : byte
{
    Booking = 1,
    DayPass = 2,
    MonthlyPass = 3,
    Housekeeping = 4,
    VisitorFuture = 5
}

public enum AccessTokenStatus : byte
{
    Active = 1,
    Used = 2,
    Expired = 3,
    Revoked = 4
}

public enum AccessDirection : byte
{
    In = 1,
    Out = 2
}

public enum AccessMethod : byte
{
    Qr = 1,
    Card = 2,
    Manual = 3,
    Nfc = 4,
    App = 5
}

public enum AccessResult : byte
{
    Granted = 1,
    Denied = 2,
    /// <summary>Invariant 9: a DeviceError result never implies a grant.</summary>
    DeviceError = 3
}

public enum AttendanceSubjectType : byte
{
    Customer = 1,
    Staff = 2
}

public enum AttendanceStatus : byte
{
    CheckedIn = 1,
    CheckedOut = 2,
    AutoClosed = 3
}
