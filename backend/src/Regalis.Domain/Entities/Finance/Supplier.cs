using Regalis.Domain.Common;

namespace Regalis.Domain.Entities.Finance;

/// <summary>MST_Supplier — the vendor a purchase order is raised against
/// (document §21). LocationId is nullable: null means the supplier serves every
/// location, matching the MST_PassPlan precedent.</summary>
public class Supplier : AuditableEntity
{
    public int SupplierId { get; set; }
    public string SupplierCode { get; set; } = string.Empty;
    public string SupplierName { get; set; } = string.Empty;
    public string? ContactPerson { get; set; }
    public string? Email { get; set; }
    public string? MobileNumber { get; set; }
    public string? Gstin { get; set; }
    public string? AddressLine { get; set; }
    public string? City { get; set; }
    public string? State { get; set; }
    public string? Pincode { get; set; }
    /// <summary>Free text, e.g. "Net 30" — not interpreted by the engine.</summary>
    public string? PaymentTerms { get; set; }
    public int? LocationId { get; set; }
    public string? Notes { get; set; }

    public ICollection<PurchaseOrder> PurchaseOrders { get; set; } = new List<PurchaseOrder>();
}
