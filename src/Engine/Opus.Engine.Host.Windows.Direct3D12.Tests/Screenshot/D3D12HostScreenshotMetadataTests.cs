using System;
using System.Linq;
using FluentAssertions;
using Opus.Engine.Diagnostics;
using Opus.Engine.Host.Windows.Direct3D12.Screenshot;
using Opus.Foundation;
using Xunit;

namespace Opus.Engine.Host.Windows.Direct3D12.Tests.Screenshot;

public sealed class D3D12HostScreenshotMetadataTests
{
    private static readonly DiagnosticAdapterHardware Hardware = DiagnosticAdapterHardware.Create(
        "NVIDIA",
        vendorId: 0x10DE,
        deviceId: 0x2684,
        dedicatedVideoMemoryBytes: 24L * 1024 * 1024 * 1024,
        adapterClass: DiagnosticAdapterClass.Discrete);

    [Fact]
    public void Build_embeds_engine_identity_assembly_adapter_frame_and_timestamp()
    {
        var buildInfo = BuildInfo.Current;
        var capturedAt = new DateTimeOffset(2026, 5, 26, 12, 30, 45, TimeSpan.Zero);

        var metadata = D3D12HostScreenshotMetadata.Build(
            buildInfo,
            adapterName: "Test Adapter",
            adapterHardware: Hardware,
            frameIndex: 1234ul,
            capturedAtUtc: capturedAt);

        metadata.Count.Should().Be(15);
        ValueFor(metadata, D3D12HostScreenshotMetadata.KeywordProduct).Should().Be(buildInfo.Engine.DisplayName);
        ValueFor(metadata, D3D12HostScreenshotMetadata.KeywordVersion).Should().Be(buildInfo.Engine.ProductVersion.ToString());
        ValueFor(metadata, D3D12HostScreenshotMetadata.KeywordChannel).Should().Be(buildInfo.Engine.ReleaseChannel);
        ValueFor(metadata, D3D12HostScreenshotMetadata.KeywordAssembly).Should().Be(buildInfo.ProjectName);
        ValueFor(metadata, D3D12HostScreenshotMetadata.KeywordConfiguration).Should().Be(buildInfo.BuildConfiguration);
        ValueFor(metadata, D3D12HostScreenshotMetadata.KeywordFramework).Should().Be(buildInfo.FrameworkDescription);
        ValueFor(metadata, D3D12HostScreenshotMetadata.KeywordOperatingSystem).Should().Be(buildInfo.OperatingSystem);
        ValueFor(metadata, D3D12HostScreenshotMetadata.KeywordAdapter).Should().Be("Test Adapter");
        ValueFor(metadata, D3D12HostScreenshotMetadata.KeywordFrameIndex).Should().Be("1234");
        ValueFor(metadata, D3D12HostScreenshotMetadata.KeywordCapturedAtUtc).Should().Be("2026-05-26T12:30:45.0000000Z");
    }

    [Fact]
    public void Build_embeds_adapter_hardware_identity_chunks()
    {
        var metadata = D3D12HostScreenshotMetadata.Build(
            BuildInfo.Current,
            adapterName: "NVIDIA GeForce RTX 4090",
            adapterHardware: Hardware,
            frameIndex: 0ul,
            capturedAtUtc: DateTimeOffset.UtcNow);

        ValueFor(metadata, D3D12HostScreenshotMetadata.KeywordAdapterVendor).Should().Be("NVIDIA");
        ValueFor(metadata, D3D12HostScreenshotMetadata.KeywordAdapterVendorId).Should().Be("0x10DE");
        ValueFor(metadata, D3D12HostScreenshotMetadata.KeywordAdapterDeviceId).Should().Be("0x2684");
        ValueFor(metadata, D3D12HostScreenshotMetadata.KeywordAdapterVramMb).Should().Be("24576");
        ValueFor(metadata, D3D12HostScreenshotMetadata.KeywordAdapterClass).Should().Be("discrete");
    }

    [Fact]
    public void Build_rejects_empty_adapter_name()
    {
        var act = () => D3D12HostScreenshotMetadata.Build(
            BuildInfo.Current,
            adapterName: " ",
            adapterHardware: Hardware,
            frameIndex: 0ul,
            capturedAtUtc: DateTimeOffset.UtcNow);

        act.Should().Throw<ArgumentException>();
    }

    private static string ValueFor(Rhi.Direct3D12.D3D12ScreenshotMetadata metadata, string keyword) =>
        metadata.Entries.Single(entry => entry.Keyword == keyword).Value;
}
