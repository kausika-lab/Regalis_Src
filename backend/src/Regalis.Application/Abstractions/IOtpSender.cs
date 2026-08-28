using Regalis.Domain.Enums;

namespace Regalis.Application.Abstractions;

/// <summary>Delivery channel for one-time codes, behind an adapter so the SMTP/SMS
/// vendor never blocks the build (Invariant 5). Dev uses the console sender.</summary>
public interface IOtpSender
{
    Task SendAsync(string destination, OtpChannel channel, OtpPurpose purpose, string code,
        CancellationToken ct = default);
}
