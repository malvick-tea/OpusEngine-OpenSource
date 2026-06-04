using FluentAssertions;
using Opus.Content.Packaging.Archive;
using Opus.Content.Packaging.Diagnostics;
using Opus.Content.Packaging.Tests.Fixtures;
using Xunit;

namespace Opus.Content.Packaging.Tests.Archive;

public sealed class PackageArchiveVerifierTests
{
    [Fact]
    public void Signed_archive_verifies_with_the_public_key()
    {
        using var content = new PackageFixtureBuilder().WithGoldenFiles();
        using var output = new TempDirectory();
        using var key = EcdsaTestKeys.CreateP256();
        var archivePath = ArchivePackaging.PackGolden(content.Root, output.Path, key, "vellum-alpha");

        var result = PackageArchiveVerifier.Verify(new PackageArchiveVerifyRequest(archivePath) { PublicKey = key });

        result.Succeeded.Should().BeTrue(Diagnostics(result));
        result.SignatureVerified.Should().BeTrue();
    }

    [Fact]
    public void Unsigned_archive_passes_integrity_but_warns_it_is_unsigned()
    {
        using var content = new PackageFixtureBuilder().WithGoldenFiles();
        using var output = new TempDirectory();
        var archivePath = ArchivePackaging.PackGolden(content.Root, output.Path);

        var result = PackageArchiveVerifier.Verify(new PackageArchiveVerifyRequest(archivePath));

        result.Succeeded.Should().BeTrue(Diagnostics(result));
        result.SignatureVerified.Should().BeFalse();
        result.Diagnostics.Should().Contain(d => d.Code == PackageDiagnosticCode.PackageUnsigned);
    }

    [Fact]
    public void Verify_fails_when_a_payload_entry_is_tampered()
    {
        using var content = new PackageFixtureBuilder().WithGoldenFiles();
        using var output = new TempDirectory();
        var archivePath = ArchivePackaging.PackGolden(content.Root, output.Path);
        ArchivePackaging.ReplaceZipEntry(archivePath, "textures/checker.png", new byte[] { 9, 9, 9, 9 });

        var result = PackageArchiveVerifier.Verify(new PackageArchiveVerifyRequest(archivePath));

        result.Succeeded.Should().BeFalse();
        result.Diagnostics.Should().Contain(d => d.Code == PackageDiagnosticCode.FileHashMismatch);
    }

    [Fact]
    public void Verify_fails_when_the_signed_manifest_is_tampered()
    {
        using var content = new PackageFixtureBuilder().WithGoldenFiles();
        using var output = new TempDirectory();
        using var key = EcdsaTestKeys.CreateP256();
        var archivePath = ArchivePackaging.PackGolden(content.Root, output.Path, key, "k");
        TamperManifest(archivePath);

        var result = PackageArchiveVerifier.Verify(new PackageArchiveVerifyRequest(archivePath) { PublicKey = key });

        result.Succeeded.Should().BeFalse();
        result.SignatureVerified.Should().BeFalse();
        result.Diagnostics.Should().Contain(d => d.Code == PackageDiagnosticCode.SignatureManifestHashMismatch);
    }

    [Fact]
    public void Require_signature_fails_on_an_unsigned_archive()
    {
        using var content = new PackageFixtureBuilder().WithGoldenFiles();
        using var output = new TempDirectory();
        using var key = EcdsaTestKeys.CreateP256();
        var archivePath = ArchivePackaging.PackGolden(content.Root, output.Path);

        var result = PackageArchiveVerifier.Verify(new PackageArchiveVerifyRequest(archivePath)
        {
            PublicKey = key,
            RequireSignature = true,
        });

        result.Succeeded.Should().BeFalse();
        result.Diagnostics.Should().Contain(d => d.Code == PackageDiagnosticCode.SignatureMissing);
    }

    [Fact]
    public void Verify_warns_when_signed_but_no_key_supplied()
    {
        using var content = new PackageFixtureBuilder().WithGoldenFiles();
        using var output = new TempDirectory();
        using var key = EcdsaTestKeys.CreateP256();
        var archivePath = ArchivePackaging.PackGolden(content.Root, output.Path, key, "k");

        var result = PackageArchiveVerifier.Verify(new PackageArchiveVerifyRequest(archivePath));

        result.Succeeded.Should().BeTrue(Diagnostics(result));
        result.SignatureVerified.Should().BeFalse();
        result.Diagnostics.Should().Contain(d => d.Code == PackageDiagnosticCode.SignaturePresentNotVerified);
    }

    [Fact]
    public void Verify_fails_with_a_wrong_public_key()
    {
        using var content = new PackageFixtureBuilder().WithGoldenFiles();
        using var output = new TempDirectory();
        using var signingKey = EcdsaTestKeys.CreateP256();
        using var wrongKey = EcdsaTestKeys.CreateP256();
        var archivePath = ArchivePackaging.PackGolden(content.Root, output.Path, signingKey, "k");

        var result = PackageArchiveVerifier.Verify(new PackageArchiveVerifyRequest(archivePath) { PublicKey = wrongKey });

        result.Succeeded.Should().BeFalse();
        result.Diagnostics.Should().Contain(d => d.Code == PackageDiagnosticCode.SignatureInvalid);
    }

    [Fact]
    public void Verify_rejects_an_entry_not_covered_by_the_manifest()
    {
        using var content = new PackageFixtureBuilder().WithGoldenFiles();
        using var output = new TempDirectory();
        var archivePath = ArchivePackaging.PackGolden(content.Root, output.Path);
        ArchivePackaging.AddZipEntry(archivePath, "smuggled/rogue.txt", new byte[] { 1, 2, 3 });

        var result = PackageArchiveVerifier.Verify(new PackageArchiveVerifyRequest(archivePath));

        result.Succeeded.Should().BeFalse("an unlisted archive entry is not covered by the signed manifest.");
        result.Diagnostics.Should().Contain(d => d.Code == PackageDiagnosticCode.FileUnlisted);
    }

    private static void TamperManifest(string archivePath)
    {
        var manifestBytes = ArchivePackaging.ReadZipEntry(archivePath, OpusPackageArchive.ManifestEntryName);
        var tampered = new byte[manifestBytes.Length + 1];
        manifestBytes.CopyTo(tampered, 0);
        tampered[^1] = (byte)'\n';
        ArchivePackaging.ReplaceZipEntry(archivePath, OpusPackageArchive.ManifestEntryName, tampered);
    }

    private static string Diagnostics(PackageArchiveVerificationResult result) =>
        "diagnostics: " + string.Join("; ", result.Diagnostics.Select(d => d.Code.Value + " " + d.Message));
}
