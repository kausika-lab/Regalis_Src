using Regalis.Domain.Common;

namespace Regalis.Domain.Entities.Masters;

/// <summary>MST_Location. Invariant 7: every bookable/schedulable table carries LocationId.</summary>
public class Location : AuditableEntity
{
    public int LocationId { get; set; }
    public string LocationCode { get; set; } = string.Empty;
    public string LocationName { get; set; } = string.Empty;
    public string? AddressLine1 { get; set; }
    public string? AddressLine2 { get; set; }
    public string? City { get; set; }
    public string? State { get; set; }
    public string? PostalCode { get; set; }
    public string? ContactNumber { get; set; }
    public string? Email { get; set; }
    public TimeOnly WorkingHoursStart { get; set; }
    public TimeOnly WorkingHoursEnd { get; set; }
    /// <summary>Comma list, e.g. "MON,TUE,WED,THU,FRI".</summary>
    public string WorkingDays { get; set; } = string.Empty;
    public decimal? Latitude { get; set; }
    public decimal? Longitude { get; set; }
    public string? ImagePath { get; set; }
    public string? Description { get; set; }
    public bool IsDefault { get; set; }

    public ICollection<Workspace> Workspaces { get; set; } = new List<Workspace>();
}
