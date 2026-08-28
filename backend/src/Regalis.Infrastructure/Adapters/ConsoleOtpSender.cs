using Microsoft.Extensions.Logging;
using Regalis.Application.Abstractions;
using Regalis.Domain.Enums;

namespace Regalis.Infrastructure.Adapters;

/// <summary>
/// Development OTP channel (Otp:Channel = "Console"): writes the code to the log
/// instead of sending mail/SMS, so the full registration and reset flows are
/// exercisable before SMTP credentials exist (A12-style adapter, Invariant 5).
/// Never selected in Production configuration.
/// </summary>
public class ConsoleOtpSender(ILogger<ConsoleOtpSender> logger) : IOtpSender
{
    public Task SendAsync(string destination, OtpChannel channel, OtpPurpose purpose, string code,
        CancellationToken ct = default)
    {
        logger.LogWarning("DEV OTP for {Destination} ({Purpose}): {Code}", destination, purpose, code);
        return Task.CompletedTask;
    }
}
