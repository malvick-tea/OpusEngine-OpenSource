using FluentAssertions;
using Opus.App.Editor.Cli;
using Opus.App.Editor.Run;
using Xunit;

namespace Opus.App.Editor.Tests;

public sealed class EditorAnimationRunnerTests
{
    private static string NewGraph(TempDirectory temp)
    {
        string path = temp.File("loco.animgraph.json");
        EditorAnimationRunner.RunNew(
            new EditorArgs(EditorMode.AnimNew, path, "Locomotion", string.Empty), new CapturingLog());
        return path;
    }

    private static void AddState(string path, string name, bool entry = false)
    {
        var anim = new AnimationArgs(StateName: name, MakeEntry: entry);
        EditorAnimationRunner.RunAddState(
            new EditorArgs(EditorMode.AnimState, path, null, string.Empty, Animation: anim), new CapturingLog());
    }

    [Fact]
    public void New_then_show_round_trips_the_graph()
    {
        using var temp = new TempDirectory();
        string path = NewGraph(temp);
        var log = new CapturingLog();

        var code = EditorAnimationRunner.RunShow(
            new EditorArgs(EditorMode.AnimShow, path, null, string.Empty), log);

        code.Should().Be(EditorConsoleRunner.ExitOk);
        log.Joined.Should().Contain("Locomotion");
        log.Joined.Should().Contain("animgraph");
    }

    [Fact]
    public void Add_state_appears_in_the_pseudo_code()
    {
        using var temp = new TempDirectory();
        string path = NewGraph(temp);
        var log = new CapturingLog();

        var anim = new AnimationArgs(StateName: "Idle", Clip: "idle.glb", MakeEntry: true);
        var code = EditorAnimationRunner.RunAddState(
            new EditorArgs(EditorMode.AnimState, path, null, string.Empty, Animation: anim), log);

        code.Should().Be(EditorConsoleRunner.ExitOk);
        log.Joined.Should().Contain("state \"Idle\"");
        log.Joined.Should().Contain("entry \"Idle\"");
        log.Joined.Should().Contain("clip \"idle.glb\"");
    }

    [Fact]
    public void Transition_between_states_is_wired_and_mirrored()
    {
        using var temp = new TempDirectory();
        string path = NewGraph(temp);
        AddState(path, "Idle", entry: true);
        AddState(path, "Walk");
        var log = new CapturingLog();

        var anim = new AnimationArgs(FromState: "Idle", ToState: "Walk", Trigger: "move", Blend: 0.2f);
        var code = EditorAnimationRunner.RunAddTransition(
            new EditorArgs(EditorMode.AnimTransition, path, null, string.Empty, Animation: anim), log);

        code.Should().Be(EditorConsoleRunner.ExitOk);
        log.Joined.Should().Contain("transition \"Idle\" -> \"Walk\" on \"move\" blend 0.2");
    }

    [Fact]
    public void Transition_with_an_unknown_state_is_a_usage_error()
    {
        using var temp = new TempDirectory();
        string path = NewGraph(temp);
        AddState(path, "Idle", entry: true);
        var log = new CapturingLog();

        var anim = new AnimationArgs(FromState: "Idle", ToState: "Ghost", Trigger: "move");
        var code = EditorAnimationRunner.RunAddTransition(
            new EditorArgs(EditorMode.AnimTransition, path, null, string.Empty, Animation: anim), log);

        code.Should().Be(EditorConsoleRunner.ExitUsage);
    }

    private static void AddTransition(string path, string from, string to, string trigger)
    {
        var anim = new AnimationArgs(FromState: from, ToState: to, Trigger: trigger);
        EditorAnimationRunner.RunAddTransition(
            new EditorArgs(EditorMode.AnimTransition, path, null, string.Empty, Animation: anim), new CapturingLog());
    }

    [Fact]
    public void Remove_state_drops_it_from_the_pseudo_code()
    {
        using var temp = new TempDirectory();
        string path = NewGraph(temp);
        AddState(path, "Idle", entry: true);
        AddState(path, "Walk");
        var log = new CapturingLog();

        var anim = new AnimationArgs(StateName: "Walk");
        var code = EditorAnimationRunner.RunRemoveState(
            new EditorArgs(EditorMode.AnimRemoveState, path, null, string.Empty, Animation: anim), log);

        code.Should().Be(EditorConsoleRunner.ExitOk);
        log.Joined.Should().NotContain("state \"Walk\"");
    }

    [Fact]
    public void Remove_state_with_an_unknown_name_is_a_usage_error()
    {
        using var temp = new TempDirectory();
        string path = NewGraph(temp);
        AddState(path, "Idle", entry: true);
        var log = new CapturingLog();

        var anim = new AnimationArgs(StateName: "Ghost");
        var code = EditorAnimationRunner.RunRemoveState(
            new EditorArgs(EditorMode.AnimRemoveState, path, null, string.Empty, Animation: anim), log);

        code.Should().Be(EditorConsoleRunner.ExitUsage);
    }

    [Fact]
    public void Remove_transition_unwires_it_in_the_pseudo_code()
    {
        using var temp = new TempDirectory();
        string path = NewGraph(temp);
        AddState(path, "Idle", entry: true);
        AddState(path, "Walk");
        AddTransition(path, "Idle", "Walk", "move");
        var log = new CapturingLog();

        var anim = new AnimationArgs(FromState: "Idle", ToState: "Walk", Trigger: "move");
        var code = EditorAnimationRunner.RunRemoveTransition(
            new EditorArgs(EditorMode.AnimRemoveTransition, path, null, string.Empty, Animation: anim), log);

        code.Should().Be(EditorConsoleRunner.ExitOk);
        log.Joined.Should().NotContain("transition \"Idle\" -> \"Walk\"");
    }

    [Fact]
    public void Remove_a_missing_transition_is_a_usage_error()
    {
        using var temp = new TempDirectory();
        string path = NewGraph(temp);
        AddState(path, "Idle", entry: true);
        AddState(path, "Walk");
        var log = new CapturingLog();

        var anim = new AnimationArgs(FromState: "Idle", ToState: "Walk", Trigger: "move");
        var code = EditorAnimationRunner.RunRemoveTransition(
            new EditorArgs(EditorMode.AnimRemoveTransition, path, null, string.Empty, Animation: anim), log);

        code.Should().Be(EditorConsoleRunner.ExitUsage);
    }

    [Fact]
    public void Show_reports_validation_issues()
    {
        using var temp = new TempDirectory();
        string path = NewGraph(temp);
        AddState(path, "Idle");
        var log = new CapturingLog();

        EditorAnimationRunner.RunShow(new EditorArgs(EditorMode.AnimShow, path, null, string.Empty), log);

        log.Joined.Should().Contain("MissingEntryState");
    }

    [Fact]
    public void New_without_a_path_returns_usage()
    {
        var code = EditorAnimationRunner.RunNew(
            new EditorArgs(EditorMode.AnimNew, null, null, string.Empty), new CapturingLog());

        code.Should().Be(EditorConsoleRunner.ExitUsage);
    }
}
