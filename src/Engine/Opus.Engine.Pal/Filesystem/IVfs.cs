using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Opus.Engine.Pal.Filesystem;

/// <summary>
/// Virtual filesystem facade. Hides the difference between <c>res://</c> (read-only,
/// possibly inside a packed archive) and <c>user://</c> (per-platform writable storage).
///
/// Implementations resolve URI-style paths starting with one of these schemes:
///   <c>res://path/to/asset.ext</c>   — bundled resources
///   <c>user://saves/slot0.bin</c>     — user-writable
///
/// Paths without a scheme are illegal — callers must be explicit about which root they
/// want, and the layout analyser enforces that.
/// </summary>
public interface IVfs
{
    bool Exists(string virtualPath);

    /// <summary>Opens for read. Caller owns disposal.</summary>
    Stream OpenRead(string virtualPath);

    /// <summary>Opens for write. Only legal under <c>user://</c>.</summary>
    Stream OpenWrite(string virtualPath);

    /// <summary>Atomically replaces the destination with the bytes. Only legal under <c>user://</c>.</summary>
    Task WriteAllBytesAtomicAsync(string virtualPath, byte[] payload, CancellationToken ct);

    /// <summary>Resolves a virtual path to a real OS path. Throws on read-only roots that have no real path (e.g. inside .gpkg).</summary>
    string Realize(string virtualPath);
}
