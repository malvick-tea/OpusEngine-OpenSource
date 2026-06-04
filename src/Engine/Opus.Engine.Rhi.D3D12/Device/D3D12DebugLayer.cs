using Silk.NET.Direct3D12;

namespace Opus.Engine.Rhi.Direct3D12;

/// <summary>Optional D3D12 debug-layer activation. Called before device creation.</summary>
internal static unsafe class D3D12DebugLayer
{
    public static void Enable(D3D12 d3d12)
    {
        ID3D12Debug* debug = null;
        var guid = ID3D12Debug.Guid;
        var hr = d3d12.GetDebugInterface(&guid, (void**)&debug);
        if (hr < 0 || debug == null)
        {
            return;
        }

        try
        {
            debug->EnableDebugLayer();
        }
        finally
        {
            debug->Release();
        }
    }
}
