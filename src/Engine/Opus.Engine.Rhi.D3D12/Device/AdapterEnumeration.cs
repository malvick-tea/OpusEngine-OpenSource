using System.Text;
using Silk.NET.Direct3D12;
using Silk.NET.DXGI;

namespace Opus.Engine.Rhi.Direct3D12;

/// <summary>Adapter selection + adapter-info marshalling helpers.</summary>
internal static unsafe class AdapterEnumeration
{
    private const int DxgiErrorNotFound = unchecked((int)0x887A0002);

    /// <summary>Walks the DXGI factory's GPU-preference enumeration (high-perf first),
    /// returns the first non-software adapter or null when none exists.</summary>
    public static IDXGIAdapter1* PickHighestPriority(IDXGIFactory6* factory)
    {
        for (uint i = 0; ; i++)
        {
            IDXGIAdapter1* candidate = null;
            var enumGuid = IDXGIAdapter1.Guid;
            var hr = factory->EnumAdapterByGpuPreference(i, GpuPreference.HighPerformance, &enumGuid, (void**)&candidate);

            if (hr == DxgiErrorNotFound || hr < 0)
            {
                return null;
            }

            AdapterDesc1 desc;
            candidate->GetDesc1(&desc);

            // Skip software adapter when something else is available (i.e. first iteration).
            if (((AdapterFlag)desc.Flags & AdapterFlag.Software) != 0 && i == 0)
            {
                candidate->Release();
                continue;
            }

            return candidate;
        }
    }

    public static AdapterInfo DescriptionToInfo(int index, in AdapterDesc1 desc)
    {
        var description = ExtractDescription(desc);
        var flavor = ((AdapterFlag)desc.Flags & AdapterFlag.Software) != 0
            ? AdapterFlavor.Software
            : desc.DedicatedVideoMemory > 0
                ? AdapterFlavor.Discrete
                : AdapterFlavor.Integrated;

        return new AdapterInfo(
            Index: index,
            Description: description,
            DedicatedVideoMemoryBytes: (long)(ulong)desc.DedicatedVideoMemory,
            DedicatedSystemMemoryBytes: (long)(ulong)desc.DedicatedSystemMemory,
            SharedSystemMemoryBytes: (long)(ulong)desc.SharedSystemMemory,
            VendorId: desc.VendorId,
            DeviceId: desc.DeviceId,
            Flavor: flavor);
    }

    private static string ExtractDescription(in AdapterDesc1 desc)
    {
        fixed (char* p = desc.Description)
        {
            var sb = new StringBuilder(128);
            for (var i = 0; i < 128 && p[i] != '\0'; i++)
            {
                sb.Append(p[i]);
            }

            return sb.ToString();
        }
    }
}
