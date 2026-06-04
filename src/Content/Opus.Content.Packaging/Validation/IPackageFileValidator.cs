using Opus.Content.Packaging.Diagnostics;
using Opus.Content.Packaging.Manifest;

namespace Opus.Content.Packaging.Validation;

internal interface IPackageFileValidator
{
    bool CanValidate(ContentPackageFile file);

    IReadOnlyList<PackageDiagnostic> Validate(PackageValidationContext context);
}
