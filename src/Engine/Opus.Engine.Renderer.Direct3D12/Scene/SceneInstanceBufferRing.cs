using System;
using System.Runtime.InteropServices;
using Opus.Engine.Rhi;
using Opus.Engine.Rhi.Direct3D12;

namespace Opus.Engine.Renderer.Direct3D12.Scene;

/// <summary>
/// Per-swap-chain-slot ring of upload-heap <see cref="GpuInstanceData"/> buffers bound as the
/// instanced forward pass's root SRV (<c>t1</c>). One buffer per slot so a frame in flight is
/// never overwritten — the same lifetime model as the renderer's scene-constant ring.
/// <para>
/// Grows on demand: when a frame needs more instances than the current capacity, the ring
/// drains the GPU (so no in-flight slot buffer is freed under the device) and reallocates every
/// slot to the next power-of-two capacity. Growth is rare (the default capacity covers the
/// alpha population) and amortises to nothing; steady state is a plain map + memcpy per frame.
/// </para>
/// </summary>
public sealed class SceneInstanceBufferRing : IDisposable
{
    /// <summary>Instances per slot before the first growth — sized to cover the alpha large-map
    /// population (player + opponent grid + transients) without reallocating.</summary>
    public const int DefaultCapacity = 256;

    private readonly D3D12RhiDevice _device;
    private readonly string _namePrefix;
    private readonly D3D12Buffer[] _buffers;
    private int _capacity;
    private bool _disposed;

    public SceneInstanceBufferRing(D3D12RhiDevice device, int slotCount, string namePrefix)
    {
        ArgumentNullException.ThrowIfNull(device);
        if (slotCount <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(slotCount), "Slot count must be positive.");
        }

        _device = device;
        _namePrefix = namePrefix;
        _capacity = DefaultCapacity;
        _buffers = new D3D12Buffer[slotCount];
        for (var i = 0; i < slotCount; i++)
        {
            _buffers[i] = CreateSlotBuffer(i, _capacity);
        }
    }

    /// <summary>Current per-slot capacity in instances. Exposed for diagnostics / tests.</summary>
    public int Capacity => _capacity;

    /// <summary>Uploads <paramref name="instances"/> into <paramref name="slot"/>'s buffer
    /// (growing every slot first if needed) and returns that buffer for root-SRV binding. An
    /// empty upload is a no-op that still returns the (always-valid) slot buffer so the pass can
    /// bind it unconditionally.</summary>
    public D3D12Buffer Upload(uint slot, ReadOnlySpan<GpuInstanceData> instances)
    {
        ThrowIfDisposed();
        EnsureCapacity(instances.Length);
        var buffer = _buffers[slot];
        if (!instances.IsEmpty)
        {
            buffer.Upload(MemoryMarshal.AsBytes(instances));
        }

        return buffer;
    }

    private void EnsureCapacity(int instanceCount)
    {
        if (instanceCount <= _capacity)
        {
            return;
        }

        var newCapacity = _capacity;
        while (newCapacity < instanceCount)
        {
            newCapacity *= 2;
        }

        // A slot buffer may still be read by an in-flight frame; drain before freeing it.
        _device.WaitForIdle();
        for (var i = 0; i < _buffers.Length; i++)
        {
            _buffers[i].Dispose();
            _buffers[i] = CreateSlotBuffer(i, newCapacity);
        }

        _capacity = newCapacity;
    }

    private D3D12Buffer CreateSlotBuffer(int slot, int capacity) =>
        _device.CreateGraphicsBuffer(new RhiBufferDescription(
            $"{_namePrefix}.instances.{slot}",
            capacity * GpuInstanceData.SizeBytes,
            RhiBufferUsage.Structured));

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        foreach (var buffer in _buffers)
        {
            buffer?.Dispose();
        }

        _disposed = true;
    }

    private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(_disposed, this);
}
