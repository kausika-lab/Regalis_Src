using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Regalis.Domain.Enums;
using Regalis.Infrastructure.Adapters;

namespace Regalis.UnitTests.Adapters;

/// <summary>
/// The Microsoft Graph OTP channel.
///
/// Nothing here reaches the network. Every test either stops before a Graph client is
/// built, or exercises a path that never builds one — so the suite can never send a
/// real email, which is the point: an OTP test that mails someone is a test that mails
/// the wrong person eventually.
/// </summary>
public class GraphOtpSenderTests
{
    private static GraphOtpSender Sender(GraphMailOptions options) =>
        new(Options.Create(options), NullLogger<GraphOtpSender>.Instance);

    private static GraphMailOptions Complete() => new()
    {
        TenantId = "c6bba7d1-0000-0000-0000-000000000000",
        ClientId = "ffbea803-0000-0000-0000-000000000000",
        ClientSecret = "not-a-real-secret",
        SenderEmail = "noreply@example.com"
    };

    // ── Configuration ───────────────────────────────────────────────────────

    [Fact]
    public void MissingKeys_OnACompleteConfiguration_IsEmpty()
    {
        Complete().MissingKeys().Should().BeEmpty();
    }

    [Fact]
    public void MissingKeys_NamesEveryAbsentKey()
    {
        new GraphMailOptions().MissingKeys().Should().BeEquivalentTo(
        [
            "MicrosoftGraph:TenantId",
            "MicrosoftGraph:ClientId",
            "MicrosoftGraph:ClientSecret",
            "MicrosoftGraph:SenderEmail"
        ]);
    }

    [Theory]
    [InlineData("TenantId")]
    [InlineData("ClientId")]
    [InlineData("ClientSecret")]
    [InlineData("SenderEmail")]
    public void MissingKeys_CatchesEachKeyIndividually(string key)
    {
        var options = Complete();
        typeof(GraphMailOptions).GetProperty(key)!.SetValue(options, "   ");

        options.MissingKeys().Should().ContainSingle().Which.Should().Be($"MicrosoftGraph:{key}");
    }

    [Fact]
    public async Task SendAsync_WithIncompleteConfiguration_ThrowsNamingTheMissingKeys()
    {
        var sender = Sender(new GraphMailOptions { TenantId = "tenant-only" });

        var act = async () => await sender.SendAsync(
            "member@example.com", OtpChannel.Email, OtpPurpose.Registration, "123456");

        var ex = await act.Should().ThrowAsync<InvalidOperationException>();
        ex.Which.Message.Should().Contain("MicrosoftGraph:ClientId")
                                 .And.Contain("MicrosoftGraph:SenderEmail");
    }

    [Fact]
    public async Task SendAsync_NeverPutsTheSecretInTheException()
    {
        // A configuration error is the most likely thing to be pasted into a ticket.
        const string secret = "super-secret-value-do-not-leak";
        var sender = Sender(new GraphMailOptions { ClientSecret = secret });

        var act = async () => await sender.SendAsync(
            "member@example.com", OtpChannel.Email, OtpPurpose.Registration, "123456");

        var ex = await act.Should().ThrowAsync<InvalidOperationException>();
        ex.Which.Message.Should().NotContain(secret);
    }

    [Fact]
    public async Task SendAsync_NeverPutsTheCodeInTheException()
    {
        var sender = Sender(new GraphMailOptions());

        var act = async () => await sender.SendAsync(
            "member@example.com", OtpChannel.Email, OtpPurpose.Registration, "654321");

        var ex = await act.Should().ThrowAsync<InvalidOperationException>();
        ex.Which.Message.Should().NotContain("654321");
    }

    // ── Channels ────────────────────────────────────────────────────────────

    [Fact]
    public async Task SendAsync_ForSms_FallsBackToTheLogWithoutTouchingGraph()
    {
        // Matches SmtpOtpSender: no SMS vendor exists yet. The configuration is empty,
        // so if this path tried to build a Graph client it would throw instead.
        var sender = Sender(new GraphMailOptions());

        var act = async () => await sender.SendAsync(
            "+919876543210", OtpChannel.Sms, OtpPurpose.Registration, "123456");

        await act.Should().NotThrowAsync();
    }

    // ── Contract ────────────────────────────────────────────────────────────

    [Fact]
    public void Sender_ImplementsTheExistingAbstraction()
    {
        // It must slot into the same seam Console and Smtp use, or Otp:Channel
        // selection in DependencyInjection cannot pick it.
        typeof(GraphOtpSender).Should().BeAssignableTo<Regalis.Application.Abstractions.IOtpSender>();
    }
}
