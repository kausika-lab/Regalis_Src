using Regalis.Domain.Common;
using Regalis.Domain.Enums;

namespace Regalis.Domain.Entities.Identity;

/// <summary>MST_User — single user table for every principal; UserType separates the portals.</summary>
public class User : AuditableEntity
{
    public long UserId { get; set; }
    public UserType UserType { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string MobileNumber { get; set; } = string.Empty;
    /// <summary>PBKDF2, versioned string. Never returned by any API.</summary>
    public string PasswordHash { get; set; } = string.Empty;
    public string? CompanyName { get; set; }
    public string? Address { get; set; }
    public string? PhotoPath { get; set; }
    public int? DefaultLocationId { get; set; }
    public bool IsEmailVerified { get; set; }
    public bool IsMobileVerified { get; set; }
    public UserStatus Status { get; set; } = UserStatus.PendingVerification;
    public int FailedLoginCount { get; set; }
    public DateTime? LockoutEndUtc { get; set; }
    public DateTime? LastLoginUtc { get; set; }
    public DateTime? LastPasswordChangeUtc { get; set; }
    public bool MustChangePassword { get; set; }

    public Masters.Location? DefaultLocation { get; set; }
    public ICollection<UserRole> UserRoles { get; set; } = new List<UserRole>();
    public ICollection<RefreshToken> RefreshTokens { get; set; } = new List<RefreshToken>();
}
