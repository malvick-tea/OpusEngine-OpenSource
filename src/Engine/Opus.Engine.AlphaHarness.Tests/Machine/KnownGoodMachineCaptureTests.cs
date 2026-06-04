using System;
using System.IO;
using FluentAssertions;
using Opus.Engine.AlphaHarness.Machine;
using Xunit;

namespace Opus.Engine.AlphaHarness.Tests.Machine;

public sealed class KnownGoodMachineCaptureTests : IDisposable
{
    private static readonly DateTimeOffset FixedTimestamp =
        new(2026, 5, 27, 9, 30, 0, TimeSpan.Zero);

    private readonly string _directory;

    public KnownGoodMachineCaptureTests()
    {
        _directory = Path.Combine(Path.GetTempPath(), $"opus-machine-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_directory);
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_directory, recursive: true);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    [Fact]
    public void Capture_records_runtime_information()
    {
        var profile = KnownGoodMachineCapture.Capture(
            profileName: "ci-host",
            capturedAtUtc: FixedTimestamp,
            graphicsAdapterName: "Test Adapter",
            notes: "ci");

        profile.ProfileName.Should().Be("ci-host");
        profile.CapturedAtUtc.Should().Be(FixedTimestamp);
        profile.GraphicsAdapterName.Should().Be("Test Adapter");
        profile.Notes.Should().Be("ci");
        profile.LogicalProcessorCount.Should().BeGreaterThan(0);
        profile.DotnetRuntimeVersion.Should().NotBeNullOrWhiteSpace();
        profile.OperatingSystemDescription.Should().NotBeNullOrWhiteSpace();
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    public void Capture_rejects_empty_profile_name(string profileName)
    {
        Action act = () => KnownGoodMachineCapture.Capture(profileName, FixedTimestamp, null, null);

        act.Should().Throw<ArgumentException>().WithMessage("*profileName*");
    }

    [Fact]
    public void Capture_normalises_blank_adapter_and_notes_to_null()
    {
        var profile = KnownGoodMachineCapture.Capture("ci-host", FixedTimestamp, "   ", "\t");

        profile.GraphicsAdapterName.Should().BeNull();
        profile.Notes.Should().BeNull();
    }

    [Fact]
    public void Serialise_round_trips_through_try_parse()
    {
        var profile = KnownGoodMachineCapture.Capture("ci-host", FixedTimestamp, "Adapter X", null);

        var json = KnownGoodMachineCapture.Serialise(profile);
        var parsed = KnownGoodMachineCapture.TryParse(json);

        parsed.Should().NotBeNull();
        parsed!.ProfileName.Should().Be(profile.ProfileName);
        parsed.OperatingSystemFamily.Should().Be(profile.OperatingSystemFamily);
        parsed.LogicalProcessorCount.Should().Be(profile.LogicalProcessorCount);
        parsed.GraphicsAdapterName.Should().Be(profile.GraphicsAdapterName);
        parsed.CapturedAtUtc.Should().Be(profile.CapturedAtUtc);
    }

    [Fact]
    public void Try_parse_returns_null_for_invalid_json()
    {
        KnownGoodMachineCapture.TryParse("{ not json").Should().BeNull();
    }

    [Fact]
    public void Try_parse_returns_null_for_invalid_payload()
    {
        var brokenJson = "{ \"profileName\": \"\", \"operatingSystemDescription\": \"x\", " +
            "\"processArchitecture\": \"X64\", \"logicalProcessorCount\": 1, " +
            "\"dotnetRuntimeVersion\": \".NET 8.0\", \"capturedAtUtc\": \"2026-05-27T00:00:00Z\", " +
            "\"operatingSystemFamily\": \"Windows\" }";

        KnownGoodMachineCapture.TryParse(brokenJson).Should().BeNull();
    }

    [Fact]
    public void Try_load_returns_null_for_empty_path()
    {
        KnownGoodMachineCapture.TryLoad("  ").Should().BeNull();
        KnownGoodMachineCapture.TryLoad(string.Empty).Should().BeNull();
    }

    [Fact]
    public void Try_load_round_trips_through_disk()
    {
        var profile = KnownGoodMachineCapture.Capture("ci-host", FixedTimestamp, "Adapter X", "note");
        var path = Path.Combine(_directory, "profile.json");
        File.WriteAllText(path, KnownGoodMachineCapture.Serialise(profile));

        var loaded = KnownGoodMachineCapture.TryLoad(path);

        loaded.Should().NotBeNull();
        loaded!.ProfileName.Should().Be(profile.ProfileName);
        loaded.Notes.Should().Be("note");
    }

    [Fact]
    public void Try_load_returns_null_for_missing_file()
    {
        var missing = Path.Combine(_directory, "missing-profile.json");

        KnownGoodMachineCapture.TryLoad(missing).Should().BeNull();
    }
}
