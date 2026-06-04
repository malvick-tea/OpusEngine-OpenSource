using System;
using FluentAssertions;
using Opus.Engine.AlphaHarness.Machine;
using Xunit;

namespace Opus.Engine.AlphaHarness.Tests.Machine;

public sealed class KnownGoodMachineProfileTests
{
    private static KnownGoodMachineProfile ValidBaseline() => new(
        ProfileName: "windows-d3d12-2026",
        OperatingSystemFamily: MachineOperatingSystemFamily.Windows,
        OperatingSystemDescription: "Microsoft Windows 11 Pro",
        ProcessArchitecture: "X64",
        LogicalProcessorCount: 16,
        DotnetRuntimeVersion: ".NET 8.0.10",
        GraphicsAdapterName: "NVIDIA GeForce RTX 4090",
        CapturedAtUtc: new DateTimeOffset(2026, 5, 27, 9, 0, 0, TimeSpan.Zero),
        Notes: "primary lead machine");

    [Fact]
    public void Valid_profile_validates_cleanly()
    {
        ValidBaseline().Invoking(p => p.Validate()).Should().NotThrow();
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    public void Empty_profile_name_rejected(string profileName)
    {
        var profile = ValidBaseline() with { ProfileName = profileName };

        profile.Invoking(p => p.Validate())
            .Should().Throw<ArgumentException>()
            .WithMessage("*ProfileName*");
    }

    [Fact]
    public void Empty_operating_system_description_rejected()
    {
        var profile = ValidBaseline() with { OperatingSystemDescription = string.Empty };

        profile.Invoking(p => p.Validate())
            .Should().Throw<ArgumentException>()
            .WithMessage("*OperatingSystemDescription*");
    }

    [Fact]
    public void Empty_process_architecture_rejected()
    {
        var profile = ValidBaseline() with { ProcessArchitecture = string.Empty };

        profile.Invoking(p => p.Validate())
            .Should().Throw<ArgumentException>()
            .WithMessage("*ProcessArchitecture*");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-4)]
    public void Non_positive_processor_count_rejected(int count)
    {
        var profile = ValidBaseline() with { LogicalProcessorCount = count };

        profile.Invoking(p => p.Validate())
            .Should().Throw<ArgumentOutOfRangeException>()
            .WithMessage("*LogicalProcessorCount*");
    }

    [Fact]
    public void Empty_dotnet_runtime_version_rejected()
    {
        var profile = ValidBaseline() with { DotnetRuntimeVersion = "  " };

        profile.Invoking(p => p.Validate())
            .Should().Throw<ArgumentException>()
            .WithMessage("*DotnetRuntimeVersion*");
    }

    [Fact]
    public void Profile_with_null_adapter_and_notes_validates()
    {
        var profile = ValidBaseline() with { GraphicsAdapterName = null, Notes = null };

        profile.Invoking(p => p.Validate()).Should().NotThrow();
    }
}
