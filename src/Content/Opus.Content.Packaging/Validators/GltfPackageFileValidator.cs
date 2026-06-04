using System.Globalization;
using System.IO;
using System.Text.Json;
using Opus.Content.Meshes;
using Opus.Content.Packaging.Diagnostics;
using Opus.Content.Packaging.Manifest;
using Opus.Content.Packaging.Paths;
using Opus.Content.Packaging.Validation;

namespace Opus.Content.Packaging.Validators;

internal sealed class GltfPackageFileValidator : IPackageFileValidator
{
    public bool CanValidate(ContentPackageFile file) =>
        string.Equals(file.Type, PackageAssetTypes.ModelGlb, StringComparison.Ordinal)
        || string.Equals(file.Type, PackageAssetTypes.ModelGltf, StringComparison.Ordinal);

    public IReadOnlyList<PackageDiagnostic> Validate(PackageValidationContext context)
    {
        try
        {
            var glbBytes = string.Equals(context.File.Type, PackageAssetTypes.ModelGltf, StringComparison.Ordinal)
                ? GltfFilePacker.PackToGlb(context.PhysicalPath, context.MaxDeepValidationBytes)
                : context.Bytes;
            var scene = GltfBinaryReader.ReadScene(glbBytes);
            _ = SceneTreeMath.ComputeWorldTransforms(scene);
            _ = GltfImageReader.ReadMaterialBindings(glbBytes);
            return Array.Empty<PackageDiagnostic>();
        }
        catch (GltfPackBudgetExceededException ex)
        {
            // The split glTF's sidecar buffers push the deep-validation read over the in-memory
            // budget. Integrity of each declared file is already streamed in DeclaredFileValidator,
            // so this is a Warning (skip deep validation), not a malformed-model Error.
            return new[] { BuildTooLargeWarning(context.RelativePath, ex) };
        }
        catch (Exception ex) when (ex is InvalidDataException or IOException or ArgumentException or JsonException)
        {
            return new[] { BuildModelInvalid(context.RelativePath, ex) };
        }
    }

    private static PackageDiagnostic BuildTooLargeWarning(
        PackageRelativePath relativePath,
        GltfPackBudgetExceededException ex) =>
        PackageDiagnosticBuilder.FileWarning(
            PackageDiagnosticCode.ModelTooLargeForDeepValidation,
            relativePath,
            $"Model '{relativePath.Value}' references sidecar buffers totalling {ex.RequiredBytes} bytes, above the {ex.BudgetBytes}-byte in-memory validation budget; integrity was verified by streaming, but deep model validation was skipped.",
            "Split the model, raise the validator's in-memory budget, or accept integrity-only validation for this model.",
            "package.model.tooLargeForDeepValidation",
            PackageDiagnosticArguments.Create(
                ("path", relativePath.Value),
                ("size", ex.RequiredBytes.ToString(CultureInfo.InvariantCulture)),
                ("budget", ex.BudgetBytes.ToString(CultureInfo.InvariantCulture))));

    private static PackageDiagnostic BuildModelInvalid(PackageRelativePath relativePath, Exception ex) =>
        PackageDiagnosticBuilder.FileError(
            PackageDiagnosticCode.ModelInvalid,
            relativePath,
            $"Model '{relativePath.Value}' is not valid for the Opus alpha model subset: {ex.Message}",
            "Export a glTF 2.0 GLB/split glTF with meshes, accessors, and valid sidecar buffers.",
            "package.model.invalid",
            PackageDiagnosticArguments.Create(
                ("path", relativePath.Value),
                ("reason", ex.Message)));
}
