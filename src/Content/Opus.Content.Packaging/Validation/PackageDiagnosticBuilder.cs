using Opus.Content.Packaging.Diagnostics;
using Opus.Content.Packaging.Paths;

namespace Opus.Content.Packaging.Validation;

internal static class PackageDiagnosticBuilder
{
    public static PackageDiagnostic Error(
        PackageDiagnosticCode code,
        PackageDiagnosticTarget target,
        string message,
        string remediation,
        string messageKey,
        IReadOnlyDictionary<string, string>? arguments = null) =>
        PackageDiagnostic.Create(
            PackageDiagnosticSeverity.Error,
            code,
            target,
            message,
            remediation,
            messageKey,
            arguments);

    public static PackageDiagnostic Warning(
        PackageDiagnosticCode code,
        PackageDiagnosticTarget target,
        string message,
        string remediation,
        string messageKey,
        IReadOnlyDictionary<string, string>? arguments = null) =>
        PackageDiagnostic.Create(
            PackageDiagnosticSeverity.Warning,
            code,
            target,
            message,
            remediation,
            messageKey,
            arguments);

    public static PackageDiagnostic FileError(
        PackageDiagnosticCode code,
        PackageRelativePath path,
        string message,
        string remediation,
        string messageKey,
        IReadOnlyDictionary<string, string>? arguments = null) =>
        Error(code, PackageDiagnosticTarget.File(path), message, remediation, messageKey, arguments);

    public static PackageDiagnostic FileWarning(
        PackageDiagnosticCode code,
        PackageRelativePath path,
        string message,
        string remediation,
        string messageKey,
        IReadOnlyDictionary<string, string>? arguments = null) =>
        Warning(code, PackageDiagnosticTarget.File(path), message, remediation, messageKey, arguments);
}
