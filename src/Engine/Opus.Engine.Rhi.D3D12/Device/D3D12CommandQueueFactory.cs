using Silk.NET.Direct3D12;

namespace Opus.Engine.Rhi.Direct3D12;

/// <summary>Creates the per-device <see cref="ID3D12CommandQueue"/> instances.</summary>
internal static unsafe class D3D12CommandQueueFactory
{
    public static ID3D12CommandQueue* CreateGraphics(ID3D12Device* device)
    {
        var desc = new CommandQueueDesc
        {
            Type = CommandListType.Direct,
            Priority = (int)CommandQueuePriority.Normal,
            Flags = CommandQueueFlags.None,
            NodeMask = 0,
        };

        ID3D12CommandQueue* queue = null;
        var queueGuid = ID3D12CommandQueue.Guid;
        var hr = device->CreateCommandQueue(&desc, &queueGuid, (void**)&queue);
        return hr < 0 ? null : queue;
    }
}
