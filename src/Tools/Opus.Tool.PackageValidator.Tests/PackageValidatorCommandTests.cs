using System.IO;
using FluentAssertions;
using Xunit;

namespace Opus.Tool.PackageValidator.Tests;

public sealed class PackageValidatorCommandTests
{
    [Fact]
    public void Run_rejects_missing_arguments()
    {
        using var stdout = new StringWriter();
        using var stderr = new StringWriter();

        var exitCode = PackageValidatorCommand.Run(Array.Empty<string>(), stdout, stderr);

        exitCode.Should().Be(PackageValidatorExitCodes.InvalidArguments);
        stderr.ToString().Should().Contain("Usage:");
    }

    [Fact]
    public void Run_rejects_unknown_subcommand()
    {
        using var stdout = new StringWriter();
        using var stderr = new StringWriter();

        var exitCode = PackageValidatorCommand.Run(
            new[] { "frobnicate", "irrelevant" },
            stdout,
            stderr);

        exitCode.Should().Be(PackageValidatorExitCodes.InvalidArguments);
        stderr.ToString().Should().Contain("Usage:");
    }

    [Fact]
    public void Run_rejects_unknown_option()
    {
        using var stdout = new StringWriter();
        using var stderr = new StringWriter();

        var exitCode = PackageValidatorCommand.Run(
            new[] { "validate", ".", "--turbo" },
            stdout,
            stderr);

        exitCode.Should().Be(PackageValidatorExitCodes.InvalidArguments);
        stderr.ToString().Should().Contain("Unknown argument: --turbo");
    }

    [Fact]
    public void Run_rejects_invalid_format_value()
    {
        using var stdout = new StringWriter();
        using var stderr = new StringWriter();

        var exitCode = PackageValidatorCommand.Run(
            new[] { "validate", ".", "--format", "yaml" },
            stdout,
            stderr);

        exitCode.Should().Be(PackageValidatorExitCodes.InvalidArguments);
        stderr.ToString().Should().Contain("Unsupported --format");
    }

    [Fact]
    public void Run_rejects_invalid_locale()
    {
        using var stdout = new StringWriter();
        using var stderr = new StringWriter();

        var exitCode = PackageValidatorCommand.Run(
            new[] { "validate", ".", "--locale", "kl" },
            stdout,
            stderr);

        exitCode.Should().Be(PackageValidatorExitCodes.InvalidArguments);
        stderr.ToString().Should().Contain("Unsupported --locale");
    }

    [Fact]
    public void Run_rejects_invalid_unlisted_policy()
    {
        using var stdout = new StringWriter();
        using var stderr = new StringWriter();

        var exitCode = PackageValidatorCommand.Run(
            new[] { "validate", ".", "--unlisted", "panic" },
            stdout,
            stderr);

        exitCode.Should().Be(PackageValidatorExitCodes.InvalidArguments);
        stderr.ToString().Should().Contain("Unsupported --unlisted");
    }

    [Fact]
    public void Run_rejects_missing_option_value()
    {
        using var stdout = new StringWriter();
        using var stderr = new StringWriter();

        var exitCode = PackageValidatorCommand.Run(
            new[] { "validate", ".", "--format" },
            stdout,
            stderr);

        exitCode.Should().Be(PackageValidatorExitCodes.InvalidArguments);
        stderr.ToString().Should().Contain("Missing value for --format");
    }

    [Fact]
    public void Run_rejects_missing_option_value_followed_by_another_option()
    {
        using var stdout = new StringWriter();
        using var stderr = new StringWriter();

        var exitCode = PackageValidatorCommand.Run(
            new[] { "validate", ".", "--format", "--locale", "en" },
            stdout,
            stderr);

        exitCode.Should().Be(PackageValidatorExitCodes.InvalidArguments);
        stderr.ToString().Should().Contain("Missing value for --format");
    }

    [Fact]
    public void Run_returns_validation_failed_for_missing_manifest()
    {
        var directory = Directory.CreateTempSubdirectory("opus-package-cli-");
        try
        {
            using var stdout = new StringWriter();
            using var stderr = new StringWriter();

            var exitCode = PackageValidatorCommand.Run(
                new[] { "validate", directory.FullName },
                stdout,
                stderr);

            exitCode.Should().Be(PackageValidatorExitCodes.ValidationFailed);
            stdout.ToString().Should().Contain("Package validation failed.");
            stdout.ToString().Should().Contain("OPKG-MAN-001");
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    [Fact]
    public void Run_can_emit_json_report_with_arguments_dictionary()
    {
        var directory = Directory.CreateTempSubdirectory("opus-package-cli-json-");
        try
        {
            using var stdout = new StringWriter();
            using var stderr = new StringWriter();

            var exitCode = PackageValidatorCommand.Run(
                new[] { "validate", directory.FullName, "--format", "json" },
                stdout,
                stderr);

            exitCode.Should().Be(PackageValidatorExitCodes.ValidationFailed);
            stdout.ToString().Should().Contain("\"valid\": false");
            stdout.ToString().Should().Contain("\"code\": \"OPKG-MAN-001\"");
            stdout.ToString().Should().Contain("\"arguments\":");
            stdout.ToString().Should().Contain("\"manifest\":");
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    [Fact]
    public void Run_uses_requested_locale_when_catalog_is_available()
    {
        var directory = Directory.CreateTempSubdirectory("opus-package-cli-ru-");
        try
        {
            using var stdout = new StringWriter();
            using var stderr = new StringWriter();

            var exitCode = PackageValidatorCommand.Run(
                new[] { "validate", directory.FullName, "--locale", "ru" },
                stdout,
                stderr);

            exitCode.Should().Be(PackageValidatorExitCodes.ValidationFailed);
            stdout.ToString().Should().Contain("Манифест пакета");
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    [Fact]
    public void Run_reports_package_root_missing_when_directory_does_not_exist()
    {
        var nonExistent = Path.Combine(Path.GetTempPath(), $"opus-cli-missing-{Guid.NewGuid():N}");
        using var stdout = new StringWriter();
        using var stderr = new StringWriter();

        var exitCode = PackageValidatorCommand.Run(
            new[] { "validate", nonExistent },
            stdout,
            stderr);

        exitCode.Should().Be(PackageValidatorExitCodes.ValidationFailed);
        stdout.ToString().Should().Contain("OPKG-PKG-001");
    }

    [Theory]
    [InlineData("lots")]
    [InlineData("0")]
    [InlineData("-1")]
    [InlineData("2147483648")]
    public void Run_rejects_an_out_of_range_max_deep_validation_bytes(string value)
    {
        using var stdout = new StringWriter();
        using var stderr = new StringWriter();

        var exitCode = PackageValidatorCommand.Run(
            new[] { "validate", ".", "--max-deep-validation-bytes", value },
            stdout,
            stderr);

        exitCode.Should().Be(PackageValidatorExitCodes.InvalidArguments);
        stderr.ToString().Should().Contain("Unsupported --max-deep-validation-bytes");
    }

    [Fact]
    public void Run_skips_deep_validation_above_the_configured_budget_but_still_succeeds()
    {
        var directory = CreateGeneratedPackageDirectory();
        try
        {
            using var stdout = new StringWriter();
            using var stderr = new StringWriter();

            var exitCode = PackageValidatorCommand.Run(
                new[] { "validate", directory.FullName, "--max-deep-validation-bytes", "1" },
                stdout,
                stderr);

            var because = "integrity is streamed, so a file above the budget is a warning, not an error; output: "
                + stdout.ToString();
            exitCode.Should().Be(PackageValidatorExitCodes.Success, because);
            stdout.ToString().Should().Contain("OPKG-FILE-007");
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    [Fact]
    public void Run_default_budget_deep_validates_small_files_without_a_budget_warning()
    {
        var directory = CreateGeneratedPackageDirectory();
        try
        {
            using var stdout = new StringWriter();
            using var stderr = new StringWriter();

            var exitCode = PackageValidatorCommand.Run(
                new[] { "validate", directory.FullName },
                stdout,
                stderr);

            exitCode.Should().Be(PackageValidatorExitCodes.Success);
            stdout.ToString().Should().NotContain("OPKG-FILE-007");
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    private static DirectoryInfo CreateGeneratedPackageDirectory()
    {
        var directory = Directory.CreateTempSubdirectory("opus-package-cli-budget-");
        var localisation = Directory.CreateDirectory(Path.Combine(directory.FullName, "localisation"));
        File.WriteAllText(Path.Combine(localisation.FullName, "en.json"), "{\"sample.title\":\"Opus\"}");

        using var stdout = new StringWriter();
        using var stderr = new StringWriter();
        var generateExit = PackageValidatorCommand.Run(
            new[]
            {
                "generate",
                directory.FullName,
                "--id",
                "vellum.opus.budget.cli",
                "--name",
                "Opus Budget Fixtures",
                "--version",
                "0.1.0-alpha.1",
            },
            stdout,
            stderr);
        generateExit.Should().Be(PackageValidatorExitCodes.Success, stderr.ToString());
        return directory;
    }
}
