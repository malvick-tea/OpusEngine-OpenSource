using Opus.Content.Packaging.Diagnostics;
using Opus.Content.Packaging.Paths;
using Opus.Content.Packaging.Validation;

namespace Opus.Content.Packaging.Archive;

/// <summary>
/// Safely extracts a <c>.opkg</c> archive to a directory. Reuses the reader's structural
/// validation (entry-name safety, size and ratio bounds), then writes each entry under the target
/// directory through a bounded copy with a defence-in-depth containment check, so a hostile
/// archive can never write outside the extraction root (zip-slip).
/// </summary>
public static class OpusPackageExtractor
{
    /// <summary>Extracts <paramref name="archivePath"/> into <paramref name="targetDirectory"/>.</summary>
    public static PackageArchiveExtractionResult Extract(
        string archivePath,
        string targetDirectory,
        OpusPackageArchiveLimits limits)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(targetDirectory);

        var diagnostics = new List<PackageDiagnostic>();
        var opened = OpusPackageArchiveReader.TryOpen(archivePath, limits, out var reader, out var openDiagnostics);
        diagnostics.AddRange(openDiagnostics);
        if (!opened)
        {
            return new PackageArchiveExtractionResult(false, diagnostics);
        }

        using (reader)
        {
            var root = Path.GetFullPath(targetDirectory);
            Directory.CreateDirectory(root);
            foreach (var entryName in OrderedEntryNames(reader!))
            {
                if (!ExtractEntry(reader!, root, entryName, diagnostics))
                {
                    return new PackageArchiveExtractionResult(false, diagnostics);
                }
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
        if (!IsWithin(root, destinationPath))
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

        Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);
        using var destination = new FileStream(
            destinationPath, FileMode.Create, FileAccess.Write, FileShare.None);
        if (!reader.TryCopyEntryTo(entryName, destination, out var error))
        {
            if (error is not null)
            {
                diagnostics.Add(error);
            }

            return false;
        }

        return true;
    }

    private static bool IsWithin(string root, string candidate)
    {
        var normalisedRoot = Path.TrimEndingDirectorySeparator(Path.GetFullPath(root));
        var fullCandidate = Path.GetFullPath(candidate);
        return string.Equals(fullCandidate, normalisedRoot, StringComparison.Ordinal)
            || fullCandidate.StartsWith(normalisedRoot + Path.DirectorySeparatorChar, StringComparison.Ordinal);
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
