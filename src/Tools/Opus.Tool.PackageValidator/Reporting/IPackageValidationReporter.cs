using Opus.Content.Packaging.Validation;

namespace Opus.Tool.PackageValidator.Reporting;

internal interface IPackageValidationReporter
{
    void Write(PackageValidationResult result, TextWriter writer);
}
