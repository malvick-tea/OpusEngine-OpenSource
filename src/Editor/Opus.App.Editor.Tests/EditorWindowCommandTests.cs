using System;
using FluentAssertions;
using Opus.App.Editor.Cli;
using Opus.App.Editor.Run;
using Xunit;

namespace Opus.App.Editor.Tests;

public sealed class EditorWindowCommandTests
{
    [Fact]
    public void Window_with_a_scene_path_routes_to_window_mode()
    {
        var args = EditorCliParser.Parse(new[] { "window", "harbor.scene.json" });

        args.Mode.Should().Be(EditorMode.Window);
        args.ScenePath.Should().Be("harbor.scene.json");
    }

    [Fact]
    public void Window_without_a_scene_path_is_still_window_mode()
    {
        var args = EditorCliParser.Parse(new[] { "window" });

        args.Mode.Should().Be(EditorMode.Window);
        args.ScenePath.Should().BeNull();
    }

    [Fact]
    public void Window_captures_content_root_and_the_frame_cap()
    {
        var args = EditorCliParser.Parse(
            new[] { "window", "h.scene.json", "--content-root", "assets", "--frames", "5" });

        args.ContentRoot.Should().Be("assets");
        args.WindowMaxFrames.Should().Be(5);
    }

    [Fact]
    public void A_negative_frame_cap_is_a_usage_error()
    {
        var args = EditorCliParser.Parse(new[] { "window", "h.scene.json", "--frames", "-2" });

        args.Mode.Should().Be(EditorMode.Help);
        args.HelpReason.Should().NotBeEmpty();
    }

    [Fact]
    public void Window_captures_the_project_option()
    {
        var args = EditorCliParser.Parse(new[] { "window", "--project", "campaign.project.json" });

        args.Mode.Should().Be(EditorMode.Window);
        args.ProjectPath.Should().Be("campaign.project.json");
        args.ScenePath.Should().BeNull("the project flag is not a scene argument");
    }

    [Fact]
    public void Window_with_a_missing_project_file_fails_before_opening()
    {
        using var temp = new TempDirectory();
        var args = new EditorArgs(
            EditorMode.Window, null, null, string.Empty, ProjectPath: temp.File("no-such.project.json"));
        var log = new CapturingLog();

        int code = EditorWindowRunner.RunWindow(args, log, maxFrames: 1);

        code.Should().Be(EditorConsoleRunner.ExitIoFailed);
    }

    [SkippableFact]
    public void A_stale_remembered_project_degrades_instead_of_failing_the_launch()
    {
        Skip.IfNot(OperatingSystem.IsWindows(), "The editor window is Windows-only (D3D12).");

        using var temp = new TempDirectory();
        string settingsPath = temp.File("editor.settings.json");
        EditorSettingsStore.TrySave(
            settingsPath,
            new EditorSettings(640, 480, null, LastProjectPath: temp.File("moved-away.project.json")),
            new CapturingLog());

        var args = new EditorArgs(EditorMode.Window, null, null, string.Empty, SettingsPath: settingsPath);
        var log = new CapturingLog();
        int code = EditorWindowRunner.RunWindow(args, log, maxFrames: 1);

        Skip.If(
            code == EditorConsoleRunner.ExitIoFailed && log.Joined.Contains("could not open", StringComparison.OrdinalIgnoreCase),
            "No D3D12 adapter, SDL video, swap chain, or DXC is available on this host (the window could not open).");
        code.Should().Be(EditorConsoleRunner.ExitOk, "a remembered project that moved must not brick the launch");
        log.Joined.Should().Contain("Remembered project could not be opened");
        EditorSettingsStore.LoadOrCreate(settingsPath, new CapturingLog()).LastProjectPath.Should().BeNull(
            "the stale reference is dropped on the next settings save");
    }

    [SkippableFact]
    public void The_settings_file_remembers_the_opened_project()
    {
        Skip.IfNot(OperatingSystem.IsWindows(), "The editor window is Windows-only (D3D12).");

        using var temp = new TempDirectory();
        string projectPath = temp.File("campaign.project.json");
        EditorProjectRunner.RunNew(
            new EditorArgs(EditorMode.ProjectNew, projectPath, "Campaign", string.Empty), new CapturingLog());
        string settingsPath = temp.File("editor.settings.json");

        var args = new EditorArgs(
            EditorMode.Window, null, null, string.Empty, SettingsPath: settingsPath, ProjectPath: projectPath);
        var log = new CapturingLog();
        int code = EditorWindowRunner.RunWindow(args, log, maxFrames: 1);

        Skip.If(
            code == EditorConsoleRunner.ExitIoFailed && log.Joined.Contains("could not open", StringComparison.OrdinalIgnoreCase),
            "No D3D12 adapter, SDL video, swap chain, or DXC is available on this host (the window could not open).");
        code.Should().Be(EditorConsoleRunner.ExitOk);
        EditorSettingsStore.LoadOrCreate(settingsPath, new CapturingLog()).LastProjectPath.Should().Be(
            Path.GetFullPath(projectPath), "the next launch with --settings reopens the project");
    }

    [SkippableFact]
    public void Window_with_a_project_opens_its_first_scene_and_exits_cleanly()
    {
        Skip.IfNot(OperatingSystem.IsWindows(), "The editor window is Windows-only (D3D12).");

        using var temp = new TempDirectory();
        string scenePath = temp.File("harbor.scene.json");
        EditorConsoleRunner.RunNew(new EditorArgs(EditorMode.New, scenePath, "Harbor", string.Empty), new CapturingLog());
        string projectPath = temp.File("campaign.project.json");
        EditorProjectRunner.RunNew(
            new EditorArgs(EditorMode.ProjectNew, projectPath, "Campaign", string.Empty), new CapturingLog());
        EditorProjectRunner.RunAdd(
            new EditorArgs(EditorMode.ProjectAdd, projectPath, "scene", string.Empty, AssetRef: "harbor.scene.json"),
            new CapturingLog());
        EditorProjectRunner.RunAdd(
            new EditorArgs(EditorMode.ProjectAdd, projectPath, "content-root", string.Empty, AssetRef: "assets"),
            new CapturingLog());

        var args = new EditorArgs(EditorMode.Window, null, null, string.Empty, ProjectPath: projectPath);
        var log = new CapturingLog();
        int code = EditorWindowRunner.RunWindow(args, log, maxFrames: 2);

        Skip.If(
            code == EditorConsoleRunner.ExitIoFailed && log.Joined.Contains("could not open", StringComparison.OrdinalIgnoreCase),
            "No D3D12 adapter, SDL video, swap chain, or DXC is available on this host (the window could not open).");
        code.Should().Be(EditorConsoleRunner.ExitOk);
        log.Joined.Should().Contain("Project 'Campaign'");
        log.Joined.Should().Contain("Harbor", "the project's first scene opened as the document");
    }

    [SkippableFact]
    public void Window_command_opens_renders_and_exits_cleanly()
    {
        Skip.IfNot(OperatingSystem.IsWindows(), "The editor window is Windows-only (D3D12).");

        using var temp = new TempDirectory();
        string scenePath = temp.File("smoke.scene.json");
        EditorConsoleRunner.RunNew(new EditorArgs(EditorMode.New, scenePath, "Smoke", string.Empty), new CapturingLog());
        EditorSceneAuthoringRunner.RunPlace(
            new EditorArgs(EditorMode.Place, scenePath, "alpha", string.Empty, "models/tank.glb"), new CapturingLog());

        var args = new EditorArgs(EditorMode.Window, scenePath, null, string.Empty, WindowMaxFrames: 2);
        int code = EditorWindowRunner.RunWindow(args, new CapturingLog(), maxFrames: args.WindowMaxFrames);

        Skip.If(
            code == EditorConsoleRunner.ExitIoFailed,
            "No D3D12 adapter, SDL video, swap chain, or DXC is available on this host (the window could not open).");
        code.Should().Be(EditorConsoleRunner.ExitOk);
    }
}
