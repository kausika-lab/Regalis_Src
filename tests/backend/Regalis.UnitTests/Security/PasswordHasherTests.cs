using FluentAssertions;
using Regalis.Infrastructure.Security;

namespace Regalis.UnitTests.Security;

public class PasswordHasherTests
{
    private readonly PasswordHasher _hasher = new();

    [Fact]
    public void Hash_ProducesVersionedString_AndVerifyRoundTrips()
    {
        var hash = _hasher.Hash("S3cure!Password");

        hash.Should().StartWith("v1.210000.");
        hash.Split('.').Should().HaveCount(4);
        _hasher.Verify("S3cure!Password", hash).Should().BeTrue();
    }

    [Fact]
    public void Verify_WrongPassword_ReturnsFalse()
    {
        var hash = _hasher.Hash("S3cure!Password");
        _hasher.Verify("s3cure!password", hash).Should().BeFalse();
    }

    [Fact]
    public void Hash_SamePasswordTwice_ProducesDifferentHashes()
    {
        // Random salt per hash — equal passwords must not be linkable.
        _hasher.Hash("S3cure!Password").Should().NotBe(_hasher.Hash("S3cure!Password"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-a-valid-format")]
    [InlineData("v2.210000.AAAA.BBBB")]
    public void Verify_MalformedOrUnknownVersion_ReturnsFalse(string storedHash)
    {
        _hasher.Verify("whatever", storedHash).Should().BeFalse();
    }
}
