using System.Security.Cryptography;
using Opus.Content.Packaging.Archive;

namespace Opus.Tool.PackageValidator;

/// <summary>
/// Handles the package tool's <c>verify</c> subcommand: it checks a <c>.opkg</c> archive's
/// structure and integrity, and — when a public key is supplied — its signature, through
/// <see cref="PackageArchiveVerifier"/>. Exit code mirrors <c>validate</c>: <c>Success</c> when the
/// package is trusted, <c>ValidationFailed</c> when it has errors.
/// </summary>
internal static class PackageVerifyCommand
{
    public static int Run(string[] args, TextWriter stdout, TextWriter stderr)
    {
        if (!TryParse(args, stderr, out var options))
        {
            return PackageValidatorExitCodes.InvalidArguments;
        }

        try
        {
            ECDsa? publicKey = null;
            if (options.KeyPath is not null
                && !PackagePemKeys.TryLoad(options.KeyPath, out publicKey, out var keyError))
            {
                stderr.WriteLine($"Failed to load public key: {keyError}");
                return PackageValidatorExitCodes.ToolFailure;
            }

            using (publicKey)
            {
                var request = new PackageArchiveVerifyRequest(options.ArchivePath)
                {
                    PublicKey = publicKey,
                    RequireSignature = !options.IntegrityOnly,
                };
                var result = PackageArchiveVerifier.Verify(request);
                var localizer = PackageDiagnosticLocalizer.Load(options.Locale);
                CliDiagnosticReporter.Report(
                    result.Diagnostics, localizer, result.Succeeded ? stdout : stderr);
                stdout.WriteLine(
                    $"Package verification: {(result.Succeeded ? "passed" : "failed")} (signature verified: {(result.SignatureVerified ? "yes" : "no")})");
                return result.Succeeded
                    ? PackageValidatorExitCodes.Success
                    : PackageValidatorExitCodes.ValidationFailed;
            }
        }
        catch (IOException ex)
        {
            stderr.WriteLine($"Package verify IO failure: {ex.Message}");
            return PackageValidatorExitCodes.ToolFailure;
        }
        catch (UnauthorizedAccessException ex)
        {
            stderr.WriteLine($"Package verify access failure: {ex.Message}");
            return PackageValidatorExitCodes.ToolFailure;
        }
    }

    private static bool TryParse(string[] args, TextWriter stderr, out VerifyOptions options)
    {
        options = new VerifyOptions(string.Empty, null, IntegrityOnly: false, CliLocales.Default);
        if (args.Length < 2)
        {
            WriteUsage(stderr);
            return false;
        }

        var archivePath = args[1];
        if (string.IsNullOrWhiteSpace(archivePath) || archivePath.StartsWith("--", StringComparison.Ordinal))
        {
            stderr.WriteLine("Missing or invalid archive path argument.");
            WriteUsage(stderr);
            return false;
        }

        string? keyPath = null;
        var integrityOnly = false;
        var locale = CliLocales.Default;
        for (var i = 2; i < args.Length; i++)
        {
            var arg = args[i];
            if (CliOptionReader.TryReadOption(arg, "--key", args, ref i, stderr, out var keyValue))
            {
                if (keyValue is null)
                {
                    return false;
                }

                keyPath = keyValue;
                continue;
            }

            if (string.Equals(arg, "--require-signature", StringComparison.Ordinal))
            {
                continue;
            }

            if (string.Equals(arg, "--integrity-only", StringComparison.Ordinal))
            {
                integrityOnly = true;
                continue;
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

                locale = localeValue;
                continue;
            }

            stderr.WriteLine($"Unknown argument: {arg}");
            WriteUsage(stderr);
            return false;
        }

        if (integrityOnly && keyPath is not null)
        {
            stderr.WriteLine("--integrity-only cannot be combined with --key.");
            WriteUsage(stderr);
            return false;
        }

        if (!integrityOnly && keyPath is null)
        {
            stderr.WriteLine(
                "verify requires --key <trusted-public-key.pem>; use --integrity-only only for non-executable development artifacts.");
            WriteUsage(stderr);
            return false;
        }

        options = new VerifyOptions(archivePath, keyPath, integrityOnly, locale);
        return true;
    }

    private static void WriteUsage(TextWriter stderr) =>
        stderr.WriteLine(
            "Usage: Opus.Tool.PackageValidator verify <package.opkg> (--key <trusted-public-key.pem> | --integrity-only) [--locale en|ru]");

    private sealed record VerifyOptions(
        string ArchivePath, string? KeyPath, bool IntegrityOnly, string Locale);
}
