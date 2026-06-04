namespace Opus.Engine.Diagnostics;

/// <summary>
/// Renderer-neutral classification of the active graphics adapter, surfaced in tester
/// diagnostics so a bug report distinguishes a discrete GPU from an integrated one or the
/// software fallback. Owned by the Diagnostics layer (no dependency on the D3D12 backend's
/// own adapter enum) so the contract stays Foundation-only; the host maps its backend
/// adapter flavor onto this enum.
/// </summary>
public enum DiagnosticAdapterClass : byte
{
    /// <summary>Adapter class was not reported (e.g. a failure before the device existed).</summary>
    Unknown = 0,

    /// <summary>Discrete GPU with dedicated video memory.</summary>
    Discrete = 1,

    /// <summary>Integrated GPU sharing system memory.</summary>
    Integrated = 2,

    /// <summary>Software rasteriser (WARP / Basic Render Driver).</summary>
    Software = 3,
}
