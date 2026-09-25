using Azure.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Graph;
using Microsoft.Graph.Models;
using Microsoft.Graph.Users.Item.SendMail;
using Regalis.Application.Abstractions;
using Regalis.Domain.Enums;

namespace Regalis.Infrastructure.Adapters;

/// <summary>
/// Bound from the "MicrosoftGraph" configuration section.
///
/// Only <see cref="SenderEmail"/> is safe to log or surface — it is the mailbox the
/// OTP is sent from, not a credential. TenantId and ClientId are not secret either,
/// but ClientSecret is: it must arrive from environment variables or user-secrets and
/// never from a committed appsettings file.
/// </summary>
public class GraphMailOptions
{
    public const string SectionName = "MicrosoftGraph";

    public string TenantId { get; set; } = "";
    public string ClientId { get; set; } = "";
    public string ClientSecret { get; set; } = "";
    /// <summary>The Microsoft 365 mailbox the OTP is sent from. Must exist in the
    /// tenant — app-only Mail.Send posts as a specific user, not as the app.</summary>
    public string SenderEmail { get; set; } = "";

    /// <summary>Everything except the secret, for a startup log line and for the
    /// configuration check. Never include ClientSecret here.</summary>
    public IEnumerable<string> MissingKeys()
    {
        if (string.IsNullOrWhiteSpace(TenantId)) yield return $"{SectionName}:TenantId";
        if (string.IsNullOrWhiteSpace(ClientId)) yield return $"{SectionName}:ClientId";
        if (string.IsNullOrWhiteSpace(ClientSecret)) yield return $"{SectionName}:ClientSecret";
        if (string.IsNullOrWhiteSpace(SenderEmail)) yield return $"{SectionName}:SenderEmail";
    }
}

/// <summary>
/// Microsoft 365 mail OTP channel (Otp:Channel = "Graph"), selected by configuration
/// like the other vendor seams (Invariant 5).
///
/// App-only ("client credentials") Graph: the app authenticates as itself with
/// ClientSecretCredential against the .default scope and posts to
/// POST /users/{senderEmail}/sendMail. No user signs in, and no mailbox password is
/// held anywhere — the tenant grants the app Mail.Send and can revoke it centrally.
///
/// The credential and the client are built once and reused: ClientSecretCredential
/// caches the access token and refreshes it on expiry, so a new one per email would
/// mean a token request per email.
///
/// SMS destinations fall back to the log, matching SmtpOtpSender — no SMS vendor
/// exists yet.
/// </summary>
public class GraphOtpSender : IOtpSender
{
    private readonly GraphMailOptions _options;
    private readonly ILogger<GraphOtpSender> _logger;
    private readonly Lazy<GraphServiceClient> _client;

    public GraphOtpSender(IOptions<GraphMailOptions> options, ILogger<GraphOtpSender> logger)
    {
        _options = options.Value;
        _logger = logger;
        // Lazy so a misconfigured deployment fails when mail is actually sent, with a
        // clear message, rather than at container build where the cause is obscured.
        _client = new Lazy<GraphServiceClient>(CreateClient);
    }

    private GraphServiceClient CreateClient()
    {
        var missing = _options.MissingKeys().ToList();
        if (missing.Count > 0)
        {
            // Names only. The values, especially the secret, never reach a log or an
            // exception message.
            throw new InvalidOperationException(
                "Otp:Channel is 'Graph' but the Microsoft Graph configuration is incomplete. " +
                $"Missing: {string.Join(", ", missing)}. " +
                "Set them via environment variables or user-secrets.");
        }

        var credential = new ClientSecretCredential(
            _options.TenantId, _options.ClientId, _options.ClientSecret);

        // .default asks for exactly the application permissions the tenant has already
        // consented to (Mail.Send) — app-only flows cannot request scopes dynamically.
        return new GraphServiceClient(credential, ["https://graph.microsoft.com/.default"]);
    }

    public async Task SendAsync(string destination, OtpChannel channel, OtpPurpose purpose, string code,
        CancellationToken ct = default)
    {
        if (channel != OtpChannel.Email)
        {
            _logger.LogWarning("No SMS sender configured; DEV OTP for {Destination} ({Purpose}): {Code}",
                destination, purpose, code);
            return;
        }

        var subject = purpose switch
        {
            OtpPurpose.Registration => "Verify your email — Regalis WorkSpace",
            OtpPurpose.ForgotPassword => "Password reset code — Regalis WorkSpace",
            _ => "Your verification code — Regalis WorkSpace"
        };

        var message = new Message
        {
            Subject = subject,
            Body = new ItemBody { ContentType = BodyType.Html, Content = HtmlBody(code) },
            ToRecipients =
            [
                new Recipient { EmailAddress = new EmailAddress { Address = destination } }
            ]
        };

        // The code is deliberately absent from every log statement here: an OTP in the
        // log file is an OTP anyone with log access can use.
        _logger.LogInformation("Sending OTP email ({Purpose}) to {Destination} via Microsoft Graph",
            purpose, Mask(destination));

        try
        {
            await _client.Value
                .Users[_options.SenderEmail]
                .SendMail
                .PostAsync(new SendMailPostRequestBody { Message = message, SaveToSentItems = false },
                    cancellationToken: ct);
        }
        catch (Exception ex)
        {
            // Graph reports a missing mailbox, a withheld admin consent and a bad secret
            // all as ODataError, and the detail is useful to us and to nobody else.
            // Rethrown as-is so the existing exception middleware maps it to the standard
            // generic 500 envelope; the caller never sees the Graph text.
            _logger.LogError(ex,
                "Microsoft Graph email sending failed for {Destination} ({Purpose}) from {Sender}",
                Mask(destination), purpose, _options.SenderEmail);
            throw;
        }

        _logger.LogInformation("OTP email sent successfully to {Destination}", Mask(destination));
    }

    /// <summary>"kausika@regalis.in" -> "k******a@regalis.in". The domain is kept because
    /// it is what makes a delivery failure diagnosable.</summary>
    private static string Mask(string email)
    {
        var at = email.IndexOf('@');
        if (at <= 0) return "***";
        var local = email[..at];
        var masked = local.Length <= 2
            ? new string('*', local.Length)
            : $"{local[0]}{new string('*', local.Length - 2)}{local[^1]}";
        return masked + email[at..];
    }

    private static string HtmlBody(string code) => $"""
        <div style="font-family:Georgia,serif;max-width:480px;margin:0 auto;padding:24px;color:#2b2b2b;">
          <h2 style="font-weight:normal;">Your verification code</h2>
          <p style="font-size:32px;letter-spacing:8px;font-weight:bold;margin:24px 0;">{code}</p>
          <p>It expires in 10 minutes.</p>
          <p style="color:#8a8a8a;font-size:13px;">If you did not request this, you can ignore this email.</p>
        </div>
        """;
}
