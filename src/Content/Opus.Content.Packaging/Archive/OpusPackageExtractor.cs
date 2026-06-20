using Opus.Content.Packaging.Diagnostics;
using Opus.Content.Packaging.Paths;
using Opus.Content.Packaging.Validation;
using Opus.Foundation.IO;

namespace Opus.Content.Packaging.Archive;

/// <summary>
/// Safely extracts a <c>.opkg</c> archive to a directory. Reuses the reader's structural
/// validation (entry-name safety, size and ratio bounds), then writes each entry under the target
/// directory through a bounded copy with a defence-in-depth containment check, so a hostile
/// archive can never write outside the extraction root (zip-slip).
/// </summary>
public static class OpusPackageExtractor
{
    /// <summary>
    /// Verifies a signed archive and extracts it into <paramref name="targetDirectory"/> while
    /// retaining the same open archive handle across both operations.
    /// </summary>
    public static PackageArchiveExtractionResult Extract(
        PackageArchiveVerifyRequest verificationRequest,
        string targetDirectory)
    {
        ArgumentNullException.ThrowIfNull(verificationRequest);
        ArgumentException.ThrowIfNullOrWhiteSpace(targetDirectory);
        if (!verificationRequest.RequireSignature || verificationRequest.PublicKey is null)
        {
            throw new ArgumentException(
                "Package extraction requires a trusted public key and RequireSignature=true.",
                nameof(verificationRequest));
        }

        var diagnostics = new List<PackageDiagnostic>();
        var opened = OpusPackageArchiveReader.TryOpen(
            verificationRequest.ArchivePath,
            verificationRequest.Limits,
            out var reader,
            out var openDiagnostics);
        diagnostics.AddRange(openDiagnostics);
        if (!opened)
        {
            return new PackageArchiveExtractionResult(false, diagnostics);
        }

        using (reader)
        {
            var verification = PackageArchiveVerifier.VerifyOpened(
                verificationRequest,
                reader!,
                diagnostics);
            diagnostics = new List<PackageDiagnostic>(verification.Diagnostics);
            if (!verification.Succeeded || !verification.SignatureVerified)
            {
                return new PackageArchiveExtractionResult(false, diagnostics);
            }

            return ExtractVerified(reader!, targetDirectory, diagnostics);
        }
    }

    private static PackageArchiveExtractionResult ExtractVerified(
        OpusPackageArchiveReader reader,
        string targetDirectory,
        List<PackageDiagnostic> diagnostics)
    {
        var root = Path.GetFullPath(targetDirectory);
        if (Directory.Exists(root) && Directory.EnumerateFileSystemEntries(root).Any())
        {
            diagnostics.Add(PackageDiagnostic.Create(
                PackageDiagnosticSeverity.Error,
                PackageDiagnosticCode.ArchiveTargetNotEmpty,
                PackageDiagnosticTarget.Package,
                $"Extraction target '{root}' is not empty.",
                "Choose an empty directory so no unverified files survive beside package content.",
                "package.archive.targetNotEmpty",
                PackageDiagnosticArguments.Create(("path", root))));
            return new PackageArchiveExtractionResult(false, diagnostics);
        }

        Directory.CreateDirectory(root);
        foreach (var entryName in OrderedEntryNames(reader))
        {
            if (!ExtractEntry(reader, root, entryName, diagnostics))
            {
                return new PackageArchiveExtractionResult(false, diagnostics);
            }
        }

        return new PackageArchiveExtractionResult(!HasErrors(diagnostics), diagnostics);
    }

    private static IEnumerable<string> OrderedEntryNames(OpusPackageArchiveReader reader)
    {
        var names = new List<string>(reader.AllEntryNames);
        names.Sort(StringComparer.Ordinal);
        return names;
    }

    private static bool ExtractEntry(
        OpusPackageArchiveReader reader,
        string root,
        string entryName,
        List<PackageDiagnostic> diagnostics)
    {
        // entryName is an already-validated PackageRelativePath value; re-resolve and assert
        // containment as defence in depth before writing any byte.
        if (!PackageRelativePath.TryCreate(entryName, out var path, out _))
        {
            return false;
        }

        var destinationPath = path.ToPhysicalPath(root);
        if (!PathContainment.IsWithin(root, destinationPath))
        {
            diagnostics.Add(PackageDiagnostic.Create(
                PackageDiagnosticSeverity.Error,
                PackageDiagnosticCode.ArchiveEntryNameUnsafe,
                PackageDiagnosticTarget.Package,
                $"Archive entry '{entryName}' resolves outside the extraction directory.",
                "Re-pack the archive; entry names must stay inside the package.",
                "package.archive.entryEscapesRoot",
                PackageDiagnosticArguments.Create(("entry", entryName))));
            return false;
        }

        var directory = Path.GetDirectoryName(destinationPath)!;
        PathContainment.RejectReparsePoints(root, directory);
        Directory.CreateDirectory(directory);
        PathContainment.RejectReparsePoints(root, directory);

        string? stagingPath = null;
        try
        {
            using (var destination = OpenStagingFile(directory, out stagingPath))
            {
                if (!reader.TryCopyEntryTo(entryName, destination, out var error))
                {
                    if (error is not null)
                    {
                        diagnostics.Add(error);
                    }

                    return false;
                }

                destination.Flush(flushToDisk: true);
            }

            File.Move(stagingPath, destinationPath, overwrite: false);
            return true;
        }
        finally
        {
            if (stagingPath is not null)
            {
                try
                {
                    File.Delete(stagingPath);
                }
                catch (IOException)
                {
                }
                catch (UnauthorizedAccessException)
                {
                }
            }
        }
    }

    private static FileStream OpenStagingFile(string directory, out string path)
    {
        for (var attempt = 0; attempt < 16; attempt++)
        {
            path = Path.Combine(directory, Path.GetRandomFileName());
            try
            {
                return new FileStream(
                    path,
                    FileMode.CreateNew,
                    FileAccess.Write,
                    FileShare.None,
                    bufferSize: 81920,
                    FileOptions.WriteThrough);
            }
            catch (IOException) when (File.Exists(path) || Directory.Exists(path))
            {
            }
        }

        path = string.Empty;
        throw new IOException("Unable to allocate an extraction staging file.");
    }

    private static bool HasErrors(List<PackageDiagnostic> diagnostics)
    {
        foreach (var diagnostic in diagnostics)
        {
            if (diagnostic.Severity == PackageDiagnosticSeverity.Error)
            {
                return true;
            }
        }

        return false;
    }
}
