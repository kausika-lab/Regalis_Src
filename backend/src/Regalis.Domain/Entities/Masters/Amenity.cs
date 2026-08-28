using Regalis.Domain.Common;

namespace Regalis.Domain.Entities.Masters;

/// <summary>MST_Amenity.</summary>
public class Amenity : AuditableEntity
{
    public int AmenityId { get; set; }
    public string AmenityName { get; set; } = string.Empty;
    public string? IconName { get; set; }
    public int DisplayOrder { get; set; }

    public ICollection<WorkspaceAmenity> WorkspaceAmenities { get; set; } = new List<WorkspaceAmenity>();
}
