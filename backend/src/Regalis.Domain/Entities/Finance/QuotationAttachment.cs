namespace Regalis.Domain.Entities.Finance;

/// <summary>TRN_QuotationAttachment (document §3.4). Allowed: .pdf .jpg .jpeg .png
/// .docx .xlsx — validated server-side by extension and content sniffing, the same
/// way TicketAttachment does it. Max 10 MB per file, 5 per quotation.
///
/// <see cref="SendWithEmail"/> is the chip's checkbox on the form: an attachment can
/// live on the quotation for the admin's reference without going out to the customer.</summary>
public class QuotationAttachment
{
    public long QuotationAttachmentId { get; set; }
    public long QuotationId { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string StoredFileName { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long FileSizeBytes { get; set; }
    public bool SendWithEmail { get; set; } = true;
    public long UploadedByUserId { get; set; }
    public DateTime UploadedUtc { get; set; }

    public Quotation Quotation { get; set; } = null!;
}
