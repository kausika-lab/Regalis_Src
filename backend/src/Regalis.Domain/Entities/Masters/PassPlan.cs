using Regalis.Domain.Common;
using Regalis.Domain.Enums;

namespace Regalis.Domain.Entities.Masters;

/// <summary>MST_PassPlan — a purchasable Day/Monthly pass definition.</summary>
public class PassPlan : AuditableEntity
{
    public int PassPlanId { get; set; }
    public string PlanCode { get; set; } = string.Empty;
    public string PlanName { get; set; } = string.Empty;
    public PassType PassType { get; set; }
    /// <summary>Null = valid at all locations.</summary>
    public int? LocationId { get; set; }
    public decimal Price { get; set; }
    public int ValidityDays { get; set; }
    public int? MaxGuests { get; set; }
    public string? Description { get; set; }
    public string? TermsAndConditions { get; set; }
    public string? ImagePath { get; set; }
    public int DisplayOrder { get; set; }

    public Location? Location { get; set; }
}
