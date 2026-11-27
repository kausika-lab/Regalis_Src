using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Azure.Identity;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Graph;
using Microsoft.Graph.Models;
using Microsoft.Graph.Users.Item.SendMail;
using MimeKit;
using Regalis.Application.Abstractions;

namespace Regalis.Infrastructure.Adapters;

/// <summary>
/// quotation-invoice kit (27 Nov 2026), B1: the email side of a quotation send.
///
/// <see cref="QuotationDispatcher"/> only knew the Smtp section, and with no Smtp:Host it
/// answered "Ok" with the note "email was not sent", so the quotation was marked Sent and
/// the screen said it had gone. Production sends mail through Microsoft Graph, with
/// Smtp:Host empty, so on production quotation mail was never sent but reported sent.
///
/// This mailer picks the transport the same way <c>ReportMailer</c> does: Microsoft Graph
/// when the MicrosoftGraph section is complete, SMTP when Smtp:Host is set, and otherwise a
/// failure with a reason. Every failure is logged with its exception and returned as
/// <see cref="QuotationDeliveryResult.Fail"/>, so the event is Failed, the quotation stays
/// where it was and the send popup shows the reason. WhatsApp is unchanged: it is handed to
/// <see cref="QuotationDispatcher"/>.
///
/// Used by QuotationService only. The other callers of IQuotationDispatcher (receipts,
/// reminders, statements, announcements, wallet desk, admin notifications) keep the old
/// dispatcher, so their behaviour does not change with this kit.
/// </summary>
public sealed class QuotationMailDispatcher(
    QuotationDispatcher inner,
    IOptions<GraphMailOptions> graphOptions,
    IOptions<SmtpOptions> smtpOptions,
    ILogger<QuotationMailDispatcher> logger) : IQuotationDispatcher
{
    /// <summary>Graph takes inline attachments up to about 3 MB per message (same limit as ReportMailer).</summary>
    public const int MaxGraphAttachmentBytes = 3145728;

    public const string NoTransportReason = "No mail server is configured (Microsoft Graph or SMTP), so the email was not sent.";

    public async Task<QuotationDeliveryResult> SendEmailAsync(QuotationEmailMessage message, CancellationToken ct = default)
    {
        if (!TryAddresses(message, out List<string> to, out List<string> cc, out List<string> bcc, out string? bad))
        {
            logger.LogWarning("Quotation email not sent: '{Address}' is not a valid email address", bad);
            return QuotationDeliveryResult.Fail($"'{bad}' is not a valid email address.");
        }

        GraphMailOptions graph = graphOptions.Value;
        SmtpOptions smtp = smtpOptions.Value;
        if (!graph.MissingKeys().Any())
        {
            return await SendGraphAsync(graph, message, to, cc, bcc, ct);
        }
        if (!string.IsNullOrWhiteSpace(smtp.Host))
        {
            return await SendSmtpAsync(smtp, message, to, cc, bcc, ct);
        }
        logger.LogError("Quotation email to {To} not sent: no mail transport is configured (MicrosoftGraph missing {Missing}; Smtp:Host empty)",
            message.To, string.Join(", ", graph.MissingKeys()));
        return QuotationDeliveryResult.Fail(NoTransportReason);
    }

    public Task<QuotationDeliveryResult> SendWhatsAppAsync(QuotationWhatsAppMessage message, CancellationToken ct = default)
        => inner.SendWhatsAppAsync(message, ct);

    private async Task<QuotationDeliveryResult> SendGraphAsync(GraphMailOptions graph, QuotationEmailMessage message,
        List<string> to, List<string> cc, List<string> bcc, CancellationToken ct)
    {
        long bytes = message.Files.Sum(f => (long)f.Content.Length);
        if (bytes > MaxGraphAttachmentBytes)
        {
            logger.LogWarning("Quotation email to {To} not sent: attachments are {Bytes} bytes, over the Graph limit of {Limit}", message.To, bytes, MaxGraphAttachmentBytes);
            return QuotationDeliveryResult.Fail(
                $"The attachments come to {bytes / 1048576.0:0.0} MB; the mail service takes at most 3 MB per email. Untick 'Send with email' on the larger attachments and send again.");
        }
        try
        {
            var client = new GraphServiceClient(new ClientSecretCredential(graph.TenantId, graph.ClientId, graph.ClientSecret),
                new[] { "https://graph.microsoft.com/.default" });
            static List<Recipient> Recipients(IEnumerable<string> list) =>
                list.Select(a => new Recipient { EmailAddress = new EmailAddress { Address = a } }).ToList();
            var mail = new Message
            {
                Subject = message.Subject,
                Body = new ItemBody { ContentType = BodyType.Html, Content = message.Body },
                ToRecipients = Recipients(to),
                CcRecipients = Recipients(cc),
                BccRecipients = Recipients(bcc),
                Attachments = message.Files.Select(f => (Attachment)new FileAttachment
                {
                    OdataType = "#microsoft.graph.fileAttachment",
                    Name = f.FileName,
                    ContentType = f.ContentType,
                    ContentBytes = f.Content
                }).ToList()
            };
            await client.Users[graph.SenderEmail].SendMail.PostAsync(new SendMailPostRequestBody { Message = mail, SaveToSentItems = false }, null, ct);
            logger.LogInformation("Quotation email sent to {To} via Microsoft Graph", message.To);
            return QuotationDeliveryResult.Ok();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Quotation email to {To} via Microsoft Graph failed", message.To);
            return QuotationDeliveryResult.Fail("The mail service (Microsoft Graph) refused the message: " + Short(ex.Message));
        }
    }

    private async Task<QuotationDeliveryResult> SendSmtpAsync(SmtpOptions smtp, QuotationEmailMessage message,
        List<string> to, List<string> cc, List<string> bcc, CancellationToken ct)
    {
        try
        {
            var mail = new MimeMessage();
            mail.From.Add(new MailboxAddress(smtp.FromName, string.IsNullOrWhiteSpace(smtp.FromAddress) ? smtp.Username : smtp.FromAddress));
            foreach (string a in to) mail.To.Add(MailboxAddress.Parse(a));
            foreach (string a in cc) mail.Cc.Add(MailboxAddress.Parse(a));
            foreach (string a in bcc) mail.Bcc.Add(MailboxAddress.Parse(a));
            mail.Subject = message.Subject;
            var body = new BodyBuilder { HtmlBody = message.Body };
            foreach (QuotationOutboundFile f in message.Files)
            {
                body.Attachments.Add(f.FileName, f.Content, MimeKit.ContentType.Parse(f.ContentType));
            }
            mail.Body = body.ToMessageBody();
            using var client = new SmtpClient();
            await client.ConnectAsync(smtp.Host, smtp.Port, smtp.UseStartTls ? SecureSocketOptions.StartTls : SecureSocketOptions.Auto, ct);
            if (!string.IsNullOrWhiteSpace(smtp.Username))
            {
                await client.AuthenticateAsync(smtp.Username, smtp.Password, ct);
            }
            await client.SendAsync(mail, ct);
            await client.DisconnectAsync(true, ct);
            logger.LogInformation("Quotation email sent to {To} via SMTP {Host}", message.To, smtp.Host);
            return QuotationDeliveryResult.Ok();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Quotation email to {To} via SMTP {Host} failed", message.To, smtp.Host);
            return QuotationDeliveryResult.Fail("The mail server refused the message: " + Short(ex.Message));
        }
    }

    /// <summary>To, Cc and Bcc may each hold several addresses separated by commas or semicolons.</summary>
    private static bool TryAddresses(QuotationEmailMessage m, out List<string> to, out List<string> cc, out List<string> bcc, out string? bad)
    {
        bad = null;
        to = Split(m.To);
        cc = Split(m.Cc);
        bcc = Split(m.Bcc);
        if (to.Count == 0)
        {
            bad = m.To ?? "";
            return false;
        }
        foreach (string a in to.Concat(cc).Concat(bcc))
        {
            if (!MailboxAddress.TryParse(a, out MailboxAddress? box) || string.IsNullOrWhiteSpace(box.Address) || !box.Address.Contains('@'))
            {
                bad = a;
                return false;
            }
        }
        return true;
    }

    private static List<string> Split(string? raw) =>
        (raw ?? "").Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();

    private static string Short(string s) => s.Length > 300 ? s.Substring(0, 300) : s;
}
