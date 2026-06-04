using System;
using System.Security.Cryptography;
using System.Text;
using FluentAssertions;
using Opus.Content.Packaging.Signing;
using Xunit;

namespace Opus.Content.Packaging.Tests.Signing;

public sealed class PackageSignerVerifierTests
{
    private static readonly byte[] ManifestBytes = Encoding.UTF8.GetBytes("""{"package":"vellum.opus.alpha"}""");

    [Fact]
    public void Sign_then_verify_succeeds_with_the_same_key()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var signature = PackageSigner.Sign(ManifestBytes, key, "vellum-alpha-2026");

        signature.Algorithm.Should().Be(PackageSignatureAlgorithm.EcdsaP256Sha256);
        PackageSignatureVerifier.Verify(ManifestBytes, signature, key)
            .Should().Be(PackageSignatureVerification.Valid);
    }

    [Fact]
    public void Verify_fails_against_a_different_key()
    {
        using var signingKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        using var otherKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var signature = PackageSigner.Sign(ManifestBytes, signingKey, "k");

        PackageSignatureVerifier.Verify(ManifestBytes, signature, otherKey)
            .Should().Be(PackageSignatureVerification.SignatureInvalid);
    }

    [Fact]
    public void Verify_detects_altered_manifest_bytes()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var signature = PackageSigner.Sign(ManifestBytes, key, "k");
        var tampered = Encoding.UTF8.GetBytes("""{"package":"evil"}""");

        PackageSignatureVerifier.Verify(tampered, signature, key)
            .Should().Be(PackageSignatureVerification.ManifestHashMismatch);
    }

    [Fact]
    public void Verify_rejects_an_unsupported_algorithm()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var signature = PackageSigner.Sign(ManifestBytes, key, "k") with { Algorithm = "rsa-9000" };

        PackageSignatureVerifier.Verify(ManifestBytes, signature, key)
            .Should().Be(PackageSignatureVerification.AlgorithmUnsupported);
    }

    [Fact]
    public void Verify_rejects_a_malformed_signature()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var signature = PackageSigner.Sign(ManifestBytes, key, "k") with { Signature = string.Empty };

        PackageSignatureVerifier.Verify(ManifestBytes, signature, key)
            .Should().Be(PackageSignatureVerification.Malformed);
    }

    [Fact]
    public void Sign_rejects_a_non_p256_key()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP384);

        var act = () => PackageSigner.Sign(ManifestBytes, key, "k");

        act.Should().Throw<ArgumentException>();
    }
}
