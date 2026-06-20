using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using Opus.Content.Packaging.Diagnostics;
using Opus.Content.Packaging.Generation;
using Opus.Content.Packaging.Manifest;
using Opus.Content.Packaging.Validation;
using Opus.Foundation.IO;
using ContentPackageValidator = Opus.Content.Packaging.Validation.PackageValidator;

namespace Opus.Tool.PackageValidator;

/// <summary>
/// Handles the package tool's <c>generate</c> subcommand: it walks a content directory
/// through <see cref="PackageManifestGenerator"/> and writes the resulting
/// <c>opus.package.json</c>, so the same tool that validates a package can also author its
/// manifest. All output flows through caller-supplied writers so the command is
/// unit-testable, and the manifest is written atomically (temp + rename).
/// </summary>
internal static class PackageGenerateCommand
{
    private const string DefaultLocale = "en";
    private const string RussianLocale = "ru";
    private static readonly HashSet<string> SupportedLocales = new(StringComparer.Ordinal)
    {
        DefaultLocale,
        RussianLocale,
    };

    public static int Run(string[] args, TextWriter stdout, TextWriter stderr)
    {
        if (!TryParse(args, stderr, out var options))
        {
            return PackageValidatorExitCodes.InvalidArguments;
        }

        try
        {
            var request = new PackageGenerationRequest(
                options.ContentRoot,
                new ContentPackageInfo(options.Id, options.DisplayName, options.Version, options.CreatedAtUtc));
            var result = new PackageManifestGenerator().Generate(request);
            var localizer = PackageDiagnosticLocalizer.Load(options.Locale);
            if (!result.HasManifest)
            {
                ReportDiagnostics(result.Diagnostics, localizer, stderr);
                return PackageValidatorExitCodes.ToolFailure;
            }

            WriteManifestAtomic(options.OutputPath, ContentPackageManifestReader.Write(result.Manifest!));
            ReportSuccess(result, options.OutputPath, localizer, stdout);
            return PackageValidatorExitCodes.Success;
        }
        catch (IOException ex)
        {
            stderr.WriteLine($"Package generator IO failure: {ex.Message}");
            return PackageValidatorExitCodes.ToolFailure;
        }
        catch (UnauthorizedAccessException ex)
        {
            stderr.WriteLine($"Package generator access failure: {ex.Message}");
            return PackageValidatorExitCodes.ToolFailure;
        }
    }

    private static bool TryParse(string[] args, TextWriter stderr, out PackageGenerateOptions options)
    {
        options = new PackageGenerateOptions(
            ContentRoot: string.Empty,
            Id: string.Empty,
            DisplayName: string.Empty,
            Version: string.Empty,
            CreatedAtUtc: null,
            OutputPath: string.Empty,
            Locale: DefaultLocale);

        if (args.Length < 2)
        {
            WriteUsage(stderr);
            return false;
        }

        var contentRoot = args[1];
        if (string.IsNullOrWhiteSpace(contentRoot) || contentRoot.StartsWith("--", StringComparison.Ordinal))
        {
            stderr.WriteLine("Missing or invalid content root argument.");
            WriteUsage(stderr);
            return false;
        }

        string? id = null;
        string? displayName = null;
        string? version = null;
        string? createdAtUtc = null;
        string? output = null;
        var locale = DefaultLocale;
        for (var i = 2; i < args.Length; i++)
        {
            var arg = args[i];
            if (CliOptionReader.TryReadOption(arg, "--id", args, ref i, stderr, out var idValue))
            {
                if (idValue is null)
                {
                    return false;
                }

                id = idValue;
                continue;
            }

            if (CliOptionReader.TryReadOption(arg, "--name", args, ref i, stderr, out var nameValue))
            {
                if (nameValue is null)
                {
                    return false;
                }

                displayName = nameValue;
                continue;
            }

            if (CliOptionReader.TryReadOption(arg, "--version", args, ref i, stderr, out var versionValue))
            {
                if (versionValue is null)
                {
                    return false;
                }

                version = versionValue;
                continue;
            }

            if (CliOptionReader.TryReadOption(arg, "--created", args, ref i, stderr, out var createdValue))
            {
                if (createdValue is null)
                {
                    return false;
                }

                createdAtUtc = createdValue;
                continue;
            }

            if (CliOptionReader.TryReadOption(arg, "--output", args, ref i, stderr, out var outputValue))
            {
                if (outputValue is null)
                {
                    return false;
                }

                output = outputValue;
                continue;
            }

            if (CliOptionReader.TryReadOption(arg, "--locale", args, ref i, stderr, out var localeValue))
            {
                if (localeValue is null)
                {
                    return false;
                }

                if (!SupportedLocales.Contains(localeValue))
                {
                    stderr.WriteLine($"Unsupported --locale '{localeValue}'. Use {string.Join('|', SupportedLocales)}.");
                    return false;
                }

                locale = localeValue;
                continue;
            }

            stderr.WriteLine($"Unknown argument: {arg}");
            WriteUsage(stderr);
            return false;
        }

        if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(displayName) || string.IsNullOrWhiteSpace(version))
        {
            stderr.WriteLine("generate requires --id, --name, and --version.");
            WriteUsage(stderr);
            return false;
        }

        var outputPath = string.IsNullOrWhiteSpace(output)
            ? Path.Combine(contentRoot, ContentPackageValidator.DefaultManifestFileName)
            : output;
        options = new PackageGenerateOptions(contentRoot, id, displayName, version, createdAtUtc, outputPath, locale);
        return true;
    }

    private static void WriteManifestAtomic(string finalPath, string content)
    {
        AtomicFile.WriteAllText(finalPath, content);
    }

    private static void ReportSuccess(
        PackageGenerationResult result,
        string outputPath,
        PackageDiagnosticLocalizer localizer,
        TextWriter stdout)
    {
        var manifest = result.Manifest!;
        var warnings = result.Diagnostics.Count(d => d.Severity == PackageDiagnosticSeverity.Warning);
        stdout.WriteLine($"Package manifest generated: {outputPath}");
        stdout.WriteLine(string.Create(
            CultureInfo.InvariantCulture,
            $"files={manifest.Files.Count} warnings={warnings}"));
        ReportDiagnostics(result.Diagnostics, localizer, stdout);
    }

    private static void ReportDiagnostics(
        IReadOnlyList<PackageDiagnostic> diagnostics,
        PackageDiagnosticLocalizer localizer,
        TextWriter writer)
    {
        foreach (var diagnostic in diagnostics)
        {
            var target = diagnostic.Target.Path ?? diagnostic.Target.Kind.ToString().ToLowerInvariant();
            writer.WriteLine(string.Create(
                CultureInfo.InvariantCulture,
                $"{diagnostic.Severity} {diagnostic.Code} {target}"));
            writer.WriteLine($"  {localizer.Message(diagnostic)}");
        }
    }

    private static void WriteUsage(TextWriter stderr)
    {
        stderr.WriteLine("Usage: Opus.Tool.PackageValidator generate <content-root> --id <id> --name <display-name> --version <semver> [--created <iso-utc>] [--output <path>] [--locale en|ru]");
    }
}
