using System.Globalization;
using Opus.Content.Packaging.Diagnostics;

namespace Opus.Tool.PackageValidator;

/// <summary>
/// Shared diagnostic printer for the package tool subcommands: one header line (severity, code,
/// target) and one localised message line per diagnostic, mirroring the validate/generate output.
/// </summary>
internal static class CliDiagnosticReporter
{
    public static void Report(
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
}
