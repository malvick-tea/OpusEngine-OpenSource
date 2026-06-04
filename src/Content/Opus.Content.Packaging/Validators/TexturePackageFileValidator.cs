using System.Buffers.Binary;
using System.IO;
using Opus.Content.Packaging.Diagnostics;
using Opus.Content.Packaging.Manifest;
using Opus.Content.Packaging.Validation;
using Opus.Content.Textures;

namespace Opus.Content.Packaging.Validators;

internal sealed class TexturePackageFileValidator : IPackageFileValidator
{
    private static readonly byte[] Ktx1Magic =
    {
        0xAB, 0x4B, 0x54, 0x58, 0x20, 0x31, 0x31, 0xBB, 0x0D, 0x0A, 0x1A, 0x0A,
    };

    private static readonly byte[] Ktx2Magic =
    {
        0xAB, 0x4B, 0x54, 0x58, 0x20, 0x32, 0x30, 0xBB, 0x0D, 0x0A, 0x1A, 0x0A,
    };

    public bool CanValidate(ContentPackageFile file) =>
        string.Equals(file.Type, PackageAssetTypes.TexturePng, StringComparison.Ordinal)
        || string.Equals(file.Type, PackageAssetTypes.TextureJpeg, StringComparison.Ordinal)
        || string.Equals(file.Type, PackageAssetTypes.TextureKtx, StringComparison.Ordinal);

    public IReadOnlyList<PackageDiagnostic> Validate(PackageValidationContext context)
    {
        if (string.Equals(context.File.Type, PackageAssetTypes.TextureKtx, StringComparison.Ordinal))
        {
            return ValidateKtxHeader(context);
        }

        try
        {
            var decoded = ImageDecoder.DecodeRgba8(context.Bytes);
            _ = MipChain.LevelCount(decoded.Width, decoded.Height);
            return Array.Empty<PackageDiagnostic>();
        }
        catch (InvalidDataException ex)
        {
            return TextureError(context, ex.Message);
        }
    }

    private static IReadOnlyList<PackageDiagnostic> ValidateKtxHeader(PackageValidationContext context)
    {
        if (context.Bytes.Length < Ktx2Magic.Length)
        {
            return TextureError(context, "KTX header is truncated.");
        }

        var span = context.Bytes.AsSpan(0, Ktx2Magic.Length);
        if (span.SequenceEqual(Ktx1Magic) || span.SequenceEqual(Ktx2Magic))
        {
            if (span.SequenceEqual(Ktx2Magic) && context.Bytes.Length >= 32)
            {
                var width = BinaryPrimitives.ReadUInt32LittleEndian(context.Bytes.AsSpan(20, 4));
                var height = BinaryPrimitives.ReadUInt32LittleEndian(context.Bytes.AsSpan(24, 4));
                if (width == 0 || height == 0)
                {
                    return TextureError(context, "KTX2 texture dimensions must be positive.");
                }
            }

            return Array.Empty<PackageDiagnostic>();
        }

        return TextureError(context, "KTX identifier does not match KTX1 or KTX2.");
    }

    private static IReadOnlyList<PackageDiagnostic> TextureError(PackageValidationContext context, string reason) =>
        new[]
        {
            PackageDiagnosticBuilder.FileError(
                PackageDiagnosticCode.TextureInvalid,
                context.RelativePath,
                $"Texture '{context.RelativePath.Value}' is invalid: {reason}",
                "Re-export or replace the texture with a supported PNG/JPEG/KTX file.",
                "package.texture.invalid",
                PackageDiagnosticArguments.Create(
                    ("path", context.RelativePath.Value),
                    ("reason", reason))),
        };
}
