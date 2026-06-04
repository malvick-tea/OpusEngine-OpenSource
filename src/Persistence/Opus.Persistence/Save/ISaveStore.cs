using System.Threading;
using System.Threading.Tasks;
using Opus.Foundation;

namespace Opus.Persistence;

/// <summary>
/// Host-side save storage. Implementations live in Engine.Pal.* (per platform), not here —
/// Persistence only knows about bytes-in / bytes-out.
///
/// Atomic-write contract: <see cref="WriteAsync"/> MUST commit the new body or leave the
/// previous slot intact. Implementations achieve this via write-temp-fsync-rename
/// (Windows / desktop) or platform-equivalent atomic writes (Android ContentResolver,
/// iOS NSFileManager with NSDataWritingAtomic).
/// </summary>
public interface ISaveStore
{
    Task<Result<byte[]>> ReadAsync(SaveSlot slot, CancellationToken ct);

    Task<Result> WriteAsync(SaveSlot slot, byte[] body, CancellationToken ct);

    Task<Result> DeleteAsync(SaveSlot slot, CancellationToken ct);

    Task<bool> ExistsAsync(SaveSlot slot, CancellationToken ct);
}
