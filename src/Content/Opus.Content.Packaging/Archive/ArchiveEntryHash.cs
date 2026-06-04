namespace Opus.Content.Packaging.Archive;

/// <summary>Result of streaming one archive entry through a bounded SHA-256.</summary>
/// <param name="ByteCount">Exact uncompressed bytes read (meaningful only when not exceeded).</param>
/// <param name="Sha256Hex">Lower-case SHA-256 digest (empty when the entry exceeded the budget).</param>
/// <param name="ExceededLimit">True when the entry expanded past the per-entry budget while being
/// read — its header understated its size, the hallmark of a zip-bomb. Integrity is not trusted.</param>
public readonly record struct ArchiveEntryHash(long ByteCount, string Sha256Hex, bool ExceededLimit);
