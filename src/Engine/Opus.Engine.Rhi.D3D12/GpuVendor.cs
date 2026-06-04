namespace Opus.Engine.Rhi.Direct3D12;

/// <summary>
/// GPU silicon vendor resolved from a DXGI adapter's PCI vendor id. Used to turn the raw
/// <see cref="AdapterInfo.VendorId"/> integer into a name a tester or lead can read in a
/// diagnostics overlay, failure report, or screenshot without memorising PCI-SIG ids.
/// </summary>
public enum GpuVendor : byte
{
    /// <summary>Vendor id did not match a known desktop GPU vendor.</summary>
    Unknown = 0,

    /// <summary>NVIDIA (GeForce / RTX / Quadro).</summary>
    Nvidia = 1,

    /// <summary>AMD (Radeon).</summary>
    Amd = 2,

    /// <summary>Intel (UHD / Iris Xe / Arc).</summary>
    Intel = 3,

    /// <summary>Microsoft (WARP / Basic Render Driver software adapter).</summary>
    Microsoft = 4,

    /// <summary>Qualcomm (Adreno on Windows-on-ARM).</summary>
    Qualcomm = 5,
}

/// <summary>
/// Resolves a DXGI PCI vendor id to a <see cref="GpuVendor"/> and a stable display name.
/// The ids are PCI-SIG assignments — fixed deployment facts about the silicon, named here
/// as constants rather than scattered hex literals so a future vendor is one entry, not a
/// grep across the renderer.
/// </summary>
public static class GpuVendorRegistry
{
    /// <summary>PCI vendor id for NVIDIA Corporation.</summary>
    public const uint NvidiaVendorId = 0x10DE;

    /// <summary>PCI vendor id for Advanced Micro Devices (ATI/AMD graphics).</summary>
    public const uint AmdVendorId = 0x1002;

    /// <summary>PCI vendor id for Intel Corporation.</summary>
    public const uint IntelVendorId = 0x8086;

    /// <summary>PCI vendor id for Microsoft (WARP / Basic Render Driver).</summary>
    public const uint MicrosoftVendorId = 0x1414;

    /// <summary>PCI vendor id for Qualcomm (Adreno on Windows-on-ARM).</summary>
    public const uint QualcommVendorId = 0x5143;

    private const string NvidiaDisplayName = "NVIDIA";
    private const string AmdDisplayName = "AMD";
    private const string IntelDisplayName = "Intel";
    private const string MicrosoftDisplayName = "Microsoft";
    private const string QualcommDisplayName = "Qualcomm";
    private const string UnknownDisplayName = "unknown";

    /// <summary>Maps a PCI vendor id to a known <see cref="GpuVendor"/>.</summary>
    public static GpuVendor Resolve(uint vendorId) => vendorId switch
    {
        NvidiaVendorId => GpuVendor.Nvidia,
        AmdVendorId => GpuVendor.Amd,
        IntelVendorId => GpuVendor.Intel,
        MicrosoftVendorId => GpuVendor.Microsoft,
        QualcommVendorId => GpuVendor.Qualcomm,
        _ => GpuVendor.Unknown,
    };

    /// <summary>Returns a stable, human-readable vendor name for diagnostics surfaces.</summary>
    public static string DisplayName(GpuVendor vendor) => vendor switch
    {
        GpuVendor.Nvidia => NvidiaDisplayName,
        GpuVendor.Amd => AmdDisplayName,
        GpuVendor.Intel => IntelDisplayName,
        GpuVendor.Microsoft => MicrosoftDisplayName,
        GpuVendor.Qualcomm => QualcommDisplayName,
        _ => UnknownDisplayName,
    };
}
