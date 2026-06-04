namespace Opus.Engine.Rhi.Direct3D12;

/// <summary>
/// One enumerated DXGI adapter (GPU). Returned by <see cref="DxgiAdapterEnumerator"/>
/// in priority order (high-performance discrete GPU first, integrated next, basic
/// display adapter last).
///
/// Carried as a record so the caller can pass it through DI / config layer without
/// dragging Silk.NET types into the rest of the engine.
/// </summary>
public sealed record AdapterInfo(
    int Index,
    string Description,
    long DedicatedVideoMemoryBytes,
    long DedicatedSystemMemoryBytes,
    long SharedSystemMemoryBytes,
    uint VendorId,
    uint DeviceId,
    AdapterFlavor Flavor)
{
    /// <summary>Silicon vendor resolved from <see cref="VendorId"/>.</summary>
    public GpuVendor Vendor => GpuVendorRegistry.Resolve(VendorId);

    /// <summary>Stable, human-readable vendor name for diagnostics surfaces.</summary>
    public string VendorDisplayName => GpuVendorRegistry.DisplayName(Vendor);

    /// <summary>Human-readable banner line for log / crash dumps.</summary>
    public string ToBannerLine() =>
        $"[{Index}] {Description} ({Flavor}) — {DedicatedVideoMemoryBytes / (1024 * 1024)} MB VRAM";
}

/// <summary>Adapter category — the reason adapter selection prefers one over another.</summary>
public enum AdapterFlavor : byte
{
    /// <summary>Discrete GPU with dedicated VRAM. Preferred for rendering.</summary>
    Discrete = 0,

    /// <summary>Integrated GPU sharing system memory. Acceptable fallback.</summary>
    Integrated = 1,

    /// <summary>Microsoft Basic Render Driver (WARP). Software rasteriser, last resort.</summary>
    Software = 2,
}
