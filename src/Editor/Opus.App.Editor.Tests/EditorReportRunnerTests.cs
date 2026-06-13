using System.IO;
using FluentAssertions;
using Opus.App.Editor.Cli;
using Opus.App.Editor.Run;
using Opus.Content.Sample;
using Opus.Editor.Core;
using Xunit;

namespace Opus.App.Editor.Tests;

public sealed class EditorReportRunnerTests
{
    private static string SceneWith(TempDirectory temp, string assetRef)
    {
        var scenePath = temp.File("h.scene.json");
        EditorConsoleRunner.RunNew(new EditorArgs(EditorMode.New, scenePath, "Harbor", string.Empty), new CapturingLog());
        EditorSceneAuthoringRunner.RunPlace(
            new EditorArgs(EditorMode.Place, scenePath, null, string.Empty, assetRef), new CapturingLog());
        return scenePath;
    }

    [Fact]
    public void Report_counts_a_resolvable_model()
    {
        using var temp = new TempDirectory();
        Directory.CreateDirectory(Path.Combine(temp.Root, "models"));
        File.WriteAllBytes(Path.Combine(temp.Root, "models", "tank.glb"), SampleAlphaTankGltfWriter.BuildGlb());
        var scenePath = SceneWith(temp, "models/tank.glb");
        var log = new CapturingLog();

        var code = EditorReportRunner.RunReport(
            new EditorArgs(EditorMode.Report, scenePath, null, string.Empty, ContentRoot: temp.Root), log);

        code.Should().Be(EditorConsoleRunner.ExitOk);
        log.Joined.Should().Contain("models/tank.glb");
        log.Joined.Should().Contain("[ok]");
    }

    [Fact]
    public void Report_flags_a_missing_asset()
    {
        using var temp = new TempDirectory();
        var scenePath = SceneWith(temp, "models/ghost.glb");
        var log = new CapturingLog();

        var code = EditorReportRunner.RunReport(
            new EditorArgs(EditorMode.Report, scenePath, null, string.Empty, ContentRoot: temp.Root), log);

        code.Should().Be(EditorConsoleRunner.ExitOk);
        log.Joined.Should().Contain("MISSING");
    }

    [Fact]
    public void Report_without_a_path_returns_usage()
    {
        var code = EditorReportRunner.RunReport(
            new EditorArgs(EditorMode.Report, null, null, string.Empty), new CapturingLog());

        code.Should().Be(EditorConsoleRunner.ExitUsage);
    }

    [Fact]
    public void Report_summarises_the_scene_lights_by_kind()
    {
        using var temp = new TempDirectory();
        var scenePath = temp.File("h.scene.json");
        EditorConsoleRunner.RunNew(
            new EditorArgs(EditorMode.New, scenePath, "Harbor", string.Empty), new CapturingLog());
        EditorLightRunner.RunAdd(
            new EditorArgs(
                EditorMode.LightAdd, scenePath, "sun", string.Empty, Light: new LightArgs(SceneLightKind.Directional)),
            new CapturingLog());
        EditorLightRunner.RunAdd(
            new EditorArgs(
                EditorMode.LightAdd, scenePath, "lamp", string.Empty, Light: new LightArgs(SceneLightKind.Point)),
            new CapturingLog());
        var log = new CapturingLog();

        EditorReportRunner.RunReport(
            new EditorArgs(EditorMode.Report, scenePath, null, string.Empty, ContentRoot: temp.Root), log);

        log.Joined.Should().Contain("Lights: 2");
        log.Joined.Should().Contain("1 directional");
        log.Joined.Should().Contain("1 point");
    }
}
