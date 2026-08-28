using Regalis.Domain.Entities.Masters;

namespace Regalis.Domain.Entities.Finance;

/// <summary>TRN_ServiceOrderLine. ItemName is snapshotted at order time.</summary>
public class ServiceOrderLine
{
    public long ServiceOrderLineId { get; set; }
    public long ServiceOrderId { get; set; }
    public int ServiceItemId { get; set; }
    public string ItemName { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal LineAmount { get; set; }

    public ServiceOrder ServiceOrder { get; set; } = null!;
    public ServiceItem ServiceItem { get; set; } = null!;
}
