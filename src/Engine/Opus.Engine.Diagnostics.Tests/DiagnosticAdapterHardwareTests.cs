using System;
using FluentAssertions;
using Opus.Engine.Diagnostics;
using Xunit;

namespace Opus.Engine.Diagnostics.Tests;

public sealed class DiagnosticAdapterHardwareTests
{
    private const long BytesPerMegabyte = 1024 * 1024;

    [Fact]
    public void Create_carries_identity_and_rounds_vram_to_megabytes()
    {
        var hardware = DiagnosticAdapterHardware.Create(
            "NVIDIA",
            vendorId: 0x10DE,
            deviceId: 0x2684,
            dedicatedVideoMemoryBytes: 24L * 1024 * BytesPerMegabyte,
            adapterClass: DiagnosticAdapterClass.Discrete);

        hardware.VendorName.Should().Be("NVIDIA");
        hardware.VendorId.Should().Be(0x10DEu);
        hardware.DeviceId.Should().Be(0x2684u);
        hardware.DedicatedVideoMemoryBytes.Should().Be(24L * 1024 * BytesPerMegabyte);
        hardware.DedicatedVideoMemoryMegabytes.Should().Be(24L * 1024);
        hardware.Class.Should().Be(DiagnosticAdapterClass.Discrete);
    }

    [Fact]
    public void Create_rejects_negative_video_memory()
    {
        var act = () => DiagnosticAdapterHardware.Create(
            "AMD", 0x1002, 0x744C, dedicatedVideoMemoryBytes: -1, DiagnosticAdapterClass.Discrete);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Create_rejects_blank_vendor_name()
    {
        var act = () => DiagnosticAdapterHardware.Create(
            " ", 0, 0, 0, DiagnosticAdapterClass.Unknown);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Unknown_preset_has_zeroed_identity_and_unknown_class()
    {
        var unknown = DiagnosticAdapterHardware.Unknown;

        unknown.VendorName.Should().Be(DiagnosticAdapterHardware.UnknownVendorName);
        unknown.VendorId.Should().Be(0u);
        unknown.DeviceId.Should().Be(0u);
        unknown.DedicatedVideoMemoryBytes.Should().Be(0);
        unknown.DedicatedVideoMemoryMegabytes.Should().Be(0);
        unknown.Class.Should().Be(DiagnosticAdapterClass.Unknown);
    }
}
