using System.IO;
using FluentAssertions;
using Opus.App.Editor.Run;
using Opus.Editor.Core;
using Xunit;

namespace Opus.App.Editor.Tests;

public sealed class EditorAutosaveTests
{
    private static EditorDocument DirtyDocumentWithOneNode()
    {
        var document = new EditorDocument("Harbor");
        document.PlaceNode("alpha", "models/tank.glb", EditorTransform.Identity);
        return document;
    }

    [Fact]
    public void The_sidecar_for_a_named_scene_sits_next_to_it()
    {
        string path = EditorAutosave.PathFor(Path.Combine("maps", "harbor.scene.json"), "ignored");

        path.Should().Be(Path.Combine("maps", "harbor.autosave.scene.json"));
    }

    [Fact]
    public void An_untitled_session_autosaves_under_the_working_directory()
    {
        string path = EditorAutosave.PathFor(scenePath: null, "work");

        path.Should().Be(Path.Combine("work", "untitled.autosave.scene.json"));
    }

    [Fact]
    public void A_scene_name_without_a_directory_uses_the_supplied_directory()
    {
        string path = EditorAutosave.PathFor("harbor.scene.json", "work");

        path.Should().Be(Path.Combine("work", "harbor.autosave.scene.json"));
    }

    [Fact]
    public void A_dirty_document_writes_a_loadable_sidecar_and_stays_dirty()
    {
        using var temp = new TempDirectory();
        var document = DirtyDocumentWithOneNode();

        string? written = EditorAutosave.WriteIfDirty(
            document, temp.File("harbor.scene.json"), temp.Root, new CapturingLog());

        written.Should().Be(temp.File("harbor.autosave.scene.json"));
        EditorSceneFileStore.Load(written!).Unwrap().Nodes.Should().ContainSingle(n => n.Name == "alpha");
        document.IsDirty.Should().BeTrue("the real scene file still lacks the edits");
    }

    [Fact]
    public void A_clean_document_writes_nothing()
    {
        using var temp = new TempDirectory();

        string? written = EditorAutosave.WriteIfDirty(
            new EditorDocument("Harbor"), temp.File("harbor.scene.json"), temp.Root, new CapturingLog());

        written.Should().BeNull();
        Directory.GetFiles(temp.Root).Should().BeEmpty();
    }

    [Fact]
    public void A_failed_sidecar_write_is_logged_not_thrown()
    {
        using var temp = new TempDirectory();
        Directory.CreateDirectory(temp.File("harbor.autosave.scene.json"));
        var log = new CapturingLog();

        string? written = EditorAutosave.WriteIfDirty(
            DirtyDocumentWithOneNode(), temp.File("harbor.scene.json"), temp.Root, log);

        written.Should().BeNull();
        log.Joined.Should().Contain("harbor.autosave.scene.json");
    }

    [Fact]
    public void Deleting_removes_an_existing_sidecar()
    {
        using var temp = new TempDirectory();
        File.WriteAllText(temp.File("harbor.autosave.scene.json"), "{}");

        EditorAutosave.TryDelete(temp.File("harbor.scene.json"), temp.Root, new CapturingLog());

        File.Exists(temp.File("harbor.autosave.scene.json")).Should().BeFalse();
    }

    [Fact]
    public void Deleting_a_missing_sidecar_is_a_quiet_no_op()
    {
        using var temp = new TempDirectory();
        var log = new CapturingLog();

        EditorAutosave.TryDelete(temp.File("harbor.scene.json"), temp.Root, log);

        log.Messages.Should().BeEmpty();
    }
}
