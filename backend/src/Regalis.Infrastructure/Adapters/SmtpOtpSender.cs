using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;
using Regalis.Application.Abstractions;
using Regalis.Domain.Enums;

namespace Regalis.Infrastructure.Adapters;

public class SmtpOptions
{
    public const string SectionName = "Smtp";
    public string Host { get; set; } = "";
    public int Port { get; set; } = 587;
    public bool UseStartTls { get; set; } = true;
    public string Username { get; set; } = "";
    public string Password { get; set; } = "";
    /// <summary>Defaults to Username when empty (Gmail rewrites the From header anyway).</summary>
    public string FromAddress { get; set; } = "";
    public string FromName { get; set; } = "Regalis WorkSpace";
}

/// <summary>
/// Real mail OTP channel (Otp:Channel = "Smtp"), selected by configuration like the
/// other vendor seams (Invariant 5). Credentials live in user-secrets/environment,
/// never in appsettings. SMS destinations still fall back to the log — no SMS
/// vendor exists yet.
/// </summary>
public class SmtpOtpSender(IOptions<SmtpOptions> options, ILogger<SmtpOtpSender> logger) : IOtpSender
{
    public async Task SendAsync(string destination, OtpChannel channel, OtpPurpose purpose, string code,
        CancellationToken ct = default)
    {
        if (channel != OtpChannel.Email)
        {
            logger.LogWarning("No SMS sender configured; DEV OTP for {Destination} ({Purpose}): {Code}",
                destination, purpose, code);
            return;
        }

        var smtp = options.Value;
        if (string.IsNullOrWhiteSpace(smtp.Host))
        {
            throw new InvalidOperationException(
                "Otp:Channel is 'Smtp' but Smtp:Host is not configured. " +
                "Set the Smtp section via user-secrets or environment variables.");
        }

        var subject = purpose switch
        {
            OtpPurpose.Registration => "Verify your email — Regalis WorkSpace",
            OtpPurpose.ForgotPassword => "Password reset code — Regalis WorkSpace",
            _ => "Your verification code — Regalis WorkSpace"
        };

        var from = string.IsNullOrWhiteSpace(smtp.FromAddress) ? smtp.Username : smtp.FromAddress;
        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(smtp.FromName, from));
        message.To.Add(MailboxAddress.Parse(destination));
        message.Subject = subject;
        message.Body = new BodyBuilder
        {
            TextBody = $"Your Regalis WorkSpace verification code is {code}. It expires in 10 minutes. " +
                       "If you did not request this, you can ignore this email.",
            HtmlBody = $"""
                <div style="font-family:Georgia,serif;max-width:480px;margin:0 auto;padding:24px;color:#2b2b2b;">
                  <h2 style="font-weight:normal;">Your verification code</h2>
                  <p style="font-size:32px;letter-spacing:8px;font-weight:bold;margin:24px 0;">{code}</p>
                  <p>It expires in 10 minutes.</p>
                  <p style="color:#8a8a8a;font-size:13px;">If you did not request this, you can ignore this email.</p>
                </div>
                """
        }.ToMessageBody();

        using var client = new SmtpClient();
        await client.ConnectAsync(smtp.Host, smtp.Port,
            smtp.UseStartTls ? SecureSocketOptions.StartTls : SecureSocketOptions.Auto, ct);
        if (!string.IsNullOrWhiteSpace(smtp.Username))
        {
            await client.AuthenticateAsync(smtp.Username, smtp.Password, ct);
        }
        await client.SendAsync(message, ct);
        await client.DisconnectAsync(quit: true, ct);

        logger.LogInformation("OTP email ({Purpose}) sent to {Destination} via {Host}",
            purpose, destination, smtp.Host);
    }
}
