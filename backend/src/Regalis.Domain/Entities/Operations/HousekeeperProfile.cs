using Regalis.Domain.Common;
using Regalis.Domain.Enums;

namespace Regalis.Domain.Entities.Operations;

/// <summary>MST_HousekeeperProfile — the housekeeping details of a staff user (HK-000018).
/// The person is an MST_User (Staff, role HOUSEKEEPING) so they can sign in to see their
/// tasks; Active / Inactive is the user's own status, so deactivating also stops sign-in.
/// A housekeeper is never deleted, only deactivated, so their work history stays.</summary>
public class HousekeeperProfile : AuditableEntity
{
    public long HousekeeperProfileId { get; set; }
    public long UserId { get; set; }
    public string HousekeeperCode { get; set; } = string.Empty;
    public HousekeeperShift Shift { get; set; } = HousekeeperShift.Morning;
    /// <summary>Comma-separated area names, used to suggest the right person for a task.</summary>
    public string? AssignedAreas { get; set; }
    public DateOnly JoiningDate { get; set; }
}
