using FluentAssertions;
using Opus.App.Editor.Cli;
using Opus.App.Editor.Run;
using Opus.Editor.Ui;
using Xunit;

namespace Opus.App.Editor.Tests;

public sealed class EditorWindowLaunchTests
{
    [Fact]
    public void Without_settings_the_launch_uses_the_defaults_and_the_explicit_scene()
    {
        var launch = EditorWindowLaunch.Resolve(WindowArgs("harbor.scene.json"), settings: null);

        launch.ScenePath.Should().Be("harbor.scene.json");
        launch.Width.Should().Be(EditorSettings.DefaultWindowWidth);
        launch.Height.Should().Be(EditorSettings.DefaultWindowHeight);
    }

    [Fact]
    public void Persisted_size_and_remembered_scene_apply_when_no_scene_is_named()
    {
        var settings = new EditorSettings(1600, 900, "remembered.scene.json");

        var launch = EditorWindowLaunch.Resolve(WindowArgs(scenePath: null), settings);

        launch.ScenePath.Should().Be("remembered.scene.json");
        launch.Width.Should().Be(1600);
        launch.Height.Should().Be(900);
    }

    [Fact]
    public void An_explicit_scene_argument_wins_over_the_remembered_scene()
    {
        var settings = new EditorSettings(1600, 900, "remembered.scene.json");

        var launch = EditorWindowLaunch.Resolve(WindowArgs("explicit.scene.json"), settings);

        launch.ScenePath.Should().Be("explicit.scene.json");
        launch.Width.Should().Be(1600);
    }

    [Fact]
    public void Without_a_settings_file_or_an_explicit_lang_the_language_is_english()
    {
        var launch = EditorWindowLaunch.Resolve(WindowArgs(scenePath: null), settings: null);

        launch.Language.Should().Be(EditorLanguage.English);
    }

    [Fact]
    public void The_persisted_language_applies_when_no_lang_is_named()
    {
        var settings = new EditorSettings(1280, 720, null, EditorLanguage.Russian);

        var launch = EditorWindowLaunch.Resolve(WindowArgs(scenePath: null), settings);

        launch.Language.Should().Be(EditorLanguage.Russian);
    }

    [Fact]
    public void An_explicit_lang_wins_over_the_persisted_language()
    {
        var settings = new EditorSettings(1280, 720, null, EditorLanguage.Russian);

        var launch = EditorWindowLaunch.Resolve(WindowArgs(scenePath: null, EditorLanguage.English), settings);

        launch.Language.Should().Be(EditorLanguage.English);
    }

    [Fact]
    public void The_persisted_project_applies_when_no_project_is_named()
    {
        var settings = new EditorSettings(1280, 720, null, LastProjectPath: "campaign.project.json");

        var launch = EditorWindowLaunch.Resolve(WindowArgs(scenePath: null), settings);

        launch.ProjectPath.Should().Be("campaign.project.json");
    }

    [Fact]
    public void An_explicit_project_wins_over_the_persisted_one()
    {
        var settings = new EditorSettings(1280, 720, null, LastProjectPath: "remembered.project.json");
        var args = WindowArgs(scenePath: null) with { ProjectPath = "explicit.project.json" };

        var launch = EditorWindowLaunch.Resolve(args, settings);

        launch.ProjectPath.Should().Be("explicit.project.json");
    }

    [Fact]
    public void Without_a_settings_file_or_an_explicit_project_there_is_no_project_context()
    {
        var launch = EditorWindowLaunch.Resolve(WindowArgs(scenePath: null), settings: null);

        launch.ProjectPath.Should().BeNull();
    }

    private static EditorArgs WindowArgs(string? scenePath, EditorLanguage? language = null) =>
        new(EditorMode.Window, scenePath, null, string.Empty, Language: language);
}
