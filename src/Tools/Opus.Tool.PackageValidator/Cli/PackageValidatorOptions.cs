using Opus.Content.Packaging.Validation;

namespace Opus.Tool.PackageValidator;

internal sealed record PackageValidatorOptions(
    string PackageRoot,
    string Format,
    string Locale,
    PackageUnlistedFilePolicy UnlistedFilePolicy,
    long MaxDeepValidationBytes);
