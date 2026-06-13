using FluentAssertions;
using Opus.App.Editor.Cli;
using Opus.App.Editor.Run;
using Opus.Editor.Core;
using Xunit;

namespace Opus.App.Editor.Tests;

public sealed class EditorLightRunnerTests
{
    private static string NewScene(TempDirectory temp)
    {
        string path = temp.File("h.scene.json");
        EditorConsoleRunner.RunNew(new EditorArgs(EditorMode.New, path, "Harbor", string.Empty), new CapturingLog());
        return path;
    }

    private static int AddLight(string path, string? name, LightArgs light, Float3? position, CapturingLog log) =>
        EditorLightRunner.RunAdd(
            new EditorArgs(EditorMode.LightAdd, path, name, string.Empty, Position: position, Light: light), log);

    [Fact]
    public void Light_add_directional_writes_a_light_to_the_mirror()
    {
        using var temp = new TempDirectory();
        string path = NewScene(temp);
        var log = new CapturingLog();

        var code = AddLight(path, "sun", new LightArgs(SceneLightKind.Directional), null, log);

        code.Should().Be(EditorConsoleRunner.ExitOk);
        log.Joined.Should().Contain("light \"sun\"");
        log.Joined.Should().Contain("kind directional");
        log.Joined.Should().Contain("direction (0, -1, 0)");
    }

    [Fact]
    public void Light_add_point_applies_colour_intensity_position_and_range()
    {
        using var temp = new TempDirectory();
        string path = NewScene(temp);
        var spec = new LightArgs(
            SceneLightKind.Point, Color: new Float3(1f, 0.5f, 0.25f), Intensity: 3f, Range: 12f);
        var log = new CapturingLog();

        AddLight(path, "lamp", spec, new Float3(2f, 4f, 0f), log);

        log.Joined.Should().Contain("kind point");
        log.Joined.Should().Contain("color (1, 0.5, 0.25)");
        log.Joined.Should().Contain("intensity 3");
        log.Joined.Should().Contain("position (2, 4, 0)");
        log.Joined.Should().Contain("range 12");
        log.Joined.Should().NotContain("direction");
    }

    [Fact]
    public void Light_add_spot_includes_the_cone()
    {
        using var temp = new TempDirectory();
        string path = NewScene(temp);
        var spec = new LightArgs(
            SceneLightKind.Spot, ConeInnerAngleDegrees: 15f, ConeOuterAngleDegrees: 35f);
        var log = new CapturingLog();

        AddLight(path, "torch", spec, null, log);

        log.Joined.Should().Contain("kind spot");
        log.Joined.Should().Contain("cone (15, 35)");
    }

    [Fact]
    public void Light_add_without_a_name_defaults_to_the_kind()
    {
        using var temp = new TempDirectory();
        string path = NewScene(temp);
        var log = new CapturingLog();

        AddLight(path, null, new LightArgs(SceneLightKind.Point), null, log);

        log.Joined.Should().Contain("light \"point\"");
    }

    [Fact]
    public void Light_add_persists_so_remove_can_drop_it()
    {
        using var temp = new TempDirectory();
        string path = NewScene(temp);
        AddLight(path, "sun", new LightArgs(SceneLightKind.Directional), null, new CapturingLog());
        var log = new CapturingLog();

        var code = EditorLightRunner.RunRemove(
            new EditorArgs(EditorMode.LightRemove, path, null, string.Empty, AssetRef: "1"), log);

        code.Should().Be(EditorConsoleRunner.ExitOk);
        log.Joined.Should().Contain("Removed light #1");
        log.Joined.Should().NotContain("light \"sun\"");
    }

    [Fact]
    public void Show_reports_the_light_count()
    {
        using var temp = new TempDirectory();
        string path = NewScene(temp);
        AddLight(path, "sun", new LightArgs(SceneLightKind.Directional), null, new CapturingLog());
        var log = new CapturingLog();

        EditorConsoleRunner.RunShow(new EditorArgs(EditorMode.Show, path, null, string.Empty), log, dslOnly: false);

        log.Joined.Should().Contain("1 light(s)");
    }

    [Fact]
    public void Light_remove_of_a_missing_light_is_a_usage_error()
    {
        using var temp = new TempDirectory();
        string path = NewScene(temp);

        var code = EditorLightRunner.RunRemove(
            new EditorArgs(EditorMode.LightRemove, path, null, string.Empty, AssetRef: "99"), new CapturingLog());

        code.Should().Be(EditorConsoleRunner.ExitUsage);
    }

    [Fact]
    public void Light_remove_with_a_non_numeric_id_is_a_usage_error()
    {
        using var temp = new TempDirectory();
        string path = NewScene(temp);

        var code = EditorLightRunner.RunRemove(
            new EditorArgs(EditorMode.LightRemove, path, null, string.Empty, AssetRef: "notanumber"), new CapturingLog());

        code.Should().Be(EditorConsoleRunner.ExitUsage);
    }

    [Fact]
    public void Light_add_without_a_light_spec_is_a_usage_error()
    {
        using var temp = new TempDirectory();
        string path = NewScene(temp);

        var code = EditorLightRunner.RunAdd(
            new EditorArgs(EditorMode.LightAdd, path, null, string.Empty), new CapturingLog());

        code.Should().Be(EditorConsoleRunner.ExitUsage);
    }

    [Fact]
    public void Light_edit_changes_the_given_fields_and_keeps_the_kind()
    {
        using var temp = new TempDirectory();
        string path = NewScene(temp);
        AddLight(path, "lamp", new LightArgs(SceneLightKind.Point), null, new CapturingLog());
        var log = new CapturingLog();

        var code = EditorLightRunner.RunEdit(
            new EditorArgs(
                EditorMode.LightEdit, path, null, string.Empty, AssetRef: "1",
                Light: new LightArgs(null, Intensity: 4f, Range: 25f)),
            log);

        code.Should().Be(EditorConsoleRunner.ExitOk);
        log.Joined.Should().Contain("kind point", "the edit keeps the existing kind");
        log.Joined.Should().Contain("intensity 4");
        log.Joined.Should().Contain("range 25");
    }

    [Fact]
    public void Light_edit_can_rename_a_light()
    {
        using var temp = new TempDirectory();
        string path = NewScene(temp);
        AddLight(path, "lamp", new LightArgs(SceneLightKind.Point), null, new CapturingLog());
        var log = new CapturingLog();

        EditorLightRunner.RunEdit(
            new EditorArgs(EditorMode.LightEdit, path, "key", string.Empty, AssetRef: "1", Light: new LightArgs(null)),
            log);

        log.Joined.Should().Contain("light \"key\"");
        log.Joined.Should().NotContain("light \"lamp\"");
    }

    [Fact]
    public void Light_edit_of_a_missing_light_is_a_usage_error()
    {
        using var temp = new TempDirectory();
        string path = NewScene(temp);

        var code = EditorLightRunner.RunEdit(
            new EditorArgs(EditorMode.LightEdit, path, null, string.Empty, AssetRef: "99", Light: new LightArgs(null)),
            new CapturingLog());

        code.Should().Be(EditorConsoleRunner.ExitUsage);
    }
}
