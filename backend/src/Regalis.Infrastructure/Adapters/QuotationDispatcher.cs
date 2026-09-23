using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;
using Regalis.Application.Abstractions;

namespace Regalis.Infrastructure.Adapters;

public class WhatsAppOptions
{
    public const string SectionName = "WhatsApp";
    /// <summary>Business API base, e.g. https://graph.facebook.com/v20.0/&lt;phone-id&gt;.
    /// Empty means no provider is configured and the wa.me fallback is used.</summary>
    public string ApiBaseUrl { get; set; } = "";
    public string AccessToken { get; set; } = "";
    /// <summary>The pre-approved template name from document §3.5.</summary>
    public string TemplateName { get; set; } = "quotation_sent";
    public string TemplateLanguage { get; set; } = "en";
}

/// <summary>
/// Real delivery for a quotation send (document §3.5), selected by configuration like
/// the other vendor seams (Invariant 5). Email reuses the same SMTP section the OTP
/// sender is configured with, so one mail account serves the whole system.
///
/// WhatsApp degrades rather than fails: with no Business API configured it returns the
/// wa.me link for the admin to send by hand, which the service logs as
/// "Shared via WhatsApp (manual)" — the document's stated fallback.
/// </summary>
public class QuotationDispatcher(
    IOptions<SmtpOptions> smtpOptions,
    IOptions<WhatsAppOptions> whatsAppOptions,
    IHttpClientFactory httpClientFactory,
    ILogger<QuotationDispatcher> logger) : IQuotationDispatcher
{
    public async Task<QuotationDeliveryResult> SendEmailAsync(QuotationEmailMessage message,
        CancellationToken ct = default)
    {
        var smtp = smtpOptions.Value;
        if (string.IsNullOrWhiteSpace(smtp.Host))
        {
            // Not an outage: a UAT box with no mail account should still be able to
            // walk the flow, so the send is recorded as skipped rather than failed.
            logger.LogWarning("No SMTP host configured; quotation email to {To} not sent",
                message.To);
            return QuotationDeliveryResult.Ok("No mail server is configured — email was not sent.");
        }

        try
        {
            var from = string.IsNullOrWhiteSpace(smtp.FromAddress) ? smtp.Username : smtp.FromAddress;
            var mail = new MimeMessage();
            mail.From.Add(new MailboxAddress(smtp.FromName, from));
            mail.To.Add(MailboxAddress.Parse(message.To));
            if (!string.IsNullOrWhiteSpace(message.Cc))
            {
                mail.Cc.Add(MailboxAddress.Parse(message.Cc));
            }
            if (!string.IsNullOrWhiteSpace(message.Bcc))
            {
                mail.Bcc.Add(MailboxAddress.Parse(message.Bcc));
            }
            mail.Subject = message.Subject;

            var body = new BodyBuilder { HtmlBody = message.Body };
            foreach (var file in message.Files)
            {
                body.Attachments.Add(file.FileName, file.Content,
                    ContentType.Parse(file.ContentType));
            }
            mail.Body = body.ToMessageBody();

            using var client = new SmtpClient();
            await client.ConnectAsync(smtp.Host, smtp.Port,
                smtp.UseStartTls ? SecureSocketOptions.StartTls : SecureSocketOptions.Auto, ct);
            if (!string.IsNullOrWhiteSpace(smtp.Username))
            {
                await client.AuthenticateAsync(smtp.Username, smtp.Password, ct);
            }
            await client.SendAsync(mail, ct);
            await client.DisconnectAsync(quit: true, ct);

            logger.LogInformation("Quotation email sent to {To} via {Host}", message.To, smtp.Host);
            return QuotationDeliveryResult.Ok();
        }
        catch (Exception ex)
        {
            // The popup shows this per channel with a Retry; one channel failing must
            // not roll back a send that the other channel completed.
            logger.LogError(ex, "Quotation email to {To} failed", message.To);
            return QuotationDeliveryResult.Fail(ex.Message);
        }
    }

    public async Task<QuotationDeliveryResult> SendWhatsAppAsync(QuotationWhatsAppMessage message,
        CancellationToken ct = default)
    {
        var options = whatsAppOptions.Value;
        var normalised = NormaliseNumber(message.To);

        if (string.IsNullOrWhiteSpace(options.ApiBaseUrl) ||
            string.IsNullOrWhiteSpace(options.AccessToken))
        {
            var link = $"https://wa.me/{normalised}?text={Uri.EscapeDataString(message.Body)}";
            logger.LogInformation("No WhatsApp Business API configured; wa.me fallback for {To}",
                normalised);
            return QuotationDeliveryResult.Ok($"Shared via WhatsApp (manual): {link}");
        }

        try
        {
            var client = httpClientFactory.CreateClient(nameof(QuotationDispatcher));
            using var request = new HttpRequestMessage(HttpMethod.Post,
                $"{options.ApiBaseUrl.TrimEnd('/')}/messages");
            request.Headers.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", options.AccessToken);
            request.Content = JsonContent(new
            {
                messaging_product = "whatsapp",
                to = normalised,
                type = "template",
                template = new
                {
                    name = options.TemplateName,
                    language = new { code = options.TemplateLanguage },
                    components = new object[]
                    {
                        new
                        {
                            type = "body",
                            parameters = new object[] { new { type = "text", text = message.Body } }
                        }
                    }
                }
            });

            using var response = await client.SendAsync(request, ct);
            if (!response.IsSuccessStatusCode)
            {
                var detail = await response.Content.ReadAsStringAsync(ct);
                logger.LogError("WhatsApp send to {To} failed: {Status} {Detail}",
                    normalised, (int)response.StatusCode, detail);
                return QuotationDeliveryResult.Fail(
                    $"WhatsApp provider returned {(int)response.StatusCode}.");
            }

            logger.LogInformation("Quotation WhatsApp template sent to {To}", normalised);
            return QuotationDeliveryResult.Ok();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "WhatsApp send to {To} failed", normalised);
            return QuotationDeliveryResult.Fail(ex.Message);
        }
    }

    /// <summary>wa.me and the Business API both want digits only, country code first.
    /// A bare 10-digit Indian number is assumed to be +91, which is what every mobile
    /// in MST_User is.</summary>
    private static string NormaliseNumber(string raw)
    {
        var digits = new string(raw.Where(char.IsDigit).ToArray());
        return digits.Length == 10 ? "91" + digits : digits;
    }

    private static HttpContent JsonContent(object payload) =>
        new StringContent(System.Text.Json.JsonSerializer.Serialize(payload),
            System.Text.Encoding.UTF8, "application/json");
}
