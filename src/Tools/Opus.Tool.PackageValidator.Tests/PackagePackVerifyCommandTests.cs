using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using FluentAssertions;
using Xunit;

namespace Opus.Tool.PackageValidator.Tests;

public sealed class PackagePackVerifyCommandTests
{
    [Fact]
    public void Pack_then_verify_round_trips_a_signed_package()
    {
        var root = Directory.CreateTempSubdirectory("opus-pack-cli-");
        try
        {
            var content = WriteContent(root.FullName);
            var (privatePem, publicPem) = WriteKeyPair(root.FullName);
            var archivePath = Path.Combine(root.FullName, "pkg.opkg");

            using var packOut = new StringWriter();
            using var packErr = new StringWriter();
            var packExit = PackageValidatorCommand.Run(
                PackArgs(content, archivePath, "--key", privatePem, "--key-id", "vellum-alpha"),
                packOut,
                packErr);

            packExit.Should().Be(PackageValidatorExitCodes.Success, packErr.ToString());
            packOut.ToString().Should().Contain("signed: yes");

            using var verifyOut = new StringWriter();
            using var verifyErr = new StringWriter();
            var verifyExit = PackageValidatorCommand.Run(
                new[] { "verify", archivePath, "--key", publicPem }, verifyOut, verifyErr);

            verifyExit.Should().Be(PackageValidatorExitCodes.Success, verifyErr.ToString());
            verifyOut.ToString().Should().Contain("signature verified: yes");
        }
        finally
        {
            root.Delete(recursive: true);
        }
    }

    [Fact]
    public void Pack_then_unpack_then_validate_pipeline_succeeds()
    {
        var root = Directory.CreateTempSubdirectory("opus-pack-cli-");
        try
        {
            var content = WriteContent(root.FullName);
            var archivePath = Path.Combine(root.FullName, "pkg.opkg");
            var unpackDirectory = Path.Combine(root.FullName, "unpacked");

            RunExpect(PackageValidatorExitCodes.Success, PackArgs(content, archivePath));
            RunExpect(PackageValidatorExitCodes.Success, "unpack", archivePath, unpackDirectory);
            RunExpect(PackageValidatorExitCodes.Success, "validate", unpackDirectory);
        }
        finally
        {
            root.Delete(recursive: true);
        }
    }

    [Fact]
    public void Pack_requires_identity_or_a_prebuilt_manifest()
    {
        var root = Directory.CreateTempSubdirectory("opus-pack-cli-");
        try
        {
            var content = WriteContent(root.FullName);
            using var stdout = new StringWriter();
            using var stderr = new StringWriter();

            var exit = PackageValidatorCommand.Run(
                new[] { "pack", content, "--output", Path.Combine(root.FullName, "pkg.opkg") },
                stdout,
                stderr);

            exit.Should().Be(PackageValidatorExitCodes.InvalidArguments);
            stderr.ToString().Should().Contain("pack requires --id, --name, and --version");
        }
        finally
        {
            root.Delete(recursive: true);
        }
    }

    [Fact]
    public void Verify_of_a_missing_archive_reports_archive_missing()
    {
        var root = Directory.CreateTempSubdirectory("opus-pack-cli-");
        try
        {
            using var stdout = new StringWriter();
            using var stderr = new StringWriter();

            var exit = PackageValidatorCommand.Run(
                new[] { "verify", Path.Combine(root.FullName, "nope.opkg") }, stdout, stderr);

            exit.Should().Be(PackageValidatorExitCodes.ValidationFailed);
            stderr.ToString().Should().Contain("OPKG-ARC-001");
        }
        finally
        {
            root.Delete(recursive: true);
        }
    }

    [Fact]
    public void Require_signature_without_a_key_is_an_argument_error()
    {
        using var stdout = new StringWriter();
        using var stderr = new StringWriter();

        var exit = PackageValidatorCommand.Run(
            new[] { "verify", "some.opkg", "--require-signature" }, stdout, stderr);

        exit.Should().Be(PackageValidatorExitCodes.InvalidArguments);
        stderr.ToString().Should().Contain("--require-signature needs --key");
    }

    [Fact]
    public void Verify_detects_a_tampered_payload_entry()
    {
        var root = Directory.CreateTempSubdirectory("opus-pack-cli-");
        try
        {
            var content = WriteContent(root.FullName);
            var archivePath = Path.Combine(root.FullName, "pkg.opkg");
            RunExpect(PackageValidatorExitCodes.Success, PackArgs(content, archivePath));
            ReplaceEntry(archivePath, "localisation/en.json", new byte[] { 9, 9, 9 });

            using var stdout = new StringWriter();
            using var stderr = new StringWriter();
            var exit = PackageValidatorCommand.Run(new[] { "verify", archivePath }, stdout, stderr);

            exit.Should().Be(PackageValidatorExitCodes.ValidationFailed);
            stderr.ToString().Should().Contain("OPKG-FILE-003");
        }
        finally
        {
            root.Delete(recursive: true);
        }
    }

    private static void RunExpect(int expected, params string[] args)
    {
        using var stdout = new StringWriter();
        using var stderr = new StringWriter();
        var exit = PackageValidatorCommand.Run(args, stdout, stderr);
        exit.Should().Be(
            expected,
            $"args [{string.Join(' ', args)}] stdout=[{stdout}] stderr=[{stderr}]");
    }

    private static string WriteContent(string root)
    {
        var content = Directory.CreateDirectory(Path.Combine(root, "content"));
        var localisation = Directory.CreateDirectory(Path.Combine(content.FullName, "localisation"));
        File.WriteAllText(Path.Combine(localisation.FullName, "en.json"), "{\"sample.title\":\"Opus\"}");
        return content.FullName;
    }

    private static (string PrivatePem, string PublicPem) WriteKeyPair(string root)
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var privatePem = Path.Combine(root, "key.pem");
        File.WriteAllText(privatePem, key.ExportPkcs8PrivateKeyPem());
        var publicPem = Path.Combine(root, "key.pub.pem");
        File.WriteAllText(publicPem, key.ExportSubjectPublicKeyInfoPem());
        return (privatePem, publicPem);
    }

    private static string[] PackArgs(string contentRoot, string output, params string[] extra)
    {
        var baseArgs = new[]
        {
            "pack",
            contentRoot,
            "--id",
            "vellum.opus.fixtures.cli",
            "--name",
            "Opus CLI Fixtures",
            "--version",
            "0.1.0-alpha.1",
            "--output",
            output,
        };
        return extra.Length == 0 ? baseArgs : baseArgs.Concat(extra).ToArray();
    }

    private static void ReplaceEntry(string archivePath, string entryName, byte[] bytes)
    {
        using var zip = ZipFile.Open(archivePath, ZipArchiveMode.Update);
        zip.GetEntry(entryName)?.Delete();
        var entry = zip.CreateEntry(entryName);
        using var stream = entry.Open();
        stream.Write(bytes, 0, bytes.Length);
    }
}
