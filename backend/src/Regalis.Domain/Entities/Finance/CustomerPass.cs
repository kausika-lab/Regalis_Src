using Regalis.Domain.Common;
using Regalis.Domain.Entities.Masters;
using Regalis.Domain.Enums;

namespace Regalis.Domain.Entities.Finance;

/// <summary>TRN_CustomerPass — a purchased Day/Monthly pass.</summary>
public class CustomerPass : AuditableEntity
{
    public long CustomerPassId { get; set; }
    public string PassNumber { get; set; } = string.Empty;
    public long CustomerId { get; set; }
    public int PassPlanId { get; set; }
    public int LocationId { get; set; }
    public PassType PassType { get; set; }
    public DateTime PurchaseUtc { get; set; }
    public DateOnly ValidFrom { get; set; }
    public DateOnly ValidTo { get; set; }
    public int GuestCount { get; set; }
    public decimal Price { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal TotalAmount { get; set; }
    public CustomerPassStatus Status { get; set; } = CustomerPassStatus.Active;
    public DateTime? UsedUtc { get; set; }
    public long? InvoiceId { get; set; }
    public long? PaymentId { get; set; }

    public PassPlan PassPlan { get; set; } = null!;
}
