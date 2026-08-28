using Regalis.Domain.Common;
using Regalis.Domain.Enums;

namespace Regalis.Domain.Entities.Masters;

/// <summary>MST_Banner — home-screen promotional banners.</summary>
public class Banner : AuditableEntity
{
    public int BannerId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Subtitle { get; set; }
    public string ImagePath { get; set; } = string.Empty;
    public string? LinkUrl { get; set; }
    public decimal? PromotionalPrice { get; set; }
    public int DisplayOrder { get; set; }
    public DateTime ValidFromUtc { get; set; }
    public DateTime ValidToUtc { get; set; }
    public int? LocationId { get; set; }
    public PortalType PortalType { get; set; }

    public Location? Location { get; set; }
}
