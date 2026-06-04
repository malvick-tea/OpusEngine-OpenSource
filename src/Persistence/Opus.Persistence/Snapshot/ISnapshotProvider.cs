using Opus.Foundation;

namespace Opus.Persistence;

/// <summary>
/// Sim-side hook: anything that participates in save/replay implements this. Implementations
/// live in Sim assemblies but the contract lives here so Persistence can read snapshots
/// without taking a reference to Sim — the dependency arrow is one-way.
///
/// Snapshots must be byte-stable: two consecutive calls without world mutation MUST
/// produce identical byte arrays (CI gate, see tech-spec §5.1.5).
/// </summary>
public interface ISnapshotProvider
{
    /// <summary>Schema version of the snapshot this provider currently writes.</summary>
    int SchemaVersion { get; }

    /// <summary>Writes the current state to a fresh byte buffer. Must not allocate after warmup.</summary>
    byte[] Capture();

    /// <summary>Restores world state from a snapshot. Returns Err if schema mismatch / corrupt.</summary>
    Result Restore(byte[] body);
}
