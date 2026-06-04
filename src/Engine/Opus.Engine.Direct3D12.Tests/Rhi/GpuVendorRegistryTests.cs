using FluentAssertions;
using Opus.Engine.Rhi.Direct3D12;
using Xunit;

namespace Opus.Engine.Direct3D12.Tests.Rhi;

public sealed class GpuVendorRegistryTests
{
    [Theory]
    [InlineData(GpuVendorRegistry.NvidiaVendorId, GpuVendor.Nvidia)]
    [InlineData(GpuVendorRegistry.AmdVendorId, GpuVendor.Amd)]
    [InlineData(GpuVendorRegistry.IntelVendorId, GpuVendor.Intel)]
    [InlineData(GpuVendorRegistry.MicrosoftVendorId, GpuVendor.Microsoft)]
    [InlineData(GpuVendorRegistry.QualcommVendorId, GpuVendor.Qualcomm)]
    public void Resolve_maps_known_pci_vendor_ids(uint vendorId, GpuVendor expected)
    {
        GpuVendorRegistry.Resolve(vendorId).Should().Be(expected);
    }

    [Fact]
    public void Resolve_returns_unknown_for_unrecognised_vendor_id()
    {
        GpuVendorRegistry.Resolve(0xDEAD).Should().Be(GpuVendor.Unknown);
    }

    [Theory]
    [InlineData(GpuVendor.Nvidia, "NVIDIA")]
    [InlineData(GpuVendor.Amd, "AMD")]
    [InlineData(GpuVendor.Intel, "Intel")]
    [InlineData(GpuVendor.Microsoft, "Microsoft")]
    [InlineData(GpuVendor.Qualcomm, "Qualcomm")]
    [InlineData(GpuVendor.Unknown, "unknown")]
    public void DisplayName_is_stable_for_each_vendor(GpuVendor vendor, string expected)
    {
        GpuVendorRegistry.DisplayName(vendor).Should().Be(expected);
    }

    [Fact]
    public void AdapterInfo_resolves_vendor_and_display_name_from_vendor_id()
    {
        var adapter = new AdapterInfo(
            Index: 0,
            Description: "NVIDIA GeForce RTX 4090",
            DedicatedVideoMemoryBytes: 24L * 1024 * 1024 * 1024,
            DedicatedSystemMemoryBytes: 0,
            SharedSystemMemoryBytes: 0,
            VendorId: GpuVendorRegistry.NvidiaVendorId,
            DeviceId: 0x2684,
            Flavor: AdapterFlavor.Discrete);

        adapter.Vendor.Should().Be(GpuVendor.Nvidia);
        adapter.VendorDisplayName.Should().Be("NVIDIA");
    }
}
