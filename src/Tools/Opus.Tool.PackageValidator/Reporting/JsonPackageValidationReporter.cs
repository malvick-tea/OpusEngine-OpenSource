using System.Text.Json;
using Opus.Content.Packaging.Validation;

namespace Opus.Tool.PackageValidator.Reporting;

/// <summary>
/// JSON reporter for CI consumers. Output is stable: the underlying
/// <see cref="PackageValidationResult"/> already sorts diagnostics deterministically, so
/// piping the same input twice produces byte-identical JSON. Includes the diagnostic
/// argument dictionary so programmatic tools can read structured values without parsing
/// the human-readable message.
/// </summary>
internal sealed class JsonPackageValidationReporter : IPackageValidationReporter
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
    };

    private readonly PackageDiagnosticLocalizer _localizer;

    public JsonPackageValidationReporter(PackageDiagnosticLocalizer localizer)
    {
        ArgumentNullException.ThrowIfNull(localizer);
        _localizer = localizer;
    }

    public void Write(PackageValidationResult result, TextWriter writer)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(writer);

        var payload = new
        {
            valid = result.IsValid,
            errors = result.ErrorCount,
            warnings = result.WarningCount,
            info = result.InfoCount,
            diagnostics = result.Diagnostics.Select(d => new
            {
                severity = d.Severity.ToString(),
                code = d.Code.Value,
                target = d.Target.Kind.ToString(),
                path = d.Target.Path,
                message = _localizer.Message(d),
                remediation = d.Remediation,
                arguments = d.Arguments,
            }),
        };
        writer.WriteLine(JsonSerializer.Serialize(payload, SerializerOptions));
    }
}
