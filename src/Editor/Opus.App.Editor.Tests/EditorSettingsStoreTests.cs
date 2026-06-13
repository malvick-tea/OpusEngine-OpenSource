using System.IO;
using FluentAssertions;
using Opus.App.Editor.Run;
using Opus.Editor.Ui;
using Xunit;

namespace Opus.App.Editor.Tests;

public sealed class EditorSettingsStoreTests
{
    [Fact]
    public void Save_then_load_round_trips_the_settings()
    {
        using var temp = new TempDirectory();
        string path = temp.File("editor.settings.json");
        var settings = new EditorSettings(
            1600, 900, "harbor.scene.json", EditorLanguage.Russian, "campaign.project.json");

        EditorSettingsStore.TrySave(path, settings, new CapturingLog()).Should().BeTrue();
        var loaded = EditorSettingsStore.LoadOrCreate(path, new CapturingLog());

        loaded.Should().Be(settings);
    }

    [Fact]
    public void A_file_predating_the_project_field_loads_with_no_project()
    {
        // Additive like the language field: an older file simply lacks the member, and the ctor default
        // (null) applies — no schema bump, every existing settings file keeps loading.
        using var temp = new TempDirectory();
        string path = temp.File("editor.settings.json");
        File.WriteAllText(
            path,
            "{ \"schemaVersion\": 1, \"settings\": { \"windowWidth\": 1600, \"windowHeight\": 900, \"lastScenePath\": null, \"language\": \"Russian\" } }");

        var loaded = EditorSettingsStore.LoadOrCreate(path, new CapturingLog());

        loaded.Should().Be(new EditorSettings(1600, 900, null, EditorLanguage.Russian, null));
    }

    [Fact]
    public void A_file_predating_the_language_field_loads_as_english()
    {
        // The language field was added without a schema bump (English is enum value 0), so a v1 file written
        // before the field must still load — System.Text.Json fills the missing member with the ctor default.
        using var temp = new TempDirectory();
        string path = temp.File("editor.settings.json");
        File.WriteAllText(
            path,
            "{ \"schemaVersion\": 1, \"settings\": { \"windowWidth\": 1600, \"windowHeight\": 900, \"lastScenePath\": null } }");

        var loaded = EditorSettingsStore.LoadOrCreate(path, new CapturingLog());

        loaded.Should().Be(new EditorSettings(1600, 900, null, EditorLanguage.English));
    }

    [Fact]
    public void A_missing_file_is_seeded_with_the_defaults()
    {
        using var temp = new TempDirectory();
        string path = temp.File("editor.settings.json");

        var loaded = EditorSettingsStore.LoadOrCreate(path, new CapturingLog());

        loaded.Should().Be(EditorSettings.Default);
        File.Exists(path).Should().BeTrue("a missing file is seeded so the author has an editable profile");
    }

    [Fact]
    public void A_corrupt_file_falls_back_to_defaults_and_is_left_untouched()
    {
        using var temp = new TempDirectory();
        string path = temp.File("editor.settings.json");
        File.WriteAllText(path, "{ this is not valid settings json ");

        var loaded = EditorSettingsStore.LoadOrCreate(path, new CapturingLog());

        loaded.Should().Be(EditorSettings.Default);
        File.ReadAllText(path).Should().Contain("not valid", "the corrupt file is preserved for inspection");
    }
}
