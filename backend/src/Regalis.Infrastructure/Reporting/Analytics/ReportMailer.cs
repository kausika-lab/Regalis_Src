using Azure.Identity;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Graph;
using Microsoft.Graph.Models;
using Microsoft.Graph.Users.Item.SendMail;
using MimeKit;
using Regalis.Infrastructure.Adapters;

namespace Regalis.Infrastructure.Reporting.Analytics;

public interface IReportMailer
{
    /// <summary>Sends one email to every recipient. Returns null on success, or the
    /// reason it could not be sent — never throws for a delivery failure.</summary>
    Task<string?> SendAsync(IReadOnlyCollection<string> to, string subject, string html,
        IReadOnlyCollection<(string FileName, string ContentType, byte[] Content)> attachments,
        CancellationToken ct = default);
}

/// <summary>
/// Mail for scheduled reports and large exports. Microsoft Graph when the tenant is
/// configured (as on UAT), SMTP otherwise; with neither, the send fails with a reason
/// the schedule shows — a schedule that silently sends nothing is worse than none.
/// </summary>
public sealed class ReportMailer(
    IOptions<GraphMailOptions> graphOptions,
    IOptions<SmtpOptions> smtpOptions,
    ILogger<ReportMailer> logger) : IReportMailer
{
    /// <summary>Graph takes inline attachments up to about 3 MB per message.</summary>
    public const int MaxAttachmentBytes = 3 * 1024 * 1024;

    private readonly Lazy<GraphServiceClient?> _graph = new(() =>
    {
        var o = graphOptions.Value;
        if (o.MissingKeys().Any())
        {
            return null;
        }
        return new GraphServiceClient(new ClientSecretCredential(o.TenantId, o.ClientId, o.ClientSecret),
            ["https://graph.microsoft.com/.default"]);
    });

    public async Task<string?> SendAsync(IReadOnlyCollection<string> to, string subject, string html,
        IReadOnlyCollection<(string FileName, string ContentType, byte[] Content)> attachments,
        CancellationToken ct = default)
    {
        if (to.Count == 0)
        {
            return "There is no one to send it to.";
        }
        try
        {
            if (_graph.Value is { } graph)
            {
                var message = new Message
                {
                    Subject = subject,
                    Body = new ItemBody { ContentType = BodyType.Html, Content = html },
                    ToRecipients = to.Select(a => new Recipient { EmailAddress = new EmailAddress { Address = a } }).ToList(),
                    Attachments = attachments.Select(a => (Attachment)new FileAttachment
                    {
                        OdataType = "#microsoft.graph.fileAttachment",
                        Name = a.FileName,
                        ContentType = a.ContentType,
                        ContentBytes = a.Content
                    }).ToList()
                };
                await graph.Users[graphOptions.Value.SenderEmail].SendMail
                    .PostAsync(new SendMailPostRequestBody { Message = message, SaveToSentItems = false },
                        cancellationToken: ct);
                return null;
            }

            var smtp = smtpOptions.Value;
            if (string.IsNullOrWhiteSpace(smtp.Host))
            {
                logger.LogWarning("Report email '{Subject}' not sent: no mail server is configured", subject);
                return "No mail server is configured.";
            }
            var mail = new MimeMessage();
            mail.From.Add(new MailboxAddress(smtp.FromName,
                string.IsNullOrWhiteSpace(smtp.FromAddress) ? smtp.Username : smtp.FromAddress));
            foreach (var address in to)
            {
                mail.To.Add(MailboxAddress.Parse(address));
            }
            mail.Subject = subject;
            var body = new BodyBuilder { HtmlBody = html };
            foreach (var a in attachments)
            {
                body.Attachments.Add(a.FileName, a.Content, MimeKit.ContentType.Parse(a.ContentType));
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
            await client.DisconnectAsync(true, ct);
            return null;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Report email '{Subject}' to {Count} recipient(s) failed", subject, to.Count);
            return "The mail server refused the message.";
        }
    }
}
