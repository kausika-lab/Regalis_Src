namespace Regalis.Domain.Enums;

public enum UserType : byte
{
    Customer = 1,
    Admin = 2,
    Staff = 3
}

public enum UserStatus : byte
{
    Active = 1,
    Inactive = 2,
    Locked = 3,
    PendingVerification = 4
}

/// <summary>Which portal a role or menu belongs to.</summary>
public enum PortalType : byte
{
    Customer = 1,
    Admin = 2,
    Both = 3
}

public enum OtpChannel : byte
{
    Email = 1,
    Sms = 2
}

public enum OtpPurpose : byte
{
    Registration = 1,
    ForgotPassword = 2,
    MobileVerification = 3
}

/// <summary>MST_Menu.MenuLevel — Module → Menu → Sub-menu → Screen.</summary>
public enum MenuLevel : byte
{
    Module = 1,
    Menu = 2,
    SubMenu = 3,
    Screen = 4
}
