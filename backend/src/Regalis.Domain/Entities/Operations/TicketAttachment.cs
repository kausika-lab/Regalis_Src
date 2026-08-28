namespace Regalis.Domain.Entities.Operations;

/// <summary>TRN_TicketAttachment. Allowed: .png .jpg .jpeg .webp .pdf — validated
/// server-side by extension and content sniffing. Max 10 MB per file, 5 per ticket.</summary>
public class TicketAttachment
{
    public long TicketAttachmentId { get; set; }
    public long TicketId { get; set; }
    public long? TicketMessageId { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string StoredFileName { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long FileSizeBytes { get; set; }
    public long UploadedByUserId { get; set; }
    public DateTime UploadedUtc { get; set; }

    public Ticket Ticket { get; set; } = null!;
}
