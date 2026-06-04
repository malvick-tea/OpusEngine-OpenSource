using System.Globalization;
using Opus.Content.Packaging.Diagnostics;
using Opus.Content.Packaging.Manifest;
using Opus.Content.Packaging.Paths;
using Opus.Content.Packaging.Validators;

namespace Opus.Content.Packaging.Validation;

/// <summary>
/// Validates a single manifest-declared file: existence, symlink rejection, declared
/// size and SHA-256 integrity, and content-aware type validation.
/// </summary>
/// <remarks>
/// Integrity (size + hash) is always computed by streaming the file, so it never depends on
/// buffering the whole file in memory. Deep, content-aware type validation (image decode,
/// glTF read, localisation parse) does need the whole file buffered, so it is gated by a
/// caller-supplied in-memory budget: a file above the budget is integrity-checked and then
/// reported with <see cref="PackageDiagnosticCode.FileTooLargeForDeepValidation"/> instead of
/// being read into memory. This is what stops a manifest declaring a very large file from
/// driving the validator out of memory on an unconditional whole-file read.
/// </remarks>
internal sealed class DeclaredFileValidator
{
    private const int SequentialReadBufferSize = 81920;

    private readonly IPackageFileValidator[] _fileValidators =
    {
        new GltfPackageFileValidator(),
        new TexturePackageFileValidator(),
        new FontPackageFileValidator(),
        new LocalisationPackageFileValidator(),
    };

    /// <summary>
    /// Validates one declared file and appends any diagnostics. Localisation key sets are
    /// captured into <paramref name="localisationKeys"/> for the outer cross-locale parity pass.
    /// </summary>
    /// <param name="maxDeepValidationBytes">In-memory budget for deep type validation. The
    /// caller (the public validator entry) has already validated it is in the legal range.</param>
    public void Validate(
        string packageRoot,
        ContentPackageFile file,
        PackageRelativePath relativePath,
        long maxDeepValidationBytes,
        List<PackageDiagnostic> diagnostics,
        SortedDictionary<string, IReadOnlySet<string>> localisationKeys)
    {
        var physicalPath = relativePath.ToPhysicalPath(packageRoot);
        if (!File.Exists(physicalPath))
        {
            diagnostics.Add(PackageDiagnosticBuilder.FileError(
                PackageDiagnosticCode.FileMissing,
                relativePath,
                $"Declared file '{relativePath.Value}' does not exist.",
                "Add the file or remove it from the manifest.",
                "package.file.missing",
                PackageDiagnosticArguments.Create(("path", relativePath.Value))));
            return;
        }

        // Reject symlinked entries — following the link target may escape the package root
        // and a tester moving the package directory cannot rely on the link surviving.
        var info = new FileInfo(physicalPath);
        if ((info.Attributes & FileAttributes.ReparsePoint) != 0)
        {
            diagnostics.Add(PackageDiagnosticBuilder.FileError(
                PackageDiagnosticCode.FileSymlinkDisallowed,
                relativePath,
                $"Declared file '{relativePath.Value}' is a symbolic link or junction.",
                "Replace the link with the actual file content inside the package.",
                "package.file.symlinkDisallowed",
                PackageDiagnosticArguments.Create(("path", relativePath.Value))));
            return;
        }

        using var stream = OpenSequentialRead(physicalPath);
        var length = stream.Length;
        ValidateSize(file, relativePath, length, diagnostics);

        if (length > maxDeepValidationBytes)
        {
            ValidateHash(file, relativePath, PackageFileHash.ComputeSha256Hex(stream), diagnostics);
            diagnostics.Add(BuildTooLargeWarning(relativePath, length, maxDeepValidationBytes));
            return;
        }

        var bytes = ReadAllBytes(stream, length);
        ValidateHash(file, relativePath, PackageFileHash.ComputeSha256Hex(bytes), diagnostics);
        ValidateContent(
            packageRoot, file, relativePath, physicalPath, bytes, maxDeepValidationBytes, diagnostics, localisationKeys);
    }

    private static FileStream OpenSequentialRead(string physicalPath) =>
        new(physicalPath, FileMode.Open, FileAccess.Read, FileShare.Read, SequentialReadBufferSize, FileOptions.SequentialScan);

    private static byte[] ReadAllBytes(FileStream stream, long length)
    {
        // length <= maxDeepValidationBytes <= int.MaxValue is guaranteed by the public entry's
        // budget guard, so the cast is safe and the buffer is bounded by the configured budget.
        var bytes = new byte[(int)length];
        stream.ReadExactly(bytes);
        return bytes;
    }

    private void ValidateContent(
        string packageRoot,
        ContentPackageFile file,
        PackageRelativePath relativePath,
        string physicalPath,
        byte[] bytes,
        long maxDeepValidationBytes,
        List<PackageDiagnostic> diagnostics,
        SortedDictionary<string, IReadOnlySet<string>> localisationKeys)
    {
        var context = new PackageValidationContext(
            packageRoot, file, relativePath, physicalPath, bytes, maxDeepValidationBytes);
        var validator = _fileValidators.FirstOrDefault(v => v.CanValidate(file));
        if (validator is null)
        {
            diagnostics.Add(PackageDiagnosticBuilder.FileError(
                PackageDiagnosticCode.FileTypeUnsupported,
                relativePath,
                $"File type '{file.Type}' is not supported by the M6 validator.",
                "Use a supported package file type or extend the validator.",
                "package.file.typeUnsupported",
                PackageDiagnosticArguments.Create(("type", file.Type))));
            return;
        }

        diagnostics.AddRange(validator.Validate(context));
        if (LocalisationPackageFileValidator.TryReadKeySet(context, out var keys))
        {
            localisationKeys[relativePath.Value] = keys;
        }
    }

    private static void ValidateSize(
        ContentPackageFile file,
        PackageRelativePath relativePath,
        long actualSize,
        List<PackageDiagnostic> diagnostics)
    {
        if (file.SizeBytes == actualSize)
        {
            return;
        }

        diagnostics.Add(PackageDiagnosticBuilder.FileError(
            PackageDiagnosticCode.FileSizeMismatch,
            relativePath,
            $"Manifest size for '{relativePath.Value}' is {file.SizeBytes} bytes, actual file is {actualSize} bytes.",
            "Regenerate the manifest after changing package files.",
            "package.file.sizeMismatch",
            PackageDiagnosticArguments.Create(
                ("path", relativePath.Value),
                ("declared", file.SizeBytes.ToString(CultureInfo.InvariantCulture)),
                ("actual", actualSize.ToString(CultureInfo.InvariantCulture)))));
    }

    private static void ValidateHash(
        ContentPackageFile file,
        PackageRelativePath relativePath,
        string actualHash,
        List<PackageDiagnostic> diagnostics)
    {
        if (string.Equals(file.Sha256, actualHash, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        diagnostics.Add(PackageDiagnosticBuilder.FileError(
            PackageDiagnosticCode.FileHashMismatch,
            relativePath,
            $"SHA-256 mismatch for '{relativePath.Value}'.",
            "Regenerate the manifest after changing package files.",
            "package.file.hashMismatch",
            PackageDiagnosticArguments.Create(
                ("path", relativePath.Value),
                ("declared", file.Sha256),
                ("actual", actualHash))));
    }

    private static PackageDiagnostic BuildTooLargeWarning(
        PackageRelativePath relativePath,
        long length,
        long budget) =>
        PackageDiagnosticBuilder.FileWarning(
            PackageDiagnosticCode.FileTooLargeForDeepValidation,
            relativePath,
            $"Declared file '{relativePath.Value}' is {length} bytes, above the {budget}-byte in-memory validation budget; integrity was verified by streaming, but deep type validation was skipped.",
            "Split the asset, raise the validator's in-memory budget, or accept integrity-only validation for this file.",
            "package.file.tooLargeForDeepValidation",
            PackageDiagnosticArguments.Create(
                ("path", relativePath.Value),
                ("size", length.ToString(CultureInfo.InvariantCulture)),
                ("budget", budget.ToString(CultureInfo.InvariantCulture))));
}
