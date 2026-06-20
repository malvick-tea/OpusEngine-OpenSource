using System;
using System.Threading;
using System.Threading.Tasks;

namespace Opus.Persistence;

/// <summary>Supplies a process-safe per-install secret for authenticated save frames.</summary>
public interface ISaveIntegrityKeyProvider
{
    ValueTask<ReadOnlyMemory<byte>> GetKeyAsync(CancellationToken cancellationToken);
}
