using System;

namespace Opus.Engine.Diagnostics;

/// <summary>
/// Renderer-neutral hardware identity of the active graphics adapter: silicon vendor,
/// raw PCI vendor/device ids, dedicated video memory, and adapter class. Shared by the
/// overlay adapter snapshot and the failure-report adapter snapshot so the two tester
/// surfaces describe the GPU the same way. Carried as flat primitives (no D3D12 backend
/// types) to keep the Diagnostics layer Foundation-only; the host flattens its live
/// adapter info into this record.
/// </summary>
public sealed record DiagnosticAdapterHardware(
    string VendorName,
    uint VendorId,
    uint DeviceId,
    long DedicatedVideoMemoryBytes,
    DiagnosticAdapterClass Class)
{
    private const long BytesPerMegabyte = 1024 * 1024;

    /// <summary>Vendor name used when the adapter hardware identity is not available.</summary>
    public const string UnknownVendorName = "unknown";

    /// <summary>Hardware identity used when no live adapter exists (startup failure, or a
    /// legacy snapshot built before the host captured adapter hardware).</summary>
    public static DiagnosticAdapterHardware Unknown { get; } = new(
        UnknownVendorName,
        VendorId: 0,
        DeviceId: 0,
        DedicatedVideoMemoryBytes: 0,
        Class: DiagnosticAdapterClass.Unknown);

    /// <summary>Dedicated video memory in whole megabytes — the unit tester-facing
    /// surfaces display, computed once here so every surface rounds identically.</summary>
    public long DedicatedVideoMemoryMegabytes => DedicatedVideoMemoryBytes / BytesPerMegabyte;

    /// <summary>Creates a validated hardware identity. Dedicated video memory must be
    /// non-negative; the vendor name must be present (callers pass
    /// <see cref="UnknownVendorName"/> when the vendor is unrecognised).</summary>
    public static DiagnosticAdapterHardware Create(
        string vendorName,
        uint vendorId,
        uint deviceId,
        long dedicatedVideoMemoryBytes,
        DiagnosticAdapterClass adapterClass)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(vendorName);
        if (dedicatedVideoMemoryBytes < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(dedicatedVideoMemoryBytes),
                "Dedicated video memory must not be negative.");
        }

        return new DiagnosticAdapterHardware(
            vendorName,
            vendorId,
            deviceId,
            dedicatedVideoMemoryBytes,
            adapterClass);
    }
}
