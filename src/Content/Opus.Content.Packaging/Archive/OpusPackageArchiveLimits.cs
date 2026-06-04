namespace Opus.Content.Packaging.Archive;

/// <summary>
/// Bounds the reader applies to an untrusted <c>.opkg</c> archive to stop zip-bomb and
/// resource-exhaustion attacks. Every limit has a generous alpha-content default; a caller can
/// tighten or loosen them for a specific deployment.
/// </summary>
/// <param name="MaxEntryCount">Maximum number of entries the archive may contain.</param>
/// <param name="MaxEntryUncompressedBytes">Maximum uncompressed size of a single entry.</param>
/// <param name="MaxTotalUncompressedBytes">Maximum total uncompressed size of all entries.</param>
/// <param name="MaxCompressionRatio">Maximum total-uncompressed-to-total-compressed ratio before
/// the archive is treated as a zip-bomb.</param>
public sealed record OpusPackageArchiveLimits(
    int MaxEntryCount,
    long MaxEntryUncompressedBytes,
    long MaxTotalUncompressedBytes,
    int MaxCompressionRatio)
{
    /// <summary>Defaults sized for alpha content packages: 4096 entries, 512 MiB per entry,
    /// 2 GiB total, 200:1 ratio.</summary>
    public static OpusPackageArchiveLimits Default { get; } = new(
        MaxEntryCount: 4096,
        MaxEntryUncompressedBytes: 512L * 1024 * 1024,
        MaxTotalUncompressedBytes: 2L * 1024 * 1024 * 1024,
        MaxCompressionRatio: 200);

    /// <summary>Throws when any limit is non-positive or the per-entry budget exceeds the
    /// total.</summary>
    public void Validate()
    {
        ThrowIfNotPositive(MaxEntryCount, nameof(MaxEntryCount));
        ThrowIfNotPositive(MaxEntryUncompressedBytes, nameof(MaxEntryUncompressedBytes));
        ThrowIfNotPositive(MaxTotalUncompressedBytes, nameof(MaxTotalUncompressedBytes));
        ThrowIfNotPositive(MaxCompressionRatio, nameof(MaxCompressionRatio));
        if (MaxEntryUncompressedBytes > MaxTotalUncompressedBytes)
        {
            throw new ArgumentOutOfRangeException(
                nameof(MaxEntryUncompressedBytes),
                MaxEntryUncompressedBytes,
                "Per-entry budget cannot exceed the total budget.");
        }
    }

    private static void ThrowIfNotPositive(long value, string name)
    {
        if (value < 1)
        {
            throw new ArgumentOutOfRangeException(name, value, "Archive limit must be positive.");
        }
    }
}
