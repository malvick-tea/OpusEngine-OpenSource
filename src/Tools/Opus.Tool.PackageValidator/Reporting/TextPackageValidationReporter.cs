using System.Globalization;
using Opus.Content.Packaging.Validation;

namespace Opus.Tool.PackageValidator.Reporting;

/// <summary>
/// Text reporter for human-readable CLI output. Order is stable because the underlying
/// <see cref="PackageValidationResult"/> sorts diagnostics in the factory.
/// </summary>
internal sealed class TextPackageValidationReporter : IPackageValidationReporter
{
    private readonly PackageDiagnosticLocalizer _localizer;

    public TextPackageValidationReporter(PackageDiagnosticLocalizer localizer)
    {
        ArgumentNullException.ThrowIfNull(localizer);
        _localizer = localizer;
    }

    public void Write(PackageValidationResult result, TextWriter writer)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(writer);

        writer.WriteLine(result.IsValid ? "Package validation passed." : "Package validation failed.");
        writer.WriteLine(string.Create(
            CultureInfo.InvariantCulture,
            $"errors={result.ErrorCount} warnings={result.WarningCount} info={result.InfoCount}"));

        foreach (var diagnostic in result.Diagnostics)
        {
            var target = diagnostic.Target.Path ?? diagnostic.Target.Kind.ToString().ToLowerInvariant();
            writer.WriteLine(string.Create(
                CultureInfo.InvariantCulture,
                $"{diagnostic.Severity} {diagnostic.Code} {target}"));
            writer.WriteLine($"  {_localizer.Message(diagnostic)}");
            writer.WriteLine($"  Hint: {diagnostic.Remediation}");
        }
    }
}
