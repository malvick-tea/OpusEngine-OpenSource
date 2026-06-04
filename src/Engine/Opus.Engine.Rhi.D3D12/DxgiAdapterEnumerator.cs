using System;
using System.Collections.Generic;
using Silk.NET.Core.Native;
using Silk.NET.DXGI;

namespace Opus.Engine.Rhi.Direct3D12;

/// <summary>
/// Enumerates DXGI adapters, classifies them, and returns them in a sane priority
/// order for renderer adapter selection. Pure helper — no GPU resources held.
///
/// Order rules (matches Direct3D 12 sample best practice):
/// <list type="number">
/// <item><description>High-performance discrete GPUs first (NVIDIA RTX, AMD Radeon, Intel Arc).</description></item>
/// <item><description>Integrated GPUs second (Intel UHD / Iris Xe).</description></item>
/// <item><description>Software adapter (WARP) last.</description></item>
/// </list>
/// </summary>
public static class DxgiAdapterEnumerator
{
    /// <summary>Returns every visible adapter on the system, classified + priority-ordered.</summary>
    public static unsafe IReadOnlyList<AdapterInfo> Enumerate()
    {
        var dxgi = DXGI.GetApi();
        var adapters = new List<AdapterInfo>();
        IDXGIFactory6* factory = null;

        try
        {
            var factoryGuid = IDXGIFactory6.Guid;
            SilkMarshal.ThrowHResult(dxgi.CreateDXGIFactory2(0, &factoryGuid, (void**)&factory));

            for (uint i = 0; ; i++)
            {
                IDXGIAdapter1* adapter = null;
                var enumGuid = IDXGIAdapter1.Guid;
                var hr = factory->EnumAdapterByGpuPreference(
                    i,
                    GpuPreference.HighPerformance,
                    &enumGuid,
                    (void**)&adapter);

                if (hr == unchecked((int)0x887A0002)) // DXGI_ERROR_NOT_FOUND
                {
                    break;
                }

                SilkMarshal.ThrowHResult(hr);

                try
                {
                    AdapterDesc1 desc;
                    SilkMarshal.ThrowHResult(adapter->GetDesc1(&desc));
                    adapters.Add(AdapterEnumeration.DescriptionToInfo((int)i, in desc));
                }
                finally
                {
                    adapter->Release();
                }
            }
        }
        finally
        {
            if (factory != null)
            {
                factory->Release();
            }

            dxgi.Dispose();
        }

        return adapters;
    }
}
