using System;
using System.Globalization;
using System.IO;
using Opus.App.OpusAlpha.Cli;
using Opus.Engine.AlphaHarness;
using Opus.Engine.AlphaHarness.Machine;
using Opus.Foundation;

namespace Opus.App.OpusAlpha.Run;

/// <summary>
/// Runs the machine profile capture / compare path (Mode = CheckMachine). Captures the
/// current host profile through <see cref="KnownGoodMachineCapture"/>; when a
/// <c>--reference</c> path is supplied the captured profile is compared against it and
/// the comparison is printed. Exit code reflects machine compatibility.
/// </summary>
public static class OpusAlphaMachineRunner
{
    private const int ExitCompatible = 0;
    private const int ExitReferenceMissing = 1;
    private const int ExitIncompatible = 2;

    /// <summary>Runs the capture / compare path; returns the conventional exit code.</summary>
    public static int Run(OpusAlphaArgs args, ILog log)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(log);
        var captured = KnownGoodMachineCapture.Capture(
            profileName: "captured",
            capturedAtUtc: DateTimeOffset.UtcNow,
            graphicsAdapterName: null,
            notes: null);
        PrintCapture(log, captured);

        if (!string.IsNullOrWhiteSpace(args.MachineSavePath))
        {
            SaveProfile(log, captured, args.MachineSavePath);
        }

        if (string.IsNullOrWhiteSpace(args.MachineReferencePath))
        {
            return ExitCompatible;
        }

        var reference = KnownGoodMachineCapture.TryLoad(args.MachineReferencePath);
        if (reference is null)
        {
            log.Error($"{AlphaHarnessDiagnosticCodes.MachineReferenceUnavailable} Reference profile '{args.MachineReferencePath}' could not be loaded.");
            return ExitReferenceMissing;
        }

        var comparison = MachineProfileComparer.Compare(reference, captured);
        PrintComparison(log, comparison);
        return comparison.IsCompatible ? ExitCompatible : ExitIncompatible;
    }

    private static void SaveProfile(ILog log, KnownGoodMachineProfile profile, string path)
    {
        try
        {
            var directory = Path.GetDirectoryName(Path.GetFullPath(path));
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(path, KnownGoodMachineCapture.Serialise(profile));
            log.Info($"Profile saved: {path}");
        }
        catch (IOException ex)
        {
            log.Error($"Profile save to '{path}' failed.", ex);
        }
        catch (UnauthorizedAccessException ex)
        {
            log.Error($"Profile save to '{path}' was denied by the filesystem.", ex);
        }
    }

    private static void PrintCapture(ILog log, KnownGoodMachineProfile profile)
    {
        log.Info(string.Create(
            CultureInfo.InvariantCulture,
            $"Captured machine profile '{profile.ProfileName}' at {profile.CapturedAtUtc:O}."));
        log.Info($"  os: {profile.OperatingSystemFamily} ({profile.OperatingSystemDescription})");
        log.Info($"  arch: {profile.ProcessArchitecture}, processors: {profile.LogicalProcessorCount.ToString(CultureInfo.InvariantCulture)}");
        log.Info($"  dotnet: {profile.DotnetRuntimeVersion}");
        log.Info($"  adapter: {profile.GraphicsAdapterName ?? "<unset>"}");
    }

    private static void PrintComparison(ILog log, MachineProfileComparison comparison)
    {
        log.Info(string.Create(
            CultureInfo.InvariantCulture,
            $"Machine profile comparison: errors={comparison.ErrorCount} warnings={comparison.WarningCount} info={comparison.InfoCount} (compatible={comparison.IsCompatible})."));
        foreach (var difference in comparison.Differences)
        {
            var level = difference.Severity switch
            {
                MachineProfileDifferenceSeverity.Error => LogLevel.Error,
                MachineProfileDifferenceSeverity.Warning => LogLevel.Warning,
                _ => LogLevel.Information,
            };
            log.Log(level, $"{difference.DiagnosticCode} [{difference.Field}] {difference.Message}");
        }
    }
}
