namespace Opus.Content.Packaging.Validation;

/// <summary>
/// Immutable request for validating one content package directory.
/// </summary>
public sealed record PackageValidationRequest(
    string PackageRoot,
    string ManifestFileName = PackageValidator.DefaultManifestFileName,
    PackageUnlistedFilePolicy UnlistedFilePolicy = PackageUnlistedFilePolicy.Warning,
    long MaxDeepValidationBytes = PackageValidator.DefaultMaxDeepValidationBytes)
{
    /// <summary>
    /// M6 prototype note: the default is deliberately conservative but non-blocking.
    /// Lead can switch alpha CI to <see cref="PackageUnlistedFilePolicy.Error"/> once
    /// consumer repositories have stable package copy rules.
    /// </summary>
    public static PackageValidationRequest ForDirectory(string packageRoot) => new(packageRoot);
}
