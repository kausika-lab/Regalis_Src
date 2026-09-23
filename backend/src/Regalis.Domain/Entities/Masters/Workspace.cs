using Regalis.Domain.Common;
using Regalis.Domain.Enums;

namespace Regalis.Domain.Entities.Masters;

/// <summary>MST_Workspace — a bookable desk, cabin or meeting room.</summary>
public class Workspace : AuditableEntity
{
    public int WorkspaceId { get; set; }
    public int LocationId { get; set; }
    public int WorkspaceTypeId { get; set; }
    /// <summary>D-001, C-001, MR-001 — unique per location.</summary>
    public string WorkspaceCode { get; set; } = string.Empty;
    public string WorkspaceName { get; set; } = string.Empty;
    public string? FloorNumber { get; set; }
    public int Capacity { get; set; } = 1;
    public decimal PricePerHour { get; set; }
    public decimal? PricePerDay { get; set; }
    /// <summary>Monthly rate for a cabin or dedicated desk. Quotations price cabins and
    /// desks by the month (document §2.1), which hourly and daily rates cannot express;
    /// null means no standard monthly price is configured and the admin types one.</summary>
    public decimal? PricePerMonth { get; set; }
    /// <summary>Comma list, e.g. "Projector,Whiteboard,VC".</summary>
    public string? Equipment { get; set; }
    public string? Description { get; set; }
    public string? ImagePath { get; set; }
    public WorkspaceStatus Status { get; set; } = WorkspaceStatus.Available;
    /// <summary>False ⇒ excluded from customer availability.</summary>
    public bool IsBookable { get; set; } = true;
    public byte[] RowVersion { get; set; } = [];

    public Location Location { get; set; } = null!;
    public WorkspaceType WorkspaceType { get; set; } = null!;
    public ICollection<WorkspaceAmenity> WorkspaceAmenities { get; set; } = new List<WorkspaceAmenity>();
}
