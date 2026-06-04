using System;
using System.Collections.Generic;
using System.Globalization;
using Opus.Engine.Diagnostics;
using Opus.Engine.Rhi.Direct3D12;
using Opus.Foundation;

namespace Opus.Engine.Host.Windows.Direct3D12.Screenshot;

/// <summary>Builds the <see cref="D3D12ScreenshotMetadata"/> attached to PNG captures
/// from the live D3D12 host: product identity, build configuration, framework, OS,
/// adapter name + hardware identity (vendor / VRAM / class), frame index, and capture
/// timestamp. Centralised here so the PNG tEXt fields stay consistent across the sample
/// host, smoke tests, and the future crash reporter — a tester emailing a screenshot
/// reveals build and GPU context without a separate envelope.</summary>
public static class D3D12HostScreenshotMetadata
{
    public const string KeywordProduct = "opus.product";
    public const string KeywordVersion = "opus.version";
    public const string KeywordChannel = "opus.channel";
    public const string KeywordAssembly = "opus.assembly";
    public const string KeywordConfiguration = "opus.configuration";
    public const string KeywordFramework = "opus.framework";
    public const string KeywordOperatingSystem = "opus.os";
    public const string KeywordAdapter = "opus.adapter";
    public const string KeywordAdapterVendor = "opus.adapter.vendor";
    public const string KeywordAdapterVendorId = "opus.adapter.vendorId";
    public const string KeywordAdapterDeviceId = "opus.adapter.deviceId";
    public const string KeywordAdapterVramMb = "opus.adapter.vramMb";
    public const string KeywordAdapterClass = "opus.adapter.class";
    public const string KeywordFrameIndex = "opus.frame";
    public const string KeywordCapturedAtUtc = "opus.capturedAtUtc";

    private const int EntryCount = 15;

    public static D3D12ScreenshotMetadata Build(
        BuildInfo buildInfo,
        string adapterName,
        DiagnosticAdapterHardware adapterHardware,
        ulong frameIndex,
        DateTimeOffset capturedAtUtc)
    {
        ArgumentNullException.ThrowIfNull(buildInfo);
        ArgumentException.ThrowIfNullOrWhiteSpace(adapterName);
        ArgumentNullException.ThrowIfNull(adapterHardware);

        var entries = new List<KeyValuePair<string, string>>(EntryCount)
        {
            new(KeywordProduct, buildInfo.Engine.DisplayName),
            new(KeywordVersion, buildInfo.Engine.ProductVersion.ToString()),
            new(KeywordChannel, buildInfo.Engine.ReleaseChannel),
            new(KeywordAssembly, buildInfo.ProjectName),
            new(KeywordConfiguration, buildInfo.BuildConfiguration),
            new(KeywordFramework, buildInfo.FrameworkDescription),
            new(KeywordOperatingSystem, buildInfo.OperatingSystem),
            new(KeywordAdapter, adapterName),
            new(KeywordAdapterVendor, adapterHardware.VendorName),
            new(KeywordAdapterVendorId, FormatPciId(adapterHardware.VendorId)),
            new(KeywordAdapterDeviceId, FormatPciId(adapterHardware.DeviceId)),
            new(KeywordAdapterVramMb, adapterHardware.DedicatedVideoMemoryMegabytes.ToString(CultureInfo.InvariantCulture)),
            new(KeywordAdapterClass, adapterHardware.Class.ToString().ToLowerInvariant()),
            new(KeywordFrameIndex, frameIndex.ToString(CultureInfo.InvariantCulture)),
            new(KeywordCapturedAtUtc, capturedAtUtc.UtcDateTime.ToString("o", CultureInfo.InvariantCulture)),
        };

        return D3D12ScreenshotMetadata.From(entries);
    }

    private static string FormatPciId(uint id) => string.Create(CultureInfo.InvariantCulture, $"0x{id:X4}");
}
