using Silk.NET.Core.Native;
using Silk.NET.Direct3D12;

namespace Opus.Engine.Rhi.Direct3D12;

/// <summary>
/// Direct3D 12 graphics command list wrapper. Owns <c>frameSlots</c>
/// <c>ID3D12CommandAllocator</c>s (one per in-flight frame so allocator reuse never
/// races GPU consumption) and a single <c>ID3D12GraphicsCommandList</c>.
///
/// Lifecycle per frame: <see cref="Begin"/>(slot) → record → <see cref="End"/> →
/// <see cref="ExecuteOn"/> → caller signals + waits via the swap chain fence before
/// reusing the same slot. <c>slot</c> typically equals <c>D3D12SwapChain.CurrentBackBufferIndex</c>
/// — the swap chain's per-buffer fence wait already gates allocator reuse.
///
/// Recording APIs live in single-responsibility partial files in this folder:
/// <list type="bullet">
///   <item><description><c>D3D12CommandList.Barriers.cs</c> — resource state transitions</description></item>
///   <item><description><c>D3D12CommandList.RenderTargets.cs</c> — RT/DSV bind, clear, viewport, scissor</description></item>
///   <item><description><c>D3D12CommandList.InputAssembly.cs</c> — vertex/index buffer + topology</description></item>
///   <item><description><c>D3D12CommandList.GraphicsRoot.cs</c> — graphics-pipeline root parameters</description></item>
///   <item><description><c>D3D12CommandList.ComputeRoot.cs</c> — compute-pipeline root parameters</description></item>
///   <item><description><c>D3D12CommandList.Commands.cs</c> — draws / dispatch / indirect / copy</description></item>
/// </list>
/// Shared bindings that apply to both graphics and compute pipelines
/// (<see cref="SetPipelineState"/>, <see cref="SetDescriptorHeaps"/>) live in this main
/// file alongside the lifecycle methods.
/// </summary>
public sealed unsafe partial class D3D12CommandList : IRhiCommandList
{
    private readonly ID3D12CommandAllocator*[] _allocators;
    private ID3D12GraphicsCommandList* _commandList;
    private bool _disposed;

    private D3D12CommandList(string debugName, ID3D12CommandAllocator*[] allocators, ID3D12GraphicsCommandList* commandList)
    {
        DebugName = debugName;
        _allocators = allocators;
        _commandList = commandList;
    }

    public string DebugName { get; }

    public bool IsOpen { get; private set; }

    public int FrameSlots => _allocators.Length;

    public ID3D12GraphicsCommandList* NativeList => _commandList;

    public static D3D12CommandList Create(ID3D12Device* device, string debugName, int frameSlots = D3D12SwapChain.BufferCount)
    {
        if (frameSlots < 1)
        {
            throw new System.ArgumentOutOfRangeException(nameof(frameSlots), "frameSlots must be >= 1");
        }

        var allocators = new ID3D12CommandAllocator*[frameSlots];
        ID3D12GraphicsCommandList* commandList = null;
        var allocatorsOwnedHere = true;

        try
        {
            for (var i = 0; i < frameSlots; i++)
            {
                ID3D12CommandAllocator* allocator = null;
                var allocGuid = ID3D12CommandAllocator.Guid;
                SilkMarshal.ThrowHResult(device->CreateCommandAllocator(
                    CommandListType.Direct,
                    &allocGuid,
                    (void**)&allocator));
                allocators[i] = allocator;
            }

            var listGuid = ID3D12GraphicsCommandList.Guid;
            SilkMarshal.ThrowHResult(device->CreateCommandList(
                nodeMask: 0,
                CommandListType.Direct,
                allocators[0],
                pInitialState: null,
                &listGuid,
                (void**)&commandList));

            // CreateCommandList returns the list in the open state — close immediately so the
            // user gets the documented "call Begin first" lifecycle.
            SilkMarshal.ThrowHResult(commandList->Close());

            var instance = new D3D12CommandList(debugName, allocators, commandList);
            allocatorsOwnedHere = false;
            commandList = null;
            return instance;
        }
        finally
        {
            if (commandList != null)
            {
                commandList->Release();
            }

            if (allocatorsOwnedHere)
            {
                for (var i = 0; i < allocators.Length; i++)
                {
                    if (allocators[i] != null)
                    {
                        allocators[i]->Release();
                        allocators[i] = null;
                    }
                }
            }
        }
    }

    /// <summary>
    /// Resets the per-slot allocator and re-opens the command list against it.
    /// <paramref name="frameSlot"/> must be in <c>[0, FrameSlots)</c> and must point to
    /// an allocator the GPU has finished consuming — typically guaranteed by waiting on
    /// the swap chain's per-buffer fence before this call (Present does that).
    /// </summary>
    public void Begin(uint frameSlot)
    {
        if (frameSlot >= _allocators.Length)
        {
            throw new System.ArgumentOutOfRangeException(nameof(frameSlot), $"frameSlot {frameSlot} >= FrameSlots {_allocators.Length}");
        }

        var allocator = _allocators[frameSlot];
        SilkMarshal.ThrowHResult(allocator->Reset());
        SilkMarshal.ThrowHResult(_commandList->Reset(allocator, pInitialState: null));
        IsOpen = true;
    }

    public void End()
    {
        SilkMarshal.ThrowHResult(_commandList->Close());
        IsOpen = false;
    }

    /// <summary>
    /// Submits this list to the device's graphics queue for execution. The caller must
    /// have closed the list via <see cref="End"/> first.
    /// </summary>
    public void ExecuteOn(D3D12RhiDevice device)
    {
        if (IsOpen)
        {
            throw new System.InvalidOperationException("Command list must be closed before execution.");
        }

        var queue = device.GraphicsQueue;
        ID3D12CommandList* list = (ID3D12CommandList*)_commandList;
        queue->ExecuteCommandLists(1u, &list);
    }

    /// <summary>Binds a graphics or compute pipeline state object. Same call covers both
    /// pipelines — D3D12 selects the right binding point from the PSO's type.</summary>
    public void SetPipelineState(D3D12GraphicsPipeline pipeline) =>
        _commandList->SetPipelineState(pipeline.Native);

    /// <summary>Binds a single shader-visible descriptor heap for the current draw. Static
    /// samplers don't need a heap; combined SRV + dynamic-sampler binding will pass two
    /// heaps once R-3 needs sampler variety.</summary>
    public void SetDescriptorHeaps(ID3D12DescriptorHeap* srvHeap)
    {
        var heap = srvHeap;
        _commandList->SetDescriptorHeaps(1u, &heap);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        if (_commandList != null)
        {
            _commandList->Release();
            _commandList = null;
        }

        for (var i = 0; i < _allocators.Length; i++)
        {
            if (_allocators[i] != null)
            {
                _allocators[i]->Release();
                _allocators[i] = null;
            }
        }

        _disposed = true;
    }
}
