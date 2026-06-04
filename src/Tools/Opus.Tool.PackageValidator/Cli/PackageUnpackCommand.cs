using Opus.Content.Packaging.Archive;

namespace Opus.Tool.PackageValidator;

/// <summary>
/// Handles the package tool's <c>unpack</c> subcommand: it safely extracts a <c>.opkg</c> archive
/// to a directory through <see cref="OpusPackageExtractor"/> (structural bounds + zip-slip guard),
/// so the unpacked tree can then be inspected or run through <c>validate</c> for deep asset checks.
/// </summary>
internal static class PackageUnpackCommand
{
    public static int Run(string[] args, TextWriter stdout, TextWriter stderr)
    {
        if (!TryParse(args, stderr, out var options))
        {
            return PackageValidatorExitCodes.InvalidArguments;
        }

        try
        {
            var result = OpusPackageExtractor.Extract(
                options.ArchivePath, options.TargetDirectory, OpusPackageArchiveLimits.Default);
            var localizer = PackageDiagnosticLocalizer.Load(options.Locale);
            CliDiagnosticReporter.Report(result.Diagnostics, localizer, result.Succeeded ? stdout : stderr);
            if (!result.Succeeded)
            {
                return PackageValidatorExitCodes.ValidationFailed;
            }

            stdout.WriteLine(
                $"Package unpacked: {options.ArchivePath} -> {Path.GetFullPath(options.TargetDirectory)}");
            return PackageValidatorExitCodes.Success;
        }
        catch (IOException ex)
        {
            stderr.WriteLine($"Package unpack IO failure: {ex.Message}");
            return PackageValidatorExitCodes.ToolFailure;
        }
        catch (UnauthorizedAccessException ex)
        {
            stderr.WriteLine($"Package unpack access failure: {ex.Message}");
            return PackageValidatorExitCodes.ToolFailure;
        }
    }

    private static bool TryParse(string[] args, TextWriter stderr, out UnpackOptions options)
    {
        options = new UnpackOptions(string.Empty, string.Empty, CliLocales.Default);
        if (args.Length < 3)
        {
            WriteUsage(stderr);
            return false;
        }

        var archivePath = args[1];
        var targetDirectory = args[2];
        if (string.IsNullOrWhiteSpace(archivePath) || archivePath.StartsWith("--", StringComparison.Ordinal)
            || string.IsNullOrWhiteSpace(targetDirectory) || targetDirectory.StartsWith("--", StringComparison.Ordinal))
        {
            stderr.WriteLine("unpack requires <package.opkg> and <target-dir> arguments.");
            WriteUsage(stderr);
            return false;
        }

        var locale = CliLocales.Default;
        for (var i = 3; i < args.Length; i++)
        {
            var arg = args[i];
            if (CliOptionReader.TryReadOption(arg, "--locale", args, ref i, stderr, out var localeValue))
            {
                if (localeValue is null)
                {
                    return false;
                }

                if (!CliLocales.IsSupported(localeValue))
                {
                    stderr.WriteLine($"Unsupported --locale '{localeValue}'. Use {CliLocales.SupportedList}.");
                    return false;
                }

                locale = localeValue;
                continue;
            }

            stderr.WriteLine($"Unknown argument: {arg}");
            WriteUsage(stderr);
            return false;
        }

        options = new UnpackOptions(archivePath, targetDirectory, locale);
        return true;
    }

    private static void WriteUsage(TextWriter stderr) =>
        stderr.WriteLine(
            "Usage: Opus.Tool.PackageValidator unpack <package.opkg> <target-dir> [--locale en|ru]");

    private sealed record UnpackOptions(string ArchivePath, string TargetDirectory, string Locale);
}
