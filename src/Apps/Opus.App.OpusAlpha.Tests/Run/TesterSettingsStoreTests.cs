using System;
using System.IO;
using FluentAssertions;
using Opus.App.OpusAlpha.Run;
using Opus.Engine.AlphaHarness.Scenes;
using Opus.Engine.Diagnostics.Overlay;
using Opus.Foundation;
using Xunit;

namespace Opus.App.OpusAlpha.Tests.Run;

public sealed class TesterSettingsStoreTests : IDisposable
{
    private readonly string _root =
        Path.Combine(Path.GetTempPath(), "opus-tester-settings-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public void LoadOrCreate_seeds_a_default_file_when_missing()
    {
        var path = Path.Combine(_root, "settings.json");

        var settings = TesterSettingsStore.LoadOrCreate(path, NullLog.Instance);

        settings.Should().Be(TesterSettings.Default);
        File.Exists(path).Should().BeTrue("a missing settings file is seeded so the tester can edit it");
    }

    [Fact]
    public void Round_trips_non_default_settings()
    {
        var path = Path.Combine(_root, "settings.json");
        var saved = new TesterSettings(
            AlphaSceneScale.Large, DiagnosticOverlayLevel.Off, EnableFrameBudget: true, EnableAsyncLogging: true);
        TesterSettingsStore.TrySave(path, saved, NullLog.Instance).Should().BeTrue();

        var loaded = TesterSettingsStore.LoadOrCreate(path, NullLog.Instance);

        loaded.Should().Be(saved);
    }

    [Fact]
    public void Corrupt_file_falls_back_to_defaults_and_is_left_untouched()
    {
        var path = Path.Combine(_root, "settings.json");
        Directory.CreateDirectory(_root);
        File.WriteAllText(path, "{ not valid json");

        var settings = TesterSettingsStore.LoadOrCreate(path, NullLog.Instance);

        settings.Should().Be(TesterSettings.Default);
        File.ReadAllText(path).Should().Be(
            "{ not valid json", "a corrupt file is preserved for inspection, not overwritten");
    }

    [Fact]
    public void Schema_version_mismatch_falls_back_to_defaults()
    {
        var path = Path.Combine(_root, "settings.json");
        Directory.CreateDirectory(_root);
        var futureVersionJson =
            "{\"schemaVersion\": 9999, \"settings\": {\"sceneScale\": \"Large\", \"overlayLevel\": \"Off\", " +
            "\"enableFrameBudget\": true, \"enableAsyncLogging\": true}}";
        File.WriteAllText(path, futureVersionJson);

        var settings = TesterSettingsStore.LoadOrCreate(path, NullLog.Instance);

        settings.Should().Be(TesterSettings.Default);
    }

    [Fact]
    public void Blank_path_returns_defaults_without_touching_disk()
    {
        var settings = TesterSettingsStore.LoadOrCreate("   ", NullLog.Instance);

        settings.Should().Be(TesterSettings.Default);
        Directory.Exists(_root).Should().BeFalse();
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }
}
