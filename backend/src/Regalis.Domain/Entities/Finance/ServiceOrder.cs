using Regalis.Domain.Common;
using Regalis.Domain.Enums;

namespace Regalis.Domain.Entities.Finance;

/// <summary>TRN_ServiceOrder — coffee / featured-service purchases.</summary>
public class ServiceOrder : AuditableEntity
{
    public long ServiceOrderId { get; set; }
    public string OrderNumber { get; set; } = string.Empty;
    public long CustomerId { get; set; }
    public int LocationId { get; set; }
    public DateTime OrderUtc { get; set; }
    public decimal SubTotal { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal TotalAmount { get; set; }
    public ServiceOrderStatus Status { get; set; } = ServiceOrderStatus.Placed;
    public long? PaymentId { get; set; }
    public long? InvoiceId { get; set; }
    public string? Notes { get; set; }

    public ICollection<ServiceOrderLine> Lines { get; set; } = new List<ServiceOrderLine>();
}
