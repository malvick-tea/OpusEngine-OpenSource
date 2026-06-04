using Opus.Content.Packaging.Manifest;
using Opus.Content.Packaging.Paths;

namespace Opus.Content.Packaging.Validation;

internal sealed record PackageValidationContext(
    string PackageRoot,
    ContentPackageFile File,
    PackageRelativePath RelativePath,
    string PhysicalPath,
    byte[] Bytes,
    long MaxDeepValidationBytes);
