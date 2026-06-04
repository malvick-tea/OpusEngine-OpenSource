using System;
using FluentAssertions;
using Opus.Foundation;
using Opus.Persistence.Settings;
using Xunit;

namespace Opus.Persistence.Tests.Settings;

public sealed class JsonSettingsSerializerTests
{
    private const int SchemaVersion = 3;

    public enum SampleMode
    {
        Calm,
        Loud,
    }

    public sealed record SampleSettings(string Name, int Level, SampleMode Mode);

    [Fact]
    public void Round_trips_a_settings_record()
    {
        var settings = new SampleSettings("alpha", 7, SampleMode.Loud);

        var json = JsonSettingsSerializer.Serialize(settings, SchemaVersion);
        var result = JsonSettingsSerializer.Deserialize<SampleSettings>(json, SchemaVersion);

        result.IsOk.Should().BeTrue();
        result.Unwrap().Should().Be(settings);
    }

    [Fact]
    public void Writes_enums_as_names_for_human_editing()
    {
        var json = JsonSettingsSerializer.Serialize(new SampleSettings("x", 1, SampleMode.Loud), SchemaVersion);

        json.Should().Contain("Loud");
        json.Should().NotContain("\"mode\": 1");
    }

    [Fact]
    public void Rejects_a_schema_version_mismatch_as_settings_corrupt()
    {
        var json = JsonSettingsSerializer.Serialize(new SampleSettings("x", 1, SampleMode.Calm), SchemaVersion);

        var result = JsonSettingsSerializer.Deserialize<SampleSettings>(json, SchemaVersion + 1);

        result.IsErr.Should().BeTrue();
        result.UnwrapErr().Code.Should().Be(ErrorCode.SettingsCorrupt);
    }

    [Fact]
    public void Malformed_json_is_settings_corrupt_not_an_exception()
    {
        var result = JsonSettingsSerializer.Deserialize<SampleSettings>("{ this is not json", SchemaVersion);

        result.IsErr.Should().BeTrue();
        result.UnwrapErr().Code.Should().Be(ErrorCode.SettingsCorrupt);
    }

    [Fact]
    public void A_null_document_is_settings_corrupt()
    {
        var result = JsonSettingsSerializer.Deserialize<SampleSettings>("null", SchemaVersion);

        result.IsErr.Should().BeTrue();
        result.UnwrapErr().Code.Should().Be(ErrorCode.SettingsCorrupt);
    }

    [Fact]
    public void Serialize_rejects_a_below_minimum_schema_version()
    {
        var act = () => JsonSettingsSerializer.Serialize(new SampleSettings("x", 1, SampleMode.Calm), schemaVersion: 0);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }
}
