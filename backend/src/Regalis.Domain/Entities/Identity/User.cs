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
    /// <summary>Empty on an account created through Google sign-in, which carries no
    /// phone number. UX_User_Mobile therefore excludes the empty string.</summary>
    public string MobileNumber { get; set; } = string.Empty;
    /// <summary>PBKDF2, versioned string. Never returned by any API. Null on an account
    /// that has only ever signed in with Google — there is no password to verify, and a
    /// placeholder hash would be indistinguishable from a real one.</summary>
    public string? PasswordHash { get; set; }
    /// <summary>Google's `sub` claim: stable for the life of the Google account and
    /// never reused. Null on an account that has never signed in with Google.</summary>
    public string? GoogleId { get; set; }
    public string? CompanyName { get; set; }
    /// <summary>Legacy single-line address. Kept and kept populated: TRN_Invoice
    /// snapshots it, and several DTOs still expose it. The structured parts below are
    /// the source of truth and this is composed from them on save.</summary>
    public string? Address { get; set; }

    // Structured profile, collected at registration or at onboarding. Nullable at the
    // table level whatever the forms demand: the accounts that predate these columns
    // have none of it, and admin-created users are not put through onboarding.
    // Names follow MST_Location / MST_Supplier rather than the SPA's spelling.
    public string? AddressLine1 { get; set; }
    public string? AddressLine2 { get; set; }
    public string? City { get; set; }
    public string? State { get; set; }
    public string? Country { get; set; }
    public string? PostalCode { get; set; }
    public DateOnly? DateOfBirth { get; set; }
    public Gender? Gender { get; set; }
    /// <summary>15-character GSTIN, upper-cased on save. Feeds TRN_Invoice.CustomerGstin,
    /// which until now had no customer-level source.</summary>
    public string? Gstin { get; set; }

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

    /// <summary>
    /// Whether this member has finished onboarding. One definition, here, so the flag the
    /// SPA redirects on and the check that guards booking and purchase can never drift
    /// apart and let someone through one but not the other.
    ///
    /// Only customers are ever asked: staff and admins are created by an administrator,
    /// not through a signup form, and have no reason to hold a GSTIN or a date of birth.
    ///
    /// Not mapped — it is derived, and the EF configuration ignores it.
    /// </summary>
    public bool IsProfileComplete =>
        UserType != UserType.Customer ||
        (!string.IsNullOrWhiteSpace(MobileNumber) &&
         !string.IsNullOrWhiteSpace(CompanyName) &&
         !string.IsNullOrWhiteSpace(AddressLine1) &&
         !string.IsNullOrWhiteSpace(AddressLine2) &&
         !string.IsNullOrWhiteSpace(City) &&
         !string.IsNullOrWhiteSpace(State) &&
         !string.IsNullOrWhiteSpace(Country) &&
         !string.IsNullOrWhiteSpace(PostalCode) &&
         !string.IsNullOrWhiteSpace(Gstin) &&
         DateOfBirth is not null &&
         Gender is not null);

    public Masters.Location? DefaultLocation { get; set; }
    public ICollection<UserRole> UserRoles { get; set; } = new List<UserRole>();
    public ICollection<RefreshToken> RefreshTokens { get; set; } = new List<RefreshToken>();
}
