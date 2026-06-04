namespace Opus.Engine.Rhi;

/// <summary>
/// Per-device feature support flags. Queried by the renderer at boot to enable / disable
/// optional code paths (mesh shaders, ray tracing, async compute, variable-rate shading).
///
/// Capability flags rather than separate query methods so the renderer can express
/// "do I have everything I need?" as a single mask check at startup.
/// </summary>
[System.Flags]
public enum RhiCapabilities : uint
{
    None = 0,

    /// <summary>SM 6.5 mesh shaders + meshlets (per ADR-0014 virtual geometry plan).</summary>
    MeshShaders = 1u << 0,

    /// <summary>DXR 1.1 / VK_KHR_ray_tracing_pipeline — hardware ray tracing (per ADR-0014).</summary>
    RayTracing = 1u << 1,

    /// <summary>Dedicated async compute queue distinct from the graphics queue.</summary>
    AsyncCompute = 1u << 2,

    /// <summary>Async copy queue (DMA) — for asset uploads off the graphics queue.</summary>
    AsyncCopy = 1u << 3,

    /// <summary>SM 6.6 dynamic resources (ResourceDescriptorHeap).</summary>
    DynamicResources = 1u << 4,

    /// <summary>Variable-rate shading (Tier 1+).</summary>
    VariableRateShading = 1u << 5,

    /// <summary>SM 6.0 wave intrinsics across the full feature surface.</summary>
    WaveIntrinsics = 1u << 6,
}
