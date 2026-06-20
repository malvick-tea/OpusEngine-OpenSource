using System.Buffers.Binary;
using Opus.Content.Packaging.Diagnostics;
using Opus.Content.Packaging.Manifest;
using Opus.Content.Packaging.Validation;

namespace Opus.Content.Packaging.Validators;

internal sealed class FontPackageFileValidator : IPackageFileValidator
{
    private const uint TrueTypeVersion = 0x00010000;
    private const uint OpenTypeCffTag = 0x4F54544F;     // OTTO
    private const uint TrueTypeCollectionTag = 0x74746366; // ttcf
    private const uint AppleTrueTypeTag = 0x74727565;   // true

    public bool CanValidate(ContentPackageFile file) =>
        string.Equals(file.Type, PackageAssetTypes.Font, StringComparison.Ordinal);

    public IReadOnlyList<PackageDiagnostic> Validate(PackageValidationContext context)
    {
        if (context.Bytes.Length < 12)
        {
            return FontError(context, "Font header is truncated.");
        }

        var tag = BinaryPrimitives.ReadUInt32BigEndian(context.Bytes.AsSpan(0, 4));
        if (tag == TrueTypeCollectionTag)
        {
            return ValidateCollection(context);
        }

        if (tag is TrueTypeVersion or OpenTypeCffTag or AppleTrueTypeTag)
        {
            var tableCount = BinaryPrimitives.ReadUInt16BigEndian(context.Bytes.AsSpan(4, 2));
            return tableCount == 0
                ? FontError(context, "Font table count is zero.")
                : Array.Empty<PackageDiagnostic>();
        }

        return FontError(context, "Font is not a supported sfnt/OTF/TTC file.");
    }

    private static IReadOnlyList<PackageDiagnostic> ValidateCollection(PackageValidationContext context)
    {
        var fontCount = BinaryPrimitives.ReadUInt32BigEndian(context.Bytes.AsSpan(8, 4));
        if (fontCount == 0)
        {
            return FontError(context, "TrueType collection has no member fonts.");
        }

        var directoryEnd = 12L + ((long)fontCount * sizeof(uint));
        return directoryEnd > context.Bytes.Length
            ? FontError(context, "TrueType collection directory is truncated.")
            : Array.Empty<PackageDiagnostic>();
    }

    private static IReadOnlyList<PackageDiagnostic> FontError(PackageValidationContext context, string reason) =>
        new[]
        {
            PackageDiagnosticBuilder.FileError(
                PackageDiagnosticCode.FontInvalid,
                context.RelativePath,
                $"Font '{context.RelativePath.Value}' is invalid: {reason}",
                "Replace the font with a valid TTF, OTF, or TTC file.",
                "package.font.invalid",
                PackageDiagnosticArguments.Create(
                    ("path", context.RelativePath.Value),
                    ("reason", reason))),
        };
}
