using System.Buffers;
using System.IO.Compression;
using Opus.Content.Packaging.Diagnostics;
using Opus.Content.Packaging.Validation;

namespace Opus.Content.Packaging.Archive;

/// <summary>
/// Opens a <c>.opkg</c> archive for verification or extraction. It validates the container
/// structure against <see cref="OpusPackageArchiveLimits"/> before any entry is decompressed,
/// then streams individual entries through a bounded copy so a lying header cannot expand past
/// the per-entry budget. Hostile input yields diagnostics, never an exception out of the
/// boundary. Holds the underlying file open until disposed.
/// </summary>
public sealed class OpusPackageArchiveReader : IDisposable
{
    private const int ReadBufferSize = 81920;

    private readonly ZipArchive _zip;
    private readonly OpusPackageArchiveLimits _limits;
    private readonly Dictionary<string, ZipArchiveEntry> _entriesByName;

    private OpusPackageArchiveReader(
        ZipArchive zip,
        OpusPackageArchiveLimits limits,
        Dictionary<string, ZipArchiveEntry> entriesByName,
        IReadOnlyList<string> payloadEntryNames)
    {
        _zip = zip;
        _limits = limits;
        _entriesByName = entriesByName;
        PayloadEntryNames = payloadEntryNames;
    }

    /// <summary>Validated payload entry names (excludes the reserved manifest/signature entries),
    /// in ordinal order.</summary>
    public IReadOnlyList<string> PayloadEntryNames { get; }

    /// <summary>All validated entry names, including the reserved manifest/signature entries.</summary>
    public IReadOnlyCollection<string> AllEntryNames => _entriesByName.Keys;

    /// <summary>True when the archive carries a signature entry.</summary>
    public bool HasSignature => _entriesByName.ContainsKey(OpusPackageArchive.SignatureEntryName);

    /// <summary>Opens and structurally validates an archive. On failure <paramref name="reader"/>
    /// is null and <paramref name="diagnostics"/> explains why; on success the caller owns the
    /// returned reader and must dispose it.</summary>
    public static bool TryOpen(
        string archivePath,
        OpusPackageArchiveLimits limits,
        out OpusPackageArchiveReader? reader,
        out IReadOnlyList<PackageDiagnostic> diagnostics)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(archivePath);
        ArgumentNullException.ThrowIfNull(limits);
        limits.Validate();

        reader = null;
        var collected = new List<PackageDiagnostic>();
        diagnostics = collected;

        if (!File.Exists(archivePath))
        {
            collected.Add(ArchiveError(
                PackageDiagnosticCode.ArchiveMissing,
                $"Package archive '{archivePath}' does not exist.",
                "Pass a path to an existing .opkg archive.",
                "package.archive.missing",
                ("path", archivePath)));
            return false;
        }

        var (zip, error) = OpenZip(archivePath);
        if (zip is null)
        {
            collected.Add(ArchiveError(
                PackageDiagnosticCode.ArchiveMalformed,
                $"Package archive '{archivePath}' is not a readable ZIP container: {error}",
                "Re-pack the archive; it may be truncated or corrupt.",
                "package.archive.malformed",
                ("path", archivePath)));
            return false;
        }

        if (!OpusPackageArchiveStructure.Validate(zip, limits, collected, out var map, out var payload))
        {
            zip.Dispose();
            return false;
        }

        reader = new OpusPackageArchiveReader(zip, limits, map, payload);
        return true;
    }

    /// <summary>True when an entry with <paramref name="entryName"/> exists in the archive.</summary>
    public bool ContainsEntry(string entryName) => _entriesByName.ContainsKey(entryName);

    /// <summary>Reads the manifest entry bytes. Fails with a diagnostic if the archive has no
    /// manifest entry or the entry exceeds the per-entry budget.</summary>
    public bool TryReadManifestBytes(out byte[] manifestBytes, out PackageDiagnostic? error)
    {
        if (!_entriesByName.ContainsKey(OpusPackageArchive.ManifestEntryName))
        {
            manifestBytes = Array.Empty<byte>();
            error = ArchiveError(
                PackageDiagnosticCode.ArchiveManifestEntryMissing,
                $"Archive does not contain a '{OpusPackageArchive.ManifestEntryName}' manifest entry.",
                "Re-pack the package so it includes its manifest.",
                "package.archive.manifestEntryMissing",
                ("entry", OpusPackageArchive.ManifestEntryName));
            return false;
        }

        return TryReadEntryBytes(OpusPackageArchive.ManifestEntryName, out manifestBytes, out error);
    }

    /// <summary>Reads the signature entry bytes. Returns false with no error when the archive is
    /// simply unsigned; the caller decides whether that is acceptable.</summary>
    public bool TryReadSignatureBytes(out byte[] signatureBytes, out PackageDiagnostic? error)
    {
        error = null;
        if (!_entriesByName.ContainsKey(OpusPackageArchive.SignatureEntryName))
        {
            signatureBytes = Array.Empty<byte>();
            return false;
        }

        return TryReadEntryBytes(OpusPackageArchive.SignatureEntryName, out signatureBytes, out error);
    }

    /// <summary>Streams an existing entry through a bounded SHA-256. The entry must exist (callers
    /// check <see cref="ContainsEntry"/> first); a bomb that expands past the budget is reported
    /// through <see cref="ArchiveEntryHash.ExceededLimit"/>.</summary>
    public ArchiveEntryHash HashEntry(string entryName)
    {
        var entry = _entriesByName[entryName];
        using var stream = entry.Open();
        return PackageFileHash.TryComputeSha256Hex(stream, _limits.MaxEntryUncompressedBytes, out var hex, out var count)
            ? new ArchiveEntryHash(count, hex, false)
            : new ArchiveEntryHash(0, string.Empty, true);
    }

    /// <summary>Copies an existing entry to <paramref name="destination"/> with a bounded copy
    /// that aborts (with a diagnostic) if the entry expands past the per-entry budget.</summary>
    public bool TryCopyEntryTo(string entryName, Stream destination, out PackageDiagnostic? error)
    {
        ArgumentNullException.ThrowIfNull(destination);
        error = null;
        var entry = _entriesByName[entryName];
        using var stream = entry.Open();
        var pool = ArrayPool<byte>.Shared.Rent(ReadBufferSize);
        try
        {
            long total = 0;
            int read;
            while ((read = stream.Read(pool, 0, pool.Length)) > 0)
            {
                total += read;
                if (total > _limits.MaxEntryUncompressedBytes)
                {
                    error = ArchiveError(
                        PackageDiagnosticCode.ArchiveEntryTooLarge,
                        $"Archive entry '{entry.FullName}' expanded past the {_limits.MaxEntryUncompressedBytes}-byte per-entry limit while reading.",
                        "The entry's header understates its size; re-pack from trusted content.",
                        "package.archive.entryTooLarge",
                        ("entry", entry.FullName));
                    return false;
                }

                destination.Write(pool, 0, read);
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(pool);
        }

        return true;
    }

    private bool TryReadEntryBytes(string entryName, out byte[] bytes, out PackageDiagnostic? error)
    {
        using var buffer = new MemoryStream();
        if (!TryCopyEntryTo(entryName, buffer, out error))
        {
            bytes = Array.Empty<byte>();
            return false;
        }

        bytes = buffer.ToArray();
        return true;
    }

    private static (ZipArchive? Zip, string? Error) OpenZip(string archivePath)
    {
        FileStream? file = null;
        try
        {
            file = new FileStream(archivePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            var zip = new ZipArchive(file, ZipArchiveMode.Read);
            return (zip, null);
        }
        catch (InvalidDataException ex)
        {
            file?.Dispose();
            return (null, ex.Message);
        }
        catch (IOException ex)
        {
            file?.Dispose();
            return (null, ex.Message);
        }
        catch (UnauthorizedAccessException ex)
        {
            file?.Dispose();
            return (null, ex.Message);
        }
    }

    private static PackageDiagnostic ArchiveError(
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

    /// <inheritdoc />
    public void Dispose() => _zip.Dispose();
}
