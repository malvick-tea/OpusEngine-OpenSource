using System.Security.Cryptography;
using FluentAssertions;
using Opus.Foundation.Security;
using Xunit;

namespace Opus.Foundation.Tests.Security;

public sealed class EcdsaKeyPolicyTests
{
    [Fact]
    public void Accepts_nist_p256()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);

        EcdsaKeyPolicy.IsNistP256(key).Should().BeTrue();
    }

    [Fact]
    public void Rejects_other_curves()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP384);

        EcdsaKeyPolicy.IsNistP256(key).Should().BeFalse();
    }
}
