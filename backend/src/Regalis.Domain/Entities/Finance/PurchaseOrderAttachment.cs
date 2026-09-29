namespace Regalis.Domain.Entities.Finance;

/// <summary>TRN_PurchaseOrderAttachment — supplier quotation, proforma, indent or any
/// supporting document. Same rules as QuotationAttachment: .pdf .jpg .jpeg .png .docx
/// .xlsx, checked by extension and content sniffing, at most 10 MB each and 10 per PO.
/// Supersedes the single PurchaseOrder.AttachmentPath, which is kept for old rows.</summary>
public class PurchaseOrderAttachment
{
    public long PurchaseOrderAttachmentId { get; set; }
    public long PurchaseOrderId { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string StoredFileName { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long FileSizeBytes { get; set; }
    public long UploadedByUserId { get; set; }
    public DateTime UploadedUtc { get; set; }

    public PurchaseOrder PurchaseOrder { get; set; } = null!;
}
