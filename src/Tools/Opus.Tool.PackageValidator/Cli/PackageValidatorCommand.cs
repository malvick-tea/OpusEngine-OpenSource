using System.Collections.Generic;
using System.Globalization;
using Opus.Content.Packaging.Validation;
using Opus.Tool.PackageValidator.Reporting;
using ContentPackageValidator = Opus.Content.Packaging.Validation.PackageValidator;

namespace Opus.Tool.PackageValidator;

/// <summary>
/// Command dispatcher for the Opus package validator tool. Owns argument parsing, the
/// stable exit-code matrix, and the failure-mode classification (argument problem vs
/// validation failure vs tool/IO failure). All output goes through caller-supplied
/// writers so the tool stays unit-testable.
/// </summary>
public static class PackageValidatorCommand
{
    private const string ValidateCommand = "validate";
    private const string GenerateCommand = "generate";
    private const string PackCommand = "pack";
    private const string VerifyCommand = "verify";
    private const string UnpackCommand = "unpack";
    private const string MaxDeepValidationBytesOption = "--max-deep-validation-bytes";
    private const string TextFormat = "text";
    private const string JsonFormat = "json";
    private const string DefaultLocale = "en";
    private const string RussianLocale = "ru";

    private static readonly HashSet<string> SupportedFormats = new(StringComparer.Ordinal)
    {
        TextFormat,
        JsonFormat,
    };

    private static readonly HashSet<string> SupportedLocales = new(StringComparer.Ordinal)
    {
        DefaultLocale,
        RussianLocale,
    };

    /// <summary>
    /// Runs the command with test-friendly writers. Returns one of
    /// <see cref="PackageValidatorExitCodes"/>: <c>Success</c> on a clean validation,
    /// <c>ValidationFailed</c> when the package itself has errors, <c>InvalidArguments</c>
    /// on a CLI usage mistake, <c>ToolFailure</c> on an unexpected IO/runtime failure.
    /// </summary>
    public static int Run(string[] args, TextWriter stdout, TextWriter stderr)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(stdout);
        ArgumentNullException.ThrowIfNull(stderr);

        if (args.Length > 0)
        {
            switch (args[0])
            {
                case GenerateCommand:
                    return PackageGenerateCommand.Run(args, stdout, stderr);
                case PackCommand:
                    return PackagePackCommand.Run(args, stdout, stderr);
                case VerifyCommand:
                    return PackageVerifyCommand.Run(args, stdout, stderr);
                case UnpackCommand:
                    return PackageUnpackCommand.Run(args, stdout, stderr);
            }
        }

        if (!TryParse(args, stderr, out var options))
        {
            return PackageValidatorExitCodes.InvalidArguments;
        }

        try
        {
            var request = new PackageValidationRequest(
                options.PackageRoot,
                UnlistedFilePolicy: options.UnlistedFilePolicy,
                MaxDeepValidationBytes: options.MaxDeepValidationBytes);
            var result = new ContentPackageValidator().ValidateDirectory(request);
            var localizer = PackageDiagnosticLocalizer.Load(options.Locale);
            IPackageValidationReporter reporter = options.Format == JsonFormat
                ? new JsonPackageValidationReporter(localizer)
                : new TextPackageValidationReporter(localizer);
            reporter.Write(result, stdout);
            return result.IsValid
                ? PackageValidatorExitCodes.Success
                : PackageValidatorExitCodes.ValidationFailed;
        }
        catch (ArgumentException ex)
        {
            stderr.WriteLine($"Package validator argument failure: {ex.Message}");
            return PackageValidatorExitCodes.InvalidArguments;
        }
        catch (IOException ex)
        {
            stderr.WriteLine($"Package validator IO failure: {ex.Message}");
            return PackageValidatorExitCodes.ToolFailure;
        }
        catch (UnauthorizedAccessException ex)
        {
            stderr.WriteLine($"Package validator access failure: {ex.Message}");
            return PackageValidatorExitCodes.ToolFailure;
        }
    }

    private static bool TryParse(string[] args, TextWriter stderr, out PackageValidatorOptions options)
    {
        options = new PackageValidatorOptions(
            PackageRoot: string.Empty,
            Format: TextFormat,
            Locale: DefaultLocale,
            UnlistedFilePolicy: PackageUnlistedFilePolicy.Warning,
            MaxDeepValidationBytes: ContentPackageValidator.DefaultMaxDeepValidationBytes);

        if (args.Length < 2 || !string.Equals(args[0], ValidateCommand, StringComparison.Ordinal))
        {
            WriteUsage(stderr);
            return false;
        }

        var packageRoot = args[1];
        if (string.IsNullOrWhiteSpace(packageRoot) || packageRoot.StartsWith("--", StringComparison.Ordinal))
        {
            stderr.WriteLine("Missing or invalid package root argument.");
            WriteUsage(stderr);
            return false;
        }

        var format = TextFormat;
        var locale = DefaultLocale;
        var unlistedPolicy = PackageUnlistedFilePolicy.Warning;
        var maxDeepValidationBytes = ContentPackageValidator.DefaultMaxDeepValidationBytes;
        for (var i = 2; i < args.Length; i++)
        {
            var arg = args[i];
            if (CliOptionReader.TryReadOption(arg, "--format", args, ref i, stderr, out var formatValue))
            {
                if (formatValue is null)
                {
                    return false;
                }

                if (!SupportedFormats.Contains(formatValue))
                {
                    stderr.WriteLine($"Unsupported --format '{formatValue}'. Use {string.Join('|', SupportedFormats)}.");
                    return false;
                }

                format = formatValue;
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

            if (CliOptionReader.TryReadOption(arg, "--unlisted", args, ref i, stderr, out var unlistedValue))
            {
                if (unlistedValue is null)
                {
                    return false;
                }

                if (!TryParseUnlistedPolicy(unlistedValue, out unlistedPolicy))
                {
                    stderr.WriteLine($"Unsupported --unlisted '{unlistedValue}'. Use warning|error|ignore.");
                    return false;
                }

                continue;
            }

            if (CliOptionReader.TryReadOption(arg, MaxDeepValidationBytesOption, args, ref i, stderr, out var budgetValue))
            {
                if (budgetValue is null)
                {
                    return false;
                }

                if (!TryParseMaxDeepValidationBytes(budgetValue, out maxDeepValidationBytes))
                {
                    stderr.WriteLine(
                        $"Unsupported {MaxDeepValidationBytesOption} '{budgetValue}'. Use an integer of bytes in [1, {int.MaxValue}].");
                    return false;
                }

                continue;
            }

            stderr.WriteLine($"Unknown argument: {arg}");
            WriteUsage(stderr);
            return false;
        }

        options = new PackageValidatorOptions(packageRoot, format, locale, unlistedPolicy, maxDeepValidationBytes);
        return true;
    }

    private static bool TryParseMaxDeepValidationBytes(string text, out long bytes)
    {
        if (long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out bytes)
            && bytes is >= 1 and <= int.MaxValue)
        {
            return true;
        }

        bytes = ContentPackageValidator.DefaultMaxDeepValidationBytes;
        return false;
    }

    private static bool TryParseUnlistedPolicy(string text, out PackageUnlistedFilePolicy policy)
    {
        if (string.Equals(text, "warning", StringComparison.OrdinalIgnoreCase))
        {
            policy = PackageUnlistedFilePolicy.Warning;
            return true;
        }

        if (string.Equals(text, "error", StringComparison.OrdinalIgnoreCase))
        {
            policy = PackageUnlistedFilePolicy.Error;
            return true;
        }

        if (string.Equals(text, "ignore", StringComparison.OrdinalIgnoreCase))
        {
            policy = PackageUnlistedFilePolicy.Ignore;
            return true;
        }

        policy = PackageUnlistedFilePolicy.Warning;
        return false;
    }

    private static void WriteUsage(TextWriter stderr)
    {
        stderr.WriteLine("Usage:");
        stderr.WriteLine("  Opus.Tool.PackageValidator validate <package-root> [--format text|json] [--locale en|ru] [--unlisted warning|error|ignore] [--max-deep-validation-bytes <bytes>]");
        stderr.WriteLine("  Opus.Tool.PackageValidator generate <content-root> --id <id> --name <display-name> --version <semver> [--created <iso-utc>] [--output <path>] [--locale en|ru]");
        stderr.WriteLine("  Opus.Tool.PackageValidator pack <content-root> [--id <id> --name <name> --version <semver>] [--manifest <path>] [--output <path.opkg>] [--key <private-key.pem> --key-id <id>] [--locale en|ru]");
        stderr.WriteLine("  Opus.Tool.PackageValidator verify <package.opkg> [--key <public-key.pem>] [--require-signature] [--locale en|ru]");
        stderr.WriteLine("  Opus.Tool.PackageValidator unpack <package.opkg> <target-dir> [--locale en|ru]");
    }
}
