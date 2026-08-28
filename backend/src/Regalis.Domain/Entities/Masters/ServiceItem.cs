using Regalis.Domain.Common;

namespace Regalis.Domain.Entities.Masters;

/// <summary>MST_ServiceItem — coffee and featured services catalogue.</summary>
public class ServiceItem : AuditableEntity
{
    public int ServiceItemId { get; set; }
    public string ItemCode { get; set; } = string.Empty;
    public string ItemName { get; set; } = string.Empty;
    /// <summary>Coffee, Tea, Snacks, Printing, Scanning, Locker.</summary>
    public string Category { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public string? Unit { get; set; }
    public string? ImagePath { get; set; }
    public string? Description { get; set; }
    public bool IsAvailable { get; set; } = true;
    public int DisplayOrder { get; set; }
    public int? LocationId { get; set; }

    public Location? Location { get; set; }
}
