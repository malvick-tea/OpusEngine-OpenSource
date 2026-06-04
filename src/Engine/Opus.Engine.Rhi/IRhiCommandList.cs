using System;

namespace Opus.Engine.Rhi;

/// <summary>
/// Recorder for GPU commands. Lifecycle:
/// <list type="number">
/// <item><description><see cref="Begin"/>(frameSlot) — open the recorder against the per-frame
///     allocator pool slot the caller has already drained the GPU on; prior contents discarded.</description></item>
/// <item><description>Record draws / dispatches / barriers / resource bindings.</description></item>
/// <item><description><see cref="End"/> — close; ready for device submission.</description></item>
/// </list>
///
/// Submission itself happens via <c>IRhiDevice.Submit(IRhiCommandList)</c> in a later
/// phase (R-1). For the Null backend, Begin / End are bookkeeping only and the slot
/// argument is ignored.
///
/// <c>frameSlot</c> typically equals the swap chain back-buffer index (after Present
/// has waited on that slot's fence) so allocator reuse never races GPU consumption.
/// Backends that use a single allocator may ignore the slot.
///
/// Phase R-0 surface is deliberately minimal — explicit barrier / pipeline binding APIs
/// arrive in R-1 / R-3 as the live backend exposes them.
/// </summary>
public interface IRhiCommandList : IDisposable
{
    string DebugName { get; }

    bool IsOpen { get; }

    void Begin(uint frameSlot);

    void End();
}
