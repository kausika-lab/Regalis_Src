namespace Regalis.Domain.Entities.Masters;

/// <summary>MAP_WorkspaceAmenity.</summary>
public class WorkspaceAmenity
{
    public long WorkspaceAmenityId { get; set; }
    public int WorkspaceId { get; set; }
    public int AmenityId { get; set; }

    public Workspace Workspace { get; set; } = null!;
    public Amenity Amenity { get; set; } = null!;
}
