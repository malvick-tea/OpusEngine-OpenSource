using System;
using System.IO;
using System.Linq;
using FluentAssertions;
using Xunit;

namespace Opus.Tool.PackageValidator.Tests;

public sealed class PackageGenerateCommandTests
{
    private const string ManifestFileName = "opus.package.json";

    [Fact]
    public void Generate_then_validate_round_trips_clean_via_the_cli()
    {
        var directory = CreateContentDirectory();
        try
        {
            using var stdout = new StringWriter();
            using var stderr = new StringWriter();

            var generateExit = PackageValidatorCommand.Run(
                GenerateArgs(directory.FullName),
                stdout,
                stderr);

            generateExit.Should().Be(PackageValidatorExitCodes.Success);
            stdout.ToString().Should().Contain("Package manifest generated:");
            File.Exists(Path.Combine(directory.FullName, ManifestFileName)).Should().BeTrue();

            using var validateOut = new StringWriter();
            using var validateErr = new StringWriter();
            var validateExit = PackageValidatorCommand.Run(
                new[] { "validate", directory.FullName },
                validateOut,
                validateErr);

            var because = "a CLI-generated manifest must validate clean through the same tool; output: "
                + validateOut.ToString();
            validateExit.Should().Be(PackageValidatorExitCodes.Success, because);
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    [Fact]
    public void Generate_writes_manifest_to_a_custom_output_path()
    {
        var directory = CreateContentDirectory();
        try
        {
            var customPath = Path.Combine(directory.FullName, "out", "custom.package.json");
            using var stdout = new StringWriter();
            using var stderr = new StringWriter();

            var exit = PackageValidatorCommand.Run(
                GenerateArgs(directory.FullName, "--output", customPath),
                stdout,
                stderr);

            exit.Should().Be(PackageValidatorExitCodes.Success);
            File.Exists(customPath).Should().BeTrue();
            Directory.GetFiles(directory.FullName, ManifestFileName).Should().BeEmpty(
                "an explicit --output path must not also drop a manifest at the default location.");
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    [Fact]
    public void Generate_requires_identity_options()
    {
        var directory = CreateContentDirectory();
        try
        {
            using var stdout = new StringWriter();
            using var stderr = new StringWriter();

            var exit = PackageValidatorCommand.Run(
                new[] { "generate", directory.FullName, "--name", "X", "--version", "0.1.0-alpha" },
                stdout,
                stderr);

            exit.Should().Be(PackageValidatorExitCodes.InvalidArguments);
            stderr.ToString().Should().Contain("generate requires --id, --name, and --version.");
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    [Fact]
    public void Generate_reports_a_missing_content_root()
    {
        var missing = Path.Combine(Path.GetTempPath(), $"opus-generate-missing-{Guid.NewGuid():N}");
        using var stdout = new StringWriter();
        using var stderr = new StringWriter();

        var exit = PackageValidatorCommand.Run(GenerateArgs(missing), stdout, stderr);

        exit.Should().Be(PackageValidatorExitCodes.ToolFailure);
        stderr.ToString().Should().Contain("OPKG-PKG-001");
    }

    [Fact]
    public void Generate_warns_about_an_unclassifiable_file_but_still_succeeds()
    {
        var directory = CreateContentDirectory();
        try
        {
            File.WriteAllText(Path.Combine(directory.FullName, "scratch.unknownext"), "junk");
            using var stdout = new StringWriter();
            using var stderr = new StringWriter();

            var exit = PackageValidatorCommand.Run(GenerateArgs(directory.FullName), stdout, stderr);

            exit.Should().Be(PackageValidatorExitCodes.Success);
            stdout.ToString().Should().Contain("OPKG-GEN-001");
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    private static DirectoryInfo CreateContentDirectory()
    {
        var directory = Directory.CreateTempSubdirectory("opus-generate-cli-");
        var localisation = Directory.CreateDirectory(Path.Combine(directory.FullName, "localisation"));
        File.WriteAllText(Path.Combine(localisation.FullName, "en.json"), "{\"sample.title\":\"Opus\"}");
        return directory;
    }

    private static string[] GenerateArgs(string contentRoot, params string[] extra)
    {
        var baseArgs = new[]
        {
            "generate",
            contentRoot,
            "--id",
            "vellum.opus.fixtures.cli",
            "--name",
            "Opus CLI Fixtures",
            "--version",
            "0.1.0-alpha.1",
        };
        return extra.Length == 0 ? baseArgs : baseArgs.Concat(extra).ToArray();
    }
}
