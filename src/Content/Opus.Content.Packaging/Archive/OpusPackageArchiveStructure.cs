using System.IO.Compression;
using Opus.Content.Packaging.Diagnostics;
using Opus.Content.Packaging.Paths;
using Opus.Content.Packaging.Validation;

namespace Opus.Content.Packaging.Archive;

/// <summary>
/// Validates the structure of an opened <c>.opkg</c> archive against the configured limits
/// before any entry is decompressed: entry count, safe entry names (zip-slip), per-entry and
/// total declared uncompressed size, and the overall compression ratio (zip-bomb). Appends a
/// diagnostic for each violation and returns whether the archive is structurally safe to read.
/// </summary>
internal static class OpusPackageArchiveStructure
{
    public static bool Validate(
        ZipArchive zip,
        OpusPackageArchiveLimits limits,
        List<PackageDiagnostic> diagnostics,
        out Dictionary<string, ZipArchiveEntry> entriesByName,
        out List<string> payloadEntryNames)
    {
        entriesByName = new Dictionary<string, ZipArchiveEntry>(StringComparer.Ordinal);
        payloadEntryNames = new List<string>();
        var portableEntryNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var startErrors = CountErrors(diagnostics);
        if (zip.Entries.Count > limits.MaxEntryCount)
        {
            diagnostics.Add(Error(
                PackageDiagnosticCode.ArchiveEntryCountExceeded,
                $"Archive declares {zip.Entries.Count} entries, above the {limits.MaxEntryCount} limit.",
                "Reduce the number of files in the package or raise the reader's entry-count limit.",
                "package.archive.entryCountExceeded",
                ("count", zip.Entries.Count.ToString(System.Globalization.CultureInfo.InvariantCulture)),
                ("limit", limits.MaxEntryCount.ToString(System.Globalization.CultureInfo.InvariantCulture))));
            return false;
        }

        long totalUncompressed = 0;
        long totalCompressed = 0;
        foreach (var entry in zip.Entries)
        {
            if (string.IsNullOrEmpty(entry.Name))
            {
                continue; // directory marker entry; carries no payload
            }

            if (!ValidateEntry(
                    entry,
                    limits,
                    diagnostics,
                    entriesByName,
                    portableEntryNames,
                    payloadEntryNames))
            {
                continue;
            }

            totalUncompressed += entry.Length;
            totalCompressed += entry.CompressedLength;
        }

        ValidateAggregate(totalUncompressed, totalCompressed, limits, diagnostics);
        payloadEntryNames.Sort(StringComparer.Ordinal);
        return CountErrors(diagnostics) == startErrors;
    }

    private static bool ValidateEntry(
        ZipArchiveEntry entry,
        OpusPackageArchiveLimits limits,
        List<PackageDiagnostic> diagnostics,
        Dictionary<string, ZipArchiveEntry> entriesByName,
        HashSet<string> portableEntryNames,
        List<string> payloadEntryNames)
    {
        if (!PackageRelativePath.TryCreate(entry.FullName, out var path, out var reason))
        {
            diagnostics.Add(Error(
                PackageDiagnosticCode.ArchiveEntryNameUnsafe,
                $"Archive entry '{entry.FullName}' has an unsafe name: {reason}",
                "Re-pack the archive; entry names must be safe package-relative paths.",
                "package.archive.entryNameUnsafe",
                ("entry", entry.FullName),
                ("reason", reason)));
            return false;
        }

        if (!entriesByName.TryAdd(path.Value, entry))
        {
            diagnostics.Add(Error(
                PackageDiagnosticCode.ArchiveMalformed,
                $"Archive declares entry '{path.Value}' more than once.",
                "Re-pack the archive without duplicate entries.",
                "package.archive.duplicateEntry",
                ("entry", path.Value)));
            return false;
        }

        if (!portableEntryNames.Add(path.Value))
        {
            entriesByName.Remove(path.Value);
            diagnostics.Add(Error(
                PackageDiagnosticCode.ArchiveMalformed,
                $"Archive entry '{path.Value}' collides by case with another entry.",
                "Re-pack the archive with names that remain unique on case-insensitive filesystems.",
                "package.archive.caseCollision",
                ("entry", path.Value)));
            return false;
        }

        if (entry.Length > limits.MaxEntryUncompressedBytes)
        {
            diagnostics.Add(Error(
                PackageDiagnosticCode.ArchiveEntryTooLarge,
                $"Archive entry '{path.Value}' declares {entry.Length} uncompressed bytes, above the {limits.MaxEntryUncompressedBytes}-byte per-entry limit.",
                "Split the asset or raise the reader's per-entry limit.",
                "package.archive.entryTooLarge",
                ("entry", path.Value)));
            return false;
        }

        if (!OpusPackageArchive.IsReservedEntry(path.Value))
        {
            payloadEntryNames.Add(path.Value);
        }

        return true;
    }

    private static void ValidateAggregate(
        long totalUncompressed,
        long totalCompressed,
        OpusPackageArchiveLimits limits,
        List<PackageDiagnostic> diagnostics)
    {
        if (totalUncompressed > limits.MaxTotalUncompressedBytes)
        {
            diagnostics.Add(Error(
                PackageDiagnosticCode.ArchiveTooLarge,
                $"Archive expands to {totalUncompressed} bytes, above the {limits.MaxTotalUncompressedBytes}-byte total limit.",
                "Reduce package content or raise the reader's total limit.",
                "package.archive.tooLarge",
                ("total", totalUncompressed.ToString(System.Globalization.CultureInfo.InvariantCulture))));
        }

        if (totalCompressed > 0 && totalUncompressed / totalCompressed > limits.MaxCompressionRatio)
        {
            diagnostics.Add(Error(
                PackageDiagnosticCode.ArchiveCompressionRatioExceeded,
                $"Archive compression ratio {totalUncompressed / totalCompressed}:1 exceeds the {limits.MaxCompressionRatio}:1 limit.",
                "An archive expanding this much is treated as a zip-bomb; re-pack from trusted content.",
                "package.archive.ratioExceeded",
                ("ratio", (totalUncompressed / totalCompressed).ToString(System.Globalization.CultureInfo.InvariantCulture))));
        }
    }

    private static int CountErrors(List<PackageDiagnostic> diagnostics)
    {
        var count = 0;
        foreach (var diagnostic in diagnostics)
        {
            if (diagnostic.Severity == PackageDiagnosticSeverity.Error)
            {
                count++;
            }
        }

        return count;
    }

    private static PackageDiagnostic Error(
        PackageDiagnosticCode code,
        string message,
        string remediation,
        string messageKey,
        params (string Key, string Value)[] arguments) =>
        PackageDiagnostic.Create(
            PackageDiagnosticSeverity.Error,
            code,
            PackageDiagnosticTarget.Package,
            message,
            remediation,
            messageKey,
            PackageDiagnosticArguments.Create(arguments));
}
