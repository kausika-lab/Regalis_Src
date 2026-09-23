namespace Regalis.Application.Abstractions;

/// <summary>One file travelling with a quotation send — the frozen PDF, plus any
/// attachment the admin ticked "Send with email" (document §3.5).</summary>
public record QuotationOutboundFile(string FileName, string ContentType, byte[] Content);

/// <summary>What the admin filled into the send popup, already resolved against the
/// settings templates by the service.</summary>
public record QuotationEmailMessage(
    string To,
    string? Cc,
    string? Bcc,
    string Subject,
    string Body,
    IReadOnlyList<QuotationOutboundFile> Files);

public record QuotationWhatsAppMessage(
    string To,
    string Body,
    string PortalLink,
    QuotationOutboundFile? Document);

/// <summary>The outcome of one channel. A failure is reported, never thrown: the send
/// popup shows the error per channel with a Retry, and a quotation that reached at
/// least one channel is still Sent (document §3.5).</summary>
public record QuotationDeliveryResult(bool Succeeded, string? FailureReason, string? Note)
{
    public static QuotationDeliveryResult Ok(string? note = null) => new(true, null, note);
    public static QuotationDeliveryResult Fail(string reason) => new(false, reason, null);
}

/// <summary>
/// Delivery of a quotation to the customer, behind an adapter so the SMTP and WhatsApp
/// vendors never block the build (Invariant 5). The WhatsApp side is expected to use the
/// Business API with a pre-approved template; where none is configured it falls back to
/// a wa.me link the admin sends by hand, reported back as a note (document §3.5).
/// </summary>
public interface IQuotationDispatcher
{
    Task<QuotationDeliveryResult> SendEmailAsync(QuotationEmailMessage message,
        CancellationToken ct = default);

    Task<QuotationDeliveryResult> SendWhatsAppAsync(QuotationWhatsAppMessage message,
        CancellationToken ct = default);
}
