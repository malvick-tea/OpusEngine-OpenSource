using System;
using System.Globalization;
using Opus.App.OpusAlpha.Cli;
using Opus.Content.Packaging.Validation;
using Opus.Engine.AlphaHarness.Packaging;
using Opus.Foundation;

namespace Opus.App.OpusAlpha.Run;

/// <summary>
/// Runs the alpha-package checklist (Mode = ValidatePackage). Loads the manifest +
/// validation result from the supplied package directory via the M6
/// <see cref="PackageValidator"/>, then folds them through
/// <see cref="AlphaPackageChecklist"/> using the canonical Opus 0.1 policy. Output is
/// printed to <paramref name="ILog"/>; exit code reflects checklist cleanliness.
/// </summary>
public static class OpusAlphaPackageRunner
{
    private const int ExitClean = 0;
    private const int ExitMissingInput = 1;
    private const int ExitChecklistFailed = 2;

    /// <summary>Runs the checklist; returns the conventional process exit code.</summary>
    public static int Run(OpusAlphaArgs args, ILog log)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(log);
        if (string.IsNullOrWhiteSpace(args.PackagePath))
        {
            log.Error("validate-package mode requires --package <path>.");
            return ExitMissingInput;
        }

        var validator = new PackageValidator();
        var validation = validator.ValidateDirectory(PackageValidationRequest.ForDirectory(args.PackagePath));
        var checklist = AlphaPackageChecklist.Run(validation.Manifest, validation, AlphaPackageChecklistPolicy.Default);
        PrintFindings(log, checklist);
        return checklist.IsClean ? ExitClean : ExitChecklistFailed;
    }

    private static void PrintFindings(ILog log, AlphaPackageChecklistResult checklist)
    {
        log.Info(string.Create(
            CultureInfo.InvariantCulture,
            $"Alpha package checklist: {checklist.Findings.Count} finding(s); errors={checklist.ErrorCount} warnings={checklist.WarningCount} info={checklist.InfoCount}."));
        foreach (var finding in checklist.Findings)
        {
            var level = finding.Severity switch
            {
                AlphaPackageChecklistSeverity.Error => LogLevel.Error,
                AlphaPackageChecklistSeverity.Warning => LogLevel.Warning,
                _ => LogLevel.Information,
            };
            log.Log(level, $"{finding.DiagnosticCode} [{finding.Item}] {finding.Message}");
        }
    }
}
