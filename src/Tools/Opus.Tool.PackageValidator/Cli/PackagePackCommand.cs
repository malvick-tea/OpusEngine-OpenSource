using System.Security.Cryptography;
using Opus.Content.Packaging.Archive;
using Opus.Content.Packaging.Generation;
using Opus.Content.Packaging.Manifest;
using ContentPackageValidator = Opus.Content.Packaging.Validation.PackageValidator;

namespace Opus.Tool.PackageValidator;

/// <summary>
/// Handles the package tool's <c>pack</c> subcommand: it resolves a manifest (generated from the
/// content directory, or loaded from <c>--manifest</c>), optionally signs it with a caller-supplied
/// PEM key, and writes a <c>.opkg</c> archive through <see cref="PackageArchivePacker"/>. All output
/// flows through caller-supplied writers so the command is unit-testable.
/// </summary>
internal static class PackagePackCommand
{
    public static int Run(string[] args, TextWriter stdout, TextWriter stderr)
    {
        if (!TryParse(args, stderr, out var options))
        {
            return PackageValidatorExitCodes.InvalidArguments;
        }

        try
        {
            var localizer = PackageDiagnosticLocalizer.Load(options.Locale);
            if (!TryResolveManifest(options, localizer, stderr, out var manifest))
            {
                return PackageValidatorExitCodes.ToolFailure;
            }

            ECDsa? signingKey = null;
            if (options.KeyPath is not null
                && !PackagePemKeys.TryLoad(options.KeyPath, out signingKey, out var keyError))
            {
                stderr.WriteLine($"Failed to load signing key: {keyError}");
                return PackageValidatorExitCodes.ToolFailure;
            }

            using (signingKey)
            {
                var request = new PackageArchivePackRequest(options.ContentRoot, manifest!, options.OutputPath)
                {
                    SigningKey = signingKey,
                    SigningKeyId = options.KeyId,
                };
                var result = PackageArchivePacker.Pack(request);
                CliDiagnosticReporter.Report(
                    result.Diagnostics, localizer, result.Succeeded ? stdout : stderr);
                if (!result.Succeeded)
                {
                    return PackageValidatorExitCodes.ToolFailure;
                }

                stdout.WriteLine(
                    $"Package packed: {result.ArchivePath} (signed: {(result.IsSigned ? "yes" : "no")})");
                return PackageValidatorExitCodes.Success;
            }
        }
        catch (IOException ex)
        {
            stderr.WriteLine($"Package pack IO failure: {ex.Message}");
            return PackageValidatorExitCodes.ToolFailure;
        }
        catch (UnauthorizedAccessException ex)
        {
            stderr.WriteLine($"Package pack access failure: {ex.Message}");
            return PackageValidatorExitCodes.ToolFailure;
        }
    }

    private static bool TryResolveManifest(
        PackagePackOptions options,
        PackageDiagnosticLocalizer localizer,
        TextWriter stderr,
        out ContentPackageManifest? manifest)
    {
        manifest = null;
        if (options.ManifestPath is not null)
        {
            if (!File.Exists(options.ManifestPath))
            {
                stderr.WriteLine($"Manifest '{options.ManifestPath}' does not exist.");
                return false;
            }

            using var stream = File.OpenRead(options.ManifestPath);
            var read = ContentPackageManifestReader.Read(stream);
            if (!read.IsOk)
            {
                stderr.WriteLine($"Manifest is malformed: {read.UnwrapErr().Message}");
                return false;
            }

            manifest = read.Unwrap();
            return true;
        }

        var generation = new PackageManifestGenerator().Generate(new PackageGenerationRequest(
            options.ContentRoot,
            new ContentPackageInfo(options.Id!, options.DisplayName!, options.Version!, options.CreatedAtUtc)));
        if (!generation.HasManifest)
        {
            CliDiagnosticReporter.Report(generation.Diagnostics, localizer, stderr);
            return false;
        }

        manifest = generation.Manifest;
        return true;
    }

    private static bool TryParse(string[] args, TextWriter stderr, out PackagePackOptions options)
    {
        options = PackagePackOptions.Empty;
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

        var parsed = new PackArguments();
        for (var i = 2; i < args.Length; i++)
        {
            if (!TryReadArgument(args, ref i, stderr, parsed, out var recognised))
            {
                return false;
            }

            if (!recognised)
            {
                stderr.WriteLine($"Unknown argument: {args[i]}");
                WriteUsage(stderr);
                return false;
            }
        }

        return TryBuildOptions(contentRoot, parsed, stderr, out options);
    }

    private static bool TryReadArgument(
        string[] args, ref int i, TextWriter stderr, PackArguments parsed, out bool recognised)
    {
        recognised = true;
        var arg = args[i];
        if (CliOptionReader.TryReadOption(arg, "--id", args, ref i, stderr, out var idValue))
        {
            parsed.Id = idValue;
            return idValue is not null;
        }

        if (CliOptionReader.TryReadOption(arg, "--name", args, ref i, stderr, out var nameValue))
        {
            parsed.DisplayName = nameValue;
            return nameValue is not null;
        }

        if (CliOptionReader.TryReadOption(arg, "--version", args, ref i, stderr, out var versionValue))
        {
            parsed.Version = versionValue;
            return versionValue is not null;
        }

        if (CliOptionReader.TryReadOption(arg, "--created", args, ref i, stderr, out var createdValue))
        {
            parsed.CreatedAtUtc = createdValue;
            return createdValue is not null;
        }

        if (CliOptionReader.TryReadOption(arg, "--output", args, ref i, stderr, out var outputValue))
        {
            parsed.Output = outputValue;
            return outputValue is not null;
        }

        if (CliOptionReader.TryReadOption(arg, "--manifest", args, ref i, stderr, out var manifestValue))
        {
            parsed.ManifestPath = manifestValue;
            return manifestValue is not null;
        }

        if (CliOptionReader.TryReadOption(arg, "--key", args, ref i, stderr, out var keyValue))
        {
            parsed.KeyPath = keyValue;
            return keyValue is not null;
        }

        if (CliOptionReader.TryReadOption(arg, "--key-id", args, ref i, stderr, out var keyIdValue))
        {
            parsed.KeyId = keyIdValue;
            return keyIdValue is not null;
        }

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

            parsed.Locale = localeValue;
            return true;
        }

        recognised = false;
        return true;
    }

    private static bool TryBuildOptions(
        string contentRoot, PackArguments parsed, TextWriter stderr, out PackagePackOptions options)
    {
        options = PackagePackOptions.Empty;
        if (parsed.ManifestPath is null
            && (string.IsNullOrWhiteSpace(parsed.Id)
                || string.IsNullOrWhiteSpace(parsed.DisplayName)
                || string.IsNullOrWhiteSpace(parsed.Version)))
        {
            stderr.WriteLine("pack requires --id, --name, and --version (or a prebuilt --manifest).");
            WriteUsage(stderr);
            return false;
        }

        if ((parsed.KeyPath is null) != (parsed.KeyId is null))
        {
            stderr.WriteLine("Signing requires both --key <private-key.pem> and --key-id <id>.");
            WriteUsage(stderr);
            return false;
        }

        var outputPath = string.IsNullOrWhiteSpace(parsed.Output)
            ? contentRoot.TrimEnd('/', '\\') + OpusPackageArchive.FileExtension
            : parsed.Output;
        options = new PackagePackOptions(
            contentRoot, parsed.Id, parsed.DisplayName, parsed.Version, parsed.CreatedAtUtc,
            outputPath, parsed.ManifestPath, parsed.KeyPath, parsed.KeyId, parsed.Locale);
        return true;
    }

    private static void WriteUsage(TextWriter stderr) =>
        stderr.WriteLine(
            "Usage: Opus.Tool.PackageValidator pack <content-root> [--id <id> --name <display-name> --version <semver>] [--created <iso-utc>] [--manifest <path>] [--output <path.opkg>] [--key <private-key.pem> --key-id <id>] [--locale en|ru]");

    private sealed class PackArguments
    {
        public string? Id { get; set; }

        public string? DisplayName { get; set; }

        public string? Version { get; set; }

        public string? CreatedAtUtc { get; set; }

        public string? Output { get; set; }

        public string? ManifestPath { get; set; }

        public string? KeyPath { get; set; }

        public string? KeyId { get; set; }

        public string Locale { get; set; } = CliLocales.Default;
    }

    private sealed record PackagePackOptions(
        string ContentRoot,
        string? Id,
        string? DisplayName,
        string? Version,
        string? CreatedAtUtc,
        string OutputPath,
        string? ManifestPath,
        string? KeyPath,
        string? KeyId,
        string Locale)
    {
        public static PackagePackOptions Empty { get; } = new(
            string.Empty, null, null, null, null, string.Empty, null, null, null, CliLocales.Default);
    }
}
